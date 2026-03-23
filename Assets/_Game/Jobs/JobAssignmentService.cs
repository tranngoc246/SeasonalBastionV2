using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class JobAssignmentService
    {
        private readonly IWorldState _w;
        private readonly IJobBoard _board;
        private readonly JobWorkplacePolicy _workplacePolicy;
        private readonly JobNotificationPolicy _notificationPolicy;

        internal JobAssignmentService(
            IWorldState w,
            IJobBoard board,
            JobWorkplacePolicy workplacePolicy,
            JobNotificationPolicy notificationPolicy)
        {
            _w = w;
            _board = board;
            _workplacePolicy = workplacePolicy;
            _notificationPolicy = notificationPolicy;
        }

        internal bool TryAssign(
            NpcId npc,
            ref NpcState ns,
            System.Func<ResourceType, bool> anyHarvestProducerHasAmount)
        {
            if (!_w.Buildings.Exists(ns.Workplace)) return false;

            var wps = _w.Buildings.Get(ns.Workplace);
            var allowed = _workplacePolicy.GetAllowedRoles(wps.DefId);
            if (allowed == WorkRoleFlags.None)
            {
                _notificationPolicy.NotifyNoJobs(ns.Workplace, wps.DefId);
                return false;
            }

            Job peek;
            if (_board is JobBoard jb)
            {
                if (!jb.TryPeekForWorkplaceFiltered(ns.Workplace, allowed, out peek))
                {
                    _notificationPolicy.NotifyNoJobs(ns.Workplace, wps.DefId);
                    return false;
                }
            }
            else
            {
                if (!_board.TryPeekForWorkplace(ns.Workplace, out peek))
                {
                    _notificationPolicy.NotifyNoJobs(ns.Workplace, wps.DefId);
                    return false;
                }

                if (!_workplacePolicy.IsJobAllowed(allowed, peek.Archetype))
                {
                    _notificationPolicy.NotifyNoJobs(ns.Workplace, wps.DefId);
                    return false;
                }
            }

            if (peek.Archetype == JobArchetype.HaulBasic && (anyHarvestProducerHasAmount == null || !anyHarvestProducerHasAmount(peek.ResourceType)))
                return false;

            if (!_board.TryClaim(peek.Id, npc))
                return false;

            if (!_board.TryGet(peek.Id, out var job))
                return false;

            job.Status = JobStatus.InProgress;
            job.ClaimedBy = npc;
            _board.Update(job);

            ns.CurrentJob = job.Id;
            ns.IsIdle = false;
            return true;
        }
    }
}
