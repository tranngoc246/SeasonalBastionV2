using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class JobBoard : IJobBoard
    {
        private readonly Dictionary<int, Job> _jobs = new();
        private readonly Dictionary<int, Queue<int>> _queues = new();
        // Reused buffer for scans (avoid per-call allocations)
        private readonly List<int> _scanBuf = new(64);
        private int _nextId = 1;

        public JobId Enqueue(Job job)
        {
            job.Id = new JobId(_nextId++);
            _jobs[job.Id.Value] = job;

            var key = job.Workplace.Value;
            if (!_queues.TryGetValue(key, out var q)) _queues[key] = q = new Queue<int>();
            q.Enqueue(job.Id.Value);
            return job.Id;
        }

        public bool TryPeekForWorkplace(BuildingId workplace, out Job job)
        {
            job = default;
            if (!_queues.TryGetValue(workplace.Value, out var q) || q.Count == 0) return false;

            CleanFront(q);
            if (q.Count == 0) return false;

            int id = q.Peek();
            return _jobs.TryGetValue(id, out job);
        }

        /// <summary>
        /// Day13: peek the first non-stale job in queue that is allowed by workplace roles.
        /// Keeps queue order stable (no rotation side effects).
        /// </summary>
        public bool TryPeekForWorkplaceFiltered(BuildingId workplace, WorkRoleFlags allowed, out Job job)
        {
            job = default;
            if (!_queues.TryGetValue(workplace.Value, out var q) || q.Count == 0) return false;

            // We must preserve order. We do a single pass into _scanBuf, then restore.
            _scanBuf.Clear();

            int n = q.Count;
            int candidate = 0;
            for (int i = 0; i < n; i++)
            {
                int id = q.Dequeue();
                if (!_jobs.TryGetValue(id, out var j)) continue;
                if (IsStale(j.Status)) continue;

                _scanBuf.Add(id);

                if (candidate == 0 && IsAllowed(allowed, j.Archetype))
                    candidate = id;
            }

            for (int i = 0; i < _scanBuf.Count; i++)
                q.Enqueue(_scanBuf[i]);

            if (candidate == 0) return false;
            return _jobs.TryGetValue(candidate, out job);
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

        public int CountForWorkplace(BuildingId workplace)
        {
            if (!_queues.TryGetValue(workplace.Value, out var q)) return 0;
            CleanFront(q);
            return q.Count;
        }

        private static bool IsStale(JobStatus s)
        {
            return s == JobStatus.Completed
                || s == JobStatus.Failed
                || s == JobStatus.Cancelled;
        }

        private static bool IsAllowed(WorkRoleFlags allowed, JobArchetype archetype)
        {
            return archetype switch
            {
                JobArchetype.Harvest => (allowed & WorkRoleFlags.Harvest) != 0,
                JobArchetype.HaulBasic => (allowed & WorkRoleFlags.HaulBasic) != 0,
                JobArchetype.BuildDeliver or JobArchetype.BuildWork => (allowed & WorkRoleFlags.Build) != 0,
                JobArchetype.CraftAmmo => (allowed & WorkRoleFlags.Craft) != 0,
                JobArchetype.ResupplyTower => (allowed & WorkRoleFlags.Armory) != 0,
                _ => true,
            };
        }

        private void CleanFront(Queue<int> q)
        {
            while (q.Count > 0)
            {
                int id = q.Peek();
                if (!_jobs.TryGetValue(id, out var j)) { q.Dequeue(); continue; }
                if (IsStale(j.Status)) { q.Dequeue(); continue; }
                break;
            }
        }
    }
}
