using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class BuildJobPlanner : IBuildJobOrchestrator
    {
        private readonly GameServices _s;
        private readonly Dictionary<int, List<JobId>> _deliverJobsBySite;
        private readonly Dictionary<int, JobId> _workJobBySite;

        public BuildJobPlanner(
            GameServices s,
            Dictionary<int, List<JobId>> deliverJobsBySite,
            Dictionary<int, JobId> workJobBySite)
        {
            _s = s;
            _deliverJobsBySite = deliverJobsBySite;
            _workJobBySite = workJobBySite;
        }

        public void EnsureBuildJobsForSite(SiteId siteId, BuildSiteState site, BuildingId workplace)
        {
            if (_s.JobBoard == null) return;

            if (_deliverJobsBySite.TryGetValue(siteId.Value, out var list))
                PruneTerminal(list);

            if (_workJobBySite.TryGetValue(siteId.Value, out var wid))
            {
                if (!_s.JobBoard.TryGet(wid, out var wj) || IsTerminal(wj.Status))
                {
                    _workJobBySite.Remove(siteId.Value);
                }
                else
                {
                    // Retarget queued build work when builder availability changes.
                    if (wj.Status == JobStatus.Created && wj.Workplace.Value != workplace.Value)
                    {
                        wj.Workplace = workplace;
                        _s.JobBoard.Update(wj);
                    }
                }
            }

            CancelDeliveryJobs(siteId);

            if (_workJobBySite.ContainsKey(siteId.Value))
                return;

            var j = new Job
            {
                Archetype = JobArchetype.BuildWork,
                Status = JobStatus.Created,
                Workplace = workplace,
                SourceBuilding = default,
                DestBuilding = default,
                Site = siteId,
                Tower = default,
                ResourceType = 0,
                Amount = 0,
                TargetCell = site.Anchor,
                CreatedAt = 0
            };

            var newId = _s.JobBoard.Enqueue(j);
            _workJobBySite[siteId.Value] = newId;
        }

        public void CancelTrackedJobsForSite(SiteId siteId)
        {
            CancelDeliveryJobs(siteId);

            if (_workJobBySite.TryGetValue(siteId.Value, out var wid))
            {
                _s.JobBoard.Cancel(wid);
                _workJobBySite.Remove(siteId.Value);
            }
        }

        private void CancelDeliveryJobs(SiteId siteId)
        {
            if (_deliverJobsBySite.TryGetValue(siteId.Value, out var list))
            {
                for (int i = 0; i < list.Count; i++)
                    _s.JobBoard.Cancel(list[i]);
                list.Clear();
                _deliverJobsBySite.Remove(siteId.Value);
            }
        }

        private void PruneTerminal(List<JobId> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var id = list[i];
                if (!_s.JobBoard.TryGet(id, out var j) || IsTerminal(j.Status))
                    list.RemoveAt(i);
            }
        }

        private static bool IsTerminal(JobStatus s)
        {
            return s == JobStatus.Completed || s == JobStatus.Failed || s == JobStatus.Cancelled;
        }
    }
}
