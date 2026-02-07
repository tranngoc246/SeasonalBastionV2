using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// Day23: Haul input resources (Wood/Iron/...) from nearest storage (HQ/Warehouse)
    /// into a Forge (DestBuilding), so CraftAmmo can use local inputs.
    ///
    /// Workplace: who does the hauling (Armory preferred, else HQ/Warehouse)
    /// DestBuilding: Forge
    /// ResourceType: Wood or Iron
    /// Amount: optional "at least" request; executor will clamp by carry & dest free
    /// </summary>
    public sealed class HaulToForgeExecutor : IJobExecutor
    {
        private readonly GameServices _s;
        private const int CarryCap = 10;

        // jobId -> phase (0 pickup, 1 deliver)
        private readonly Dictionary<int, byte> _phase = new();
        private readonly Dictionary<int, int> _carry = new();

        private readonly Dictionary<int, float> _settle = new();
        private const float HaulForgeSettleSec = 1.0f;

        public HaulToForgeExecutor(GameServices s) { _s = s; }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            if (_s.WorldState == null || _s.StorageService == null || _s.ResourceFlowService == null || _s.AgentMover == null)
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value, npc);
                return true;
            }

            var rt = job.ResourceType;

            // Dest must be Forge
            var dst = job.DestBuilding;
            if (dst.Value == 0 || !_s.WorldState.Buildings.Exists(dst))
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value, npc);
                return true;
            }

            var dstState = _s.WorldState.Buildings.Get(dst);
            if (!dstState.IsConstructed)
                return false;

            // Must be storable at dest (Forge must have capWood/capIron)
            if (!_s.StorageService.CanStore(dst, rt))
            {
                job.Status = JobStatus.Cancelled;
                Cleanup(job.Id.Value, npc);
                return true;
            }

            int cap = _s.StorageService.GetCap(dst, rt);
            int cur = _s.StorageService.GetAmount(dst, rt);
            int free = cap - cur;
            if (free <= 0)
            {
                job.Status = JobStatus.Cancelled;
                Cleanup(job.Id.Value, npc);
                return true;
            }

            int jid = job.Id.Value;

            // Hardening: external cancel -> refund carry to source (best-effort) + cleanup
            if (job.Status == JobStatus.Cancelled)
            {
                RefundToSourceIfCarrying(jid, ref job, rt);
                Cleanup(jid, npc);
                return true;
            }

            if (!_phase.TryGetValue(jid, out var ph)) ph = 0;

            // Hold dest claim during job to reduce collisions
            if (_s.ClaimService != null)
            {
                var destKey = new ClaimKey(ClaimKind.StorageDest, dst.Value, (int)rt);
                if (!_s.ClaimService.TryAcquire(destKey, npc))
                    return false;
            }

            if (ph == 0)
            {
                int want = job.Amount > 0 ? job.Amount : CarryCap;
                if (want > CarryCap) want = CarryCap;
                if (want > free) want = free;
                if (want <= 0)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid, npc);
                    return true;
                }

                // Pick/validate source storage
                if (job.SourceBuilding.Value == 0 || !_s.WorldState.Buildings.Exists(job.SourceBuilding))
                {
                    if (!_s.ResourceFlowService.TryPickSource(dstState.Anchor, rt, 1, out var pick))
                        return false;

                    job.SourceBuilding = pick.Building;
                }

                var src = job.SourceBuilding;
                if (src.Value == 0 || !_s.WorldState.Buildings.Exists(src))
                    return false;

                var srcState = _s.WorldState.Buildings.Get(src);
                if (!srcState.IsConstructed)
                    return false;

                if (!_s.StorageService.CanStore(src, rt))
                {
                    job.SourceBuilding = default;
                    return false;
                }

                int avail = _s.StorageService.GetAmount(src, rt);
                if (avail <= 0)
                {
                    job.SourceBuilding = default; // repick next tick
                    return false;
                }

                if (avail < want) want = avail;

                // Move to source ENTRY
                var srcEntry = EntryCellUtil.GetApproachCellForBuilding(_s, srcState, npcState.Cell);

                job.TargetCell = srcEntry;
                job.Status = JobStatus.InProgress;

                bool arrivedSrc = _s.AgentMover.StepToward(ref npcState, srcEntry, dt);
                if (!arrivedSrc)
                    return true;

                // Stand still before pickup
                if (!_settle.TryGetValue(jid, out var remP))
                    remP = HaulForgeSettleSec;

                remP -= dt;
                if (remP > 0f)
                {
                    _settle[jid] = remP;
                    return true;
                }
                _settle.Remove(jid);

                // Claim source during remove
                if (_s.ClaimService != null)
                {
                    var srcKey = new ClaimKey(ClaimKind.StorageSource, src.Value, (int)rt);
                    if (!_s.ClaimService.TryAcquire(srcKey, npc))
                        return false;

                    int removed = _s.StorageService.Remove(src, rt, want);

                    _s.ClaimService.Release(srcKey, npc);

                    if (removed <= 0)
                        return false;

                    _carry[jid] = removed;
                }
                else
                {
                    int removed = _s.StorageService.Remove(src, rt, want);
                    if (removed <= 0)
                        return false;
                    _carry[jid] = removed;
                }

                _phase[jid] = 1;
                return true;
            }
            else
            {
                if (!_carry.TryGetValue(jid, out int carrying) || carrying <= 0)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid, npc);
                    return true;
                }

                // Move to dest ENTRY (Forge)
                var dstEntry = EntryCellUtil.GetApproachCellForBuilding(_s, dstState, npcState.Cell);

                job.TargetCell = dstEntry;
                job.Status = JobStatus.InProgress;

                bool arrivedDst = _s.AgentMover.StepToward(ref npcState, dstEntry, dt);
                if (!arrivedDst)
                    return true;

                // Stand still before deposit
                if (!_settle.TryGetValue(jid, out var remD))
                    remD = HaulForgeSettleSec;

                remD -= dt;
                if (remD > 0f)
                {
                    _settle[jid] = remD;
                    return true;
                }
                _settle.Remove(jid);

                int added = _s.StorageService.Add(dst, rt, carrying);
                // if added < carrying, remainder is dropped (should be rare since we checked free, but safe)

                job.Amount = added;
                job.Status = JobStatus.Completed;
                Cleanup(jid, npc);
                return true;
            }
        }

        private void RefundToSourceIfCarrying(int jobId, ref Job job, ResourceType rt)
        {
            if (_s.WorldState == null || _s.StorageService == null) return;
            if (!_carry.TryGetValue(jobId, out int carried) || carried <= 0) return;

            var src = job.SourceBuilding;
            if (src.Value != 0 && _s.WorldState.Buildings.Exists(src))
                _s.StorageService.Add(src, rt, carried);

            _carry.Remove(jobId);
            _phase.Remove(jobId);
        }

        private void Cleanup(int jobId, NpcId npc)
        {
            _phase.Remove(jobId);
            _carry.Remove(jobId);
            _settle.Remove(jobId);
            // Claims will also be released by JobScheduler.ReleaseAll on terminal,
            // so we don't need extra releases here.
        }
    }
}
