using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;

namespace SeasonalBastion
{
    public sealed class AmmoService : IAmmoService, ITickable
    {
        private readonly GameServices _services;
        private readonly AmmoTopologyCache _topologyCache;
        private readonly ArmoryBufferPlanner _armoryBufferPlanner;
        private readonly TowerResupplyPlanner _towerResupplyPlanner;
        private readonly AmmoDebugHooks _debugHooks;
        private readonly AmmoRequestQueue _requestQueue;
        private readonly AmmoResupplyTracking _resupplyTracking;
        private readonly AmmoCooldownManager _cooldownManager;
        private readonly AmmoRecoveryService _recoveryService;
        private readonly AmmoMetricsReporter _metricsReporter;
        private readonly AmmoObservabilityReporter _observabilityReporter;
        private readonly AmmoConfigProvider _configProvider;
        private readonly AmmoRecipeProvider _recipeProvider;
        private readonly AmmoCraftService _craftService;
        private readonly AmmoMonitorPolicy _monitorPolicy;
        private readonly AmmoRuntimeState _runtimeState = new();
        private readonly AmmoTowerStateTracker _towerStateTracker = new();
        private readonly AmmoObservabilityState _observability = new();

        private float _simTime;
        private float _devHookTimer;

        public bool DevHook_Enabled { get; set; } = false;
        public float DevHook_ShotInterval { get; set; } = 0.50f;
        public int DevHook_AmmoPerShot { get; set; } = 1;

        public int PendingRequests => _requestQueue.PendingRequests;
        public int Debug_InFlightResupplyJobs => _resupplyTracking.InFlightCount;
        public int Debug_InFlightHaulAmmoJobs => HaulAmmoJobByArmory.Count;
        public int Debug_PendingUrgent => UrgentRequests.Count;
        public int Debug_PendingNormal => NormalRequests.Count;
        public int Debug_TotalTowers => CurrentMetrics.TotalTowers;
        public int Debug_TowersWithoutAmmo => CurrentMetrics.TowersWithoutAmmo;
        public int Debug_ActiveResupplyJobs => CurrentMetrics.ActiveResupplyJobs;
        public int Debug_ArmoryAvailableAmmo => CurrentMetrics.ArmoryAvailableAmmo;
        public string Debug_ArmoryStatus => _observability.ArmoryStatus;
        public string Debug_ResupplyStatus => _observability.ResupplyStatus;

        internal GameServices Services => _services;
        internal float SimTime => _simTime;
        internal float DevHookTimer { get => _devHookTimer; set => _devHookTimer = value; }
        internal AmmoMetricsSnapshot CurrentMetrics => _metricsReporter.LastSnapshot;
        internal bool DebugAmmoLogsValue => DebugAmmoLogs;
        internal int ForgeTargetCraftsValue => ForgeTargetCrafts;
        internal float ReqCooldownLowValue => _configProvider.GetFloat("ammoMonitor", "reqCooldownLowSec", 8f);
        internal float ReqCooldownEmptyValue => _configProvider.GetFloat("ammoMonitor", "reqCooldownEmptySec", 4f);

        internal List<AmmoRequest> UrgentRequests => _requestQueue.UrgentRequests;
        internal List<AmmoRequest> NormalRequests => _requestQueue.NormalRequests;
        internal AmmoRequestQueue Requests => _requestQueue;
        internal AmmoCooldownManager Cooldowns => _cooldownManager;
        internal HashSet<int> PendingReqTower => _requestQueue.PendingReqTower;
        internal Dictionary<int, AmmoRequestPriority> PendingPriorityByTower => _requestQueue.PendingPriorityByTower;

        internal Dictionary<int, int> LastAmmoByTower => _towerStateTracker.LastAmmoByTower;
        internal Dictionary<int, int> LastCapByTower => _towerStateTracker.LastCapByTower;
        internal HashSet<int> TowerNoSourceLogged => _recoveryService.TowerNoSourceLogged;
        internal HashSet<int> TowerNoJobLogged => _recoveryService.TowerNoJobLogged;
        internal HashSet<int> TowerDeadlockLogged => _recoveryService.TowerDeadlockLogged;

