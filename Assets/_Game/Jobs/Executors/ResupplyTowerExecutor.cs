using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// Day26: Transporter resupply tower from Armory.
    /// Job:
    /// - Workplace: Armory (so Armory-role NPC claims)
    /// - SourceBuilding: Armory (pickup ammo)
    /// - Tower: target tower
    /// - ResourceType: Ammo
    /// - Amount: delivery chunk decided by provider; executor clamps by available / tower free.
    /// </summary>
    public sealed class ResupplyTowerExecutor : IJobExecutor
    {
        private readonly GameServices _s;

        // jobId -> phase (0 pickup, 1 deliver)
        private readonly Dictionary<int, byte> _phase = new();
        private readonly Dictionary<int, int> _carry = new();

        public ResupplyTowerExecutor(GameServices s) { _s = s; }

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

            var arm = job.Workplace.Value != 0 ? job.Workplace : job.SourceBuilding;
            var tower = job.Tower;

            if (arm.Value == 0 || tower.Value == 0)
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value);
                return true;
            }

            if (!_s.WorldState.Buildings.Exists(arm))
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value);
                return true;
            }

            var armSt = _s.WorldState.Buildings.Get(arm);
            if (!armSt.IsConstructed)
            {
                job.Status = JobStatus.Cancelled;
                Cleanup(job.Id.Value);
                return true;
            }

            if (!_s.StorageService.CanStore(arm, ResourceType.Ammo))
            {
                job.Status = JobStatus.Cancelled;
                Cleanup(job.Id.Value);
                return true;
            }

            int jid = job.Id.Value;
            if (!_phase.TryGetValue(jid, out var ph)) ph = 0;

            // ---------------- Phase 0: pickup from Armory ----------------
            if (ph == 0)
            {
                int want = job.Amount > 0 ? job.Amount : 1;

                int avail = _s.StorageService.GetAmount(arm, ResourceType.Ammo);
                if (avail <= 0)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                if (want > avail) want = avail;
                if (want <= 0)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                job.TargetCell = armSt.Anchor;
                job.Status = JobStatus.InProgress;

                bool arrived = _s.AgentMover.StepToward(ref npcState, armSt.Anchor);
                if (!arrived) return true;

                int removed = _s.StorageService.Remove(arm, ResourceType.Ammo, want);
                if (removed <= 0)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                _carry[jid] = removed;
                _phase[jid] = 1;
                job.Amount = removed; // actual carry
                return true;
            }

            // ---------------- Phase 1: deliver to Tower ----------------
            if (!_carry.TryGetValue(jid, out int carried) || carried <= 0)
            {
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            if (!_s.WorldState.Towers.Exists(tower))
            {
                // Refund to Armory (best-effort)
                _s.StorageService.Add(arm, ResourceType.Ammo, carried);

                job.Status = JobStatus.Cancelled;
                Cleanup(jid);
                return true;
            }

            // Đã tới tower => luôn re-fetch state mới nhất để tránh stale overwrite khi có job khác deliver trước đó
            var tsNow = _s.WorldState.Towers.Get(tower);

            int free = tsNow.AmmoCap - tsNow.Ammo;
            if (free < 0) free = 0;

            int add = carried;
            if (add > free) add = free;

            if (add > 0)
            {
                tsNow.Ammo += add;
                _s.WorldState.Towers.Set(tower, tsNow);

                // optional: cập nhật monitor/UI
                _s.AmmoService?.NotifyTowerAmmoChanged(tower, tsNow.Ammo, tsNow.AmmoCap);
            }

            // refund phần dư
            int refund = carried - add;
            if (refund > 0)
                _s.StorageService.Add(arm, ResourceType.Ammo, refund);

            job.Status = JobStatus.Completed;
            Cleanup(jid);
            return true;
        }

        private void Cleanup(int jobId)
        {
            _phase.Remove(jobId);
            _carry.Remove(jobId);
        }
    }
}
