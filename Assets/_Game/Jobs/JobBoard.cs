// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class JobBoard : IJobBoard
    {
        private readonly System.Collections.Generic.Dictionary<int, Job> _jobs = new();
        private readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.Queue<int>> _queues = new();
        private int _nextId = 1;

        public JobId Enqueue(Job job)
        {
            job.Id = new JobId(_nextId++);
            _jobs[job.Id.Value] = job;

            var key = job.Workplace.Value;
            if (!_queues.TryGetValue(key, out var q)) _queues[key] = q = new System.Collections.Generic.Queue<int>();
            q.Enqueue(job.Id.Value);
            return job.Id;
        }

        public bool TryPeekForWorkplace(BuildingId workplace, out Job job)
        {
            job = default;
            if (!_queues.TryGetValue(workplace.Value, out var q) || q.Count == 0) return false;

            // TODO: skip cancelled/completed stale ids (while loop)
            var id = q.Peek();
            return _jobs.TryGetValue(id, out job);
        }

        public bool TryClaim(JobId id, NpcId npc)
        {
            if (!_jobs.TryGetValue(id.Value, out var j)) return false;
            if (j.Status != JobStatus.Created) return false;
            j.Status = JobStatus.Claimed;
            j.ClaimedBy = npc;
            _jobs[id.Value] = j;
            return true;
        }

        public bool TryGet(JobId id, out Job job) => _jobs.TryGetValue(id.Value, out job);

        public void Update(Job job) => _jobs[job.Id.Value] = job;

        public void Cancel(JobId id)
        {
            if (_jobs.TryGetValue(id.Value, out var j))
            {
                j.Status = JobStatus.Cancelled;
                _jobs[id.Value] = j;
            }
        }

        public int CountForWorkplace(BuildingId workplace) =>
            _queues.TryGetValue(workplace.Value, out var q) ? q.Count : 0;
    }
}
