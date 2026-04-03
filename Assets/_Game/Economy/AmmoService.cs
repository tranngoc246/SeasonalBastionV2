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
        private readonly List<AmmoRequest> _urgent = new();
        private readonly List<AmmoRequest> _normal = new();
        internal List<AmmoRequest> UrgentRequests => _urgent;
        internal List<AmmoRequest> NormalRequests => _normal;

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

        // Cooldown timestamps (sim time)
        private readonly Dictionary<int, float> _nextReqLowAt = new();
        private readonly Dictionary<int, float> _nextReqEmptyAt = new();

        // Anti-dup pending request per tower (supports promote low->empty)
        private readonly HashSet<int> _pendingReqTower = new();
        internal HashSet<int> PendingReqTower => _pendingReqTower;
        private readonly Dictionary<int, AmmoRequestPriority> _pendingPriorityByTower = new();
        internal Dictionary<int, AmmoRequestPriority> PendingPriorityByTower => _pendingPriorityByTower;

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
        private readonly Dictionary<int, JobId> _resupplyJobByArmory = new();
        internal Dictionary<int, JobId> ResupplyJobByArmory => _resupplyJobByArmory;
        private readonly Dictionary<int, JobId> _resupplyJobByTower = new();
        internal Dictionary<int, JobId> ResupplyJobByTower => _resupplyJobByTower;
        private readonly List<int> _tmpTowerKeys = new(64);
        internal List<int> TempTowerKeys => _tmpTowerKeys;

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

        public int Debug_InFlightResupplyJobs => _resupplyJobByTower.Count;
        public int Debug_InFlightHaulAmmoJobs => _haulAmmoJobByArmory.Count;
        public int Debug_PendingUrgent => _urgent.Count;
        public int Debug_PendingNormal => _normal.Count;
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
        }

        public int PendingRequests => _urgent.Count + _normal.Count;
        internal GameServices Services => _s;

        // ----------------- Tunables (prefer Balance json if present; fallback to defaults) -----------------

        private int LowAmmoPercent => GetBalInt("ammoMonitor", "lowAmmoPct", 25);
        private bool DebugAmmoLogs => GetBalBool("ammoMonitor", "debugLogs", false);
        internal bool DebugAmmoLogsValue => DebugAmmoLogs;

        private float ReqCooldownLow => GetBalFloat("ammoMonitor", "reqCooldownLowSec", 8f);
        private float ReqCooldownEmpty => GetBalFloat("ammoMonitor", "reqCooldownEmptySec", 4f);

        private float NotifyCooldownLow => GetBalFloat("ammoMonitor", "notifyCooldownLowSec", 6f);
        private float NotifyCooldownEmpty => GetBalFloat("ammoMonitor", "notifyCooldownEmptySec", 4f);

        private int ForgeTargetCrafts => GetBalInt("ammoSupply", "forgeTargetCrafts", 5);
        internal int ForgeTargetCraftsValue => ForgeTargetCrafts;

        private string AmmoRecipeId => GetBalString("crafting", "ammoRecipeId", "ForgeAmmo");
        internal string AmmoRecipeIdValue => AmmoRecipeId;

        // ----------------- Public API -----------------

        public void NotifyTowerAmmoChanged(TowerId tower, int current, int max)
        {
            if (tower.Value == 0) return;
            if (max <= 0) return;

            int tid = tower.Value;

            // Day38: nếu tower đã có ResupplyTower job in-flight thì không enqueue request mới (anti-spam)
            if (_resupplyJobByTower.TryGetValue(tid, out var inflight))
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

            if (pri == AmmoRequestPriority.Urgent)
            {
                if (_nextReqEmptyAt.TryGetValue(tid, out var until) && _simTime < until)
                    return;

                _nextReqEmptyAt[tid] = _simTime + ReqCooldownEmpty;
            }
            else
            {
                if (_nextReqLowAt.TryGetValue(tid, out var until) && _simTime < until)
                    return;

                _nextReqLowAt[tid] = _simTime + ReqCooldownLow;
            }

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

        public void EnqueueRequest(AmmoRequest req)
        {
            int tid = req.Tower.Value;
            if (tid == 0) return;

            if (_pendingReqTower.Contains(tid))
            {
                if (_pendingPriorityByTower.TryGetValue(tid, out var oldPri)
                    && oldPri == AmmoRequestPriority.Normal
                    && req.Priority == AmmoRequestPriority.Urgent)
                {
                    for (int i = 0; i < _normal.Count; i++)
                    {
                        if (_normal[i].Tower.Value == tid)
                        {
                            _normal.RemoveAt(i);
                            break;
                        }
                    }

                    _urgent.Add(req);
                    _pendingPriorityByTower[tid] = AmmoRequestPriority.Urgent;
                }

                return;
            }

            _pendingReqTower.Add(tid);
            _pendingPriorityByTower[tid] = req.Priority;

            if (req.Priority == AmmoRequestPriority.Urgent) _urgent.Add(req);
            else _normal.Add(req);
        }

        public bool TryDequeueNext(out AmmoRequest req)
        {
            if (_urgent.Count > 0)
            {
                req = _urgent[0];
                _urgent.RemoveAt(0);

                int tid = req.Tower.Value;
                if (tid != 0)
                {
                    _pendingReqTower.Remove(tid);
                    _pendingPriorityByTower.Remove(tid);
                }
                return true;
            }

            if (_normal.Count > 0)
            {
                req = _normal[0];
                _normal.RemoveAt(0);

                int tid = req.Tower.Value;
                if (tid != 0)
                {
                    _pendingReqTower.Remove(tid);
                    _pendingPriorityByTower.Remove(tid);
                }
                return true;
            }

            req = default;
            return false;
        }

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

            _simTime += dt;

            _topologyCache.CleanupDestroyedTowerCaches();
            _debugHooks.EnsureTestTowerExistsIfNeeded();

            _debugHooks.Tick(dt);     // simulate ammo drain (optional)
            _topologyCache.ScanTowersAndNotify();   // detect changes + enqueue request

            _topologyCache.RebuildWorkplaceHasNpcSet();

            // Cache recipe once per tick (avoid repeated parse/lookup)
            bool hasRecipe = _armoryBufferPlanner.TryGetAmmoRecipe(out var recipe);

            // For each Forge: ensure supply, then try start craft
            var forges = _s.WorldIndex.Forges;
            for (int i = 0; i < forges.Count; i++)
            {
                var forge = forges[i];
                if (!_s.WorldState.Buildings.Exists(forge)) continue;

                var bs = _s.WorldState.Buildings.Get(forge);
                if (!bs.IsConstructed) continue;

                // If no NPC at forge, skip starting craft (still can enqueue supply)
                bool forgeHasNpc = _workplacesWithNpc.Contains(forge.Value);

                if (!hasRecipe)
                    continue;

                // Ensure Forge has local caps for inputs (must be >0)
                // If cap is 0, hauling won't work; user must set in Buildings.json
                if (!_armoryBufferPlanner.HasCapForForgeInputs(forge, recipe))
                    continue;

                _armoryBufferPlanner.EnsureForgeSupplyByRecipe(forge, bs.Anchor, recipe);

                // Start craft if possible (only if there is a worker at forge)
                if (forgeHasNpc)
                    _armoryBufferPlanner.TryStartCraft(forge);
            }

            _towerResupplyPlanner.CleanupResupplyTowerInFlight();
            _towerResupplyPlanner.EnsureResupplyTowerJobs();   // Day26
            _topologyCache.ReconcileOutstandingTowerNeeds();
            _towerResupplyPlanner.EnsureResupplyTowerJobs();   // backfill only if cleanup/terminal paths dropped demand
            _armoryBufferPlanner.EnsureArmoryAmmoBuffer();    // Day24
            _towerResupplyPlanner.UpdateDebugMetrics();
            _towerResupplyPlanner.LogPotentialResupplyDeadlock();
        }

        public void ClearAll()
        {
            // IMPORTANT (VS3 hardening): clear ALL runtime caches.

            _urgent.Clear();
            _normal.Clear();

            _simTime = 0f;
            _devHookTimer = 0f;

            _lastAmmoByTower.Clear();
            _lastCapByTower.Clear();
            _lastStateByTower.Clear();
            _towerNeedLogged.Clear();
            _towerNoSourceLogged.Clear();
            _towerNoJobLogged.Clear();
            _towerDeadlockLogged.Clear();

            _nextReqLowAt.Clear();
            _nextReqEmptyAt.Clear();

            _pendingReqTower.Clear();
            _pendingPriorityByTower.Clear();

            _supplyJobByForgeAndType.Clear();
            _craftJobByForge.Clear();
            _haulAmmoJobByArmory.Clear();
            _resupplyJobByArmory.Clear();
            _resupplyJobByTower.Clear();

            _tmpTowerKeys.Clear();
            _npcIds.Clear();
            _workplacesWithNpc.Clear();

            _cachedAmmoRecipe = null;
            _cachedAmmoRecipeId = null;

            Debug_TotalTowers = 0;
            Debug_TowersWithoutAmmo = 0;
            Debug_ActiveResupplyJobs = 0;
            Debug_ArmoryAvailableAmmo = 0;
        }

        internal void UpdateDebugMetrics_Core() => _towerResupplyPlanner.UpdateDebugMetrics();

        internal void LogPotentialResupplyDeadlock_Core() => _towerResupplyPlanner.LogPotentialResupplyDeadlock();

        private void LogDeadlockForRequests(List<AmmoRequest> list)
        {
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                int tid = list[i].Tower.Value;
                if (tid == 0) continue;
                if (_towerDeadlockLogged.Add(tid))
                    Log.E($"[Ammo] Armory has ammo but no job created. tower={tid} totalTowers={Debug_TotalTowers} emptyTowers={Debug_TowersWithoutAmmo} activeResupplyJobs={Debug_ActiveResupplyJobs} armoryAmmo={Debug_ArmoryAvailableAmmo} pending={PendingRequests}");
            }
        }

        private int CountEligibleRequests()
        {
            int count = 0;
            count += CountEligibleRequests(_urgent);
            count += CountEligibleRequests(_normal);
            return count;
        }

        private bool TryGetAmmoRecipe(out RecipeDef recipe) => TryGetAmmoRecipe_Core(out recipe);
        private void RebuildWorkplaceHasNpcSet() => RebuildWorkplaceHasNpcSet_Core();
        private bool TryPickPreferredHaulerWorkplace(CellPos forgeAnchor, out BuildingId workplace) => TryPickPreferredHaulerWorkplace_Core(forgeAnchor, out workplace);
        private bool TryCreateNextResupplyTowerJob() => TryCreateNextResupplyTowerJob_Core();
        private bool TryPickBestResupplySource(TowerState towerState, out BuildingId source, out BuildingState sourceState, out int availableAmmo) => TryPickBestResupplySource_Core(towerState, out source, out sourceState, out availableAmmo);
        private void EvaluateResupplySources(IReadOnlyList<BuildingId> candidates, CellPos targetCell, int rank, ref BuildingId bestSource, ref BuildingState bestState, ref int bestAmmo, ref int bestRank, ref int bestDist, ref int bestId) => EvaluateResupplySources_Core(candidates, targetCell, rank, ref bestSource, ref bestState, ref bestAmmo, ref bestRank, ref bestDist, ref bestId);
        private bool TryPickNearestWorkplaceFromIndex(IReadOnlyList<BuildingId> list, CellPos from, bool requireNpc, out BuildingId best) => TryPickNearestWorkplaceFromIndex_Core(list, from, requireNpc, out best);
        private bool TryPickForgeAmmoSource(CellPos refPos, out BuildingId bestForge, out int bestTakeable) => TryPickForgeAmmoSource_Core(refPos, out bestForge, out bestTakeable);
        private bool ContainsRequestForTower(List<AmmoRequest> list, TowerId tower) => ContainsRequestForTower_Core(list, tower);

        private int CountTrackedActiveResupplyJobs()
        {
            int count = 0;
            foreach (var kv in _resupplyJobByTower)
            {
                if (!_s.JobBoard.TryGet(kv.Value, out var job))
                    continue;

                if (!IsTerminal(job.Status))
                    count++;
            }
            return count;
        }

        private int CountEligibleRequests(List<AmmoRequest> list)
        {
            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var req = list[i];
                if (req.Tower.Value == 0) continue;
                if (!_s.WorldState.Towers.Exists(req.Tower)) continue;
                var tower = _s.WorldState.Towers.Get(req.Tower);
                if (tower.AmmoCap <= 0) continue;
                if (tower.Ammo >= tower.AmmoCap) continue;
                count++;
            }
            return count;
        }

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
            catch
            {
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
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogWarning($"[AmmoService] Failed to load fallback recipe 'ForgeAmmo' after recipe '{rid}' lookup failed: {ex}");
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

        private void CleanupResupplyArmoryMappings()
        {
            if (_resupplyJobByArmory.Count == 0) return;

            _tmpTowerKeys.Clear();
            foreach (var kv in _resupplyJobByArmory)
                _tmpTowerKeys.Add(kv.Key);

            for (int i = 0; i < _tmpTowerKeys.Count; i++)
            {
                int armoryId = _tmpTowerKeys[i];
                var jid = _resupplyJobByArmory[armoryId];
                if (!_s.JobBoard.TryGet(jid, out var j) || IsTerminal(j.Status))
                    _resupplyJobByArmory.Remove(armoryId);
            }
        }

        private void RemoveArmoryMappingByJob(JobId jobId)
        {
            if (_resupplyJobByArmory.Count == 0) return;

            _tmpTowerKeys.Clear();
            foreach (var kv in _resupplyJobByArmory)
            {
                if (kv.Value.Value == jobId.Value)
                    _tmpTowerKeys.Add(kv.Key);
            }

            for (int i = 0; i < _tmpTowerKeys.Count; i++)
                _resupplyJobByArmory.Remove(_tmpTowerKeys[i]);
        }

        internal bool TryPickBestRequest(out List<AmmoRequest> list, out int index, out AmmoRequest req, out TowerState towerState)
        {
            if (TryFindBestRequestIndex(_urgent, out index, out req, out towerState))
            {
                list = _urgent;
                return true;
            }

            if (TryFindBestRequestIndex(_normal, out index, out req, out towerState))
            {
                list = _normal;
                return true;
            }

            list = null;
            index = -1;
            req = default;
            towerState = default;
            return false;
        }

        private bool TryFindBestRequestIndex(List<AmmoRequest> src, out int bestIndex, out AmmoRequest bestReq, out TowerState bestTowerState)
        {
            bestIndex = -1;
            bestReq = default;
            bestTowerState = default;

            int bestStateRank = int.MaxValue;
            int bestAmmo = int.MaxValue;
            int bestTid = int.MaxValue;

            for (int i = 0; i < src.Count; i++)
            {
                var r = src[i];

                int tid = r.Tower.Value;
                if (tid == 0) continue;
                if (!_s.WorldState.Towers.Exists(r.Tower)) continue;

                if (_resupplyJobByTower.TryGetValue(tid, out var inFlightJob))
                {
                    if (_s.JobBoard.TryGet(inFlightJob, out var jj) && !IsTerminal(jj.Status))
                        continue;

                    _resupplyJobByTower.Remove(tid);
                }

                var ts = _s.WorldState.Towers.Get(r.Tower);
                int need = ts.AmmoCap - ts.Ammo;
                if (ts.AmmoCap <= 0 || need <= 0) continue;

                int stateRank = ts.Ammo <= 0 ? 0 : 1;
                int ammo = ts.Ammo;
                if (stateRank < bestStateRank || (stateRank == bestStateRank && (ammo < bestAmmo || (ammo == bestAmmo && tid < bestTid))))
                {
                    bestStateRank = stateRank;
                    bestAmmo = ammo;
                    bestTid = tid;
                    bestIndex = i;
                    bestReq = r;
                    bestTowerState = ts;
                }
            }

            return bestIndex >= 0;
        }

        internal bool TryCreateNextResupplyTowerJob_Core() => _towerResupplyPlanner.TryCreateNextResupplyTowerJob();

        internal bool TryPickBestResupplySource_Core(TowerState towerState, out BuildingId source, out BuildingState sourceState, out int availableAmmo) => _towerResupplyPlanner.TryPickBestResupplySource(towerState, out source, out sourceState, out availableAmmo);

        internal void EvaluateResupplySources_Core(IReadOnlyList<BuildingId> candidates, CellPos targetCell, int rank, ref BuildingId bestSource, ref BuildingState bestState, ref int bestAmmo, ref int bestRank, ref int bestDist, ref int bestId) => _towerResupplyPlanner.EvaluateResupplySources(candidates, targetCell, rank, ref bestSource, ref bestState, ref bestAmmo, ref bestRank, ref bestDist, ref bestId);

        internal void ConsumeRequestAt(List<AmmoRequest> list, int index)
        {
            if (list == null) return;
            if (index < 0 || index >= list.Count) return;

            int tid = list[index].Tower.Value;
            list.RemoveAt(index);

            if (tid != 0)
            {
                _pendingReqTower.Remove(tid);
                _pendingPriorityByTower.Remove(tid);
            }
        }

        internal void RotateRequestToBack(List<AmmoRequest> list, int index, AmmoRequest req)
        {
            if (list == null) return;
            if (index < 0 || index >= list.Count) return;

            list.RemoveAt(index);
            list.Add(req);
        }

        private void PruneInvalidRequests(List<AmmoRequest> list)
        {
            if (list == null || list.Count == 0) return;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var r = list[i];
                int tid = r.Tower.Value;

                bool remove = false;

                if (tid == 0) remove = true;
                else if (!_s.WorldState.Towers.Exists(r.Tower)) remove = true;
                else
                {
                    var ts = _s.WorldState.Towers.Get(r.Tower);
                    if (ts.AmmoCap <= 0) remove = true;
                    else
                    {
                        int need = ts.AmmoCap - ts.Ammo;
                        if (need <= 0) remove = true;
                    }
                }

                if (!remove) continue;

                list.RemoveAt(i);
                if (tid != 0)
                {
                    _pendingReqTower.Remove(tid);
                    _pendingPriorityByTower.Remove(tid);
                    _towerNoJobLogged.Remove(tid);
                    _towerDeadlockLogged.Remove(tid);
                }
            }
        }

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
            _nextReqLowAt.Remove(tid);
            _nextReqEmptyAt.Remove(tid);
            _pendingReqTower.Remove(tid);
            _pendingPriorityByTower.Remove(tid);
            _resupplyJobByTower.Remove(tid);
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

        internal void MaybeRequeueTowerAmmoRequest(TowerId tower)
        {
            if (tower.Value == 0) return;
            if (_s.WorldState == null || !_s.WorldState.Towers.Exists(tower)) return;

            var ts = _s.WorldState.Towers.Get(tower);
            int cap = ts.AmmoCap;
            if (cap <= 0) return;

            int cur = ts.Ammo;
            int need = cap - cur;
            if (need <= 0) return;

            _nextReqEmptyAt.Remove(tower.Value);
            _nextReqLowAt.Remove(tower.Value);
            _pendingReqTower.Remove(tower.Value);
            _pendingPriorityByTower.Remove(tower.Value);
            _towerNoJobLogged.Remove(tower.Value);
            _towerDeadlockLogged.Remove(tower.Value);

            int thr = GetLowAmmoThreshold(cap);
            AmmoRequestPriority pri = cur <= 0 ? AmmoRequestPriority.Urgent
                : (cur <= thr ? AmmoRequestPriority.Normal : (AmmoRequestPriority)(-1));
            if ((int)pri < 0) return;

            EnqueueRequest(new AmmoRequest
            {
                Tower = tower,
                AmountNeeded = need,
                Priority = pri,
                CreatedAt = _simTime
            });

            if (DebugAmmoLogs)
                Log.E($"[Ammo] resupply requeued tower={tower.Value} ammo={cur}/{cap} priority={pri}");
        }

        // ----------------- Balance reflection helpers -----------------
        // These read:
        // GameServices.Balance.Config.<section>.<key>
        // If not present, return fallback.

        private object TryGetBalanceConfig()
        {
            if (_s == null) return null;

            try
            {
                var sType = _s.GetType();
                var balField = sType.GetField("Balance");
                if (balField == null) return null;

                var balObj = balField.GetValue(_s);
                if (balObj == null) return null;

                var balType = balObj.GetType();
                var cfgProp = balType.GetProperty("Config");
                if (cfgProp != null)
                    return cfgProp.GetValue(balObj, null);

                var cfgField = balType.GetField("Config");
                if (cfgField != null)
                    return cfgField.GetValue(balObj);

                return null;
            }
            catch { return null; }
        }

        private int GetBalInt(string section, string key, int fallback)
        {
            try
            {
                var cfg = TryGetBalanceConfig();
                if (cfg == null) return fallback;

                var secField = cfg.GetType().GetField(section);
                if (secField == null) return fallback;

                var secObj = secField.GetValue(cfg);
                if (secObj == null) return fallback;

                var keyField = secObj.GetType().GetField(key);
                if (keyField == null) return fallback;

                var v = keyField.GetValue(secObj);
                if (v is int i) return i;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[AmmoService] Failed to access Balance.Config.{section}.{key} as int: {ex}");
            }
            return fallback;
        }

        private bool GetBalBool(string section, string key, bool fallback)
        {
            try
            {
                var cfg = TryGetBalanceConfig();
                if (cfg == null) return fallback;

                var secField = cfg.GetType().GetField(section);
                if (secField == null) return fallback;

                var secObj = secField.GetValue(cfg);
                if (secObj == null) return fallback;

                var keyField = secObj.GetType().GetField(key);
                if (keyField == null) return fallback;

                var v = keyField.GetValue(secObj);
                if (v is bool b) return b;
                if (v is int i) return i != 0;
                if (v is string s && bool.TryParse(s, out var parsed)) return parsed;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[AmmoService] Failed to access Balance.Config.{section}.{key} as bool: {ex}");
            }
            return fallback;
        }

        private float GetBalFloat(string section, string key, float fallback)
        {
            try
            {
                var cfg = TryGetBalanceConfig();
                if (cfg == null) return fallback;

                var secField = cfg.GetType().GetField(section);
                if (secField == null) return fallback;

                var secObj = secField.GetValue(cfg);
                if (secObj == null) return fallback;

                var keyField = secObj.GetType().GetField(key);
                if (keyField == null) return fallback;

                var v = keyField.GetValue(secObj);
                if (v is float f) return f;
                if (v is double d) return (float)d;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[AmmoService] Failed to access Balance.Config.{section}.{key} as float: {ex}");
            }
            return fallback;
        }

        private string GetBalString(string section, string key, string fallback)
        {
            try
            {
                var cfg = TryGetBalanceConfig();
                if (cfg == null) return fallback;

                var secField = cfg.GetType().GetField(section);
                if (secField == null) return fallback;

                var secObj = secField.GetValue(cfg);
                if (secObj == null) return fallback;

                var keyField = secObj.GetType().GetField(key);
                if (keyField == null) return fallback;

                var v = keyField.GetValue(secObj) as string;
                if (!string.IsNullOrEmpty(v)) return v;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[AmmoService] Failed to access Balance.Config.{section}.{key} as string: {ex}");
            }
            return fallback;
        }
    }
}
