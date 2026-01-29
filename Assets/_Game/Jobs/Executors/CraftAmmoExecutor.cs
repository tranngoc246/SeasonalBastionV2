using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class CraftAmmoExecutor : IJobExecutor
    {
        private readonly GameServices _s;

        // jobId -> remaining craft time
        private readonly Dictionary<int, float> _remain = new();

        // LOCKED recipe
        private const int InIron = 2;
        private const int InWood = 1;
        private const int OutAmmo = 10;
        private const float CraftTime = 6f;

        public CraftAmmoExecutor(GameServices s) { _s = s; }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            if (_s.WorldState == null || _s.StorageService == null || _s.AgentMover == null)
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value);
                return true;
            }

            var forge = job.Workplace;
            if (forge.Value == 0 || !_s.WorldState.Buildings.Exists(forge))
            {
                job.Status = JobStatus.Failed;
                Cleanup(job.Id.Value);
                return true;
            }

            var bs = _s.WorldState.Buildings.Get(forge);
            if (!bs.IsConstructed)
                return false;

            // Move to Forge anchor first
            job.TargetCell = bs.Anchor;
            job.Status = JobStatus.InProgress;

            bool arrived = _s.AgentMover.StepToward(ref npcState, bs.Anchor);
            if (!arrived)
                return true;

            int jid = job.Id.Value;

            // Start craft: consume inputs once
            if (!_remain.TryGetValue(jid, out var rem))
            {
                // Need local inputs IN FORGE
                int iron = _s.StorageService.GetAmount(forge, ResourceType.Iron);
                int wood = _s.StorageService.GetAmount(forge, ResourceType.Wood);

                // Need local ammo space for output
                int ammoCap = _s.StorageService.GetCap(forge, ResourceType.Ammo);
                int ammoCur = _s.StorageService.GetAmount(forge, ResourceType.Ammo);
                if (ammoCap <= 0 || (ammoCap - ammoCur) < OutAmmo)
                {
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                if (iron < InIron || wood < InWood)
                {
                    // Not enough input in forge => cancel so NPC can do other jobs;
                    // AmmoService will enqueue haul-to-forge and re-enqueue craft later.
                    job.Status = JobStatus.Cancelled;
                    Cleanup(jid);
                    return true;
                }

                // consume (craft spent = inputs actually removed from forge)
                int remIron = _s.StorageService.Remove(forge, ResourceType.Iron, InIron);
                if (remIron > 0)
                    _s.EventBus?.Publish(new ResourceSpentEvent(ResourceType.Iron, remIron, forge));

                int remWood = _s.StorageService.Remove(forge, ResourceType.Wood, InWood);
                if (remWood > 0)
                    _s.EventBus?.Publish(new ResourceSpentEvent(ResourceType.Wood, remWood, forge));

                rem = CraftTime;
                _remain[jid] = rem;
            }

            // Work time
            rem -= dt;
            if (rem > 0f)
            {
                _remain[jid] = rem;
                return true;
            }

            // Finish: deposit ammo to forge
            _s.StorageService.Add(forge, ResourceType.Ammo, OutAmmo);

            job.ResourceType = ResourceType.Ammo;
            job.Amount = OutAmmo;
            job.Status = JobStatus.Completed;

            Cleanup(jid);
            return true;
        }

        private void Cleanup(int jobId)
        {
            _remain.Remove(jobId);
        }
    }
}
