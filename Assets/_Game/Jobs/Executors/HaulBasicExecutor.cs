using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class HaulBasicExecutor : IJobExecutor
    {
        private readonly GameServices _s;

        private const int CarryCap = 10;

        private static bool IsWarehouseOnly(string defId) => EqualsIgnoreCase(defId, "bld_warehouse_t1");
        private static bool IsHQOnly(string defId) => EqualsIgnoreCase(defId, "bld_hq_t1");

        // jobId -> phase (0 pickup, 1 deliver)
        private readonly Dictionary<int, byte> _phase = new();
        private readonly Dictionary<int, int> _carry = new();
        private readonly Dictionary<int, int> _pile = new(); // jobId -> pileId.Value

        public HaulBasicExecutor(GameServices s) { _s = s; }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            if (_s.WorldState == null || _s.StorageService == null || _s.AgentMover == null)
            {
                job.Status = JobStatus.Failed;
                return true;
            }

            if (_s.WorldState.Piles == null)
            {
                job.Status = JobStatus.Failed;
                return true;
            }

            int jid = job.Id.Value;
            var rt = job.ResourceType;

            // External cancel: refund carry back to pile if possible, cleanup
            if (job.Status == JobStatus.Cancelled)
            {
                RefundCarryToPileIfPossible(jid, rt, job.DestBuilding);
                Cleanup(jid);
                return true;
            }

            // Validate destination = producer building (must exist + constructed)
            var dest = job.DestBuilding;
            if (dest.Value == 0 || !_s.WorldState.Buildings.Exists(dest))
            {
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            var dstState = _s.WorldState.Buildings.Get(dest);
            if (!dstState.IsConstructed)
            {
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            if (!_phase.TryGetValue(jid, out var ph)) ph = 0;

            if (ph == 0)
            {
                // Choose a pile for this producer+resource
                if (!_pile.TryGetValue(jid, out var pileInt) || pileInt == 0)
                {
                    if (!_s.WorldState.Piles.TryFindNonEmpty(rt, dest, out var pid))
                    {
                        job.Status = JobStatus.Cancelled;
                        Cleanup(jid);
                        return true;
                    }
                    pileInt = pid.Value;
                    _pile[jid] = pileInt;
                }

                var pileId = new PileId(pileInt);
                if (!_s.WorldState.Piles.Exists(pileId))
                {
                    // pile disappeared, retry next tick
                    _pile.Remove(jid);
                    return true;
                }

                var pile = _s.WorldState.Piles.Get(pileId);

                // Move to pile cell
                job.TargetCell = pile.Cell;
                job.Status = JobStatus.InProgress;

                bool arrived = _s.AgentMover.StepToward(ref npcState, pile.Cell);
                if (!arrived) return true;

                // pickup
                int want = CarryCap;
                if (!_s.WorldState.Piles.TryTake(pileId, want, out int taken) || taken <= 0)
                {
                    _pile.Remove(jid);
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
                // Deliver to producer building (anchor)
                int carried = _carry.TryGetValue(jid, out var c) ? c : 0;
                if (carried <= 0)
                {
                    job.Status = JobStatus.Completed;
                    Cleanup(jid);
                    return true;
                }

                job.TargetCell = dstState.Anchor;
                job.Status = JobStatus.InProgress;

                bool arrived = _s.AgentMover.StepToward(ref npcState, dstState.Anchor);
                if (!arrived) return true;

                // Add to producer local storage (this is where resource increases)
                int added = _s.StorageService.Add(dest, rt, carried);

                if (added > 0)
                {
                    int left = carried - added;
                    if (left > 0)
                    {
                        // If producer is full, drop remainder back as pile near producer anchor (safe)
                        _s.WorldState.Piles.AddOrIncrease(dstState.Anchor, rt, left, dest);
                    }
                }
                else
                {
                    // Could not add (full) => return all to pile near producer
                    _s.WorldState.Piles.AddOrIncrease(dstState.Anchor, rt, carried, dest);
                }

                job.Status = JobStatus.Completed;
                Cleanup(jid);
                return true;
            }
        }

        private void RefundCarryToPileIfPossible(int jid, ResourceType rt, BuildingId dest)
        {
            if (_s.WorldState == null || _s.WorldState.Piles == null) return;

            if (_carry.TryGetValue(jid, out var carried) && carried > 0)
            {
                // return to pile near destination to avoid loss
                if (_s.WorldState.Buildings.Exists(dest))
                {
                    var ds = _s.WorldState.Buildings.Get(dest);
                    _s.WorldState.Piles.AddOrIncrease(ds.Anchor, rt, carried, dest);
                }
            }
        }

        private void Cleanup(int jid)
        {
            _phase.Remove(jid);
            _carry.Remove(jid);
            _pile.Remove(jid);
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

            int bestDistWh = int.MaxValue, bestIdWh = int.MaxValue;
            int bestDistHq = int.MaxValue, bestIdHq = int.MaxValue;
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
                int idv = bid.Value;

                if (IsWarehouseOnly(bs.DefId))
                {
                    if (d < bestDistWh || (d == bestDistWh && idv < bestIdWh))
                    {
                        bestDistWh = d; bestIdWh = idv; bestWh = bid;
                    }
                }
                else if (IsHQOnly(bs.DefId))
                {
                    if (d < bestDistHq || (d == bestDistHq && idv < bestIdHq))
                    {
                        bestDistHq = d; bestIdHq = idv; bestHq = bid;
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
                int idv = bid.Value;

                // Priority: higher fill -> shorter distance -> smaller id
                if (fill > bestFill
                    || (fill == bestFill && d < bestDist)
                    || (fill == bestFill && d == bestDist && idv < bestId))
                {
                    bestFill = fill;
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

        private static int Manhattan(CellPos a, CellPos b)
        {
            int dx = a.X - b.X; if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
            return dx + dy;
        }

        private static bool IsWarehouseWorkplace(string defId)
        {
            return EqualsIgnoreCase(defId, "bld_warehouse_t1")
                || EqualsIgnoreCase(defId, "bld_hq_t1");
        }

        private static bool IsHarvestProducer(string defId)
        {
            return EqualsIgnoreCase(defId, "bld_farmhouse_t1")
                || EqualsIgnoreCase(defId, "bld_lumbercamp_t1")
                || EqualsIgnoreCase(defId, "bld_quarry_t1")
                || EqualsIgnoreCase(defId, "bld_ironhut_t1");
        }

        private static ResourceType HarvestResourceType(string defId)
        {
            if (EqualsIgnoreCase(defId, "bld_farmhouse_t1")) return ResourceType.Food;
            if (EqualsIgnoreCase(defId, "bld_lumbercamp_t1")) return ResourceType.Wood;
            if (EqualsIgnoreCase(defId, "bld_quarry_t1")) return ResourceType.Stone;
            if (EqualsIgnoreCase(defId, "bld_ironhut_t1")) return ResourceType.Iron;
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

            if (EqualsIgnoreCase(dstState.DefId, "bld_warehouse_t1"))
            {
                cap = rt switch
                {
                    ResourceType.Wood or ResourceType.Food or ResourceType.Stone or ResourceType.Iron
                        => lvl == 1 ? 300 : lvl == 2 ? 600 : 1000,
                    _ => 0
                };
            }
            else if (EqualsIgnoreCase(dstState.DefId, "bld_hq_t1"))
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
            if (EqualsIgnoreCase(defId, "bld_farmhouse_t1")) return level == 1 ? 30 : level == 2 ? 60 : 90;
            if (EqualsIgnoreCase(defId, "bld_lumbercamp_t1")) return level == 1 ? 40 : level == 2 ? 80 : 120;
            if (EqualsIgnoreCase(defId, "bld_quarry_t1")) return level == 1 ? 40 : level == 2 ? 80 : 120;
            if (EqualsIgnoreCase(defId, "bld_ironhut_t1")) return level == 1 ? 30 : level == 2 ? 60 : 90;
            return 0;
        }

        private static bool EqualsIgnoreCase(string a, string b)
        {
            if (a == null || b == null) return false;
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
