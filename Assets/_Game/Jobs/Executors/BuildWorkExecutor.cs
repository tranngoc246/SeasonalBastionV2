using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class BuildWorkExecutor : IJobExecutor
    {
        private readonly GameServices _s;

        // optional: keep local phase if you want (not required now)
        private readonly HashSet<int> _started = new();

        public BuildWorkExecutor(GameServices s) { _s = s; }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            if (_s.WorldState == null || _s.AgentMover == null)
            {
                job.Status = JobStatus.Failed;
                _started.Remove(job.Id.Value);
                return true;
            }

            if (job.Site.Value == 0 || !_s.WorldState.Sites.Exists(job.Site))
            {
                job.Status = JobStatus.Cancelled;
                _started.Remove(job.Id.Value);
                return true;
            }

            var site = _s.WorldState.Sites.Get(job.Site);
            if (!site.IsActive)
            {
                job.Status = JobStatus.Cancelled;
                _started.Remove(job.Id.Value);
                return true;
            }

            // Gate: only work when no remaining costs
            if (site.RemainingCosts != null && site.RemainingCosts.Count > 0)
            {
                // wait for deliveries
                job.Status = JobStatus.InProgress;
                job.TargetCell = site.Anchor;
                return true;
            }

            // Move to site
            job.TargetCell = site.Anchor;
            job.Status = JobStatus.InProgress;

            bool arrived = _s.AgentMover.StepToward(ref npcState, site.Anchor);
            if (!arrived) return true;

            // Work progress
            if (dt > 0f)
            {
                site.WorkSecondsDone += dt;
                if (site.WorkSecondsDone > site.WorkSecondsTotal)
                    site.WorkSecondsDone = site.WorkSecondsTotal;

                _s.WorldState.Sites.Set(job.Site, site);
            }

            if (site.WorkSecondsDone + 1e-4f >= site.WorkSecondsTotal)
            {
                job.Status = JobStatus.Completed;
                _started.Remove(job.Id.Value);
                return true;
            }

            _started.Add(job.Id.Value);

            return true;
        }
    }
}
