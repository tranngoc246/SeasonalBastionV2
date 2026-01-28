using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// Day24: Move Ammo from Forge -> Armory in chunks.
    /// Job:
    /// - Workplace: Armory (so Armory-role NPC claims)
    /// - SourceBuilding: Forge
    /// - DestBuilding: Armory (usually equals Workplace)
    /// - ResourceType: Ammo
    /// - Amount: requested chunk (provider computes); executor clamps by available/free.
    /// </summary>
    public sealed class HaulAmmoToArmoryExecutor : IJobExecutor
    {
        private readonly GameServices _s;

        // jobId -> phase (0 pickup, 1 deliver)
        private readonly Dictionary<int, byte> _phase = new();
        private readonly Dictionary<int, int> _carry = new();

        public HaulAmmoToArmoryExecutor(GameServices s) { _s = s; }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            if (_s.WorldState == null || _s.StorageService == null || _s.AgentMover == null)
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value);
                return true;
            }

            if (job.ResourceType != ResourceType.Ammo)
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value);
                return true;
            }

            var src = job.SourceBuilding;
            var dst = job.DestBuilding.Value != 0 ? job.DestBuilding : job.Workplace;

            if (src.Value == 0 || dst.Value == 0)
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value);
                return true;
            }

            if (!_s.WorldState.Buildings.Exists(src) || !_s.WorldState.Buildings.Exists(dst))
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value);
                return true;
            }

            var srcState = _s.WorldState.Buildings.Get(src);
            var dstState = _s.WorldState.Buildings.Get(dst);

            if (!srcState.IsConstructed || !dstState.IsConstructed)
            {
                job.Status = JobStatus.Cancelled;
                Cleanup(job.Id.Value);
                return true;
            }

            // Hard gate: must be able to store ammo at both ends (StorageService enforces ammo only Forge/Armory)
            if (!_s.StorageService.CanStore(src, ResourceType.Ammo) || !_s.StorageService.CanStore(dst, ResourceType.Ammo))
            {
                job.Status = JobStatus.Cancelled;
                Cleanup(job.Id.Value);
                return true;
            }

            int dstFree = _s.StorageService.GetCap(dst, ResourceType.Ammo) - _s.StorageService.GetAmount(dst, ResourceType.Ammo);
            if (dstFree <= 0)
            {
                job.Status = JobStatus.Cancelled;
                Cleanup(job.Id.Value);
                return true;
            }

            int jid = job.Id.Value;
            if (!_phase.TryGetValue(jid, out var ph)) ph = 0;

            if (ph == 0)
            {
                int want = job.Amount > 0 ? job.Amount : 1;
                // Day38: hard cap carry
                if (want > 80) want = 80;
                if (want > dstFree) want = dstFree;

                int srcAvail = _s.StorageService.GetAmount(src, ResourceType.Ammo);
                if (srcAvail <= 0) return false;
                if (want > srcAvail) want = srcAvail;
                if (want <= 0) return false;

                // Move to source
                job.TargetCell = srcState.Anchor;
                job.Status = JobStatus.InProgress;

                bool arrivedSrc = _s.AgentMover.StepToward(ref npcState, srcState.Anchor);
                if (!arrivedSrc) return true;

                int removed = _s.StorageService.Remove(src, ResourceType.Ammo, want);
                if (removed <= 0) return false;

                _carry[jid] = removed;
                _phase[jid] = 1;
                job.Amount = removed;
                return true;
            }
            else
            {
                if (!_carry.TryGetValue(jid, out int carried) || carried <= 0)
                {
                    job.Status = JobStatus.Failed;
                    Cleanup(jid);
                    return true;
                }

                // Move to dest
                job.TargetCell = dstState.Anchor;
                job.Status = JobStatus.InProgress;

                bool arrivedDst = _s.AgentMover.StepToward(ref npcState, dstState.Anchor);
                if (!arrivedDst) return true;

                int added = _s.StorageService.Add(dst, ResourceType.Ammo, carried);

                // Refund remainder back to source (best-effort)
                int refund = carried - added;
                if (refund > 0 && _s.WorldState.Buildings.Exists(src))
                    _s.StorageService.Add(src, ResourceType.Ammo, refund);

                job.Status = JobStatus.Completed;
                Cleanup(jid);
                return true;
            }
        }

        private void Cleanup(int jobId)
        {
            _phase.Remove(jobId);
            _carry.Remove(jobId);
        }
    }
}
