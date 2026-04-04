using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;

namespace SeasonalBastion
{
    public sealed class AmmoService : IAmmoService, ITickable
    {
        private readonly GameServices _s;
        private readonly AmmoTopologyCache _topologyCache;
        private readonly ArmoryBufferPlanner _armoryBufferPlanner;
        private readonly TowerResupplyPlanner _towerResupplyPlanner;
        private readonly AmmoDebugHooks _debugHooks;
        private readonly AmmoRequestQueue _requestQueue;
        private readonly AmmoResupplyTracking _resupplyTracking;
        private readonly AmmoCooldownManager _cooldownManager;
        private readonly AmmoRecoveryService _recoveryService;
        private readonly AmmoMetricsReporter _metricsReporter;
        private readonly AmmoConfigProvider _configProvider;
        private readonly AmmoRecipeProvider _recipeProvider;
        private readonly AmmoCraftService _craftService;
        private readonly AmmoRuntimeState _runtimeState = new();
        private readonly AmmoTowerStateTracker _towerStateTracker = new();
        internal List<AmmoRequest> UrgentRequests => _requestQueue.UrgentRequests;
        internal List<AmmoRequest> NormalRequests => _requestQueue.NormalRequests;
        internal AmmoRequestQueue Requests => _requestQueue;
        internal AmmoCooldownManager Cooldowns => _cooldownManager;

        private float _simTime;
        internal float SimTime => _simTime;

        internal Dictionary<int, int> LastAmmoByTower => _towerStateTracker.LastAmmoByTower;
        internal Dictionary<int, int> LastCapByTower => _towerStateTracker.LastCapByTower;
        internal HashSet<int> TowerNoSourceLogged => _recoveryService.TowerNoSourceLogged;
        internal HashSet<int> TowerNoJobLogged => _recoveryService.TowerNoJobLogged;
        internal HashSet<int> TowerDeadlockLogged => _recoveryService.TowerDeadlockLogged;

        internal HashSet<int> PendingReqTower => _requestQueue.PendingReqTower;
        internal Dictionary<int, AmmoRequestPriority> PendingPriorityByTower => _requestQueue.PendingPriorityByTower;

        public bool DevHook_Enabled { get; set; } = false;
        public float DevHook_ShotInterval { get; set; } = 0.50f;
        public int DevHook_AmmoPerShot { get; set; } = 1;

        private float _devHookTimer;
        internal float DevHookTimer { get => _devHookTimer; set => _devHookTimer = value; }

        internal Dictionary<int, JobId> SupplyJobByForgeAndType => _runtimeState.SupplyJobByForgeAndType;
        internal Dictionary<int, JobId> CraftJobByForge => _runtimeState.CraftJobByForge;
        internal Dictionary<int, JobId> HaulAmmoJobByArmory => _runtimeState.HaulAmmoJobByArmory;

        internal Dictionary<int, JobId> ResupplyJobByArmory => _resupplyTracking.ResupplyJobByArmory;
        internal Dictionary<int, JobId> ResupplyJobByTower => _resupplyTracking.ResupplyJobByTower;
        internal List<int> TempTowerKeys => _resupplyTracking.TempKeys;

        internal List<NpcId> NpcIds => _runtimeState.NpcIds;
        internal HashSet<int> WorkplacesWithNpc => _runtimeState.WorkplacesWithNpc;
        internal int LastNpcVersionForWorkplaces { get => _runtimeState.LastNpcVersionForWorkplaces; set => _runtimeState.LastNpcVersionForWorkplaces = value; }

        public int Debug_InFlightResupplyJobs => _resupplyTracking.InFlightCount;
        public int Debug_InFlightHaulAmmoJobs => HaulAmmoJobByArmory.Count;
        public int Debug_PendingUrgent => _requestQueue.UrgentRequests.Count;
        public int Debug_PendingNormal => _requestQueue.NormalRequests.Count;
        public int Debug_TotalTowers => _metricsReporter.LastSnapshot.TotalTowers;
        public int Debug_TowersWithoutAmmo => _metricsReporter.LastSnapshot.TowersWithoutAmmo;
        public int Debug_ActiveResupplyJobs => _metricsReporter.LastSnapshot.ActiveResupplyJobs;
        public int Debug_ArmoryAvailableAmmo => _metricsReporter.LastSnapshot.ArmoryAvailableAmmo;
        internal AmmoMetricsSnapshot CurrentMetrics => _metricsReporter.LastSnapshot;

