using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class BuildJobPlanner : IBuildJobOrchestrator
    {
        private readonly IWorldState _worldState;
        private readonly IJobBoard _jobBoard;
        private readonly IPathfinderRuntime _pathfinder;
        private readonly Dictionary<int, List<JobId>> _deliverJobsBySite;
        private readonly Dictionary<int, JobId> _workJobBySite;

        public BuildJobPlanner(
            IWorldState worldState,
            IJobBoard jobBoard,
            IPathfinderRuntime pathfinder,
            Dictionary<int, List<JobId>> deliverJobsBySite,
            Dictionary<int, JobId> workJobBySite)
        {
            _worldState = worldState;
            _jobBoard = jobBoard;
            _pathfinder = pathfinder;
            _deliverJobsBySite = deliverJobsBySite;
            _workJobBySite = workJobBySite;
        }

        public void EnsureBuildJobsForSite(SiteId siteId, BuildSiteState site, BuildingId workplace)
        {
            if (_jobBoard == null) return;
            if (_worldState == null || !_worldState.Buildings.Exists(workplace)) return;

            var workplaceState = _worldState.Buildings.Get(workplace);
            var workplaceEntry = EntryCellUtil.GetApproachCellForBuilding(_worldState, workplaceState, site.Anchor);
            if (!JobReachabilityHelper.IsSiteEntryReachable(_pathfinder, site, workplaceEntry))
            {
                CancelTrackedJobsForSite(siteId);
                return;
            }

            if (_deliverJobsBySite.TryGetValue(siteId.Value, out var list))
                PruneTerminal(list);

            if (_workJobBySite.TryGetValue(siteId.Value, out var wid))
            {
                if (!_jobBoard.TryGet(wid, out var wj) || IsTerminal(wj.Status))
                {
                    _workJobBySite.Remove(siteId.Value);
                }
                else
                {
                    // Retarget queued build work when builder availability changes.
                    if (wj.Status == JobStatus.Created && wj.Workplace.Value != workplace.Value)
                    {
                        wj.Workplace = workplace;
                        _jobBoard.Update(wj);
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

            var newId = _jobBoard.Enqueue(j);
            _workJobBySite[siteId.Value] = newId;
        }

        public void CancelTrackedJobsForSite(SiteId siteId)
        {
            CancelDeliveryJobs(siteId);

            if (_workJobBySite.TryGetValue(siteId.Value, out var wid))
            {
                _jobBoard.Cancel(wid);
                _workJobBySite.Remove(siteId.Value);
            }
        }

        private void CancelDeliveryJobs(SiteId siteId)
        {
            if (_deliverJobsBySite.TryGetValue(siteId.Value, out var list))
            {
                for (int i = 0; i < list.Count; i++)
                    _jobBoard.Cancel(list[i]);
                list.Clear();
                _deliverJobsBySite.Remove(siteId.Value);
            }
        }

        private void PruneTerminal(List<JobId> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var id = list[i];
                if (!_jobBoard.TryGet(id, out var j) || IsTerminal(j.Status))
                    list.RemoveAt(i);
            }
        }

        private static bool IsTerminal(JobStatus s)
        {
            return s == JobStatus.Completed || s == JobStatus.Failed || s == JobStatus.Cancelled;
        }
    }
}
