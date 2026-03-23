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
                EnqueueHarvestJobsIfNeeded();
                EnqueueHaulJobsIfNeeded();

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

        // -------------------------
        // Enqueue logic (Day 12)
        // -------------------------

        private void EnqueueHarvestJobsIfNeeded()
        {
            for (int i = 0; i < _buildingIds.Count; i++)
            {
                var bid = _buildingIds[i];
                if (!_w.Buildings.Exists(bid)) continue;

                var bs = _w.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                // Day13: harvest only if workplace roles allow it
                if (!_workplacePolicy.HasRole(bs.DefId, WorkRoleFlags.Harvest)) continue;

                // only if there is at least 1 NPC assigned to this workplace
                if (!_workplacesWithNpc.Contains(bid.Value)) continue;

                // Số NPC assigned vào workplace này => số "slot job" tối đa.
                // Cap 2 để tránh spam job (đủ cho debug spawn + assign).
                int assigned = 1;
                _workplaceNpcCount.TryGetValue(bid.Value, out assigned);
                if (assigned < 1) assigned = 1;

                int slots = assigned;
                if (slots > 2) slots = 2;

                // Local cap gate (LOCKED caps) — giống bản cũ.
                // Lưu ý: gate này áp cho workplace, nên vẫn OK khi nhiều slot:
                // nếu local full thì không enqueue slot nào.
                var rt = _resourcePolicy.HarvestResourceType(bs.DefId);
                int cap = _resourcePolicy.HarvestLocalCap(bs.DefId, _resourcePolicy.NormalizeLevel(bs.Level));
                int cur = _resourcePolicy.GetAmountFromBuilding(bs, rt);
                if (cap > 0 && cur >= cap) continue;

                var zoneCell = _w.Zones.PickCell(rt, bs.Anchor);

                // If zone resolves to default, skip enqueue to avoid NPCs marching to (0,0).
                if (zoneCell.X == 0 && zoneCell.Y == 0)
                    continue;

                // Enqueue theo slot
                for (int slot = 0; slot < slots; slot++)
                {
                    int key = bid.Value * 4 + slot; // 0..1 (cap 2), để dư 4 cho tương lai

                    // Slot này đang có job sống thì bỏ qua (không tạo thêm)
                    if (_harvestJobByWorkplaceSlot.TryGetValue(key, out var oldId))
                    {
                        if (_board.TryGet(oldId, out var old) && !_cleanupService.IsTerminal(old.Status))
                            continue;
                    }

                    var j = new Job
                    {
                        Archetype = JobArchetype.Harvest,
                        Status = JobStatus.Created,
                        Workplace = bid,
                        SourceBuilding = bid,
                        DestBuilding = default,
                        Site = default,
                        Tower = default,
                        ResourceType = rt,
                        Amount = 0,
                        TargetCell = zoneCell,
                        CreatedAt = 0
                    };

                    var newId = _board.Enqueue(j);
                    _harvestJobByWorkplaceSlot[key] = newId;
                }
            }
        }

        private void EnqueueHaulJobsIfNeeded()
        {
            // Enqueue hauling jobs for Warehouse/HQ workplaces with at least one NPC.
            for (int i = 0; i < _buildingIds.Count; i++)
            {
                var wid = _buildingIds[i];
                if (!_w.Buildings.Exists(wid)) continue;

                var bs = _w.Buildings.Get(wid);
                if (!bs.IsConstructed) continue;
                if (!_workplacePolicy.HasRole(bs.DefId, WorkRoleFlags.HaulBasic)) continue;
                if (!_workplacesWithNpc.Contains(wid.Value)) continue;

                TryEnsureHaulJob(wid, bs, ResourceType.Wood);
                TryEnsureHaulJob(wid, bs, ResourceType.Food);
                TryEnsureHaulJob(wid, bs, ResourceType.Stone);
                TryEnsureHaulJob(wid, bs, ResourceType.Iron);
            }
        }

        private void TryEnsureHaulJob(BuildingId workplace, in BuildingState destState, ResourceType rt)
        {
            // Only enqueue if there is something to haul now
            if (!AnyHarvestProducerHasAmount(rt)) return;

            // Gate by THIS workplace free capacity (HQ/Warehouse)
            if (destState.Anchor.X == 0 && destState.Anchor.Y == 0)
                return;

            int cap = _resourcePolicy.DestCap(destState.DefId, _resourcePolicy.NormalizeLevel(destState.Level), rt);
            if (cap <= 0) return;

            int cur = _resourcePolicy.GetAmountFromBuilding(destState, rt);
            if (cur >= cap) return;

            // Gate only by "ANY destination has free capacity" (NOT by workplace itself).
            // This preserves reroute behavior while preventing full-dest cancel/enqueue loops.

            int key = workplace.Value * 16 + (int)rt;

            if (_haulJobByWorkplaceAndType.TryGetValue(key, out var oldId))
            {
                if (_board.TryGet(oldId, out var old) && !_cleanupService.IsTerminal(old.Status))
                    return;
            }

            var j = new Job
            {
                Archetype = JobArchetype.HaulBasic,
                Status = JobStatus.Created,
                Workplace = workplace,

                SourceBuilding = default,

                // Allow executor to pick a destination dynamically.
                // We'll still set default dest = workplace as "preferred".
                DestBuilding = workplace,

                Site = default,
                Tower = default,
                ResourceType = rt,
                Amount = 0,
                TargetCell = default,
                CreatedAt = 0
            };

            var newId = _board.Enqueue(j);
            _haulJobByWorkplaceAndType[key] = newId;
        }

        private void TryEnsureHaulJobToProducer(BuildingId workplace, in BuildingState workplaceState, ResourceType rt)
        {
            // Resolve producer destination (lumbercamp for wood, farmhouse for food)
            if (!TryGetProducerFor(rt, out var producer))
                return;

            // Only enqueue if there is something to haul now (pile exists for this producer+rt)
            if (_w.Piles == null || !_w.Piles.TryFindNonEmpty(rt, producer, out _))
                return;

            // Gate by producer local cap (avoid hauling into full producer -> cancel loops)
            if (_w.Buildings.Exists(producer))
            {
                var ps = _w.Buildings.Get(producer);
                int cap = _resourcePolicy.HarvestLocalCap(ps.DefId, _resourcePolicy.NormalizeLevel(ps.Level));
                int cur = _resourcePolicy.GetAmountFromBuilding(ps, rt);
                if (cap > 0 && cur >= cap)
                    return;
            }

            // key must include workplace + producer + rt (prevent spam per pair)
            int key = (workplace.Value * 100000) + (producer.Value * 16) + (int)rt;

            if (_haulJobByWorkplaceAndType.TryGetValue(key, out var oldId))
            {
                if (_board.TryGet(oldId, out var old) && !_cleanupService.IsTerminal(old.Status))
                    return;
            }

            var j = new Job
            {
                Archetype = JobArchetype.HaulBasic,
                Status = JobStatus.Created,
                Workplace = workplace,

                SourceBuilding = default,
                DestBuilding = producer, // IMPORTANT: deliver to producer

                Site = default,
                Tower = default,
                ResourceType = rt,
                Amount = 0,
                TargetCell = default,
                CreatedAt = 0
            };

            var newId = _board.Enqueue(j);
            _haulJobByWorkplaceAndType[key] = newId;
        }

        private bool AnyHaulDestinationHasFree(ResourceType rt)
        {
            for (int i = 0; i < _buildingIds.Count; i++)
            {
                var bid = _buildingIds[i];
                if (!_w.Buildings.Exists(bid)) continue;

                var bs = _w.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                // HARDENING: In this project, anchors should never be (0,0) (buildableRect starts at 12,12).
                // If anchor is default, RunStart/placement hasn't fully applied yet => skip enqueue this tick.
                if (bs.Anchor.X == 0 && bs.Anchor.Y == 0)
                    continue;

                // destinations are Warehouse/HQ only
                if (!_resourcePolicy.IsWarehouseWorkplace(bs.DefId)) continue;

                int cap = _resourcePolicy.DestCap(bs.DefId, _resourcePolicy.NormalizeLevel(bs.Level), rt);
                if (cap <= 0) continue;

                int cur = _resourcePolicy.GetAmountFromBuilding(bs, rt);
                if (cur < cap) return true; // has free space
            }

            return false;
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