        internal Dictionary<int, JobId> SupplyJobByForgeAndType => _runtimeState.SupplyJobByForgeAndType;
        internal Dictionary<int, JobId> CraftJobByForge => _runtimeState.CraftJobByForge;
        internal Dictionary<int, JobId> HaulAmmoJobByArmory => _runtimeState.HaulAmmoJobByArmory;
        internal List<NpcId> NpcIds => _runtimeState.NpcIds;
        internal HashSet<int> WorkplacesWithNpc => _runtimeState.WorkplacesWithNpc;
        internal int LastNpcVersionForWorkplaces { get => _runtimeState.LastNpcVersionForWorkplaces; set => _runtimeState.LastNpcVersionForWorkplaces = value; }

        internal Dictionary<int, JobId> ResupplyJobByArmory => _resupplyTracking.ResupplyJobByArmory;
        internal Dictionary<int, JobId> ResupplyJobByTower => _resupplyTracking.ResupplyJobByTower;
        internal List<int> TempTowerKeys => _resupplyTracking.TempKeys;

        private int LowAmmoPercent => _configProvider.GetInt("ammoMonitor", "lowAmmoPct", 25);
        private bool DebugAmmoLogs => _configProvider.GetBool("ammoMonitor", "debugLogs", false);
        private float NotifyCooldownLow => _configProvider.GetFloat("ammoMonitor", "notifyCooldownLowSec", 6f);
        private float NotifyCooldownEmpty => _configProvider.GetFloat("ammoMonitor", "notifyCooldownEmptySec", 4f);
        private int ForgeTargetCrafts => _configProvider.GetInt("ammoSupply", "forgeTargetCrafts", 5);
        private string AmmoRecipeId => _configProvider.GetString("crafting", "ammoRecipeId", "ForgeAmmo");

        public AmmoService(GameServices services)
        {
            _services = services;
            _requestQueue = new AmmoRequestQueue(services);
            _resupplyTracking = new AmmoResupplyTracking(services);
            _cooldownManager = new AmmoCooldownManager(this);
            _metricsReporter = new AmmoMetricsReporter(services.WorldState, services.WorldIndex, services.StorageService);
            _observabilityReporter = new AmmoObservabilityReporter(_observability, () => PendingRequests);
            _configProvider = new AmmoConfigProvider(services);
            _recipeProvider = new AmmoRecipeProvider(services.DataRegistry, () => AmmoRecipeId);
            _recoveryService = new AmmoRecoveryService(
                services.WorldState,
                services.NotificationService,
                _cooldownManager,
                _requestQueue,
                () => CurrentMetrics,
                () => PendingRequests,
                CountEligibleResupplyRequests,
                () => UrgentRequests,
                () => NormalRequests,
                GetLowAmmoThresholdValue,
                EnqueueRequest,
                () => _simTime,
                () => DebugAmmoLogs);
            _monitorPolicy = new AmmoMonitorPolicy(
                services.NotificationService,
                services.CombatService,
                _cooldownManager,
                _towerStateTracker,
                _recoveryService,
                EnqueueRequest,
                HasActiveResupplyJob,
                () => LowAmmoPercent,
                () => NotifyCooldownLow,
                () => NotifyCooldownEmpty,
                () => _simTime,
                () => DebugAmmoLogs);
            _topologyCache = new AmmoTopologyCache(this);
            _craftService = new AmmoCraftService(
                services.WorldState,
                services.StorageService,
                services.JobBoard,
                _recipeProvider,
                _runtimeState.CraftJobByForge,
                RebuildWorkplaceHasNpcSet,
                () => _runtimeState.WorkplacesWithNpc);
            _armoryBufferPlanner = new ArmoryBufferPlanner(
                services.WorldState,
                services.WorldIndex,
                services.StorageService,
                services.JobBoard,
                _runtimeState.SupplyJobByForgeAndType,
                _runtimeState.HaulAmmoJobByArmory,
                () => _runtimeState.WorkplacesWithNpc,
                () => ForgeTargetCraftsValue,
                GetArmoryChunkByLevel_Value,
                PickPreferredHaulerWorkplace,
                PickForgeAmmoSource,
                TryStartCraft);
            _towerResupplyPlanner = new TowerResupplyPlanner(
                services.WorldState,
                services.WorldIndex,
                services.StorageService,
                services.JobBoard,
                services.NotificationService,
                _resupplyTracking.ResupplyJobByTower,
                _resupplyTracking.ResupplyJobByArmory,
                _recoveryService.TowerNoSourceLogged,
                _recoveryService.TowerNoJobLogged,
                _recoveryService.TowerDeadlockLogged,
                _resupplyTracking.TempKeys,
                () => UrgentRequests,
                () => NormalRequests,
                () => PendingRequests,
                () => Debug_TotalTowers,
                () => Debug_TowersWithoutAmmo,
                () => Debug_ActiveResupplyJobs,
                () => Debug_ArmoryAvailableAmmo,
                () => DebugAmmoLogsValue,
                () => WorkplacesWithNpc,
                GetArmoryResupplyTripByLevel_Value,
                CountEligibleResupplyRequests,
                PruneInvalidResupplyRequests,
                PickBestRequest,
                ConsumeRequestAt,
                RotateRequestToBack,
                MaybeRequeueTowerAmmoRequest,
                CleanupResupplyArmoryMappings,
                RemoveArmoryMappingByJob);
            _debugHooks = new AmmoDebugHooks(this);
        }