        public AmmoService(GameServices s)
        {
            _s = s;
            _topologyCache = new AmmoTopologyCache(this);
            _armoryBufferPlanner = new ArmoryBufferPlanner(this);
            _towerResupplyPlanner = new TowerResupplyPlanner(this);
            _debugHooks = new AmmoDebugHooks(this);
            _requestQueue = new AmmoRequestQueue(s);
            _resupplyTracking = new AmmoResupplyTracking(s);
            _cooldownManager = new AmmoCooldownManager(this);
            _recoveryService = new AmmoRecoveryService(this);
            _metricsReporter = new AmmoMetricsReporter(this);
            _configProvider = new AmmoConfigProvider(s);
            _recipeProvider = new AmmoRecipeProvider(this);
            _craftService = new AmmoCraftService(this, _recipeProvider);
        }

        public int PendingRequests => _requestQueue.PendingRequests;
        internal GameServices Services => _s;

        private int LowAmmoPercent => _configProvider.GetInt("ammoMonitor", "lowAmmoPct", 25);
        private bool DebugAmmoLogs => _configProvider.GetBool("ammoMonitor", "debugLogs", false);
        internal bool DebugAmmoLogsValue => DebugAmmoLogs;

        internal float ReqCooldownLowValue => _configProvider.GetFloat("ammoMonitor", "reqCooldownLowSec", 8f);
        internal float ReqCooldownEmptyValue => _configProvider.GetFloat("ammoMonitor", "reqCooldownEmptySec", 4f);

        private float NotifyCooldownLow => _configProvider.GetFloat("ammoMonitor", "notifyCooldownLowSec", 6f);
        private float NotifyCooldownEmpty => _configProvider.GetFloat("ammoMonitor", "notifyCooldownEmptySec", 4f);

        private int ForgeTargetCrafts => _configProvider.GetInt("ammoSupply", "forgeTargetCrafts", 5);
        internal int ForgeTargetCraftsValue => ForgeTargetCrafts;

        private string AmmoRecipeId => _configProvider.GetString("crafting", "ammoRecipeId", "ForgeAmmo");
        internal string AmmoRecipeIdValue => AmmoRecipeId;

        public void NotifyTowerAmmoChanged(TowerId tower, int current, int max)
        {
            if (tower.Value == 0) return;
            if (max <= 0) return;

            int tid = tower.Value;

            if (ResupplyJobByTower.TryGetValue(tid, out var inflight))
            {
                if (_s.JobBoard != null && _s.JobBoard.TryGet(inflight, out var jj) && !IsTerminal(jj.Status))
                    return;
            }

            int thr = GetLowAmmoThreshold(max);

            byte stateNow;
            if (current <= 0) stateNow = 2;
            else if (current <= thr) stateNow = 1;
            else stateNow = 0;

            _towerStateTracker.SetState(tid, stateNow);

            if (_s.NotificationService != null && stateNow != 0)
            {
                bool combatActive = false;
                if (_s.CombatService != null)
                    combatActive = _s.CombatService.IsActive;

                if (stateNow == 2)
                {
                    _s.NotificationService.Push(
                        key: $"TowerAmmo_Empty_{tid}",
                        title: "Tháp hết đạn",
                        body: $"Tower {tid}: Ammo {current}/{max}",
                        severity: combatActive ? NotificationSeverity.Error : NotificationSeverity.Warning,
                        payload: default,
                        cooldownSeconds: NotifyCooldownEmpty,
                        dedupeByKey: true
                    );
                }
                else
                {
                    _s.NotificationService.Push(
                        key: $"TowerAmmo_Low_{tid}",
                        title: "Tháp cần tiếp đạn",
                        body: $"Tower {tid}: Ammo {current}/{max}",
                        severity: combatActive ? NotificationSeverity.Warning : NotificationSeverity.Info,
                        payload: default,
                        cooldownSeconds: NotifyCooldownLow,
                        dedupeByKey: true
                    );
                }
            }

            if (DebugAmmoLogs && stateNow != 0 && _towerStateTracker.TryMarkNeedLogged(tid))
            {
                Log.E($"[Ammo] tower {tid} requests resupply ammo={current}/{max} state={(stateNow == 2 ? "empty" : "low")} thr={thr}");
                _recoveryService.ClearNeedLogs(tid);
            }

            if (stateNow == 0)
            {
                _towerStateTracker.ClearNeedLogged(tid);
                _recoveryService.ClearNeedLogs(tid);
                return;
            }

            var pri = (stateNow == 2) ? AmmoRequestPriority.Urgent : AmmoRequestPriority.Normal;

            if (!_cooldownManager.TryConsumeRequestCooldown(tower, pri))
                return;

            int need = max - current;
            if (need <= 0) return;

            EnqueueRequest(new AmmoRequest
            {
                Tower = tower,
                AmountNeeded = need,
                Priority = pri,
                CreatedAt = _simTime
            });
        }

