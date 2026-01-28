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

        public HaulBasicExecutor(GameServices s) { _s = s; }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            if (_s.WorldState == null || _s.StorageService == null || _s.WorldIndex == null || _s.AgentMover == null)
            {
                job.Status = JobStatus.Failed;
                return true;
            }

            var rt = job.ResourceType;

            // ---------------------------
            // Destination selection (tuned):
            // 1) Prefer workplace if workplace is WAREHOUSE and has space.
            // 2) Else select destination near SOURCE anchor.
            // 3) Prefer Warehouse over HQ (HQ only fallback).
            // ---------------------------

            // Resolve preferred workplace state (if any)
            BuildingState prefState = default;
            bool hasPrefState = job.Workplace.Value != 0
                                && _s.WorldState.Buildings.Exists(job.Workplace)
                                && (prefState = _s.WorldState.Buildings.Get(job.Workplace)).IsConstructed
                                && IsWarehouseWorkplace(prefState.DefId);

            bool prefIsWarehouse = hasPrefState && IsWarehouseOnly(prefState.DefId);

            // A) Workplace Warehouse has space => hard prefer it
            if (prefIsWarehouse && GetDestFree(prefState, rt) > 0)
            {
                job.DestBuilding = job.Workplace;
            }
            else
            {
                // B) Need a source candidate to choose dest "near source"
                EnsureSourceCandidate(ref job, rt, hasPrefState ? prefState.Anchor : npcState.Cell);
                if (job.SourceBuilding.Value == 0)
                {
                    job.Status = JobStatus.Cancelled;
                    RefundToSourceIfCarrying(job.Id.Value, ref job, rt);
                    Cleanup(job.Id.Value);
                    return true;
                }

                if (!_s.WorldState.Buildings.Exists(job.SourceBuilding))
                    return false;

                var srcState0 = _s.WorldState.Buildings.Get(job.SourceBuilding);
                if (!srcState0.IsConstructed || !IsHarvestProducer(srcState0.DefId))
                    return false;

                if (!TryResolveBestDestination(rt, job.Workplace, srcState0.Anchor, out var chosen))
                {
                    // No destination has space => cancel (avoid flicker)
                    job.Status = JobStatus.Cancelled;
                    RefundToSourceIfCarrying(job.Id.Value, ref job, rt);
                    Cleanup(job.Id.Value);
                    return true;
                }

                // Switch dest (release old dest claim if switching)
                if (job.DestBuilding.Value == 0 || job.DestBuilding.Value != chosen.Value)
                {
                    if (_s.ClaimService != null && job.DestBuilding.Value != 0)
                    {
                        var oldKey = new ClaimKey(ClaimKind.StorageDest, job.DestBuilding.Value, (int)rt);
                        _s.ClaimService.Release(oldKey, npc);
                    }
                    job.DestBuilding = chosen;
                }
            }

            var chosenDest = job.DestBuilding;

            // Validate chosen dest
            if (chosenDest.Value == 0 || !_s.WorldState.Buildings.Exists(chosenDest))
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value);
                return true;
            }

            var dstState = _s.WorldState.Buildings.Get(chosenDest);
            if (!dstState.IsConstructed || !IsWarehouseWorkplace(dstState.DefId))
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value);
                return true;
            }

            int free = GetDestFree(dstState, rt);

            // Reroute if destination became full
            if (free <= 0)
            {
                // refPos ưu tiên SOURCE anchor (near resource), fallback pref anchor, fallback npc cell
                CellPos refPos = hasPrefState ? prefState.Anchor : npcState.Cell;

                if (job.SourceBuilding.Value != 0 && _s.WorldState.Buildings.Exists(job.SourceBuilding))
                {
                    var ss = _s.WorldState.Buildings.Get(job.SourceBuilding);
                    if (ss.IsConstructed) refPos = ss.Anchor;
                }

                if (!TryResolveBestDestination(rt, job.Workplace, refPos, out var reroute))
                {
                    job.Status = JobStatus.Cancelled;
                    RefundToSourceIfCarrying(job.Id.Value, ref job, rt);
                    Cleanup(job.Id.Value);
                    return true;
                }

                // Release old dest claim before switching
                if (_s.ClaimService != null && chosenDest.Value != 0)
                {
                    var oldKey = new ClaimKey(ClaimKind.StorageDest, chosenDest.Value, (int)rt);
                    _s.ClaimService.Release(oldKey, npc);
                }

                job.DestBuilding = reroute;
                chosenDest = reroute;

                dstState = _s.WorldState.Buildings.Get(chosenDest);
                free = GetDestFree(dstState, rt);

                if (free <= 0)
                {
                    job.Status = JobStatus.Cancelled;
                    RefundToSourceIfCarrying(job.Id.Value, ref job, rt);
                    Cleanup(job.Id.Value);
                    return true;
                }
            }

            // Hold dest claim during job (prevents two haulers choosing same dest+rt)
            if (_s.ClaimService != null)
            {
                var destKey = new ClaimKey(ClaimKind.StorageDest, chosenDest.Value, (int)rt);
                if (!_s.ClaimService.TryAcquire(destKey, npc))
                    return false;
            }

            int jid = job.Id.Value;
            if (!_phase.TryGetValue(jid, out var ph)) ph = 0;

            if (ph == 0)
            {
                // Clamp amount by free capacity
                int want = CarryCap;
                if (free < want) want = free;
                if (want <= 0)
                {
                    job.Status = JobStatus.Cancelled;
                    RefundToSourceIfCarrying(job.Id.Value, ref job, rt);
                    Cleanup(jid);
                    return true;
                }

                // pick source lazily (ensure it has >= want)
                if (job.SourceBuilding.Value == 0)
                {
                    if (!TryPickBestHarvestProducerSource(dstState.Anchor, rt, want, out var src))
                        return false;
                    job.SourceBuilding = src;
                }

                var srcId = job.SourceBuilding;
                if (!_s.WorldState.Buildings.Exists(srcId))
                    return false;

                var srcState = _s.WorldState.Buildings.Get(srcId);
                if (!srcState.IsConstructed || !IsHarvestProducer(srcState.DefId))
                    return false;

                // Ensure enough at source; if not, repick
                if (GetAmountFromBuilding(srcState, rt) < want)
                {
                    if (!TryPickBestHarvestProducerSource(srcState.Anchor, rt, want, out var repick))
                        return false;

                    job.SourceBuilding = repick;
                    srcId = repick;

                    if (!_s.WorldState.Buildings.Exists(srcId))
                        return false;
                    srcState = _s.WorldState.Buildings.Get(srcId);
                    if (!srcState.IsConstructed || !IsHarvestProducer(srcState.DefId))
                        return false;
                }

                // Move toward source (Day14: no teleport)
                job.TargetCell = srcState.Anchor;
                job.Status = JobStatus.InProgress;

                bool arrivedSrc = _s.AgentMover.StepToward(ref npcState, srcState.Anchor);
                if (!arrivedSrc)
                    return true;

                // pickup (claim source while removing)
                if (_s.ClaimService != null)
                {
                    var srcKey = new ClaimKey(ClaimKind.StorageSource, srcId.Value, (int)rt);
                    if (!_s.ClaimService.TryAcquire(srcKey, npc))
                        return false;

                    int removed = _s.StorageService.Remove(srcId, rt, want);

                    _s.ClaimService.Release(srcKey, npc);

                    if (removed <= 0)
                        return false;

                    _carry[jid] = removed;
                    _phase[jid] = 1;

                    job.Amount = removed;
                    job.Status = JobStatus.InProgress;
                    return true;
                }
                else
                {
                    int removed = _s.StorageService.Remove(srcId, rt, want);
                    if (removed <= 0)
                        return false;

                    _carry[jid] = removed;
                    _phase[jid] = 1;

                    job.Amount = removed;
                    job.Status = JobStatus.InProgress;
                    return true;
                }
            }
            else
            {
                // deliver
                if (!_carry.TryGetValue(jid, out var carried) || carried <= 0)
                {
                    job.Status = JobStatus.Failed;
                    Cleanup(jid);
                    return true;
                }

                // Move toward destination (Day14: no teleport)
                job.TargetCell = dstState.Anchor;
                job.Status = JobStatus.InProgress;

                bool arrivedDst = _s.AgentMover.StepToward(ref npcState, dstState.Anchor);
                if (!arrivedDst)
                    return true;

                int added = _s.StorageService.Add(chosenDest, rt, carried);

                // refund remainder back to source (best-effort)
                int refund = carried - added;
                if (refund > 0 && job.SourceBuilding.Value != 0 && _s.WorldState.Buildings.Exists(job.SourceBuilding))
                {
                    _s.StorageService.Add(job.SourceBuilding, rt, refund);
                }

                job.Status = JobStatus.Completed;
                Cleanup(jid);
                return true;
            }
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

        private void Cleanup(int jobId)
        {
            _phase.Remove(jobId);
            _carry.Remove(jobId);
            // Claims will be released by JobScheduler via ReleaseAll(npc) on terminal status.
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
