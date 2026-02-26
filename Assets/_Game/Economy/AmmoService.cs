using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;

namespace SeasonalBastion
{
    public sealed class AmmoService : IAmmoService, ITickable
    {
        private readonly GameServices _s;
        private readonly List<AmmoRequest> _urgent = new();
        private readonly List<AmmoRequest> _normal = new();

        // Deterministic sim-time (no Unity realtime)
        private float _simTime;

        // Cache to detect ammo changes without relying on combat firing hook
        private readonly Dictionary<int, int> _lastAmmoByTower = new();
        private readonly Dictionary<int, int> _lastCapByTower = new();
        private readonly Dictionary<int, byte> _lastStateByTower = new(); // 0=ok,1=low,2=empty

        // Cooldown timestamps (sim time)
        private readonly Dictionary<int, float> _nextReqLowAt = new();
        private readonly Dictionary<int, float> _nextReqEmptyAt = new();

        // Anti-dup pending request per tower (supports promote low->empty)
        private readonly HashSet<int> _pendingReqTower = new();
        private readonly Dictionary<int, AmmoRequestPriority> _pendingPriorityByTower = new();

        // ----------------- Day25 DEV HOOK (no combat yet) -----------------
        // Toggle from Debug HUD. When enabled, ammo will be drained periodically from towers to simulate firing.
        public bool DevHook_Enabled { get; set; } = false;
        public float DevHook_ShotInterval { get; set; } = 0.50f;
        public int DevHook_AmmoPerShot { get; set; } = 1;

        private float _devHookTimer;

        // Prevent enqueue spam: (forgeId*16 + resType) -> jobId
        private readonly Dictionary<int, JobId> _supplyJobByForgeAndType = new();
        private readonly Dictionary<int, JobId> _craftJobByForge = new();

        // Day24: 1 pending HaulAmmoToArmory job per Armory
        private readonly Dictionary<int, JobId> _haulAmmoJobByArmory = new();

        // Day26: 1 pending ResupplyTower job per Armory
        private readonly Dictionary<int, JobId> _resupplyJobByArmory = new();
        private readonly Dictionary<int, JobId> _resupplyJobByTower = new();
        private readonly List<int> _tmpTowerKeys = new(64);

        // Reuse buffers
        private readonly List<NpcId> _npcIds = new(64);
        private readonly HashSet<int> _workplacesWithNpc = new();

        // Cached recipe snapshot (reloaded lazily)
        private RecipeDef _cachedAmmoRecipe;
        private string _cachedAmmoRecipeId = null;

        public int Debug_InFlightResupplyJobs => _resupplyJobByTower.Count;
        public int Debug_InFlightHaulAmmoJobs => _haulAmmoJobByArmory.Count;
        public int Debug_PendingUrgent => _urgent.Count;
        public int Debug_PendingNormal => _normal.Count;

        public AmmoService(GameServices s) { _s = s; }

        public int PendingRequests => _urgent.Count + _normal.Count;

        // ----------------- Tunables (prefer Balance json if present; fallback to defaults) -----------------

        private int LowAmmoPercent => GetBalInt("ammoMonitor", "lowAmmoPct", 25);

        private float ReqCooldownLow => GetBalFloat("ammoMonitor", "reqCooldownLowSec", 8f);
        private float ReqCooldownEmpty => GetBalFloat("ammoMonitor", "reqCooldownEmptySec", 4f);

        private float NotifyCooldownLow => GetBalFloat("ammoMonitor", "notifyCooldownLowSec", 6f);
        private float NotifyCooldownEmpty => GetBalFloat("ammoMonitor", "notifyCooldownEmptySec", 4f);

        private int ForgeTargetCrafts => GetBalInt("ammoSupply", "forgeTargetCrafts", 5);

        private string AmmoRecipeId => GetBalString("crafting", "ammoRecipeId", "ForgeAmmo");

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

            int thr = (max * LowAmmoPercent + 99) / 100; // ceil
            if (thr < 1) thr = 1;

            byte stateNow;
            if (current <= 0) stateNow = 2;          // empty
            else if (current <= thr) stateNow = 1;   // low
            else stateNow = 0;                       // ok

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

            // ---- Request queue (non-spam + per tower cooldown) ----
            if (stateNow == 0) return;

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

        public bool TryStartCraft(BuildingId forge)
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

            EnsureTestTowerExists_IfNeeded();

            DevHook_Tick(dt);         // simulate ammo drain (optional)
            ScanTowers_AndNotify();   // detect changes + enqueue request

