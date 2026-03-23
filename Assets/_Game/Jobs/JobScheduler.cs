using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class JobScheduler : IJobScheduler, ITickable
    {
        private readonly IWorldState _w;
        private readonly IJobBoard _board;
        private readonly IClaimService _claims;
        private readonly JobExecutorRegistry _exec;
        private readonly IEventBus _bus;
        private readonly JobWorkplacePolicy _workplacePolicy;
        private readonly ResourceLogisticsPolicy _resourcePolicy;
        private readonly JobNotificationPolicy _notificationPolicy;
        private readonly JobStateCleanupService _cleanupService;
        private readonly JobAssignmentService _assignmentService;
        private readonly JobSchedulerCache _cacheService;
        private readonly JobEnqueueService _enqueueService;

        public int AssignedThisTick { get; private set; }

        // Reuse buffers (avoid per-tick GC)
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
            IWorldState w,
            IJobBoard board,
            IClaimService claims,
            JobExecutorRegistry exec,
            IEventBus bus,
            IDataRegistry data,
            INotificationService noti)
        {
            _w = w;
            _board = board;
            _claims = claims;
            _exec = exec;
            _bus = bus;
            _workplacePolicy = new JobWorkplacePolicy(data);
            _resourcePolicy = new ResourceLogisticsPolicy();
            _notificationPolicy = new JobNotificationPolicy(noti);
            _cleanupService = new JobStateCleanupService(claims);
            _assignmentService = new JobAssignmentService(w, board, _workplacePolicy, _notificationPolicy);
            _cacheService = new JobSchedulerCache(w);
        }

        public void Tick(float dt)
        {
            AssignedThisTick = 0;

            // 0) Maintain caches + enqueue + assign only every X sim seconds (avoid heavy per-frame cost)
            const float MaintenanceInterval = 0.25f; // sim seconds
            _maintenanceTimer += dt;

            if (!_cacheReady || _maintenanceTimer >= MaintenanceInterval)
            {
                _maintenanceTimer = 0f;
                _cacheReady = true;

                _cacheService.BuildSortedNpcIds(_npcIds);
                _cacheService.BuildSortedBuildingIds(_buildingIds);
                _cacheService.BuildWorkplaceHasNpcSet(_npcIds, _workplacesWithNpc, _workplaceNpcCount);

                // Ensure jobs exist (Harvest + HaulBasic)
                _enqueueService.EnqueueHarvestJobsIfNeeded(_buildingIds, _workplacesWithNpc, _workplaceNpcCount, _harvestJobByWorkplaceSlot);
                _enqueueService.EnqueueHaulJobsIfNeeded(_buildingIds, _workplacesWithNpc, _haulJobByWorkplaceAndType, AnyHarvestProducerHasAmount);

                // Assign jobs to idle NPCs (deterministic order from cached lists)
                for (int i = 0; i < _npcIds.Count; i++)
                {
                    var nid = _npcIds[i];
                    if (!_w.Npcs.Exists(nid)) continue;

                    var ns = _w.Npcs.Get(nid);

                    if (!ns.IsIdle || ns.CurrentJob.Value != 0) { _w.Npcs.Set(nid, ns); continue; }
                    if (ns.Workplace.Value == 0) { _w.Npcs.Set(nid, ns); continue; }

                    if (_assignmentService.TryAssign(nid, ref ns, AnyHarvestProducerHasAmount))
                        AssignedThisTick++;

                    _w.Npcs.Set(nid, ns);
                }
            }

            // 1) ALWAYS tick current jobs every frame (so Build progress is smooth and respects x3)
            // If cache wasn't built yet (very first frame), build minimal npc list now.
            if (!_cacheReady)
                _cacheService.BuildSortedNpcIds(_npcIds);

            for (int i = 0; i < _npcIds.Count; i++)
            {
                var nid = _npcIds[i];
                if (!_w.Npcs.Exists(nid)) continue;

                var ns = _w.Npcs.Get(nid);
                if (ns.CurrentJob.Value == 0) { _w.Npcs.Set(nid, ns); continue; }

                if (!_board.TryGet(ns.CurrentJob, out var job))
                {
                    _cleanupService.CleanupNpcJob(nid, ref ns);
                    _w.Npcs.Set(nid, ns);
                    continue;
                }

                var executor = _exec.Get(job.Archetype);
                executor.Tick(nid, ref ns, ref job, dt);

                _board.Update(job);

                if (_cleanupService.IsTerminal(job.Status))
                    _cleanupService.CleanupNpcJob(nid, ref ns);

                _w.Npcs.Set(nid, ns);
            }

            // 2) Periodic cleanup
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

            if (!_assignmentService.TryAssign(npc, ref ns, AnyHarvestProducerHasAmount)) return false;
            _w.Npcs.Set(npc, ns);

            return _board.TryGet(ns.CurrentJob, out assigned);
        }

        private bool TryGetProducerFor(ResourceType rt, out BuildingId producer)
        {
            // Deterministic: pick smallest BuildingId that is constructed and matches resource type
            producer = default;

            for (int i = 0; i < _buildingIds.Count; i++)
            {
                var bid = _buildingIds[i];
                if (!_w.Buildings.Exists(bid)) continue;

                var bs = _w.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;
                if (!_workplacePolicy.HasRole(bs.DefId, WorkRoleFlags.Harvest)) continue;

                var prt = _resourcePolicy.HarvestResourceType(bs.DefId);
                if (prt != rt) continue;

                // M4: only wood+food producers
                if (rt == ResourceType.Wood && !DefIdTierUtil.IsBase(bs.DefId, "bld_lumbercamp")) continue;
                if (rt == ResourceType.Food && !DefIdTierUtil.IsBase(bs.DefId, "bld_farmhouse")) continue;

                producer = bid;
                return true;
            }

            return false;
        }

        private bool AnyHarvestProducerHasAmount(ResourceType rt)
        {
            for (int i = 0; i < _buildingIds.Count; i++)
            {
                var bid = _buildingIds[i];
                if (!_w.Buildings.Exists(bid)) continue;

                var bs = _w.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;
                if (!_workplacePolicy.HasRole(bs.DefId, WorkRoleFlags.Harvest)) continue;
                if (_resourcePolicy.HarvestResourceType(bs.DefId) != rt) continue;

                if (_resourcePolicy.GetAmountFromBuilding(bs, rt) > 0) return true;
            }
            return false;
        }

        private bool AnyPileHasAmount(ResourceType rt, BuildingId owner)
        {
            return _w.Piles != null && owner.Value != 0 && _w.Piles.TryFindNonEmpty(rt, owner, out _);
        }

    }
}

        {
            return _w.Piles != null && owner.Value != 0 && _w.Piles.TryFindNonEmpty(rt, owner, out _);
        }

    }
}

