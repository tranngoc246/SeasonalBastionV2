using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class HaulBasicExecutor : IJobExecutor
    {
        private readonly GameServices _s;

        private static bool IsWarehouseOnly(string defId) => DefIdTierUtil.IsBase(defId, "bld_warehouse");
        private static bool IsHQOnly(string defId) => DefIdTierUtil.IsBase(defId, "bld_hq");

        // jobId -> phase (0 pickup, 1 deliver)
        private readonly Dictionary<int, byte> _phase = new();
        private readonly Dictionary<int, int> _carry = new();
        private readonly Dictionary<int, int> _source = new(); // jobId -> source BuildingId.Value

        private readonly Dictionary<int, float> _settle = new();
        private const float HaulSettleSec = 1.0f;

        public HaulBasicExecutor(GameServices s) { _s = s; }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            if (_s.WorldState == null || _s.StorageService == null || _s.AgentMover == null)
            {
                job.Status = JobStatus.Failed;
                return true;
            }

            int jid = job.Id.Value;
            var rt = job.ResourceType;

            // External cancel: refund carry back to source local storage
            if (job.Status == JobStatus.Cancelled)
            {
                RefundCarryToSourceIfPossible(jid, rt);
                Cleanup(jid);
                return true;
            }

            // Resolve destination dynamically:
            // - Prefer nearest Warehouse (bld_warehouse_t1) first
            // - Fallback to HQ (bld_hq_t1) if no warehouse has space
            // NOTE: Use source anchor if we already have a source, otherwise use npc cell.
            var refPos = npcState.Cell;
            if (job.SourceBuilding.Value != 0 && _s.WorldState.Buildings.Exists(job.SourceBuilding))
            {
                var ss = _s.WorldState.Buildings.Get(job.SourceBuilding);
                if (ss.IsConstructed) refPos = ss.Anchor;
            }

            if (TryResolveBestDestination(rt, job.Workplace, refPos, out var bestDest))
                job.DestBuilding = bestDest;

            var dest = job.DestBuilding;
            if (dest.Value == 0 || !_s.WorldState.Buildings.Exists(dest))
            {
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            var dstState = _s.WorldState.Buildings.Get(dest);
            if (!dstState.IsConstructed || !IsWarehouseWorkplace(dstState.DefId))
            {
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            if (!_phase.TryGetValue(jid, out var ph)) ph = 0;

            if (ph == 0)
            {
                // Pick best producer source that has rt in local storage
                BuildingId srcId = default;

                if (_source.TryGetValue(jid, out var srcInt) && srcInt != 0)
                    srcId = new BuildingId(srcInt);

                if (srcId.Value == 0 || !_s.WorldState.Buildings.Exists(srcId))
                {
                    // Choose by: higher fill -> nearer to NPC -> smaller id
                    if (!TryPickBestHarvestProducerSource(npcState.Cell, rt, 1, out srcId))
                    {
                        job.Status = JobStatus.Cancelled;
                        Cleanup(jid);
                        return true;
                    }
                    _source[jid] = srcId.Value;
                }

                var srcState = _s.WorldState.Buildings.Get(srcId);
                if (!srcState.IsConstructed)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                // Re-resolve destination using SOURCE anchor (ưu tiên kho gần nguồn/pickup)
                if (TryResolveBestDestination(rt, job.Workplace, srcState.Anchor, out var bestDestFromSource))
                {
                    job.DestBuilding = bestDestFromSource;
                    dest = job.DestBuilding;
                    dstState = _s.WorldState.Buildings.Get(dest);
                }

                // Move to source ENTRY
                var srcEntry = EntryCellUtil.GetApproachCellForBuilding(_s, srcState, npcState.Cell);

                job.SourceBuilding = srcId;
                job.TargetCell = srcEntry;
                job.Status = JobStatus.InProgress;

                bool arrived = _s.AgentMover.StepToward(ref npcState, srcEntry, dt);
                if (!arrived) return true;

                // Stand still before pickup
                if (!_settle.TryGetValue(jid, out var remP))
                    remP = HaulSettleSec;

                remP -= dt;
                if (remP > 0f)
                {
                    _settle[jid] = remP;
                    return true;
                }
                _settle.Remove(jid);

                int whTier = _s.Balance != null ? _s.Balance.GetWarehouseTier() : 1;
                int cap = _s.Balance != null ? _s.Balance.GetCarryHaulBasic(whTier) : 10;

                int want = cap;

                int taken = _s.StorageService.Remove(srcId, rt, want);
                if (taken <= 0)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                _carry[jid] = taken;
                _phase[jid] = 1;

                job.Amount = taken;
                job.Status = JobStatus.InProgress;
                return true;
            }
            else
            {
                // Deliver to HQ/Warehouse (anchor)
                int carried = _carry.TryGetValue(jid, out var c) ? c : 0;
                if (carried <= 0)
                {
                    job.Status = JobStatus.Completed;
                    Cleanup(jid);
                    return true;
                }

                var dstEntry = EntryCellUtil.GetApproachCellForBuilding(_s, dstState, npcState.Cell);

                job.TargetCell = dstEntry;
                job.Status = JobStatus.InProgress;

                bool arrived = _s.AgentMover.StepToward(ref npcState, dstEntry, dt);
                if (!arrived) return true;

                // Stand still before deposit
                if (!_settle.TryGetValue(jid, out var remD))
                    remD = HaulSettleSec;

                remD -= dt;
                if (remD > 0f)
                {
                    _settle[jid] = remD;
                    return true;
                }
                _settle.Remove(jid);

                int added = _s.StorageService.Add(dest, rt, carried);

                if (added < carried)
                {
                    int left = carried - added;

                    // Try reroute to another destination that has free space (Warehouse > HQ)
                    if (TryResolveBestDestination(rt, job.Workplace, dstState.Anchor, out var reroute))
                    {
                        // If reroute is same dest but it's full, TryResolveBestDestination would normally avoid it (free=0)
                        if (reroute.Value != 0 && reroute.Value != dest.Value)
                        {
                            job.DestBuilding = reroute;
                            _carry[jid] = left;
                            job.Amount = left;
                            job.Status = JobStatus.InProgress;
                            return true; // continue phase=deliver toward new dest
                        }
                    }

                    // No reroute possible => return remainder to source to avoid loss
                    if (_source.TryGetValue(jid, out var srcInt) && srcInt != 0)
                    {
                        var srcId = new BuildingId(srcInt);
                        _s.StorageService.Add(srcId, rt, left);
                    }
                }

                job.Amount = 0;
                job.Status = JobStatus.Completed;
                Cleanup(jid);
                return true;
            }
        }

        private void RefundCarryToSourceIfPossible(int jid, ResourceType rt)
        {
            if (_carry.TryGetValue(jid, out var carried) && carried > 0)
            {
                if (_source.TryGetValue(jid, out var srcInt) && srcInt != 0)
                {
                    var src = new BuildingId(srcInt);
                    if (_s.WorldState.Buildings.Exists(src))
                        _s.StorageService.Add(src, rt, carried);
                }
            }
        }

        private void Cleanup(int jid)
        {
            _phase.Remove(jid);
            _carry.Remove(jid);
            _source.Remove(jid);
            _settle.Remove(jid);
        }

        /// <summary>
        /// Ensures job.SourceBuilding has a candidate so we can choose destination near SOURCE.
        /// This uses minRequired=1 (only to get an anchor), later pickup will repick/validate against CarryCap.
        /// </summary>
        private void EnsureSourceCandidate(ref Job job, ResourceType rt, CellPos from)
        {
            if (job.SourceBuilding.Value != 0) return;

            if (TryPickBestHarvestProducerSource(from, rt, 1, out var src))
                job.SourceBuilding = src;
        }

        /// <summary>
        /// Pick a destination Warehouse/HQ with free capacity for rt.
        /// Priority:
        /// 1) Workplace if it's a WAREHOUSE (not HQ) and has space
        /// 2) Nearest Warehouse by Manhattan(refPos), tie-break by BuildingId
        /// 3) Nearest HQ by Manhattan(refPos), tie-break by BuildingId
        /// </summary>
        private bool TryResolveBestDestination(ResourceType rt, BuildingId preferredWorkplace, CellPos refPos, out BuildingId best)
        {
            best = default;

            var whs = _s.WorldIndex.Warehouses; // includes HQ
            if (whs == null || whs.Count == 0) return false;

            // 0) Preferred workplace: ONLY hard-prefer if it is a Warehouse (not HQ) and has space
            if (preferredWorkplace.Value != 0 && _s.WorldState.Buildings.Exists(preferredWorkplace))
            {
                var ps = _s.WorldState.Buildings.Get(preferredWorkplace);
                if (ps.IsConstructed && IsWarehouseOnly(ps.DefId) && GetDestFree(ps, rt) > 0)
                {
                    best = preferredWorkplace;
                    return true;
                }
            }

            int bestCostWh = int.MaxValue, bestDistWh = int.MaxValue, bestIdWh = int.MaxValue;
            int bestCostHq = int.MaxValue, bestDistHq = int.MaxValue, bestIdHq = int.MaxValue;
            BuildingId bestWh = default, bestHq = default;

            for (int i = 0; i < whs.Count; i++)
            {
                var bid = whs[i];
                if (!_s.WorldState.Buildings.Exists(bid)) continue;

                var bs = _s.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;
                if (!IsWarehouseWorkplace(bs.DefId)) continue;

                int free = GetDestFree(bs, rt);
                if (free <= 0) continue;

                int d = Manhattan(refPos, bs.Anchor);
                int cost = TryEstimateTravelCost(refPos, bs.Anchor, out var c) ? c : d;
                int idv = bid.Value;

                if (IsWarehouseOnly(bs.DefId))
                {
                    if (cost < bestCostWh || (cost == bestCostWh && d < bestDistWh) || (cost == bestCostWh && d == bestDistWh && idv < bestIdWh))
                    {
                        bestCostWh = cost; bestDistWh = d; bestIdWh = idv; bestWh = bid;
                    }
                }
                else if (IsHQOnly(bs.DefId))
                {
                    if (cost < bestCostHq || (cost == bestCostHq && d < bestDistHq) || (cost == bestCostHq && d == bestDistHq && idv < bestIdHq))
                    {
                        bestCostHq = cost; bestDistHq = d; bestIdHq = idv; bestHq = bid;
                    }
                }
            }

            // Warehouse > HQ
            if (bestWh.Value != 0) { best = bestWh; return true; }
            if (bestHq.Value != 0) { best = bestHq; return true; }

            return false;
        }

        private bool TryPickBestHarvestProducerSource(CellPos from, ResourceType rt, int minRequired, out BuildingId best)
        {
            best = default;

            var producers = _s.WorldIndex.Producers;
            if (producers == null || producers.Count == 0) return false;

            int bestFill = int.MinValue;   // fill per-mille (0..1000)
            int bestCost = int.MaxValue;
            int bestDist = int.MaxValue;
            int bestId = int.MaxValue;

            for (int i = 0; i < producers.Count; i++)
            {
                var bid = producers[i];
                if (!_s.WorldState.Buildings.Exists(bid)) continue;

                var bs = _s.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                if (!IsHarvestProducer(bs.DefId)) continue;
                if (HarvestResourceType(bs.DefId) != rt) continue;

                int amt = GetAmountFromBuilding(bs, rt);
                if (amt < minRequired) continue;

                int cap = LocalCapForProducer(bs.DefId, NormalizeLevel(bs.Level), rt);
                int fill = (cap > 0) ? (amt * 1000 / cap) : 0; // per-mille

                int d = Manhattan(from, bs.Anchor);
                int cost = TryEstimateTravelCost(from, bs.Anchor, out var c) ? c : d;
                int idv = bid.Value;

                // Priority: higher fill -> lower travel cost -> shorter manhattan -> smaller id
                if (fill > bestFill
                    || (fill == bestFill && cost < bestCost)
                    || (fill == bestFill && cost == bestCost && d < bestDist)
                    || (fill == bestFill && cost == bestCost && d == bestDist && idv < bestId))
                {
                    bestFill = fill;
                    bestCost = cost;
                    bestDist = d;
                    bestId = idv;
                    best = bid;
                }
            }

            return best.Value != 0;
        }

        private void RefundToSourceIfCarrying(int jobId, ref Job job, ResourceType rt)
        {
            if (!_carry.TryGetValue(jobId, out var carried) || carried <= 0) return;

            var src = job.SourceBuilding;
            if (src.Value != 0 && _s.WorldState != null && _s.WorldState.Buildings.Exists(src))
            {
                _s.StorageService.Add(src, rt, carried);
            }

            _carry.Remove(jobId);
            _phase.Remove(jobId);
        }

        private bool TryEstimateTravelCost(CellPos from, CellPos to, out int cost)
        {
            cost = 0;
            if (_s?.GridMap == null) return false;
            var pf = new NpcPathfinder(_s.GridMap);
            return pf.TryEstimateCost(from, to, out cost);
        }

        private static int Manhattan(CellPos a, CellPos b)
        {
            int dx = a.X - b.X; if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
            return dx + dy;
        }

        private static bool IsWarehouseWorkplace(string defId)
        {
            return DefIdTierUtil.IsBase(defId, "bld_warehouse")
                || DefIdTierUtil.IsBase(defId, "bld_hq");
        }

        private static bool IsHarvestProducer(string defId)
        {
            return DefIdTierUtil.IsBase(defId, "bld_farmhouse")
                || DefIdTierUtil.IsBase(defId, "bld_lumbercamp")
                || DefIdTierUtil.IsBase(defId, "bld_quarry")
                || DefIdTierUtil.IsBase(defId, "bld_ironhut");
        }

        private static ResourceType HarvestResourceType(string defId)
        {
            if (DefIdTierUtil.IsBase(defId, "bld_farmhouse")) return ResourceType.Food;
            if (DefIdTierUtil.IsBase(defId, "bld_lumbercamp")) return ResourceType.Wood;
            if (DefIdTierUtil.IsBase(defId, "bld_quarry")) return ResourceType.Stone;
            if (DefIdTierUtil.IsBase(defId, "bld_ironhut")) return ResourceType.Iron;
            return ResourceType.Food;
        }

        private static int GetAmountFromBuilding(in BuildingState bs, ResourceType rt)
        {
            return rt switch
            {
                ResourceType.Wood => bs.Wood,
                ResourceType.Food => bs.Food,
                ResourceType.Stone => bs.Stone,
                ResourceType.Iron => bs.Iron,
                ResourceType.Ammo => bs.Ammo,
                _ => 0
            };
        }

        private static int GetDestFree(in BuildingState dstState, ResourceType rt)
        {
            int lvl = NormalizeLevel(dstState.Level);
            int cap = 0;

            if (DefIdTierUtil.IsBase(dstState.DefId, "bld_warehouse"))
            {
                cap = rt switch
                {
                    ResourceType.Wood or ResourceType.Food or ResourceType.Stone or ResourceType.Iron
                        => lvl == 1 ? 300 : lvl == 2 ? 600 : 1000,
                    _ => 0
                };
            }
            else if (DefIdTierUtil.IsBase(dstState.DefId, "bld_hq"))
            {
                cap = rt switch
                {
                    ResourceType.Wood or ResourceType.Food or ResourceType.Stone or ResourceType.Iron
                        => lvl == 1 ? 120 : lvl == 2 ? 180 : 240,
                    _ => 0
                };
            }

            if (cap <= 0) return 0;

            int cur = rt switch
            {
                ResourceType.Wood => dstState.Wood,
                ResourceType.Food => dstState.Food,
                ResourceType.Stone => dstState.Stone,
                ResourceType.Iron => dstState.Iron,
                _ => 0
            };

            int free = cap - cur;
            return free < 0 ? 0 : free;
        }

        private static int NormalizeLevel(int level) => level <= 0 ? 1 : (level > 3 ? 3 : level);

        private static int LocalCapForProducer(string defId, int level, ResourceType rt)
        {
            // local caps LOCKED (đúng mapping bạn đã dùng ở JobScheduler)
            if (DefIdTierUtil.IsBase(defId, "bld_farmhouse")) return level == 1 ? 30 : level == 2 ? 60 : 90;
            if (DefIdTierUtil.IsBase(defId, "bld_lumbercamp")) return level == 1 ? 40 : level == 2 ? 80 : 120;
            if (DefIdTierUtil.IsBase(defId, "bld_quarry")) return level == 1 ? 40 : level == 2 ? 80 : 120;
            if (DefIdTierUtil.IsBase(defId, "bld_ironhut")) return level == 1 ? 30 : level == 2 ? 60 : 90;
            return 0;
        }

        private static bool EqualsIgnoreCase(string a, string b)
        {
            if (a == null || b == null) return false;
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
