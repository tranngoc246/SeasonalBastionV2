using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class JobScheduler : IJobScheduler, ITickable
    {
        private readonly GameServices _s;
        private readonly IWorldState _w;
        private readonly IJobBoard _board;
        private readonly IClaimService _claims;

        private readonly JobStateCleanupService _cleanupService;
        private readonly JobSchedulerCache _cacheService;
        private readonly JobAssignmentService _assignmentService;
        private readonly JobEnqueueService _enqueueService;
        private readonly JobExecutionService _executionService;
        private readonly NpcIdleRoamService _idleRoamService;

        public int AssignedThisTick { get; private set; }

        private readonly List<NpcId> _npcIds = new(64);
        private readonly List<BuildingId> _buildingIds = new(128);
        private readonly HashSet<int> _workplacesWithNpc = new();

        // key = workplaceId * 4 + slotIndex (0..3). v0.1 dùng 0..1 (cap 2)
        private readonly Dictionary<int, JobId> _harvestJobByWorkplaceSlot = new();
        private readonly Dictionary<int, JobId> _haulJobByWorkplaceAndType = new(); // key = workplace*16 + type
        private readonly Dictionary<int, int> _workplaceNpcCount = new();

        private float _claimCleanupTimer;
        private float _maintenanceTimer;
        private bool _cacheReady;

        public JobScheduler(
            GameServices s,
            IWorldState w,
            IJobBoard board,
            IClaimService claims,
            JobExecutorRegistry exec,
            IEventBus bus,
            IDataRegistry data,
            INotificationService noti,
            IJobWorkplacePolicy workplacePolicy = null,
            IHarvestTargetSelector harvestTargetSelector = null)
        {
            _s = s;
            _w = w;
            _board = board;
            _claims = claims;

            workplacePolicy ??= new JobWorkplacePolicy(data);
            var resourcePolicy = new ResourceLogisticsPolicy();
            var notificationPolicy = new JobNotificationPolicy(noti);
            harvestTargetSelector ??= new DefaultHarvestTargetSelector();

            _cleanupService = new JobStateCleanupService(claims);
            _cacheService = new JobSchedulerCache(w);
            _assignmentService = new JobAssignmentService(w, board, workplacePolicy, notificationPolicy);
            _enqueueService = new JobEnqueueService(s, w, board, workplacePolicy, resourcePolicy, _cleanupService, harvestTargetSelector);
            _executionService = new JobExecutionService(s, w, board, exec, _cleanupService);
            _idleRoamService = new NpcIdleRoamService(s, w);
        }

        public void Tick(float dt)
        {
            AssignedThisTick = 0;

            const float MaintenanceInterval = 0.25f;
            _maintenanceTimer += dt;

            if (!_cacheReady || _maintenanceTimer >= MaintenanceInterval)
            {
                _maintenanceTimer = 0f;
                _cacheReady = true;

                RebuildCaches();

                _enqueueService.EnqueueHarvestJobsIfNeeded(
                    _buildingIds,
                    _workplacesWithNpc,
                    _workplaceNpcCount,
                    _harvestJobByWorkplaceSlot);

                _enqueueService.EnqueueHaulJobsIfNeeded(
                    _buildingIds,
                    _workplacesWithNpc,
                    _haulJobByWorkplaceAndType,
                    AnyHarvestProducerHasAmount);

                AssignedThisTick = AssignIdleNpcs();
            }

            if (!_cacheReady)
                _cacheService.BuildSortedNpcIds(_npcIds);

            TickIdleNpcs(dt);
            _executionService.TickCurrentJobs(_npcIds, dt);

            _claimCleanupTimer += dt;
            if (_claimCleanupTimer >= 2f)
            {
                _claimCleanupTimer = 0f;
                _claims?.CleanupInvalidOwners(_w.Npcs);
            }
        }

        public bool TryAssign(NpcId npc, out Job assigned)
        {
            assigned = default;
            if (!_w.Npcs.Exists(npc)) return false;

            var ns = _w.Npcs.Get(npc);
            if (!ns.IsIdle || ns.CurrentJob.Value != 0) return false;
            if (ns.Workplace.Value == 0) return false;

            if (!_assignmentService.TryAssign(npc, ref ns, AnyHarvestProducerHasAmount))
                return false;

            _w.Npcs.Set(npc, ns);
            return _board.TryGet(ns.CurrentJob, out assigned);
        }

        private void RebuildCaches()
        {
            _cacheService.BuildSortedNpcIds(_npcIds);
            _cacheService.BuildSortedBuildingIds(_buildingIds);
            _cacheService.BuildWorkplaceHasNpcSet(_npcIds, _workplacesWithNpc, _workplaceNpcCount);
        }

        private int AssignIdleNpcs()
        {
            int assignedThisTick = 0;

            for (int i = 0; i < _npcIds.Count; i++)
            {
                var nid = _npcIds[i];
                if (!_w.Npcs.Exists(nid)) continue;

                var ns = _w.Npcs.Get(nid);

                if (!ns.IsIdle || ns.CurrentJob.Value != 0)
                {
                    _w.Npcs.Set(nid, ns);
                    continue;
                }

                if (ns.Workplace.Value == 0)
                {
                    _w.Npcs.Set(nid, ns);
                    continue;
                }

                if (_assignmentService.TryAssign(nid, ref ns, AnyHarvestProducerHasAmount))
                    assignedThisTick++;

                _w.Npcs.Set(nid, ns);
            }

            return assignedThisTick;
        }

        private void TickIdleNpcs(float dt)
        {
            for (int i = 0; i < _npcIds.Count; i++)
            {
                var nid = _npcIds[i];
                if (!_w.Npcs.Exists(nid))
                    continue;

                var ns = _w.Npcs.Get(nid);
                if (!ns.IsIdle || ns.CurrentJob.Value != 0)
                {
                    _idleRoamService.ClearNpc(nid);
                    _w.Npcs.Set(nid, ns);
                    continue;
                }

                if (InteractionCellExitHelper.HasPendingStepOff(nid))
                {
                    _idleRoamService.ClearNpc(nid);
                    _w.Npcs.Set(nid, ns);
                    continue;
                }

                _idleRoamService.TickIdleNpc(nid, ref ns, dt);
                _w.Npcs.Set(nid, ns);
            }
        }

        private bool AnyHarvestProducerHasAmount(ResourceType rt)
        {
            if (_buildingIds.Count == 0)
                _cacheService.BuildSortedBuildingIds(_buildingIds);

            for (int i = 0; i < _buildingIds.Count; i++)
            {
                var bid = _buildingIds[i];
                if (!_w.Buildings.Exists(bid)) continue;

                var bs = _w.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                if (rt switch
                {
                    ResourceType.Food => DefIdTierUtil.IsBase(bs.DefId, "bld_farmhouse"),
                    ResourceType.Wood => DefIdTierUtil.IsBase(bs.DefId, "bld_lumbercamp"),
                    ResourceType.Stone => DefIdTierUtil.IsBase(bs.DefId, "bld_quarry"),
                    ResourceType.Iron => DefIdTierUtil.IsBase(bs.DefId, "bld_ironhut"),
                    _ => false,
                })
                {
                    int amount = rt switch
                    {
                        ResourceType.Wood => bs.Wood,
                        ResourceType.Food => bs.Food,
                        ResourceType.Stone => bs.Stone,
                        ResourceType.Iron => bs.Iron,
                        ResourceType.Ammo => bs.Ammo,
                        _ => 0,
                    };

                    if (amount > 0)
                        return true;
                }
            }

            return false;
        }
    }
}