            RebuildWorkplaceHasNpcSet();

            // Cache recipe once per tick (avoid repeated parse/lookup)
            bool hasRecipe = TryGetAmmoRecipe(out var recipe);

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
                if (!HasCapForForgeInputs(forge, recipe))
                    continue;

                EnsureForgeSupplyByRecipe(forge, bs.Anchor, recipe);

                // Start craft if possible (only if there is a worker at forge)
                if (forgeHasNpc)
                    TryStartCraft(forge);
            }

            CleanupResupplyTowerInFlight();
            EnsureResupplyTowerJobs();   // Day26
            EnsureArmoryAmmoBuffer();    // Day24
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
        }

        // ----------------- Recipe-driven forge supply -----------------

        private bool TryGetAmmoRecipe(out RecipeDef recipe)
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
                    catch { }
                }
                return false;
            }
        }

        private bool HasCapForForgeInputs(BuildingId forge, RecipeDef recipe)
        {
            int capMain = _s.StorageService.GetCap(forge, recipe.InputType);
            if (capMain <= 0) return false;

            var extras = recipe.ExtraInputs;
            if (extras != null && extras.Length > 0)
            {
                for (int i = 0; i < extras.Length; i++)
                {
                    var c = extras[i];
                    if (c == null || c.Amount <= 0) continue;
                    int capX = _s.StorageService.GetCap(forge, c.Resource);
                    if (capX <= 0) return false;
                }
            }

            return true;
        }

        private void EnsureForgeSupplyByRecipe(BuildingId forge, CellPos forgeAnchor, RecipeDef recipe)
        {
            // Target = perCraftAmount * ForgeTargetCrafts (clamp by cap)
            // We enqueue HaulToForge jobs for any deficit.
            int crafts = ForgeTargetCrafts;
            if (crafts < 1) crafts = 1;

            // main input
            EnsureSupplyJobToForge_ByTarget(forge, forgeAnchor, recipe.InputType, recipe.InputAmount, crafts);

            // extras
            var extras = recipe.ExtraInputs;
            if (extras != null && extras.Length > 0)
            {
                for (int i = 0; i < extras.Length; i++)
                {
                    var c = extras[i];
                    if (c == null || c.Amount <= 0) continue;
                    EnsureSupplyJobToForge_ByTarget(forge, forgeAnchor, c.Resource, c.Amount, crafts);
                }
            }
        }

        private void EnsureSupplyJobToForge_ByTarget(BuildingId forge, CellPos forgeAnchor, ResourceType rt, int perCraftAmount, int craftsTarget)
        {
            if (perCraftAmount <= 0) return;

            int cap = _s.StorageService.GetCap(forge, rt);
            int cur = _s.StorageService.GetAmount(forge, rt);
            if (cap <= 0) return;
            if (cur >= cap) return;

            int target = perCraftAmount * craftsTarget;
            if (target > cap) target = cap;

            int want = target - cur;
            if (want <= 0) return;

            int free = cap - cur;
            if (want > free) want = free;

            // Clamp by per-trip carry cap (fallback = 10).
            // If you later data-drive HaulToForge carry, update this constant or read from Balance.
            const int CarryCapFallback = 10;
            if (want > CarryCapFallback) want = CarryCapFallback;

            if (want <= 0) return;

            int key = forge.Value * 16 + (int)rt;
            if (_supplyJobByForgeAndType.TryGetValue(key, out var oldId))
            {
                if (_s.JobBoard.TryGet(oldId, out var old) && !IsTerminal(old.Status))
                    return;
            }

            if (!TryPickPreferredHaulerWorkplace(forgeAnchor, out var workplace))
                return;

            var j = new Job
            {
                Archetype = JobArchetype.HaulToForge,
                Status = JobStatus.Created,

                Workplace = workplace,
                SourceBuilding = default,     // executor will pick nearest storage with amount
                DestBuilding = forge,

                ResourceType = rt,
                Amount = want,
                TargetCell = default,
                CreatedAt = 0
            };

            var id = _s.JobBoard.Enqueue(j);
            _supplyJobByForgeAndType[key] = id;
        }

        // ----------------- Day26: ResupplyTower provider -----------------

        private void EnsureResupplyTowerJobs()
        {
            var armories = _s.WorldIndex.Armories;
            if (armories == null || armories.Count == 0) return;

            PruneInvalidRequests(_urgent);
            PruneInvalidRequests(_normal);

            for (int i = 0; i < armories.Count; i++)
            {
                var arm = armories[i];
                if (!_s.WorldState.Buildings.Exists(arm)) continue;

                var armSt = _s.WorldState.Buildings.Get(arm);
                if (!armSt.IsConstructed) continue;

                if (!_workplacesWithNpc.Contains(arm.Value)) continue;
                if (!_s.StorageService.CanStore(arm, ResourceType.Ammo)) continue;

                int armAmmo = _s.StorageService.GetAmount(arm, ResourceType.Ammo);
                if (armAmmo <= 0) continue;

                if (_resupplyJobByArmory.TryGetValue(arm.Value, out var oldId))
                {
                    if (_s.JobBoard.TryGet(oldId, out var old) && !IsTerminal(old.Status))
                        continue;
                }

                if (!TryPickBestRequestForArmory(armSt.Anchor, out var list, out var idx, out var req))
                    continue;

                if (!_s.WorldState.Towers.Exists(req.Tower))
                {
                    ConsumeRequestAt(list, idx);
                    continue;
                }

                var ts = _s.WorldState.Towers.Get(req.Tower);
                if (ts.AmmoCap <= 0)
                {
                    ConsumeRequestAt(list, idx);
                    continue;
                }

                int need = ts.AmmoCap - ts.Ammo;
                if (need <= 0)
                {
                    ConsumeRequestAt(list, idx);
                    continue;
                }

                int trip = GetArmoryResupplyTripByLevel(armSt.Level);
                int amount = trip;
                if (amount > need) amount = need;
                if (amount > armAmmo) amount = armAmmo;
                if (amount <= 0) continue;

                ConsumeRequestAt(list, idx);

                var j = new Job
                {
                    Archetype = JobArchetype.ResupplyTower,
                    Status = JobStatus.Created,

                    Workplace = arm,
                    SourceBuilding = arm,

                    Tower = req.Tower,
                    ResourceType = ResourceType.Ammo,
                    Amount = amount,

                    TargetCell = default,
                    CreatedAt = 0
                };

                var id = _s.JobBoard.Enqueue(j);
                _resupplyJobByArmory[arm.Value] = id;
                _resupplyJobByTower[req.Tower.Value] = id;
            }
        }

        private void CleanupResupplyTowerInFlight()
        {
            if (_resupplyJobByTower.Count == 0) return;

            _tmpTowerKeys.Clear();
            foreach (var kv in _resupplyJobByTower)
                _tmpTowerKeys.Add(kv.Key);

            for (int i = 0; i < _tmpTowerKeys.Count; i++)
            {
                int tid = _tmpTowerKeys[i];
                var jid = _resupplyJobByTower[tid];

                if (!_s.JobBoard.TryGet(jid, out var j) || IsTerminal(j.Status))
                {
                    _resupplyJobByTower.Remove(tid);

                    if (_s.WorldState != null && _s.WorldState.Towers.Exists(new TowerId(tid)))
                    {
                        if (j.Status == JobStatus.Cancelled || j.Status == JobStatus.Failed)
                            MaybeRequeueTowerAmmoRequest(new TowerId(tid));
                    }
                }
            }
        }

        private bool TryPickBestRequestForArmory(CellPos armAnchor, out List<AmmoRequest> list, out int index, out AmmoRequest req)
        {
            if (TryFindBestRequestIndex(_urgent, armAnchor, out index, out req))
            {
                list = _urgent;
                return true;
            }

            if (TryFindBestRequestIndex(_normal, armAnchor, out index, out req))
            {
                list = _normal;
                return true;
            }

            list = null;
            index = -1;
            req = default;
            return false;
        }

        private bool TryFindBestRequestIndex(List<AmmoRequest> src, CellPos armAnchor, out int bestIndex, out AmmoRequest bestReq)
        {
            bestIndex = -1;
            bestReq = default;

            int bestDist = int.MaxValue;
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
                }

                var ts = _s.WorldState.Towers.Get(r.Tower);
                int need = ts.AmmoCap - ts.Ammo;
                if (need <= 0) continue;

                int d = Manhattan(armAnchor, ts.Cell);

                if (d < bestDist || (d == bestDist && tid < bestTid))
                {
                    bestDist = d;
                    bestTid = tid;
                    bestIndex = i;
                    bestReq = r;
                }
            }

            return bestIndex >= 0;
        }

        private void ConsumeRequestAt(List<AmmoRequest> list, int index)
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
                }
            }
        }

        private bool TryPickPreferredHaulerWorkplace(CellPos forgeAnchor, out BuildingId workplace)
        {
            workplace = default;

            // 1) Prefer Armory with at least one NPC assigned
            if (TryPickNearestWorkplaceFromIndex(_s.WorldIndex.Armories, forgeAnchor, requireNpc: true, out workplace))
                return true;

            // 2) Fallback: Warehouse/HQ with NPC
            if (TryPickNearestWorkplaceFromIndex(_s.WorldIndex.Warehouses, forgeAnchor, requireNpc: true, out workplace))
                return true;

            return false;
        }

        private bool TryPickNearestWorkplaceFromIndex(IReadOnlyList<BuildingId> list, CellPos from, bool requireNpc, out BuildingId best)
        {
            best = default;

            int bestDist = int.MaxValue;
            int bestId = int.MaxValue;

            for (int i = 0; i < list.Count; i++)
            {
                var bid = list[i];
                if (!_s.WorldState.Buildings.Exists(bid)) continue;

                var bs = _s.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                if (requireNpc && !_workplacesWithNpc.Contains(bid.Value)) continue;

                int d = Manhattan(from, bs.Anchor);
                int idv = bid.Value;

                if (d < bestDist || (d == bestDist && idv < bestId))
                {
                    bestDist = d;
                    bestId = idv;
                    best = bid;
                }
            }

            return best.Value != 0;
        }

        private void RebuildWorkplaceHasNpcSet()
        {
            _npcIds.Clear();
            foreach (var id in _s.WorldState.Npcs.Ids) _npcIds.Add(id);
            _npcIds.Sort((a, b) => a.Value.CompareTo(b.Value));

            _workplacesWithNpc.Clear();
            for (int i = 0; i < _npcIds.Count; i++)
            {
                var nid = _npcIds[i];
                if (!_s.WorldState.Npcs.Exists(nid)) continue;
                var ns = _s.WorldState.Npcs.Get(nid);
                if (ns.Workplace.Value != 0)
                    _workplacesWithNpc.Add(ns.Workplace.Value);
            }
        }

        private static bool IsTerminal(JobStatus s)
        {
            return s == JobStatus.Completed || s == JobStatus.Failed || s == JobStatus.Cancelled;
        }

        private static int Manhattan(CellPos a, CellPos b)
        {
            int dx = a.X - b.X; if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
            return dx + dy;
        }

        // ----------------- Day24: Armory buffer + HaulAmmo -----------------

        private void EnsureArmoryAmmoBuffer()
        {
            var armories = _s.WorldIndex.Armories;
            if (armories == null || armories.Count == 0) return;

            for (int i = 0; i < armories.Count; i++)
            {
                var arm = armories[i];
                if (!_s.WorldState.Buildings.Exists(arm)) continue;

                var armSt = _s.WorldState.Buildings.Get(arm);
                if (!armSt.IsConstructed) continue;

                if (!_workplacesWithNpc.Contains(arm.Value)) continue;

                if (!_s.StorageService.CanStore(arm, ResourceType.Ammo)) continue;

                int cap = _s.StorageService.GetCap(arm, ResourceType.Ammo);
                if (cap <= 0) continue;

                int cur = _s.StorageService.GetAmount(arm, ResourceType.Ammo);

                int target = (cap * 80) / 100;
                if (cur >= target) continue;

                if (_haulAmmoJobByArmory.TryGetValue(arm.Value, out var oldId))
                {
                    if (_s.JobBoard.TryGet(oldId, out var old) && !IsTerminal(old.Status))
                        continue;
                }

                if (!TryPickForgeAmmoSource(armSt.Anchor, out var forge, out var takeable))
                    continue;

                int free = cap - cur;
                if (free <= 0) continue;

                int need = target - cur;

                int chunk = GetArmoryChunkByLevel(armSt.Level);

                int amount = chunk;
                if (amount > need) amount = need;
                if (amount > free) amount = free;
                if (amount > takeable) amount = takeable;

                if (amount <= 0) continue;

                var j = new Job
                {
                    Archetype = JobArchetype.HaulAmmoToArmory,
                    Status = JobStatus.Created,

                    Workplace = arm,
                    SourceBuilding = forge,
                    DestBuilding = arm,

                    ResourceType = ResourceType.Ammo,
                    Amount = amount,

                    TargetCell = default,
                    CreatedAt = 0
                };

                var id = _s.JobBoard.Enqueue(j);
                _haulAmmoJobByArmory[arm.Value] = id;
            }
        }

        private bool TryPickForgeAmmoSource(CellPos refPos, out BuildingId bestForge, out int bestTakeable)
        {
            bestForge = default;
            bestTakeable = 0;

            var forges = _s.WorldIndex.Forges;
            if (forges == null || forges.Count == 0) return false;

            int bestDist = int.MaxValue;
            int bestId = int.MaxValue;

            for (int i = 0; i < forges.Count; i++)
            {
                var f = forges[i];
                if (!_s.WorldState.Buildings.Exists(f)) continue;

                var fs = _s.WorldState.Buildings.Get(f);
                if (!fs.IsConstructed) continue;

                if (!_s.StorageService.CanStore(f, ResourceType.Ammo)) continue;

                int cap = _s.StorageService.GetCap(f, ResourceType.Ammo);
                if (cap <= 0) continue;

                int cur = _s.StorageService.GetAmount(f, ResourceType.Ammo);
                if (cur <= 0) continue;

                int keep = (cap * 20 + 99) / 100; // ceil
                if (keep < 1) keep = 1;

                if (cur < keep) continue;

                int takeable = cur - keep;
                if (takeable <= 0) continue;

                int d = Manhattan(refPos, fs.Anchor);
                int idv = f.Value;

                if (d < bestDist || (d == bestDist && idv < bestId))
                {
                    bestDist = d;
                    bestId = idv;
                    bestForge = f;
                    bestTakeable = takeable;
                }
            }

            return bestForge.Value != 0;
        }

        private static int GetArmoryChunkByLevel(int level)
        {
            int lvl = level <= 0 ? 1 : (level > 3 ? 3 : level);
            return lvl == 1 ? 40 : (lvl == 2 ? 60 : 80);
        }

        private static int GetArmoryResupplyTripByLevel(int level)
        {
            int lvl = level <= 0 ? 1 : (level > 3 ? 3 : level);
            return lvl == 1 ? 20 : (lvl == 2 ? 30 : 40);
        }

        private void ScanTowers_AndNotify()
        {
            var towers = _s.WorldIndex.Towers;
            if (towers == null) return;

            for (int i = 0; i < towers.Count; i++)
            {
                var tid = towers[i];
                if (!_s.WorldState.Towers.Exists(tid)) continue;

                var ts = _s.WorldState.Towers.Get(tid);

                int cur = ts.Ammo;
                int cap = ts.AmmoCap;

                if (_lastAmmoByTower.TryGetValue(tid.Value, out var lastAmmo) &&
                    _lastCapByTower.TryGetValue(tid.Value, out var lastCap) &&
                    lastAmmo == cur && lastCap == cap)
                    continue;

                _lastAmmoByTower[tid.Value] = cur;
                _lastCapByTower[tid.Value] = cap;

                NotifyTowerAmmoChanged(tid, cur, cap);
            }
        }

        private void DevHook_Tick(float dt)
        {
            if (!DevHook_Enabled) return;

            _devHookTimer -= dt;
            if (_devHookTimer > 0f) return;

            _devHookTimer += (DevHook_ShotInterval > 0f ? DevHook_ShotInterval : 0.5f);

            var towers = _s.WorldIndex.Towers;
            if (towers == null) return;

            for (int i = 0; i < towers.Count; i++)
            {
                var tid = towers[i];
                if (!_s.WorldState.Towers.Exists(tid)) continue;

                var ts = _s.WorldState.Towers.Get(tid);
                if (ts.AmmoCap <= 0) continue;
                if (ts.Ammo <= 0) continue;

                int dec = DevHook_AmmoPerShot <= 0 ? 1 : DevHook_AmmoPerShot;
                int newAmmo = ts.Ammo - dec;
                if (newAmmo < 0) newAmmo = 0;

                ts.Ammo = newAmmo;
                _s.WorldState.Towers.Set(tid, ts);

                _lastAmmoByTower[tid.Value] = newAmmo;
                _lastCapByTower[tid.Value] = ts.AmmoCap;

                NotifyTowerAmmoChanged(tid, newAmmo, ts.AmmoCap);
                break; // 1 shot per interval (deterministic)
            }
        }

        private void EnsureTestTowerExists_IfNeeded()
        {
            if (!DevHook_Enabled) return;
            if (_s.WorldState == null || _s.WorldIndex == null || _s.GridMap == null || _s.DataRegistry == null) return;

            if (_s.WorldIndex.Towers != null && _s.WorldIndex.Towers.Count > 0)
                return;

            CellPos center = default;
            bool foundHQ = false;

            foreach (var bid in _s.WorldState.Buildings.Ids)
            {
                var bs = _s.WorldState.Buildings.Get(bid);
                if (bs.IsConstructed && bs.DefId == "bld_hq_t1")
                {
                    center = bs.Anchor;
                    foundHQ = true;
                    break;
                }
            }

            if (!foundHQ) center = new CellPos(0, 0);

            CellPos spawn = default;
            bool found = false;

            const int R = 12;
            for (int r = 1; r <= R && !found; r++)
            {
                for (int dx = -r; dx <= r && !found; dx++)
                {
                    var c1 = new CellPos(center.X + dx, center.Y + r);
                    var c2 = new CellPos(center.X + dx, center.Y - r);

                    if (_s.GridMap.IsInside(c1) && _s.GridMap.Get(c1).Kind == CellOccupancyKind.Empty) { spawn = c1; found = true; break; }
                    if (_s.GridMap.IsInside(c2) && _s.GridMap.Get(c2).Kind == CellOccupancyKind.Empty) { spawn = c2; found = true; break; }
                }

                for (int dy = -r + 1; dy <= r - 1 && !found; dy++)
                {
                    var c1 = new CellPos(center.X + r, center.Y + dy);
                    var c2 = new CellPos(center.X - r, center.Y + dy);

                    if (_s.GridMap.IsInside(c1) && _s.GridMap.Get(c1).Kind == CellOccupancyKind.Empty) { spawn = c1; found = true; break; }
                    if (_s.GridMap.IsInside(c2) && _s.GridMap.Get(c2).Kind == CellOccupancyKind.Empty) { spawn = c2; found = true; break; }
                }
            }

            if (!found) return;

            TowerDef def;
            try { def = _s.DataRegistry.GetTower("bld_tower_arrow_t1"); }
            catch { def = null; }

            int ammoCap = def != null ? def.AmmoMax : 60;
            int hpMax = def != null ? def.MaxHp : 200;

            var st = new TowerState
            {
                Id = default,
                Cell = spawn,
                Ammo = ammoCap,
                AmmoCap = ammoCap,
                Hp = hpMax,
                HpMax = hpMax
            };

            var tid = _s.WorldState.Towers.Create(st);
            st.Id = tid;
            _s.WorldState.Towers.Set(tid, st);

            _s.WorldIndex.RebuildAll();

            _s.NotificationService?.Push(
                key: $"Dev_TowerSpawn_{tid.Value}",
                title: "DEV",
                body: $"Spawn test tower {tid.Value} at ({spawn.X},{spawn.Y}) ammo {ammoCap}/{ammoCap}",
                severity: NotificationSeverity.Info,
                payload: default,
                cooldownSeconds: 0.5f,
                dedupeByKey: true
            );
        }

        private void MaybeRequeueTowerAmmoRequest(TowerId tower)
        {
            if (tower.Value == 0) return;
            if (_s.WorldState == null || !_s.WorldState.Towers.Exists(tower)) return;

            var ts = _s.WorldState.Towers.Get(tower);
            int cap = ts.AmmoCap;
            if (cap <= 0) return;

            int cur = ts.Ammo;
            int need = cap - cur;
            if (need <= 0) return;

            if (_pendingReqTower.Contains(tower.Value)) return;

            if (_resupplyJobByTower.TryGetValue(tower.Value, out var inflight))
            {
                if (_s.JobBoard != null && _s.JobBoard.TryGet(inflight, out var jj) && !IsTerminal(jj.Status))
                    return;
            }

            int thr = (cap * LowAmmoPercent + 99) / 100;
            if (thr < 1) thr = 1;

            AmmoRequestPriority pri;
            if (cur <= 0) pri = AmmoRequestPriority.Urgent;
            else if (cur <= thr) pri = AmmoRequestPriority.Normal;
            else return;

            float now = _simTime;
            if (pri == AmmoRequestPriority.Urgent)
            {
                if (_nextReqEmptyAt.TryGetValue(tower.Value, out var until) && now < until) return;
                _nextReqEmptyAt[tower.Value] = now + ReqCooldownEmpty;
            }
            else
            {
                if (_nextReqLowAt.TryGetValue(tower.Value, out var until) && now < until) return;
                _nextReqLowAt[tower.Value] = now + ReqCooldownLow;
            }

            var req = new AmmoRequest
            {
                Tower = tower,
                AmountNeeded = need,
                Priority = pri,
                CreatedAt = now
            };
            EnqueueRequest(req);
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
            catch { }
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
            catch { }
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
            catch { }
            return fallback;
        }
    }
}