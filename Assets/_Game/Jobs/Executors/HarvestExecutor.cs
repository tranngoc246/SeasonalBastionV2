using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class HarvestExecutor : IJobExecutor
    {
        private readonly GameServices _s;

        // jobId -> remaining work seconds
        private readonly Dictionary<int, float> _remaining = new();

        public HarvestExecutor(GameServices s) { _s = s; }


        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            if (_s.WorldState == null || _s.StorageService == null || _s.AgentMover == null)
            {
                job.Status = JobStatus.Failed;
                return true;
            }

            var producer = job.Workplace;
            if (producer.Value == 0 || !_s.WorldState.Buildings.Exists(producer))
            {
                job.Status = JobStatus.Failed;
                return true;
            }

            var bs = _s.WorldState.Buildings.Get(producer);
            if (!bs.IsConstructed)
            {
                job.Status = JobStatus.Failed;
                return true;
            }

            // Day12 harvest subset only
            if (!IsHarvestProducer(bs.DefId))
            {
                job.Status = JobStatus.Cancelled;
                return true;
            }

            var rt = HarvestResourceType(bs.DefId);
            GetHarvestParams(bs.DefId, NormalizeLevel(bs.Level), out float workSec, out int yield);

            // Claim producer node (held during moving+working; released by JobScheduler on terminal)
            if (_s.ClaimService != null)
            {
                var key = new ClaimKey(ClaimKind.ProducerNode, producer.Value, (int)rt);
                if (!_s.ClaimService.TryAcquire(key, npc))
                    return false; // waiting
            }

            job.SourceBuilding = producer;
            job.ResourceType = rt;
            job.TargetCell = bs.Anchor;
            job.Status = JobStatus.InProgress;

            // Day14: Move to anchor (no teleport)
            bool arrived = _s.AgentMover.StepToward(ref npcState, bs.Anchor);
            if (!arrived)
                return true;

            // Work timer only after arrived
            int jid = job.Id.Value;
            if (!_remaining.TryGetValue(jid, out var rem))
                rem = workSec;

            rem -= dt;
            if (rem > 0f)
            {
                _remaining[jid] = rem;
                return true;
            }

            _remaining.Remove(jid);

            // Complete => add to producer local storage
            int added = _s.StorageService.Add(producer, rt, yield);
            job.Amount = yield;

            job.Status = (added > 0) ? JobStatus.Completed : JobStatus.Failed;
            return true;
        }

        private static void GetHarvestParams(string defId, int level, out float workSec, out int yield)
        {
            // PART27 Day12 defaults (LOCKED tables)
            if (EqualsIgnoreCase(defId, "bld_farmhouse_t1"))
            {
                workSec = 6f;
                yield = level == 1 ? 6 : level == 2 ? 8 : 10;
                return;
            }

            if (EqualsIgnoreCase(defId, "bld_lumbercamp_t1"))
            {
                workSec = 4f;
                yield = level == 1 ? 6 : level == 2 ? 8 : 10;
                return;
            }

            if (EqualsIgnoreCase(defId, "bld_quarry_t1"))
            {
                workSec = 5f;
                yield = level == 1 ? 6 : level == 2 ? 8 : 10;
                return;
            }

            if (EqualsIgnoreCase(defId, "bld_ironhut_t1"))
            {
                workSec = 6f;
                yield = level == 1 ? 4 : level == 2 ? 6 : 8;
                return;
            }

            workSec = 6f;
            yield = 6;
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

        private static int NormalizeLevel(int level) => level <= 0 ? 1 : (level > 3 ? 3 : level);

        private static bool EqualsIgnoreCase(string a, string b)
        {
            if (a == null || b == null) return false;
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
