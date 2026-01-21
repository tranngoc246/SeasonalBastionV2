using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class HaulBasicExecutor : IJobExecutor
    {
        private readonly GameServices _s;

        private const int CarryCap = 10; // Deliverable_C HaulBasic L1

        // jobId -> phase (0 pickup, 1 deliver)
        private readonly Dictionary<int, byte> _phase = new();
        private readonly Dictionary<int, int> _carry = new();

        public HaulBasicExecutor(GameServices s) { _s = s; }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            if (_s.WorldState == null || _s.StorageService == null || _s.WorldIndex == null)
            {
                job.Status = JobStatus.Failed;
                return true;
            }

            var rt = job.ResourceType;

            // ---------------------------
            // 1) Choose destination dynamically (Warehouse/HQ)
            // ---------------------------

            // Prefer workplace if it's a valid Warehouse/HQ.
            // Otherwise still choose any Warehouse/HQ with free capacity.
            if (!TryResolveBestDestination(rt, job.Workplace, out var chosenDest))
            {
                // All destinations full (or no warehouse/hq at all) -> cancel to avoid oscillation.
                job.Status = JobStatus.Cancelled;
                Cleanup(job.Id.Value);
                return true;
            }

            // Update job.DestBuilding to chosen dest (sticky unless full later)
            if (job.DestBuilding.Value == 0 || job.DestBuilding.Value != chosenDest.Value)
            {
                // If switching, release old dest claim to avoid holding two.
                if (_s.ClaimService != null && job.DestBuilding.Value != 0)
                {
                    var oldKey = new ClaimKey(ClaimKind.StorageDest, job.DestBuilding.Value, (int)rt);
                    _s.ClaimService.Release(oldKey, npc);
                }

                job.DestBuilding = chosenDest;
            }

            // Validate chosen dest state
            if (!_s.WorldState.Buildings.Exists(chosenDest))
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
            if (free <= 0)
            {
                // Destination became full since resolve (rare but possible). Try once more.
                if (!TryResolveBestDestination(rt, job.Workplace, out chosenDest))
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(job.Id.Value);
                    return true;
                }

                job.DestBuilding = chosenDest;
                dstState = _s.WorldState.Buildings.Get(chosenDest);
                free = GetDestFree(dstState, rt);

                if (free <= 0)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(job.Id.Value);
                    return true;
                }
            }

            // Hold dest claim during job
            if (_s.ClaimService != null)
            {
                var destKey = new ClaimKey(ClaimKind.StorageDest, chosenDest.Value, (int)rt);
                if (!_s.ClaimService.TryAcquire(destKey, npc))
                    return false; // waiting
            }

            int jid = job.Id.Value;
            if (!_phase.TryGetValue(jid, out var ph)) ph = 0;

            if (ph == 0)
            {
                // Clamp amount by free capacity (prevents pickup when dest can't take it)
                int want = CarryCap;
                if (free < want) want = free;
                if (want <= 0)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                // pick source lazily
                if (job.SourceBuilding.Value == 0)
                {
                    // require enough for this trip to avoid micro-haul hiding producer growth
                    if (!TryPickBestHarvestProducerSource(dstState.Anchor, rt, want, out var src))
                        return false; // nothing suitable now

                    job.SourceBuilding = src;
                }

                var srcId = job.SourceBuilding;
                if (!_s.WorldState.Buildings.Exists(srcId))
                    return false;

                var srcState = _s.WorldState.Buildings.Get(srcId);
                if (!srcState.IsConstructed || !IsHarvestProducer(srcState.DefId))
                    return false;

                // pickup claim
                if (_s.ClaimService != null)
                {
                    var srcKey = new ClaimKey(ClaimKind.StorageSource, srcId.Value, (int)rt);
                    if (!_s.ClaimService.TryAcquire(srcKey, npc))
                        return false;

                    // Teleport to source
                    npcState.Cell = srcState.Anchor;

                    int removed = _s.StorageService.Remove(srcId, rt, want);

                    // release pickup claim immediately (teleport model)
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
                    npcState.Cell = srcState.Anchor;

                    int removed = _s.StorageService.Remove(srcId, rt, want);
                    if (removed <= 0) return false;

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

                // teleport to dest
                npcState.Cell = dstState.Anchor;

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
        /// Pick a destination Warehouse/HQ with free capacity for rt.
        /// Prefer Workplace if it's a valid Warehouse/HQ and has space.
        /// Deterministic: nearest to preferred anchor, tie-break by BuildingId.
        /// </summary>
        private bool TryResolveBestDestination(ResourceType rt, BuildingId preferredWorkplace, out BuildingId best)
        {
            best = default;

            var whs = _s.WorldIndex.Warehouses; // should include HQ too if WorldIndexService tags it as warehouse
            if (whs == null || whs.Count == 0) return false;

            // Preferred anchor = workplace anchor if valid, else (0,0)
            CellPos prefAnchor = default;
            bool hasPref = preferredWorkplace.Value != 0
                           && _s.WorldState.Buildings.Exists(preferredWorkplace)
                           && _s.WorldState.Buildings.Get(preferredWorkplace).IsConstructed
                           && IsWarehouseWorkplace(_s.WorldState.Buildings.Get(preferredWorkplace).DefId);

            if (hasPref)
            {
                var ps = _s.WorldState.Buildings.Get(preferredWorkplace);
                prefAnchor = ps.Anchor;

                // If preferred has space, take it immediately
                if (GetDestFree(ps, rt) > 0)
                {
                    best = preferredWorkplace;
                    return true;
                }
            }

            int bestDist = int.MaxValue;
            int bestId = int.MaxValue;

            for (int i = 0; i < whs.Count; i++)
            {
                var bid = whs[i];
                if (!_s.WorldState.Buildings.Exists(bid)) continue;

                var bs = _s.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;
                if (!IsWarehouseWorkplace(bs.DefId)) continue;

                int free = GetDestFree(bs, rt);
                if (free <= 0) continue;

                // distance from preferred anchor if exists, else from this building's anchor (tie-break by id only)
                int d = hasPref ? Manhattan(prefAnchor, bs.Anchor) : 0;
                int idv = bid.Value;

                if (d < bestDist || (d == bestDist && idv < bestId))
                {
                    bestDist = d;
                    bestId = idv;
                    best = bid;
                }
            }

            return best.Value != 0;
        }

        private bool TryPickBestHarvestProducerSource(CellPos from, ResourceType rt, int minRequired, out BuildingId best)
        {
            best = default;

            var producers = _s.WorldIndex.Producers;
            if (producers == null || producers.Count == 0) return false;

            int bestDist = int.MaxValue;
            int bestId = int.MaxValue;

            for (int i = 0; i < producers.Count; i++)
            {
                var bid = producers[i];
                if (!_s.WorldState.Buildings.Exists(bid)) continue;

                var bs = _s.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                // Day12 filter: DO NOT pick Forge
                if (!IsHarvestProducer(bs.DefId)) continue;
                if (HarvestResourceType(bs.DefId) != rt) continue;

                int amt = GetAmountFromBuilding(bs, rt);
                if (amt < minRequired) continue;

                int d = Manhattan(from, bs.Anchor);
                int idv = bid.Value;

                if (d < bestDist || (d == bestDist && idv < bestId))
                {
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

        private static int Manhattan(CellPos a, CellPos b)
        {
            int dx = a.X - b.X; if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
            return dx + dy;
        }

        private static bool IsWarehouseWorkplace(string defId)
        {
            return EqualsIgnoreCase(defId, "Warehouse")
                || EqualsIgnoreCase(defId, "HQ");
        }

        private static bool IsHarvestProducer(string defId)
        {
            return EqualsIgnoreCase(defId, "Farm")
                || EqualsIgnoreCase(defId, "Lumber")
                || EqualsIgnoreCase(defId, "Quarry")
                || EqualsIgnoreCase(defId, "IronHut");
        }

        private static ResourceType HarvestResourceType(string defId)
        {
            if (EqualsIgnoreCase(defId, "Farm")) return ResourceType.Food;
            if (EqualsIgnoreCase(defId, "Lumber")) return ResourceType.Wood;
            if (EqualsIgnoreCase(defId, "Quarry")) return ResourceType.Stone;
            if (EqualsIgnoreCase(defId, "IronHut")) return ResourceType.Iron;
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

            if (EqualsIgnoreCase(dstState.DefId, "Warehouse"))
            {
                cap = rt switch
                {
                    ResourceType.Wood or ResourceType.Food or ResourceType.Stone or ResourceType.Iron
                        => lvl == 1 ? 300 : lvl == 2 ? 600 : 1000,
                    _ => 0
                };
            }
            else if (EqualsIgnoreCase(dstState.DefId, "HQ"))
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

        private static bool EqualsIgnoreCase(string a, string b)
        {
            if (a == null || b == null) return false;
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