        public void NotifyTowerAmmoChanged(TowerId tower, int current, int max)
        {
            JobId? inFlight = null;
            if (tower.Value != 0 && ResupplyJobByTower.TryGetValue(tower.Value, out var activeJob))
                inFlight = activeJob;

            _monitorPolicy.NotifyTowerAmmoChanged(tower, current, max, inFlight);
        }

        public void EnqueueRequest(AmmoRequest req)
            => _requestQueue.Enqueue(req);

        public bool TryDequeueNext(out AmmoRequest req)
            => _requestQueue.TryDequeueNext(out req);

        public bool TryStartCraft(BuildingId forge)
            => _craftService.TryStartCraft(forge);

        public void Tick(float dt)
        {
            if (!HasRequiredServices())
                return;

            RebuildInFlightResupplyFromJobBoardAfterLoad();
            _simTime += dt;

            CollectAmmoRuntimeState(dt);
            PlanAmmoFlow();
            ExecuteAmmoFlow();
        }

        public void RebuildInFlightResupplyFromJobBoardAfterLoad()
            => _resupplyTracking.RebuildFromJobBoard();

        public void ClearAll()
        {
            _requestQueue.Clear();
            _simTime = 0f;
            _devHookTimer = 0f;
            _towerStateTracker.Clear();
            _recoveryService.ClearAll();
            _cooldownManager.ClearAll();
            _runtimeState.Clear();
            _resupplyTracking.Clear();
            _recipeProvider.Clear();
            _metricsReporter.Clear();
        }

        internal bool HasActiveResupplyJob(JobId jobId)
            => _services.JobBoard != null && _services.JobBoard.TryGet(jobId, out var job) && !IsTerminal(job.Status);

        internal BuildingId PickPreferredHaulerWorkplace(CellPos forgeAnchor)
        {
            if (_topologyCache.TryPickPreferredHaulerWorkplace(forgeAnchor, out var workplace))
                return workplace;
            return default;
        }

        internal (bool found, BuildingId forge, int takeable) PickForgeAmmoSource(CellPos refPos)
        {
            if (_topologyCache.TryPickForgeAmmoSource(refPos, out var forge, out var takeable))
                return (true, forge, takeable);
            return (false, default, 0);
        }

        internal (bool found, List<AmmoRequest> list, int index, AmmoRequest req, TowerState towerState) PickBestRequest(Dictionary<int, JobId> resupplyJobByTower)
        {
            if (_requestQueue.TryPickBestRequest(resupplyJobByTower, out var list, out var index, out var req, out var towerState))
                return (true, list, index, req, towerState);
            return (false, null, -1, default, default);
        }

        internal void CleanupResupplyArmoryMappings()
            => _resupplyTracking.CleanupArmoryMappings();

        internal void RemoveArmoryMappingByJob(JobId jobId)
            => _resupplyTracking.RemoveArmoryMappingByJob(jobId);

        internal int CountEligibleResupplyRequests()
            => _requestQueue.CountEligibleRequests();

        internal void PruneInvalidResupplyRequests()
            => _requestQueue.PruneInvalidRequests(TowerNoJobLogged, TowerDeadlockLogged);

        internal void ConsumeRequestAt(List<AmmoRequest> list, int index)
            => _requestQueue.ConsumeRequestAt(list, index);

        internal void RotateRequestToBack(List<AmmoRequest> list, int index, AmmoRequest req)
            => _requestQueue.RotateRequestToBack(list, index, req);

        internal void RebuildWorkplaceHasNpcSet()
            => _topologyCache.RebuildWorkplaceHasNpcSet();

