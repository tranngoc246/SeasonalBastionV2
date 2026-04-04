using System;
using System.Collections.Generic;
using System.Reflection;
using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SeasonalBastion.DebugTools
{
    public enum DebugHubTab
    {
        Home = 0,
        Notifications = 1,
        WorldIndex = 2,
        Npc = 3,
        Storage = 4
    }

    public enum DebugHubMode
    {
        None = 0,
        Build = 1,
        Road = 2,
        Npc = 3,
        Storage = 4
    }

    public static class DebugHubState
    {
        public static bool Enabled;
    }

    /// <summary>
    /// Single Debug HUD Hub (Level 2):
    /// - Only ONE panel on screen
    /// - Hub owns all mode toggles (F-keys)
    /// - Tools become hub-controlled: no standalone toggles, no extra HUD panels
    /// </summary>
    public sealed partial class DebugHUDHub : MonoBehaviour
    {
        [Header("Bootstrap")]
        [SerializeField] private GameBootstrap _bootstrap;

        [Header("Modules (optional auto-find)")]
        [SerializeField] private DebugBuildingTool _buildTool;
        [SerializeField] private DebugRoadTool _roadTool;
        [SerializeField] private DebugNpcTool _npcTool;
        [SerializeField] private DebugStorageHUD _storageHud;
        [SerializeField] private DebugNotificationsHUD _notiHud;
        [SerializeField] private DebugWorldIndexHUD _worldIndexHud;
        [SerializeField] private DebugCombatLaneHUD _combatLaneHud;
        [SerializeField] private DebugRunClockHUD _runClockHud;
        [SerializeField] private DebugWaveHUD _waveHud;

        [Header("Hotkeys (Hub only)")]
        [SerializeField] private Key _toggleUiKey = Key.F1;
        [SerializeField] private Key _modeBuildKey = Key.F2;
        [SerializeField] private Key _modeRoadKey = Key.F3;
        [SerializeField] private Key _modeNpcKey = Key.F4;
        [SerializeField] private Key _modeStorageKey = Key.F5;

        [SerializeField] private Key _tabNotiKey = Key.F6;
        [SerializeField] private Key _tabWorldIndexKey = Key.F7;

        [SerializeField] private Key _clearModeKey = Key.Escape;

        [Header("State")]
        [SerializeField] private bool _showUi = true;
        [SerializeField] private DebugHubMode _mode = DebugHubMode.None;
        [SerializeField] private DebugHubTab _tab = DebugHubTab.Home;

        // VS3 QA: Quick Debug always visible + optional Advanced.
        [SerializeField] private bool _showAdvanced = false;

        // Quick spawn enemy
        [SerializeField] private string _quickEnemyDefId = "Swarmling";
        [SerializeField] private int _quickSpawnCount = 1;

        [SerializeField] private string _quickGiveAmtStr = "200";
        [SerializeField] private string _quickDamageAmtStr = "50";
        [SerializeField] private string _quickAmmoDrainAmtStr = "30";

        // tmp lists (avoid modifying stores while iterating)
        private readonly List<EnemyId> _enemyIdsTmp = new List<EnemyId>(128);
        private readonly List<TowerId> _towerIdsTmp = new List<TowerId>(64);
        private readonly List<SiteId> _siteIdsTmp = new List<SiteId>(64);

        [SerializeField] private bool _selfPollHotkeysWhenNoRouter = true;
        private bool _hasRouter;

        // Day34: Home scroll + section toggles (IMGUI)
        private Vector2 _homeScroll;
        private bool _homeShowData = true;
        private bool _homeShowRunClock = true;
        private bool _homeShowHints = true;
        private bool _homeShowLanes = true;
        private bool _homeShowSaveLoad = true;
        private bool _homeShowWave = true;

        private bool _homeShowRewards = true;

        // EndSeasonRewardRequested debug listener
        private bool _rewardListenerBound;
        private int _endSeasonRewardReqCount;
        private EndSeasonRewardRequested _lastEndSeasonRewardReq;
        private bool _hasLastEndSeasonRewardReq;
        private float _lastEndSeasonRewardRealtime;

        private GameServices _gs;

        // --- Data validation HUD (Day17) ---
        private readonly List<string> _dataErrors = new List<string>(128);
        private Vector2 _dataScroll;
        private bool _dataLastOk = true;
        private string _dataLastSummary = "Not validated";

        private readonly List<string> _buildSlotsTmp = new List<string>(8);

        private DebugSaveLoadHUD _saveLoadHUD = new DebugSaveLoadHUD();

        // ===== Hover cache (so button click doesn't lose target) =====
        private BuildingId _cachedHoverBuilding;
        private BuildingId _lockedTargetBuilding;
        private float _cachedHoverTime;
        private const float HoverCacheTTL = 3.0f; // seconds

        private void Awake()
        {
            DebugHubState.Enabled = true;

            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
            _gs = _bootstrap != null ? _bootstrap.Services : null;

            TryBindRewardListener();

            if (_buildTool == null) _buildTool = FindObjectOfType<DebugBuildingTool>(true);
            if (_roadTool == null) _roadTool = FindObjectOfType<DebugRoadTool>(true);
            if (_npcTool == null) _npcTool = FindObjectOfType<DebugNpcTool>(true);

            if (_storageHud == null) _storageHud = FindObjectOfType<DebugStorageHUD>(true);
            if (_notiHud == null) _notiHud = FindObjectOfType<DebugNotificationsHUD>(true);
            if (_worldIndexHud == null) _worldIndexHud = FindObjectOfType<DebugWorldIndexHUD>(true);
            if (_combatLaneHud == null) _combatLaneHud = FindObjectOfType<DebugCombatLaneHUD>(true);
            if (_runClockHud == null) _runClockHud = FindObjectOfType<DebugRunClockHUD>(true);
            if (_waveHud == null) _waveHud = FindObjectOfType<DebugWaveHUD>(true);

            // Hub-control: disable standalone HUD + disable standalone toggle hotkeys
            if (_buildTool != null) _buildTool.SetHubControlled(true);
            if (_roadTool != null) _roadTool.SetHubControlled(true);
            if (_npcTool != null) _npcTool.SetHubControlled(true);

            if (_storageHud != null) _storageHud.SetHubControlled(true);
            if (_notiHud != null) _notiHud.SetHubControlled(true);
            if (_worldIndexHud != null) _worldIndexHud.SetHubControlled(true);
            if (_combatLaneHud != null) _combatLaneHud.SetHubControlled(true);
            if (_runClockHud != null) _runClockHud.SetHubControlled(true);
            if (_waveHud != null) _waveHud.SetHubControlled(true);

            var kb = Keyboard.current;
            if (kb != null) HandleHotkeys(kb);

            ApplyMode(_mode);

            _hasRouter = FindObjectOfType<DebugInputRouter>(true) != null;
        }

        private void OnDestroy()
        {
            DebugHubState.Enabled = false;
        }
        private void Update()
        {
            CacheHoverTarget();
            TryLockTargetFromClick();
            // Fallback: n?u scene chua c� DebugInputRouter th� Hub t? poll hotkeys d? kh?i �m?t ph�m�.
            if (_selfPollHotkeysWhenNoRouter)
            {
                // N?u sau n�y b?n add Router v�o scene, Hub s? t? t?t fallback.
                _hasRouter = _hasRouter || (FindObjectOfType<DebugInputRouter>(true) != null);

                if (!_hasRouter)
                {
                    var kb = Keyboard.current;
                    if (kb != null) HandleHotkeys(kb);
                }
            }

            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
            _gs ??= _bootstrap != null ? _bootstrap.Services : null;

            TryBindRewardListener();

            if (_buildTool == null) _buildTool = FindObjectOfType<DebugBuildingTool>();
            if (_roadTool == null) _roadTool = FindObjectOfType<DebugRoadTool>();
            if (_npcTool == null) _npcTool = FindObjectOfType<DebugNpcTool>();

            if (_storageHud == null) _storageHud = FindObjectOfType<DebugStorageHUD>();
            if (_notiHud == null) _notiHud = FindObjectOfType<DebugNotificationsHUD>();
            if (_worldIndexHud == null) _worldIndexHud = FindObjectOfType<DebugWorldIndexHUD>();
            if (_combatLaneHud == null) _combatLaneHud = FindObjectOfType<DebugCombatLaneHUD>();
            if (_runClockHud == null) _runClockHud = FindObjectOfType<DebugRunClockHUD>();
            if (_waveHud == null) _waveHud = FindObjectOfType<DebugWaveHUD>();

            ApplyMode(_mode);
        }


        public void HandleHotkeys(Keyboard kb)
        {
            static bool Pressed(Keyboard k, Key key)
            {
                if (k == null || key == Key.None) return false;
                var c = k[key];
                return c != null && c.wasPressedThisFrame;
            }

            if (Pressed(kb, _toggleUiKey))
                _showUi = !_showUi;

            if (Pressed(kb, _clearModeKey))
                ApplyMode(DebugHubMode.None);

            if (Pressed(kb, _modeBuildKey)) { ApplyMode(DebugHubMode.Build); _tab = DebugHubTab.Home; }
            if (Pressed(kb, _modeRoadKey)) { ApplyMode(DebugHubMode.Road); _tab = DebugHubTab.Home; }
            if (Pressed(kb, _modeNpcKey)) { ApplyMode(DebugHubMode.Npc); _tab = DebugHubTab.Npc; }
            if (Pressed(kb, _modeStorageKey)) { ApplyMode(DebugHubMode.Storage); _tab = DebugHubTab.Storage; }

            if (Pressed(kb, _tabNotiKey)) _tab = DebugHubTab.Notifications;
            if (Pressed(kb, _tabWorldIndexKey)) _tab = DebugHubTab.WorldIndex;
        }

        private void ApplyMode(DebugHubMode m)
        {
            _mode = m;

            if (_buildTool != null) _buildTool.SetEnabledFromHub(m == DebugHubMode.Build);
            if (_roadTool != null) _roadTool.SetEnabledFromHub(m == DebugHubMode.Road);
            if (_npcTool != null) _npcTool.SetEnabledFromHub(m == DebugHubMode.Npc);
            if (_storageHud != null) _storageHud.SetEnabledFromHub(m == DebugHubMode.Storage);
        }

        private void TryBindRewardListener()
        {
            if (_rewardListenerBound) return;
            if (_gs == null || _gs.EventBus == null) return;

            _gs.EventBus.Subscribe<EndSeasonRewardRequested>(OnEndSeasonRewardRequested);
            _rewardListenerBound = true;
        }

        private void OnEndSeasonRewardRequested(EndSeasonRewardRequested ev)
        {
            _endSeasonRewardReqCount++;
            _lastEndSeasonRewardReq = ev;
            _hasLastEndSeasonRewardReq = true;
            _lastEndSeasonRewardRealtime = Time.realtimeSinceStartup;
        }

        private void OnGUI()
        {
            DebugHubState.Enabled = _showUi;
            if (!_showUi) return;
            GUILayout.BeginArea(new Rect(10, 50, 640, (Screen.height - 20)), GUI.skin.box);

            GUILayout.Label("[DebugHUDHub] VS3 QA QUICK | F1 UI | F2 Build | F3 Road | F4 NPC | F5 Storage | F6 Noti | F7 Index | Esc None");

            GUILayout.BeginHorizontal();
            _showAdvanced = GUILayout.Toggle(_showAdvanced, "Advanced", GUILayout.Width(110));
            GUILayout.Label($"Mode: {_mode}");
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            DrawQuick(); 

            if (_showAdvanced)
            {
                GUILayout.Space(10);
                DrawAdvancedTabs();
            }

            GUILayout.EndArea();
        }

        private void DrawQuick()
        {
            if (_gs == null)
            {
                GUILayout.Label("GameServices = null");
                return;
            }

            GUILayout.Label("ESSENTIAL DEBUG PANEL");
            GUILayout.Label("Core actions only. Advanced tabs stay below for deep debugging.");

            GUILayout.Space(4);
            GUILayout.Label("Time");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Pause", GUILayout.Width(70))) _gs.RunClock?.SetTimeScale(0f);
            if (GUILayout.Button("1x", GUILayout.Width(50))) _gs.RunClock?.SetTimeScale(1f);
            if (GUILayout.Button("2x", GUILayout.Width(50))) _gs.RunClock?.SetTimeScale(2f);
            if (GUILayout.Button("3x", GUILayout.Width(50))) _gs.RunClock?.SetTimeScale(3f);
            if (GUILayout.Button("5x", GUILayout.Width(60))) _gs.RunClock?.SetTimeScale(5f);
            GUILayout.EndHorizontal();

            if (_gs.RunClock is RunClockService clockImpl)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Now: Y{clockImpl.YearIndex} {clockImpl.CurrentSeason} D{clockImpl.DayIndex}", GUILayout.Width(180));
                if (GUILayout.Button("Prev Day", GUILayout.Width(90))) Quick_AdvanceDay(-1);
                if (GUILayout.Button("Next Day", GUILayout.Width(90))) Quick_AdvanceDay(1);
                if (GUILayout.Button("Next Season", GUILayout.Width(110))) Quick_AdvanceSeason();
                if (GUILayout.Button("Winter D4 Near End", GUILayout.Width(150))) Quick_JumpWinterD4NearEnd();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            GUILayout.Label("Economy / Unlock");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Amt", GUILayout.Width(30));
            _quickGiveAmtStr = GUILayout.TextField(_quickGiveAmtStr, GUILayout.Width(60));
            if (GUILayout.Button("100", GUILayout.Width(50))) _quickGiveAmtStr = "100";
            if (GUILayout.Button("300", GUILayout.Width(50))) _quickGiveAmtStr = "300";
            if (GUILayout.Button("1000", GUILayout.Width(55))) _quickGiveAmtStr = "1000";
            if (GUILayout.Button("Give Core x4", GUILayout.Width(110))) Quick_GiveCoreResources();
            if (GUILayout.Button("Unlock ALL", GUILayout.Width(110))) Quick_UnlockAll();
            GUILayout.EndHorizontal();

            int giveAmt = 200;
            if (!int.TryParse(_quickGiveAmtStr, out giveAmt) || giveAmt <= 0) giveAmt = 200;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Wood", GUILayout.Width(70))) Quick_GiveResource(ResourceType.Wood, giveAmt);
            if (GUILayout.Button("Food", GUILayout.Width(70))) Quick_GiveResource(ResourceType.Food, giveAmt);
            if (GUILayout.Button("Stone", GUILayout.Width(70))) Quick_GiveResource(ResourceType.Stone, giveAmt);
            if (GUILayout.Button("Iron", GUILayout.Width(70))) Quick_GiveResource(ResourceType.Iron, giveAmt);
            if (GUILayout.Button("Ammo", GUILayout.Width(70))) Quick_GiveResource(ResourceType.Ammo, giveAmt);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            DrawCurrentHoverTargetInfo();
            GUILayout.Label("Build / Repair (under mouse when applicable)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Complete hovered site", GUILayout.Width(160))) Quick_CompleteCurrentSiteUnderMouse();
            if (GUILayout.Button("Complete ALL sites", GUILayout.Width(150))) Quick_CompleteAllBuildSites();
            if (GUILayout.Button("Create Repair", GUILayout.Width(120))) Quick_CreateRepairOrderUnderMouse();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Dmg", GUILayout.Width(30));
            _quickDamageAmtStr = GUILayout.TextField(_quickDamageAmtStr, GUILayout.Width(60));
            if (GUILayout.Button("10", GUILayout.Width(40))) _quickDamageAmtStr = "10";
            if (GUILayout.Button("50", GUILayout.Width(40))) _quickDamageAmtStr = "50";
            if (GUILayout.Button("200", GUILayout.Width(50))) _quickDamageAmtStr = "200";
            GUILayout.EndHorizontal();

            int dmg = 50;
            if (!int.TryParse(_quickDamageAmtStr, out dmg) || dmg <= 0) dmg = 50;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Damage -{dmg}", GUILayout.Width(130))) Quick_DamageBuildingUnderMouse(dmg);
            if (GUILayout.Button("Set HP = 1", GUILayout.Width(110))) Quick_SetHpUnderMouse(1);
            if (GUILayout.Button("Heal Full", GUILayout.Width(110))) Quick_HealBuildingUnderMouse();
            if (GUILayout.Button("Finish Building", GUILayout.Width(120))) Quick_CompleteHoveredBuildingIfSiteOrRepairTarget();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Tower Ammo");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Drain", GUILayout.Width(35));
            _quickAmmoDrainAmtStr = GUILayout.TextField(_quickAmmoDrainAmtStr, GUILayout.Width(50));
            if (GUILayout.Button("10", GUILayout.Width(40))) _quickAmmoDrainAmtStr = "10";
            if (GUILayout.Button("30", GUILayout.Width(40))) _quickAmmoDrainAmtStr = "30";
            if (GUILayout.Button("90", GUILayout.Width(40))) _quickAmmoDrainAmtStr = "90";
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Drain hovered tower", GUILayout.Width(160))) Quick_DrainTowerUnderMouse();
            if (GUILayout.Button("Refill hovered tower", GUILayout.Width(160))) Quick_RefillTowerUnderMouse();
            if (GUILayout.Button("Drain ALL towers", GUILayout.Width(140))) Quick_DrainAllTowersToZero();
            if (GUILayout.Button("Refill ALL towers", GUILayout.Width(140))) Quick_RefillAllTowers();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Combat / Lane Spawn");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Enemy", GUILayout.Width(42));
            GUILayout.Label(_quickEnemyDefId, GUILayout.Width(100));
            GUILayout.Label("x", GUILayout.Width(12));
            var cntStr = GUILayout.TextField(_quickSpawnCount.ToString(), GUILayout.Width(40));
            if (int.TryParse(cntStr, out var c)) _quickSpawnCount = Mathf.Clamp(c, 1, 50);
            GUILayout.EndHorizontal();

            DrawEnemyPresetButtons();
            DrawQuickLaneButtons();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Kill ALL enemies", GUILayout.Width(150))) Quick_KillAllEnemies();
            if (GUILayout.Button("Force Resolve Wave", GUILayout.Width(170))) _gs.CombatService?.ForceResolveWave();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Save / Load Quick");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Run", GUILayout.Width(110))) Quick_SaveRun();
            if (GUILayout.Button("Load + Apply", GUILayout.Width(110))) Quick_LoadApply();
            if (GUILayout.Button("Quick Save+Load", GUILayout.Width(130))) Quick_SaveLoadRoundTrip();
            if (GUILayout.Button("Delete Save", GUILayout.Width(110))) Quick_DeleteSave();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Run SaveLoad Matrix", GUILayout.Width(180))) Quick_RunSaveLoadMatrix();
            if (GUILayout.Button("Internal CI SaveLoad", GUILayout.Width(180))) Quick_RunInternalSaveLoadCi();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("NPC Quick Spawn");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn Worker x1", GUILayout.Width(140))) Quick_SpawnNpc("Worker", 1);
            if (GUILayout.Button("Spawn Worker x5", GUILayout.Width(140))) Quick_SpawnNpc("Worker", 5);
            if (GUILayout.Button("Spawn NPC x10", GUILayout.Width(140))) Quick_SpawnNpc("Worker", 10);
            GUILayout.EndHorizontal();
        }

        private void DrawAdvancedTabs()
        {
            GUILayout.Label($"Tab: {_tab}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Home", GUILayout.Width(90))) _tab = DebugHubTab.Home;
            if (GUILayout.Button("Noti (F6)", GUILayout.Width(120))) _tab = DebugHubTab.Notifications;
            if (GUILayout.Button("Index (F7)", GUILayout.Width(120))) _tab = DebugHubTab.WorldIndex;
            if (GUILayout.Button("NPC", GUILayout.Width(90))) _tab = DebugHubTab.Npc;
            if (GUILayout.Button("Storage", GUILayout.Width(110))) _tab = DebugHubTab.Storage;
            _homeShowRewards = GUILayout.Toggle(_homeShowRewards, "Rewards", GUILayout.Width(90));
            _homeShowHints = GUILayout.Toggle(_homeShowHints, "Hints", GUILayout.Width(70));
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            switch (_tab)
            {
                case DebugHubTab.Home:
                    DrawHome();
                    break;

                case DebugHubTab.Notifications:
                    if (_notiHud != null) _notiHud.DrawHubGUI();
                    else GUILayout.Label("DebugNotificationsHUD not found in scene.");
                    break;

                case DebugHubTab.WorldIndex:
                    if (_worldIndexHud != null) _worldIndexHud.DrawHubGUI();
                    else GUILayout.Label("DebugWorldIndexHUD not found in scene.");
                    break;

                case DebugHubTab.Npc:
                    if (_npcTool != null) _npcTool.DrawHubGUI();
                    else GUILayout.Label("DebugNpcTool not found in scene.");
                    break;

                case DebugHubTab.Storage:
                    if (_storageHud != null) _storageHud.DrawHubGUI();
                    else GUILayout.Label("DebugStorageHUD not found in scene.");
                    break;
            }
        }


        private void Quick_GiveResource(ResourceType type, int amount)
        {
            if (_gs?.WorldState == null || _gs.StorageService == null || _gs.DataRegistry == null) return;
            if (amount <= 0) return;

            int remaining = amount;
            int addedTotal = 0;

            // Pass order deterministic:
            // - Non-ammo: HQ -> Warehouses -> any store that CanStore
            // - Ammo: Armory -> Forge -> any store that CanStore
            if (type == ResourceType.Ammo)
            {
                addedTotal += GiveToPreferredBuildings(type, ref remaining, preferArmory: true);
                addedTotal += GiveToPreferredBuildings(type, ref remaining, preferForge: true);
            }
            else
            {
                addedTotal += GiveToPreferredBuildings(type, ref remaining, preferHQ: true);
                addedTotal += GiveToPreferredBuildings(type, ref remaining, preferWarehouse: true);
            }

            // Fallback any
            if (remaining > 0)
            {
                foreach (var bid in _gs.WorldState.Buildings.Ids)
                {
                    if (remaining <= 0) break;
                    if (!_gs.WorldState.Buildings.Exists(bid)) continue;

                    var bs = _gs.WorldState.Buildings.Get(bid);
                    if (!bs.IsConstructed) continue;

                    int add = _gs.StorageService.Add(bid, type, remaining);
                    if (add <= 0) continue;

                    remaining -= add;
                    addedTotal += add;
                }
            }

            _gs.NotificationService?.Push("debug_give_res", "Debug",
                $"Give {type} {amount} -> added {addedTotal} (remain {remaining}).",
                NotificationSeverity.Info, default, 0.2f, true);
        }

        private int GiveToPreferredBuildings(ResourceType type, ref int remaining,
            bool preferHQ = false, bool preferWarehouse = false, bool preferArmory = false, bool preferForge = false)
        {
            if (remaining <= 0) return 0;

            int added = 0;
            foreach (var bid in _gs.WorldState.Buildings.Ids)
            {
                if (remaining <= 0) break;
                if (!_gs.WorldState.Buildings.Exists(bid)) continue;

                var bs = _gs.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                if (!_gs.DataRegistry.TryGetBuilding(bs.DefId, out var def) || def == null)
                    continue;

                if (preferHQ && !def.IsHQ) continue;
                if (preferWarehouse && !def.IsWarehouse) continue;
                if (preferArmory && !def.IsArmory) continue;
                if (preferForge && !def.IsForge) continue;

                int a = _gs.StorageService.Add(bid, type, remaining);
                if (a <= 0) continue;

                remaining -= a;
                added += a;
            }
            return added;
        }




        private void EnsureHp(ref BuildingState bs)
        {
            if (bs.MaxHP <= 0)
            {
                int mhp = 100;
                try { mhp = Mathf.Max(1, _gs.DataRegistry.GetBuilding(bs.DefId).MaxHp); }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DebugHUDHub] Failed to resolve building max HP for '{bs.DefId}': {ex}");
                }
                bs.MaxHP = mhp;
                if (bs.HP <= 0) bs.HP = bs.MaxHP;
            }

            if (bs.HP > bs.MaxHP) bs.HP = bs.MaxHP;
            if (bs.HP < 0) bs.HP = 0;
        }


        private void Quick_GiveCoreResources()
        {
            int giveAmt = 200;
            if (!int.TryParse(_quickGiveAmtStr, out giveAmt) || giveAmt <= 0) giveAmt = 200;
            Quick_GiveResource(ResourceType.Wood, giveAmt);
            Quick_GiveResource(ResourceType.Food, giveAmt);
            Quick_GiveResource(ResourceType.Stone, giveAmt);
            Quick_GiveResource(ResourceType.Iron, giveAmt);
        }

        private void Quick_AdvanceDay(int delta)
        {
            if (!(_gs?.RunClock is RunClockService clock))
                return;

            int target = Mathf.Max(1, clock.DayIndex + delta);
            clock.ForceSeasonDay(clock.CurrentSeason, target);
            _gs.NotificationService?.Push("dbg_adv_day", "Debug", $"Jumped to {clock.CurrentSeason} D{target}.", NotificationSeverity.Info, default, 0.15f, true);
        }

        private void Quick_AdvanceSeason()
        {
            if (!(_gs?.RunClock is RunClockService clock))
                return;

            Season next = clock.CurrentSeason switch
            {
                Season.Spring => Season.Summer,
                Season.Summer => Season.Autumn,
                Season.Autumn => Season.Winter,
                _ => Season.Spring
            };

            clock.ForceSeasonDay(next, 1);
            _gs.NotificationService?.Push("dbg_adv_season", "Debug", $"Jumped to {next} D1.", NotificationSeverity.Info, default, 0.15f, true);
        }

        private void Quick_JumpWinterD4NearEnd()
        {
            if (!(_gs?.RunClock is RunClockService clock))
                return;

            float nearEnd = Mathf.Max(0f, clock.DayLengthSeconds - 0.2f);
            clock.LoadSnapshot(
                yearIndex: Mathf.Max(1, clock.YearIndex),
                seasonText: Season.Winter.ToString(),
                dayIndex: 4,
                dayTimerSeconds: nearEnd,
                timeScale: Mathf.Max(1f, clock.TimeScale));

            _gs.NotificationService?.Push(
                "dbg_jump_winter_d4_near_end",
                "Debug",
                $"Jumped to Y{clock.YearIndex} Winter D4 near end. Let clock tick to rollover naturally.",
                NotificationSeverity.Info,
                default,
                0.15f,
                true);
        }

        private void Quick_SpawnNpc(string defId, int count)
        {
            if (_npcTool == null)
                _npcTool = FindObjectOfType<DebugNpcTool>(true);

            if (_npcTool == null)
            {
                _gs?.NotificationService?.Push("dbg_spawn_npc_missing", "Debug", "DebugNpcTool not found in scene.", NotificationSeverity.Warning, default, 0.3f, true);
                return;
            }

            _npcTool.DebugSpawn(string.IsNullOrWhiteSpace(defId) ? "Worker" : defId, Mathf.Max(1, count));
        }


        private void Quick_UnlockAll()
        {
            if (_gs?.UnlockService == null)
                return;

            try
            {
                var t = _gs.UnlockService.GetType();
                var unlockedField = t.GetField("_unlocked", BindingFlags.Instance | BindingFlags.NonPublic);
                var scheduleField = t.GetField("_schedule", BindingFlags.Instance | BindingFlags.NonPublic);
                if (unlockedField == null || scheduleField == null)
                    throw new InvalidOperationException("UnlockService internals not found.");

                var unlocked = unlockedField.GetValue(_gs.UnlockService) as System.Collections.IEnumerable;
                var unlockedSet = unlockedField.GetValue(_gs.UnlockService);
                var schedule = scheduleField.GetValue(_gs.UnlockService);
                if (unlockedSet == null || schedule == null)
                    throw new InvalidOperationException("UnlockService state missing.");

                var addMethod = unlockedSet.GetType().GetMethod("Add");
                int added = 0;

                var startUnlockedField = schedule.GetType().GetField("StartUnlocked");
                if (startUnlockedField?.GetValue(schedule) is System.Collections.IEnumerable startList)
                {
                    foreach (var id in startList)
                        if (id is string s && !string.IsNullOrWhiteSpace(s)) { addMethod?.Invoke(unlockedSet, new object[] { s }); added++; }
                }

                var entriesField = schedule.GetType().GetField("Entries");
                if (entriesField?.GetValue(schedule) is System.Collections.IEnumerable entries)
                {
                    foreach (var e in entries)
                    {
                        if (e == null) continue;
                        var defField = e.GetType().GetField("DefId");
                        var id = defField?.GetValue(e) as string;
                        if (!string.IsNullOrWhiteSpace(id)) { addMethod?.Invoke(unlockedSet, new object[] { id }); added++; }
                    }
                }

                var bus = _gs.EventBus;
                var evtType = Type.GetType("SeasonalBastion.Contracts.UnlocksChangedEvent, Assembly-CSharp") ?? Type.GetType("SeasonalBastion.UnlocksChangedEvent, Assembly-CSharp");
                if (bus != null && evtType != null)
                {
                    var ctor = evtType.GetConstructor(new[] { typeof(int) });
                    if (ctor != null)
                    {
                        var publish = bus.GetType().GetMethod("Publish")?.MakeGenericMethod(evtType);
                        publish?.Invoke(bus, new[] { ctor.Invoke(new object[] { Environment.TickCount }) });
                    }
                }

                _gs.NotificationService?.Push("dbg_unlock_all", "Debug", "Unlocked all scheduled defs.", NotificationSeverity.Info, default, 0.2f, true);
            }
            catch (Exception e)
            {
                _gs.NotificationService?.Push("dbg_unlock_all_fail", "Debug", "Unlock ALL failed: " + e.Message, NotificationSeverity.Warning, default, 0.5f, true);
            }
        }



    }
}