        public void EnqueueRequest(AmmoRequest req) => _requestQueue.Enqueue(req);
        public bool TryDequeueNext(out AmmoRequest req) => _requestQueue.TryDequeueNext(out req);
        public bool TryStartCraft(BuildingId forge) => _craftService.TryStartCraft(forge);

        public void Tick(float dt)
        {
            if (_s.WorldState == null || _s.StorageService == null || _s.JobBoard == null || _s.WorldIndex == null || _s.DataRegistry == null)
                return;

            RebuildInFlightResupplyFromJobBoardAfterLoad();
            _simTime += dt;

            CollectAmmoRuntimeState(dt);
            PlanAmmoFlow();
            ExecuteAmmoFlow();
        }

        public void RebuildInFlightResupplyFromJobBoardAfterLoad() => _resupplyTracking.RebuildFromJobBoard();

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

        internal void UpdateDebugMetrics_Core() => _metricsReporter.UpdateDebugMetrics(_resupplyTracking.CountTrackedActiveJobs());
        internal void LogPotentialResupplyDeadlock_Core() => _recoveryService.LogPotentialResupplyDeadlock();
        internal void CleanupResupplyArmoryMappings_Core() => _resupplyTracking.CleanupArmoryMappings();
        internal void RemoveArmoryMappingByJob_Core(JobId jobId) => _resupplyTracking.RemoveArmoryMappingByJob(jobId);
        internal int CountEligibleResupplyRequests() => _requestQueue.CountEligibleRequests();
        internal int CountTrackedActiveResupplyJobs_Core() => _resupplyTracking.CountTrackedActiveJobs();
        internal void PruneInvalidResupplyRequests() => _requestQueue.PruneInvalidRequests(TowerNoJobLogged, TowerDeadlockLogged);
        internal bool ContainsRequestForTower_Core(List<AmmoRequest> list, TowerId tower) => _topologyCache.ContainsRequestForTower(list, tower);
        internal bool TryGetAmmoRecipe_Core(out RecipeDef recipe) => _recipeProvider.TryGetAmmoRecipe(out recipe);
        internal bool HasCapForForgeInputs_Core(BuildingId forge, RecipeDef recipe) => _armoryBufferPlanner.HasCapForForgeInputs(forge, recipe);
        internal void EnsureForgeSupplyByRecipe_Core(BuildingId forge, CellPos forgeAnchor, RecipeDef recipe) => _armoryBufferPlanner.EnsureForgeSupplyByRecipe(forge, forgeAnchor, recipe);
        internal void EnsureResupplyTowerJobs_Core() => _towerResupplyPlanner.EnsureResupplyTowerJobs();
        internal void CleanupResupplyTowerInFlight_Core() => _towerResupplyPlanner.CleanupResupplyTowerInFlight();
        internal bool TryPickBestRequest(out List<AmmoRequest> list, out int index, out AmmoRequest req, out TowerState towerState)
            => _requestQueue.TryPickBestRequest(ResupplyJobByTower, out list, out index, out req, out towerState);
        internal bool TryCreateNextResupplyTowerJob_Core() => _towerResupplyPlanner.TryCreateNextResupplyTowerJob();
        internal bool TryPickBestResupplySource_Core(TowerState towerState, out BuildingId source, out BuildingState sourceState, out int availableAmmo) => _towerResupplyPlanner.TryPickBestResupplySource(towerState, out source, out sourceState, out availableAmmo);
        internal void EvaluateResupplySources_Core(IReadOnlyList<BuildingId> candidates, CellPos targetCell, int rank, ref BuildingId bestSource, ref BuildingState bestState, ref int bestAmmo, ref int bestRank, ref int bestDist, ref int bestId) => _towerResupplyPlanner.EvaluateResupplySources(candidates, targetCell, rank, ref bestSource, ref bestState, ref bestAmmo, ref bestRank, ref bestDist, ref bestId);
        internal void ConsumeRequestAt(List<AmmoRequest> list, int index) => _requestQueue.ConsumeRequestAt(list, index);
        internal void RotateRequestToBack(List<AmmoRequest> list, int index, AmmoRequest req) => _requestQueue.RotateRequestToBack(list, index, req);
        internal bool TryPickPreferredHaulerWorkplace_Core(CellPos forgeAnchor, out BuildingId workplace) => _topologyCache.TryPickPreferredHaulerWorkplace(forgeAnchor, out workplace);
        internal bool TryPickNearestWorkplaceFromIndex_Core(IReadOnlyList<BuildingId> list, CellPos from, bool requireNpc, out BuildingId best) => _topologyCache.TryPickNearestWorkplaceFromIndex(list, from, requireNpc, out best);
        internal void RebuildWorkplaceHasNpcSet_Core() => _topologyCache.RebuildWorkplaceHasNpcSet();

