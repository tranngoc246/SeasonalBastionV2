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
            int jid = job.Id.Value;

            if (_s.WorldState == null || _s.StorageService == null || _s.AgentMover == null)
            {
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            if (job.ResourceType != ResourceType.Ammo)
            {
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            // Resolve armory building (workplace preferred)
            var armoryBld = job.Workplace.Value != 0 ? job.Workplace : job.SourceBuilding;
            var towerId = job.Tower;

            if (armoryBld.Value == 0 || towerId.Value == 0)
            {
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            // Hardening: external cancel -> refund carry to armory (best-effort) + cleanup
            if (job.Status == JobStatus.Cancelled)
            {
                if (_s.WorldState.Buildings.Exists(armoryBld))
                {
                    if (_carry.TryGetValue(jid, out int carriedAmmo) && carriedAmmo > 0)
                        _s.StorageService.Add(armoryBld, ResourceType.Ammo, carriedAmmo);
                }

                Cleanup(jid);
                return true;
            }

            if (!_s.WorldState.Buildings.Exists(armoryBld))
            {
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            var armSt = _s.WorldState.Buildings.Get(armoryBld);
            if (!armSt.IsConstructed)
            {
                // Armory no longer usable -> cancel without refund (we did not remove yet in phase 0)
                // If already carrying (phase 1) we will refund below when relevant.
                job.Status = JobStatus.Cancelled;

                // If already carrying, refund best-effort
                if (_carry.TryGetValue(jid, out int carriedAmmo) && carriedAmmo > 0)
                    _s.StorageService.Add(armoryBld, ResourceType.Ammo, carriedAmmo);

                Cleanup(jid);
                return true;
            }

            if (!_s.StorageService.CanStore(armoryBld, ResourceType.Ammo))
            {
                job.Status = JobStatus.Cancelled;

                // If already carrying, refund best-effort
                if (_carry.TryGetValue(jid, out int carriedAmmo) && carriedAmmo > 0)
                    _s.StorageService.Add(armoryBld, ResourceType.Ammo, carriedAmmo);

                Cleanup(jid);
                return true;
            }

            if (!_phase.TryGetValue(jid, out var ph)) ph = 0;

            // ---------------- Phase 0: pickup from Armory ----------------
            if (ph == 0)
            {
                int want = job.Amount > 0 ? job.Amount : 1;

                int avail = _s.StorageService.GetAmount(armoryBld, ResourceType.Ammo);
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

                int removed = _s.StorageService.Remove(armoryBld, ResourceType.Ammo, want);
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
            if (!_carry.TryGetValue(jid, out int carriedNow) || carriedNow <= 0)
            {
                job.Status = JobStatus.Failed;
                Cleanup(jid);
                return true;
            }

            if (!_s.WorldState.Towers.Exists(towerId))
            {
                // Refund to Armory (best-effort)
                _s.StorageService.Add(armoryBld, ResourceType.Ammo, carriedNow);

                job.Status = JobStatus.Cancelled;
                Cleanup(jid);
                return true;
            }

            // Re-fetch newest tower state to avoid stale overwrite if multiple deliveries happen.
            var tsNow = _s.WorldState.Towers.Get(towerId);

            int free = tsNow.AmmoCap - tsNow.Ammo;
            if (free < 0) free = 0;

            int add = carriedNow;
            if (add > free) add = free;

            if (add > 0)
            {
                tsNow.Ammo += add;
                _s.WorldState.Towers.Set(towerId, tsNow);

                _s.AmmoService?.NotifyTowerAmmoChanged(towerId, tsNow.Ammo, tsNow.AmmoCap);
            }

            int refund = carriedNow - add;
            if (refund > 0)
                _s.StorageService.Add(armoryBld, ResourceType.Ammo, refund);

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
