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
        internal List<AmmoRequest> UrgentRequests => _requestQueue.UrgentRequests;
        internal List<AmmoRequest> NormalRequests => _requestQueue.NormalRequests;

        // Deterministic sim-time (no Unity realtime)
        private float _simTime;
        internal float SimTime => _simTime;

        // Cache to detect ammo changes without relying on combat firing hook
        private readonly Dictionary<int, int> _lastAmmoByTower = new();
        private readonly Dictionary<int, int> _lastCapByTower = new();
        internal Dictionary<int, int> LastAmmoByTower => _lastAmmoByTower;
        internal Dictionary<int, int> LastCapByTower => _lastCapByTower;
        private readonly Dictionary<int, byte> _lastStateByTower = new(); // 0=ok,1=low,2=empty
        private readonly HashSet<int> _towerNeedLogged = new();
        private readonly HashSet<int> _towerNoSourceLogged = new();
        internal HashSet<int> TowerNoSourceLogged => _towerNoSourceLogged;
        private readonly HashSet<int> _towerNoJobLogged = new();
        internal HashSet<int> TowerNoJobLogged => _towerNoJobLogged;
        private readonly HashSet<int> _towerDeadlockLogged = new();
        internal HashSet<int> TowerDeadlockLogged => _towerDeadlockLogged;

        // Anti-dup pending request per tower (supports promote low->empty)
        internal HashSet<int> PendingReqTower => _requestQueue.PendingReqTower;
        internal Dictionary<int, AmmoRequestPriority> PendingPriorityByTower => _requestQueue.PendingPriorityByTower;

        // ----------------- Day25 DEV HOOK (no combat yet) -----------------
        // Toggle from Debug HUD. When enabled, ammo will be drained periodically from towers to simulate firing.
        public bool DevHook_Enabled { get; set; } = false;
        public float DevHook_ShotInterval { get; set; } = 0.50f;
        public int DevHook_AmmoPerShot { get; set; } = 1;

        private float _devHookTimer;
        internal float DevHookTimer { get => _devHookTimer; set => _devHookTimer = value; }

        // Prevent enqueue spam: (forgeId*16 + resType) -> jobId
        private readonly Dictionary<int, JobId> _supplyJobByForgeAndType = new();
        internal Dictionary<int, JobId> SupplyJobByForgeAndType => _supplyJobByForgeAndType;
        private readonly Dictionary<int, JobId> _craftJobByForge = new();
        internal Dictionary<int, JobId> CraftJobByForge => _craftJobByForge;

        // Day24: 1 pending HaulAmmoToArmory job per Armory
        private readonly Dictionary<int, JobId> _haulAmmoJobByArmory = new();
        internal Dictionary<int, JobId> HaulAmmoJobByArmory => _haulAmmoJobByArmory;

        // Day26: 1 pending ResupplyTower job per source/tower
        internal Dictionary<int, JobId> ResupplyJobByArmory => _resupplyTracking.ResupplyJobByArmory;
        internal Dictionary<int, JobId> ResupplyJobByTower => _resupplyTracking.ResupplyJobByTower;
        internal List<int> TempTowerKeys => _resupplyTracking.TempKeys;

        // Reuse buffers
        private readonly List<NpcId> _npcIds = new(64);
        internal List<NpcId> NpcIds => _npcIds;
        private readonly HashSet<int> _workplacesWithNpc = new();
        internal HashSet<int> WorkplacesWithNpc => _workplacesWithNpc;

        // Cached recipe snapshot (reloaded lazily)
        private RecipeDef _cachedAmmoRecipe;
        internal RecipeDef CachedAmmoRecipe { get => _cachedAmmoRecipe; set => _cachedAmmoRecipe = value; }
        private string _cachedAmmoRecipeId = null;
        internal string CachedAmmoRecipeId { get => _cachedAmmoRecipeId; set => _cachedAmmoRecipeId = value; }

        public int Debug_InFlightResupplyJobs => _resupplyTracking.InFlightCount;
        public int Debug_InFlightHaulAmmoJobs => _haulAmmoJobByArmory.Count;
        public int Debug_PendingUrgent => _requestQueue.UrgentRequests.Count;
        public int Debug_PendingNormal => _requestQueue.NormalRequests.Count;
        public int Debug_TotalTowers { get; internal set; }
        public int Debug_TowersWithoutAmmo { get; internal set; }
        public int Debug_ActiveResupplyJobs { get; internal set; }
        public int Debug_ArmoryAvailableAmmo { get; internal set; }

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
        }

        public int PendingRequests => _requestQueue.PendingRequests;
        internal GameServices Services => _s;

        // ----------------- Tunables (prefer Balance json if present; fallback to defaults) -----------------

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

        // ----------------- Public API -----------------

        public void NotifyTowerAmmoChanged(TowerId tower, int current, int max)
        {
            if (tower.Value == 0) return;
            if (max <= 0) return;

            int tid = tower.Value;

            // Day38: nếu tower đã có ResupplyTower job in-flight thì không enqueue request mới (anti-spam)
            if (ResupplyJobByTower.TryGetValue(tid, out var inflight))
            {
                if (_s.JobBoard != null && _s.JobBoard.TryGet(inflight, out var jj) && !IsTerminal(jj.Status))
                    return;
            }

            int thr = GetLowAmmoThreshold(max);

            byte stateNow;
            if (current <= 0) stateNow = 2;             // empty: highest priority
            else if (current <= thr) stateNow = 1;      // low ammo threshold
            else stateNow = 0;                          // healthy enough: no request

            _lastStateByTower.TryGetValue(tid, out var statePrev);
            _lastStateByTower[tid] = stateNow;

            // ---- Notifications (non-spam) ----
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

            if (DebugAmmoLogs && stateNow != 0 && _towerNeedLogged.Add(tid))
            {
                Log.E($"[Ammo] tower {tid} requests resupply ammo={current}/{max} state={(stateNow == 2 ? "empty" : "low")} thr={thr}");
                _towerNoSourceLogged.Remove(tid);
                _towerNoJobLogged.Remove(tid);
            }

            if (stateNow == 0)
            {
                _towerNeedLogged.Remove(tid);
                _towerNoSourceLogged.Remove(tid);
                _towerNoJobLogged.Remove(tid);
                return;
            }

            var pri = (stateNow == 2) ? AmmoRequestPriority.Urgent : AmmoRequestPriority.Normal;

            if (!_cooldownManager.TryConsumeRequestCooldown(tower, pri))
                return;

            int need = max - current;
            if (need <= 0) return;

            var req = new AmmoRequest
            {
                Tower = tower,
                AmountNeeded = need,
                Priority = pri,
                CreatedAt = _simTime
            };

            EnqueueRequest(req);
        }

        public void EnqueueRequest(AmmoRequest req) => _requestQueue.Enqueue(req);

        public bool TryDequeueNext(out AmmoRequest req) => _requestQueue.TryDequeueNext(out req);

        public bool TryStartCraft(BuildingId forge) => TryStartCraft_Core(forge);

        internal bool TryStartCraft_Core(BuildingId forge)
        {
            if (_s.WorldState == null || _s.StorageService == null || _s.JobBoard == null || _s.DataRegistry == null) return false;
            if (!_s.WorldState.Buildings.Exists(forge)) return false;

            var bs = _s.WorldState.Buildings.Get(forge);
            if (!bs.IsConstructed) return false;

            if (!TryGetAmmoRecipe(out var recipe))
                return false;

            // Must have NPC at forge workplace, otherwise enqueue is pointless
            RebuildWorkplaceHasNpcSet();
            if (!_workplacesWithNpc.Contains(forge.Value)) return false;

            // Check space for output
            int outCap = _s.StorageService.GetCap(forge, recipe.OutputType);
            int outCur = _s.StorageService.GetAmount(forge, recipe.OutputType);
            if (outCap <= 0 || (outCap - outCur) < recipe.OutputAmount) return false;

            // Check local inputs
            int inCur = _s.StorageService.GetAmount(forge, recipe.InputType);
            if (inCur < recipe.InputAmount) return false;

            var extras = recipe.ExtraInputs;
            if (extras != null && extras.Length > 0)
            {
                for (int i = 0; i < extras.Length; i++)
                {
                    var c = extras[i];
                    if (c == null || c.Amount <= 0) continue;
                    int cur = _s.StorageService.GetAmount(forge, c.Resource);
                    if (cur < c.Amount) return false;
                }
            }

            // 1 pending craft job per forge
            if (_craftJobByForge.TryGetValue(forge.Value, out var oldId))
            {
                if (_s.JobBoard.TryGet(oldId, out var old) && !IsTerminal(old.Status))
                    return false;
            }

            var j = new Job
            {
                Archetype = JobArchetype.CraftAmmo,
                Status = JobStatus.Created,
                Workplace = forge,
                SourceBuilding = forge,
                DestBuilding = default,
                ResourceType = recipe.OutputType,
                Amount = recipe.OutputAmount,
                TargetCell = bs.Anchor,
                CreatedAt = 0
            };

            var id = _s.JobBoard.Enqueue(j);
            _craftJobByForge[forge.Value] = id;
            return true;
        }

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
            // IMPORTANT (VS3 hardening): clear ALL runtime caches.

            _requestQueue.Clear();

            _simTime = 0f;
            _devHookTimer = 0f;

            _lastAmmoByTower.Clear();
            _lastCapByTower.Clear();
            _lastStateByTower.Clear();
            _towerNeedLogged.Clear();
            _towerNoSourceLogged.Clear();
            _towerNoJobLogged.Clear();
            _towerDeadlockLogged.Clear();

            _cooldownManager.ClearAll();

            _supplyJobByForgeAndType.Clear();
            _craftJobByForge.Clear();
            _haulAmmoJobByArmory.Clear();
            _resupplyTracking.Clear();

            _npcIds.Clear();
            _workplacesWithNpc.Clear();

            _cachedAmmoRecipe = null;
            _cachedAmmoRecipeId = null;

            Debug_TotalTowers = 0;
            Debug_TowersWithoutAmmo = 0;
            Debug_ActiveResupplyJobs = 0;
            Debug_ArmoryAvailableAmmo = 0;
        }

        internal void UpdateDebugMetrics_Core() => _metricsReporter.UpdateDebugMetrics();

        internal void LogPotentialResupplyDeadlock_Core() => _recoveryService.LogPotentialResupplyDeadlock();
        internal void CleanupResupplyArmoryMappings_Core() => CleanupResupplyArmoryMappings();
        internal void RemoveArmoryMappingByJob_Core(JobId jobId) => RemoveArmoryMappingByJob(jobId);
        internal int CountEligibleResupplyRequests() => CountEligibleRequests();
        internal int CountTrackedActiveResupplyJobs_Core() => CountTrackedActiveResupplyJobs();
        internal void PruneInvalidResupplyRequests() => _requestQueue.PruneInvalidRequests(_towerNoJobLogged, _towerDeadlockLogged);

        private int CountEligibleRequests() => _requestQueue.CountEligibleRequests();

        private bool TryGetAmmoRecipe(out RecipeDef recipe) => TryGetAmmoRecipe_Core(out recipe);
        private void RebuildWorkplaceHasNpcSet() => RebuildWorkplaceHasNpcSet_Core();
        private bool TryPickPreferredHaulerWorkplace(CellPos forgeAnchor, out BuildingId workplace) => TryPickPreferredHaulerWorkplace_Core(forgeAnchor, out workplace);
        private bool TryCreateNextResupplyTowerJob() => TryCreateNextResupplyTowerJob_Core();
        private bool TryPickBestResupplySource(TowerState towerState, out BuildingId source, out BuildingState sourceState, out int availableAmmo) => TryPickBestResupplySource_Core(towerState, out source, out sourceState, out availableAmmo);
        private void EvaluateResupplySources(IReadOnlyList<BuildingId> candidates, CellPos targetCell, int rank, ref BuildingId bestSource, ref BuildingState bestState, ref int bestAmmo, ref int bestRank, ref int bestDist, ref int bestId) => EvaluateResupplySources_Core(candidates, targetCell, rank, ref bestSource, ref bestState, ref bestAmmo, ref bestRank, ref bestDist, ref bestId);
        private bool TryPickNearestWorkplaceFromIndex(IReadOnlyList<BuildingId> list, CellPos from, bool requireNpc, out BuildingId best) => TryPickNearestWorkplaceFromIndex_Core(list, from, requireNpc, out best);
        private bool TryPickForgeAmmoSource(CellPos refPos, out BuildingId bestForge, out int bestTakeable) => TryPickForgeAmmoSource_Core(refPos, out bestForge, out bestTakeable);
        private bool ContainsRequestForTower(List<AmmoRequest> list, TowerId tower) => ContainsRequestForTower_Core(list, tower);

        private int CountTrackedActiveResupplyJobs() => _resupplyTracking.CountTrackedActiveJobs();

        internal bool ContainsRequestForTower_Core(List<AmmoRequest> list, TowerId tower) => _topologyCache.ContainsRequestForTower(list, tower);

        // ----------------- Recipe-driven forge supply -----------------

        internal bool TryGetAmmoRecipe_Core(out RecipeDef recipe)
        {
            recipe = null;

            string rid = AmmoRecipeId;
            if (string.IsNullOrEmpty(rid)) rid = "ForgeAmmo";

            // If recipe id changed, drop cache
            if (!string.Equals(_cachedAmmoRecipeId, rid, StringComparison.OrdinalIgnoreCase))
            {
                _cachedAmmoRecipeId = rid;
                _cachedAmmoRecipe = null;
            }

            if (_cachedAmmoRecipe != null)
            {
                recipe = _cachedAmmoRecipe;
                return true;
            }

            try
            {
                var r = _s.DataRegistry.GetRecipe(rid);
                if (r == null) return false;
                _cachedAmmoRecipe = r;
                recipe = r;
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[AmmoService] Failed to load ammo recipe '{rid}'. Attempting fallback recipe 'ForgeAmmo'. {ex}");
                // fallback to default once
                if (!string.Equals(rid, "ForgeAmmo", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var r = _s.DataRegistry.GetRecipe("ForgeAmmo");
                        if (r == null) return false;
                        _cachedAmmoRecipeId = "ForgeAmmo";
                        _cachedAmmoRecipe = r;
                        recipe = r;
                        return true;
                    }
                    catch (Exception fallbackEx)
                    {
                        UnityEngine.Debug.LogWarning($"[AmmoService] Failed to load fallback recipe 'ForgeAmmo' after recipe '{rid}' lookup failed: {fallbackEx}");
                    }
                }
                return false;
            }
        }

        internal bool HasCapForForgeInputs_Core(BuildingId forge, RecipeDef recipe) => _armoryBufferPlanner.HasCapForForgeInputs(forge, recipe);

        internal void EnsureForgeSupplyByRecipe_Core(BuildingId forge, CellPos forgeAnchor, RecipeDef recipe) => _armoryBufferPlanner.EnsureForgeSupplyByRecipe(forge, forgeAnchor, recipe);

        // ----------------- Day26: ResupplyTower provider -----------------

        internal void EnsureResupplyTowerJobs_Core() => _towerResupplyPlanner.EnsureResupplyTowerJobs();

        internal void CleanupResupplyTowerInFlight_Core() => _towerResupplyPlanner.CleanupResupplyTowerInFlight();

        private void CleanupResupplyArmoryMappings() => _resupplyTracking.CleanupArmoryMappings();

        private void RemoveArmoryMappingByJob(JobId jobId) => _resupplyTracking.RemoveArmoryMappingByJob(jobId);

        internal bool TryPickBestRequest(out List<AmmoRequest> list, out int index, out AmmoRequest req, out TowerState towerState)
            => _requestQueue.TryPickBestRequest(ResupplyJobByTower, out list, out index, out req, out towerState);

        internal bool TryCreateNextResupplyTowerJob_Core() => _towerResupplyPlanner.TryCreateNextResupplyTowerJob();

        internal bool TryPickBestResupplySource_Core(TowerState towerState, out BuildingId source, out BuildingState sourceState, out int availableAmmo) => _towerResupplyPlanner.TryPickBestResupplySource(towerState, out source, out sourceState, out availableAmmo);

        internal void EvaluateResupplySources_Core(IReadOnlyList<BuildingId> candidates, CellPos targetCell, int rank, ref BuildingId bestSource, ref BuildingState bestState, ref int bestAmmo, ref int bestRank, ref int bestDist, ref int bestId) => _towerResupplyPlanner.EvaluateResupplySources(candidates, targetCell, rank, ref bestSource, ref bestState, ref bestAmmo, ref bestRank, ref bestDist, ref bestId);

        internal void ConsumeRequestAt(List<AmmoRequest> list, int index) => _requestQueue.ConsumeRequestAt(list, index);

        internal void RotateRequestToBack(List<AmmoRequest> list, int index, AmmoRequest req) => _requestQueue.RotateRequestToBack(list, index, req);

        internal bool TryPickPreferredHaulerWorkplace_Core(CellPos forgeAnchor, out BuildingId workplace) => _topologyCache.TryPickPreferredHaulerWorkplace(forgeAnchor, out workplace);

        internal bool TryPickNearestWorkplaceFromIndex_Core(IReadOnlyList<BuildingId> list, CellPos from, bool requireNpc, out BuildingId best) => _topologyCache.TryPickNearestWorkplaceFromIndex(list, from, requireNpc, out best);

        private int _lastNpcVersionForWorkplaces = -1;
        internal int LastNpcVersionForWorkplaces { get => _lastNpcVersionForWorkplaces; set => _lastNpcVersionForWorkplaces = value; }

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

        // ----------------- Day24: Armory buffer + HaulAmmo -----------------

        internal void EnsureArmoryAmmoBuffer_Core() => _armoryBufferPlanner.EnsureArmoryAmmoBuffer();

        internal bool TryPickForgeAmmoSource_Core(CellPos refPos, out BuildingId bestForge, out int bestTakeable) => _armoryBufferPlanner.TryPickForgeAmmoSource(refPos, out bestForge, out bestTakeable);

        internal void ReconcileOutstandingTowerNeeds_Core() => _topologyCache.ReconcileOutstandingTowerNeeds();

        internal void CleanupDestroyedTowerCaches_Core() => _topologyCache.CleanupDestroyedTowerCaches();

        internal void RemoveTowerCacheState(int tid)
        {
            _lastAmmoByTower.Remove(tid);
            _lastCapByTower.Remove(tid);
            _lastStateByTower.Remove(tid);
            _towerNeedLogged.Remove(tid);
            _towerNoSourceLogged.Remove(tid);
            _towerNoJobLogged.Remove(tid);
            _towerDeadlockLogged.Remove(tid);
            _cooldownManager.ClearTower(tid);
            PendingReqTower.Remove(tid);
            PendingPriorityByTower.Remove(tid);
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

        internal void MaybeRequeueTowerAmmoRequest(TowerId tower) => _recoveryService.MaybeRequeueTowerAmmoRequest(tower);

        internal int GetLowAmmoThresholdValue(int max) => GetLowAmmoThreshold(max);

        internal void ResetRequestStateForTower(int towerId)
        {
            _cooldownManager.ResetForTower(towerId);
            PendingReqTower.Remove(towerId);
            PendingPriorityByTower.Remove(towerId);
            _towerNoJobLogged.Remove(towerId);
            _towerDeadlockLogged.Remove(towerId);
        }

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
            bool hasRecipe = _armoryBufferPlanner.TryGetAmmoRecipe(out var recipe);

            var forges = _s.WorldIndex.Forges;
            for (int i = 0; i < forges.Count; i++)
            {
                var forge = forges[i];
                if (!_s.WorldState.Buildings.Exists(forge)) continue;

                var bs = _s.WorldState.Buildings.Get(forge);
                if (!bs.IsConstructed) continue;

                bool forgeHasNpc = _workplacesWithNpc.Contains(forge.Value);
                if (!hasRecipe)
                    continue;

                if (!_armoryBufferPlanner.HasCapForForgeInputs(forge, recipe))
                    continue;

                _armoryBufferPlanner.EnsureForgeSupplyByRecipe(forge, bs.Anchor, recipe);
                if (forgeHasNpc)
                    _armoryBufferPlanner.TryStartCraft(forge);
            }

            _armoryBufferPlanner.EnsureArmoryAmmoBuffer();
            _metricsReporter.UpdateDebugMetrics();
            _recoveryService.LogPotentialResupplyDeadlock();
        }
    }
}