        internal void RecordTowerSnapshot(TowerId towerId, int ammo, int cap)
            => _towerStateTracker.RecordSnapshot(towerId, ammo, cap);

        internal bool MatchesTowerSnapshot(TowerId towerId, int ammo, int cap)
            => _towerStateTracker.MatchesSnapshot(towerId, ammo, cap);

        internal void MaybeRequeueTowerAmmoRequest(TowerId tower)
            => _recoveryService.MaybeRequeueTowerAmmoRequest(tower);

        internal int GetLowAmmoThresholdValue(int max)
            => _monitorPolicy.GetLowAmmoThreshold(max);

        internal void ResetRequestStateForTower(int towerId)
            => _recoveryService.ResetRequestStateForTower(towerId);

        internal void RemoveTowerCacheState(int towerId)
        {
            _towerStateTracker.RemoveTower(towerId);
            _recoveryService.ClearTowerLogs(towerId);
            _cooldownManager.ClearTower(towerId);
            _requestQueue.RemovePendingForTower(towerId);
            _resupplyTracking.RemoveTower(towerId);
        }

        internal int GetArmoryChunkByLevel_Value(int level)
            => GetArmoryChunkByLevel(level);

        internal int GetArmoryResupplyTripByLevel_Value(int level)
            => GetArmoryResupplyTripByLevel(level);

        internal static bool IsTerminal(JobStatus status)
            => status == JobStatus.Completed || status == JobStatus.Failed || status == JobStatus.Cancelled;

        internal static int Manhattan(CellPos a, CellPos b)
        {
            int dx = a.X - b.X;
            if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y;
            if (dy < 0) dy = -dy;
            return dx + dy;
        }

        private bool HasRequiredServices()
            => _services.WorldState != null
                && _services.StorageService != null
                && _services.JobBoard != null
                && _services.WorldIndex != null
                && _services.DataRegistry != null;

        private void CollectAmmoRuntimeState(float dt)
        {
            _topologyCache.CleanupDestroyedTowerCaches();
            _debugHooks.EnsureTestTowerExistsIfNeeded();
            _debugHooks.Tick(dt);
            _topologyCache.ScanTowersAndNotify();
            _topologyCache.RebuildWorkplaceHasNpcSet();
        }

        private void PlanAmmoFlow()
        {
            _towerResupplyPlanner.CleanupResupplyTowerInFlight();
            _towerResupplyPlanner.EnsureResupplyTowerJobs();
            _topologyCache.ReconcileOutstandingTowerNeeds();
            _towerResupplyPlanner.EnsureResupplyTowerJobs();
        }

        private void ExecuteAmmoFlow()
        {
            bool hasRecipe = _recipeProvider.TryGetAmmoRecipe(out var recipe);
            TickForgeAmmoLoop(hasRecipe, recipe);
            _armoryBufferPlanner.EnsureArmoryAmmoBuffer();
            _metricsReporter.UpdateDebugMetrics(_resupplyTracking.CountTrackedActiveJobs());
            _observabilityReporter.Update(CurrentMetrics);
            _recoveryService.LogPotentialResupplyDeadlock();
        }

        private void TickForgeAmmoLoop(bool hasRecipe, RecipeDef recipe)
        {
            var forges = _services.WorldIndex.Forges;
            for (int i = 0; i < forges.Count; i++)
            {
                var forge = forges[i];
                if (!_services.WorldState.Buildings.Exists(forge))
                    continue;

                var building = _services.WorldState.Buildings.Get(forge);
                if (!building.IsConstructed || !hasRecipe)
                    continue;

                if (!_armoryBufferPlanner.HasCapForForgeInputs(forge, recipe))
                    continue;

                _armoryBufferPlanner.EnsureForgeSupplyByRecipe(forge, building.Anchor, recipe);
                if (WorkplacesWithNpc.Contains(forge.Value))
                    _armoryBufferPlanner.TryStartCraft(forge);
            }
        }

        private static int GetArmoryChunkByLevel(int level)
        {
            int clamped = level <= 0 ? 1 : (level > 3 ? 3 : level);
            return clamped == 1 ? 40 : (clamped == 2 ? 60 : 80);
        }

        private static int GetArmoryResupplyTripByLevel(int level)
        {
            int clamped = level <= 0 ? 1 : (level > 3 ? 3 : level);
            return clamped == 1 ? 20 : (clamped == 2 ? 30 : 40);
        }
    }
}