        internal static bool IsTerminal(JobStatus s)
        {
            return s == JobStatus.Completed || s == JobStatus.Failed || s == JobStatus.Cancelled;
        }

        internal static int Manhattan(CellPos a, CellPos b)
        {
            int dx = a.X - b.X; if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
            return dx + dy;
        }

        internal void EnsureArmoryAmmoBuffer_Core() => _armoryBufferPlanner.EnsureArmoryAmmoBuffer();
        internal bool TryPickForgeAmmoSource_Core(CellPos refPos, out BuildingId bestForge, out int bestTakeable) => _armoryBufferPlanner.TryPickForgeAmmoSource(refPos, out bestForge, out bestTakeable);
        internal void ReconcileOutstandingTowerNeeds_Core() => _topologyCache.ReconcileOutstandingTowerNeeds();
        internal void CleanupDestroyedTowerCaches_Core() => _topologyCache.CleanupDestroyedTowerCaches();

        internal void RemoveTowerCacheState(int tid)
        {
            _towerStateTracker.RemoveTower(tid);
            _recoveryService.ClearTowerLogs(tid);
            _cooldownManager.ClearTower(tid);
            _requestQueue.RemovePendingForTower(tid);
            _resupplyTracking.RemoveTower(tid);
        }

        internal int GetArmoryChunkByLevel_Value(int level) => GetArmoryChunkByLevel(level);

        private static int GetArmoryChunkByLevel(int level)
        {
            int lvl = level <= 0 ? 1 : (level > 3 ? 3 : level);
            return lvl == 1 ? 40 : (lvl == 2 ? 60 : 80);
        }

        internal int GetArmoryResupplyTripByLevel_Value(int level) => GetArmoryResupplyTripByLevel(level);

        private static int GetArmoryResupplyTripByLevel(int level)
        {
            int lvl = level <= 0 ? 1 : (level > 3 ? 3 : level);
            return lvl == 1 ? 20 : (lvl == 2 ? 30 : 40);
        }

        private int GetLowAmmoThreshold(int max)
        {
            int thr = (max * LowAmmoPercent + 99) / 100;
            if (thr < 1) thr = 1;
            return thr;
        }

        internal void ScanTowersAndNotify_Core() => _topologyCache.ScanTowersAndNotify();
        internal void DevHookTick_Core(float dt) => _debugHooks.Tick(dt);
        internal void EnsureTestTowerExistsIfNeeded_Core() => _debugHooks.EnsureTestTowerExistsIfNeeded();
        internal void RecordTowerSnapshot(TowerId towerId, int ammo, int cap) => _towerStateTracker.RecordSnapshot(towerId, ammo, cap);
        internal bool MatchesTowerSnapshot(TowerId towerId, int ammo, int cap) => _towerStateTracker.MatchesSnapshot(towerId, ammo, cap);
        internal void MaybeRequeueTowerAmmoRequest(TowerId tower) => _recoveryService.MaybeRequeueTowerAmmoRequest(tower);
        internal int GetLowAmmoThresholdValue(int max) => GetLowAmmoThreshold(max);
        internal void ResetRequestStateForTower(int towerId) => _recoveryService.ResetRequestStateForTower(towerId);

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

            var forges = _s.WorldIndex.Forges;
            for (int i = 0; i < forges.Count; i++)
            {
                var forge = forges[i];
                if (!_s.WorldState.Buildings.Exists(forge)) continue;

                var bs = _s.WorldState.Buildings.Get(forge);
                if (!bs.IsConstructed) continue;

                bool forgeHasNpc = WorkplacesWithNpc.Contains(forge.Value);
                if (!hasRecipe)
                    continue;

                if (!_armoryBufferPlanner.HasCapForForgeInputs(forge, recipe))
                    continue;

                _armoryBufferPlanner.EnsureForgeSupplyByRecipe(forge, bs.Anchor, recipe);
                if (forgeHasNpc)
                    _armoryBufferPlanner.TryStartCraft(forge);
            }

            _armoryBufferPlanner.EnsureArmoryAmmoBuffer();
            _metricsReporter.UpdateDebugMetrics(_resupplyTracking.CountTrackedActiveJobs());
            _recoveryService.LogPotentialResupplyDeadlock();
        }
    }
}
