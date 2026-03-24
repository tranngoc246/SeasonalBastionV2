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
    public sealed class DebugHUDHub : MonoBehaviour
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
        [SerializeField] private int _quickLaneId = 0;
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

        private void CacheHoverTarget()
        {
            if (_gs?.WorldState?.Buildings == null || _gs.GridMap == null) return;
            if (!SeasonalBastion.DebugTools.MouseCellSharedState.HasValue) return;

            var cell = SeasonalBastion.DebugTools.MouseCellSharedState.Cell;
            var occ = _gs.GridMap.Get(cell);

            // Building
            if (occ.Kind == CellOccupancyKind.Building && occ.Building.Value != 0 && _gs.WorldState.Buildings.Exists(occ.Building))
            {
                _cachedHoverBuilding = occ.Building;
                _cachedHoverTime = Time.unscaledTime;
                return;
            }

            // Site -> TargetBuilding
            if (occ.Kind == CellOccupancyKind.Site && occ.Site.Value != 0 && _gs.WorldState.Sites != null && _gs.WorldState.Sites.Exists(occ.Site))
            {
                var st = _gs.WorldState.Sites.Get(occ.Site);
                if (st.TargetBuilding.Value != 0 && _gs.WorldState.Buildings.Exists(st.TargetBuilding))
                {
                    _cachedHoverBuilding = st.TargetBuilding;
                    _cachedHoverTime = Time.unscaledTime;
                }
            }
        }

        private bool TryGetCachedBuilding(out BuildingId bid, out BuildingState bs)
        {
            bid = default;
            bs = default;

            if (_gs?.WorldState?.Buildings == null) return false;
            if (_cachedHoverBuilding.Value == 0) return false;

            if (Time.unscaledTime - _cachedHoverTime > HoverCacheTTL) return false;
            if (!_gs.WorldState.Buildings.Exists(_cachedHoverBuilding)) return false;

            bid = _cachedHoverBuilding;
            bs = _gs.WorldState.Buildings.Get(bid);
            return true;
        }

        private void TryLockTargetFromClick()
        {
            if (_gs?.WorldState?.Buildings == null) return;
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            if (!MouseCellSharedState.HasValue) return;
            var cell = MouseCellSharedState.Cell;
            var occ = _gs.GridMap.Get(cell);

            if (occ.Kind == CellOccupancyKind.Building && occ.Building.Value != 0 && _gs.WorldState.Buildings.Exists(occ.Building))
            {
                _lockedTargetBuilding = occ.Building;
                _cachedHoverBuilding = occ.Building;
                _cachedHoverTime = Time.unscaledTime;
                return;
            }

            if (occ.Kind == CellOccupancyKind.Site && occ.Site.Value != 0 && _gs.WorldState.Sites != null && _gs.WorldState.Sites.Exists(occ.Site))
            {
                var st = _gs.WorldState.Sites.Get(occ.Site);
                if (st.TargetBuilding.Value != 0 && _gs.WorldState.Buildings.Exists(st.TargetBuilding))
                {
                    _lockedTargetBuilding = st.TargetBuilding;
                    _cachedHoverBuilding = st.TargetBuilding;
                    _cachedHoverTime = Time.unscaledTime;
                }
            }
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

        private void DrawCurrentHoverTargetInfo()
        {
            if (_gs == null)
                return;

            string buildingLabel = "Building: (none)";
            string towerLabel = "Tower: (none)";
            string lockLabel = _lockedTargetBuilding.Value != 0 ? $"Lock: #{_lockedTargetBuilding.Value}" : "Lock: hover only";

            if (TryFindBuildingFromHover(out var bid, out var bs))
            {
                EnsureHp(ref bs);
                buildingLabel = $"Building: {bs.DefId} #{bid.Value}  HP {bs.HP}/{bs.MaxHP}";

                if (TryResolveTowerForBuilding(bid, bs, out var tid, out var ts))
                    towerLabel = $"Tower: #{tid.Value}  Ammo {ts.Ammo}/{ts.AmmoCap}  HP {ts.Hp}/{ts.HpMax}";
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("CURRENT TARGET");
            GUILayout.Label(buildingLabel);
            GUILayout.Label(towerLabel);
            GUILayout.BeginHorizontal();
            GUILayout.Label(lockLabel, GUILayout.Width(180));
            if (GUILayout.Button("Clear Lock", GUILayout.Width(100))) _lockedTargetBuilding = default;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void Quick_CompleteAllBuildSites()
        {
            if (_gs?.WorldState == null) return;
            var sites = _gs.WorldState.Sites;
            if (sites == null) return;

            _siteIdsTmp.Clear();
            foreach (var sid in sites.Ids) _siteIdsTmp.Add(sid);

            int changed = 0;
            for (int i = 0; i < _siteIdsTmp.Count; i++)
            {
                var sid = _siteIdsTmp[i];
                if (!sites.Exists(sid)) continue;
                var st = sites.Get(sid);
                if (!st.IsActive) continue;

                st.RemainingCosts?.Clear();
                st.RemainingCosts = null;
                if (st.WorkSecondsTotal <= 0f) st.WorkSecondsTotal = 0.1f;
                st.WorkSecondsDone = st.WorkSecondsTotal;

                sites.Set(sid, st);
                changed++;
            }

            if (_gs.BuildOrderService is BuildOrderService bos)
                bos.Tick(0.0001f);

            _gs.NotificationService?.Push("debug_build_instant", "Debug", $"Instant completed {changed} build sites.", NotificationSeverity.Info, default, 0.2f, true);
        }

        private void Quick_CompleteCurrentSiteUnderMouse()
        {
            if (_gs?.WorldState == null || _gs.GridMap == null) return;
            if (!SeasonalBastion.DebugTools.MouseCellSharedState.HasValue)
            {
                _gs.NotificationService?.Push("debug_site_none", "Debug", "No hover cell (MouseCellSharedState).", NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            if (!TryFindActiveSiteFromHover(out var sid, out var st))
            {
                _gs.NotificationService?.Push("debug_site_notfound", "Debug", "No active build/upgrade site found under mouse.", NotificationSeverity.Info, default, 0.2f, true);
                return;
            }

            ForceCompleteSite(sid, st);
        }

        private bool TryFindActiveSiteFromHover(out SiteId sid, out BuildSiteState site)
        {
            sid = default;
            site = default;

            var cell = SeasonalBastion.DebugTools.MouseCellSharedState.Cell;
            var occ = _gs.GridMap.Get(cell);

            // Case 1: PlaceNew site occupies grid => GridMap stores SiteId
            if (occ.Kind == CellOccupancyKind.Site && occ.Site.Value != 0 && _gs.WorldState.Sites.Exists(occ.Site))
            {
                var s = _gs.WorldState.Sites.Get(occ.Site);
                if (s.IsActive)
                {
                    sid = occ.Site;
                    site = s;
                    return true;
                }
            }

            // Case 2: Upgrade site does NOT occupy grid => find by TargetBuilding
            if (occ.Kind == CellOccupancyKind.Building && occ.Building.Value != 0)
            {
                foreach (var x in _gs.WorldState.Sites.Ids)
                {
                    if (!_gs.WorldState.Sites.Exists(x)) continue;
                    var s = _gs.WorldState.Sites.Get(x);
                    if (!s.IsActive) continue;
                    if (s.TargetBuilding.Value == occ.Building.Value)
                    {
                        sid = x;
                        site = s;
                        return true;
                    }
                }
            }

            return false;
        }

        private void ForceCompleteSite(SiteId sid, BuildSiteState st)
        {
            var sites = _gs.WorldState.Sites;
            if (!sites.Exists(sid)) return;

            st.RemainingCosts?.Clear();
            st.RemainingCosts = null;

            if (st.WorkSecondsTotal <= 0f) st.WorkSecondsTotal = 0.1f;
            st.WorkSecondsDone = st.WorkSecondsTotal;

            sites.Set(sid, st);

            // Tick nh? d? BuildOrderService detect completion ngay
            if (_gs.BuildOrderService is BuildOrderService bos)
                bos.Tick(0.0001f);

            _gs.NotificationService?.Push("debug_site_complete_one", "Debug",
                $"Completed site {sid.Value} (kind={(st.IsUpgrade ? "Upgrade" : "PlaceNew")}).",
                NotificationSeverity.Info, default, 0.2f, true);
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

        private void Quick_DrainAllTowersToZero()
        {
            if (_gs?.WorldState == null) return;
            var towers = _gs.WorldState.Towers;
            if (towers == null) return;

            _towerIdsTmp.Clear();
            foreach (var tid in towers.Ids) _towerIdsTmp.Add(tid);

            int changed = 0;
            for (int i = 0; i < _towerIdsTmp.Count; i++)
            {
                var tid = _towerIdsTmp[i];
                if (!towers.Exists(tid)) continue;
                var ts = towers.Get(tid);
                ts.Ammo = 0;
                towers.Set(tid, ts);
                _gs.AmmoService?.NotifyTowerAmmoChanged(ts.Id, ts.Ammo, ts.AmmoCap);
                changed++;
            }

            _gs.NotificationService?.Push("debug_ammo_drain", "Debug", $"Drained {changed} towers to 0.", NotificationSeverity.Info, default, 0.2f, true);
        }

        private void Quick_RefillAllTowers()
        {
            if (_gs?.WorldState == null) return;
            var towers = _gs.WorldState.Towers;
            if (towers == null) return;

            _towerIdsTmp.Clear();
            foreach (var tid in towers.Ids) _towerIdsTmp.Add(tid);

            int changed = 0;
            for (int i = 0; i < _towerIdsTmp.Count; i++)
            {
                var tid = _towerIdsTmp[i];
                if (!towers.Exists(tid)) continue;
                var ts = towers.Get(tid);
                ts.Ammo = ts.AmmoCap;
                towers.Set(tid, ts);
                _gs.AmmoService?.NotifyTowerAmmoChanged(ts.Id, ts.Ammo, ts.AmmoCap);
                changed++;
            }

            _gs.NotificationService?.Push("debug_ammo_refill", "Debug", $"Refilled {changed} towers.", NotificationSeverity.Info, default, 0.2f, true);
        }

        private void Quick_SpawnEnemy(string enemyDefId, int laneId, int count)
        {
            if (_gs?.WorldState == null || _gs.DataRegistry == null || _gs.RunStartRuntime == null) return;
            if (_gs.RunStartRuntime.Lanes == null || !_gs.RunStartRuntime.Lanes.TryGetValue(laneId, out var lane))
            {
                Debug.LogWarning($"[DebugHUDHub] Lane {laneId} not found.");
                return;
            }

            EnemyDef def;
            try { def = _gs.DataRegistry.GetEnemy(enemyDefId); }
            catch
            {
                Debug.LogWarning($"[DebugHUDHub] EnemyDef not found: '{enemyDefId}'");
                return;
            }

            // Debug convenience: auto-enable combat so spawned enemies start marching immediately,
            // even if the run is currently outside normal Defend flow.
            if (_gs.CombatService is CombatService combat && !combat.IsActive)
                combat.OnDefendPhaseStarted();

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                var st = new EnemyState
                {
                    DefId = enemyDefId,
                    Cell = lane.StartCell,
                    Hp = def.MaxHp,
                    Lane = laneId,
                    MoveProgress01 = 0f
                };

                var id = _gs.WorldState.Enemies.Create(st);
                st.Id = id;
                _gs.WorldState.Enemies.Set(id, st);
                spawned++;
            }

            string phaseNote = _gs.RunClock != null ? $" phase={_gs.RunClock.CurrentPhase}" : string.Empty;
            _gs.NotificationService?.Push("debug_spawn_enemy", "Debug", $"Spawned {spawned} '{enemyDefId}' lane {laneId}; combat auto-enabled.{phaseNote}", NotificationSeverity.Info, default, 0.2f, true);
        }

        private void Quick_KillAllEnemies()
        {
            if (_gs?.WorldState == null) return;
            var enemies = _gs.WorldState.Enemies;
            if (enemies == null) return;

            _enemyIdsTmp.Clear();
            foreach (var eid in enemies.Ids) _enemyIdsTmp.Add(eid);

            int killed = 0;
            for (int i = _enemyIdsTmp.Count - 1; i >= 0; i--)
            {
                var eid = _enemyIdsTmp[i];
                if (!enemies.Exists(eid)) continue;
                enemies.Destroy(eid);
                killed++;
            }

            _gs.NotificationService?.Push("debug_kill_enemies", "Debug", $"Killed {killed} enemies.", NotificationSeverity.Info, default, 0.2f, true);
        }

        private void ValidateDataNow()
        {
            _dataErrors.Clear();

            if (_gs == null)
            {
                _dataLastOk = false;
                _dataLastSummary = "GameServices is null";
                _dataErrors.Add(_dataLastSummary);
                return;
            }

            var validator = _gs.DataValidator;
            var registry = _gs.DataRegistry;
            if (validator == null || registry == null)
            {
                _dataLastOk = false;
                _dataLastSummary = "Missing DataValidator/DataRegistry";
                _dataErrors.Add(_dataLastSummary);
                return;
            }

            _dataLastOk = validator.ValidateAll(registry, _dataErrors);
            _dataLastSummary = _dataLastOk ? "OK" : $"FAIL ({_dataErrors.Count} errors)";

            if (!_dataLastOk)
            {
                try
                {
                    _gs.NotificationService?.Push("data_invalid", "Data INVALID", _dataLastSummary, NotificationSeverity.Error, default, cooldownSeconds: 0f, dedupeByKey: true);
                }
                catch { }
            }
        }

        private void DrawHome()
        {
            // ===== Top summary (no scroll) =====
            GUILayout.Label("Active Modules:");
            GUILayout.Label($"BuildTool: {(_buildTool != null ? "OK" : "missing")}  | RoadTool: {(_roadTool != null ? "OK" : "missing")}  | NpcTool: {(_npcTool != null ? "OK" : "missing")}");
            GUILayout.Label($"NotiHUD: {(_notiHud != null ? "OK" : "missing")}  | WorldIndexHUD: {(_worldIndexHud != null ? "OK" : "missing")}  | StorageHUD: {(_storageHud != null ? "OK" : "missing")}");

            GUILayout.Space(6);
            GUILayout.Label("Notes:");
            GUILayout.Label("- All old standalone toggle keys are disabled (B/R/N/H/I/S).");
            GUILayout.Label("- Tools only respond to inputs when their mode is active.");

            GUILayout.Space(8);
            if (_gs != null && _gs.JobBoard is JobBoard jb)
                GUILayout.Label($"HaulBasic jobs active: {jb.CountActiveJobs(JobArchetype.HaulBasic)}");

            if (_mode == DebugHubMode.Build && _gs != null && _buildTool != null && _gs.UnlockService != null)
            {
                GUILayout.Space(8);
                GUILayout.Label("Build Slots (Unlocked only)");

                _buildTool.GetBuildSlotDefs(_buildSlotsTmp);

                bool any = false;
                for (int i = 0; i < _buildSlotsTmp.Count; i++)
                {
                    var defId = _buildSlotsTmp[i];
                    if (string.IsNullOrEmpty(defId)) continue;

                    if (_gs.UnlockService.IsUnlocked(defId))
                    {
                        any = true;
                        GUILayout.Label($"{i + 1}: {defId}");
                    }
                }

                if (!any)
                    GUILayout.Label("(none unlocked in current time)");
            }

            GUILayout.Space(8);

            // ===== Section toggles (compact) =====
            GUILayout.BeginHorizontal();
            _homeShowData = GUILayout.Toggle(_homeShowData, "Data", GUILayout.Width(60));
            _homeShowRunClock = GUILayout.Toggle(_homeShowRunClock, "Clock", GUILayout.Width(70));
            _homeShowLanes = GUILayout.Toggle(_homeShowLanes, "Lanes", GUILayout.Width(70));
            _homeShowSaveLoad = GUILayout.Toggle(_homeShowSaveLoad, "Save", GUILayout.Width(60));
            _homeShowWave = GUILayout.Toggle(_homeShowWave, "Wave", GUILayout.Width(70));
            GUILayout.EndHorizontal();

            // ===== Scrollable content =====
            _homeScroll = GUILayout.BeginScrollView(_homeScroll, GUILayout.ExpandHeight(true));

            // ---- Day17: Data Validator ----
            if (_homeShowData)
            {
                GUILayout.Space(10);
                GUILayout.Label("Day17: Data Validator");

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Validate Data", GUILayout.Width(140)))
                    ValidateDataNow();
                GUILayout.Label($"Result: {_dataLastSummary}");
                GUILayout.EndHorizontal();

                if (!_dataLastOk)
                {
                    GUILayout.Label("Errors:");
                    _dataScroll = GUILayout.BeginScrollView(_dataScroll, GUILayout.Height(240));
                    int show = Mathf.Min(_dataErrors.Count, 50);
                    for (int i = 0; i < show; i++)
                        GUILayout.Label("- " + _dataErrors[i]);
                    if (_dataErrors.Count > show)
                        GUILayout.Label($"...({_dataErrors.Count - show} more)");
                    GUILayout.EndScrollView();
                }

                GUILayout.Space(6);
            }

            // ---- EndSeasonRewardRequested ----
            if (_homeShowRewards)
            {
                GUILayout.Space(10);
                GUILayout.Label("EndSeasonRewardRequested (Reward placeholder)");

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Bound: {_rewardListenerBound}   Count: {_endSeasonRewardReqCount}");
                if (GUILayout.Button("Clear", GUILayout.Width(80)))
                {
                    _endSeasonRewardReqCount = 0;
                    _hasLastEndSeasonRewardReq = false;
                    _lastEndSeasonRewardRealtime = 0f;
                }
                GUILayout.EndHorizontal();

                if (_hasLastEndSeasonRewardReq)
                {
                    float dt = Time.realtimeSinceStartup - _lastEndSeasonRewardRealtime;
                    GUILayout.Label($"Last: Season={_lastEndSeasonRewardReq.Season}  Year={_lastEndSeasonRewardReq.YearIndex}  Day={_lastEndSeasonRewardReq.DayIndex}   ({dt:0.00}s ago)");
                }
                else
                {
                    GUILayout.Label("Last: (none yet)");
                }

                GUILayout.Space(6);
            }

            // ---- Day41: Tutorial Hints ----
            if (_homeShowHints)
            {
                GUILayout.Space(10);
                GUILayout.Label("Day41: Tutorial Hints");

                if (_gs == null || _gs.TutorialHints == null)
                {
                    GUILayout.Label("TutorialHintsService: missing");
                }
                else
                {
                    var h = _gs.TutorialHints;
                    GUILayout.Label($"ActiveWindow: 10min | RunAge(sim): {h.RunAge:0.0}s");
                    GUILayout.Label($"Counts: UnassignedNPC={h.HintNpcUnassignedCount} | ProducerFull={h.HintProducerFullCount} | OutOfAmmo={h.HintOutOfAmmoCount} | WaveIncoming={h.HintWaveIncomingCount}");
                    GUILayout.Label($"LastHintRealtime: {h.LastHintRealtime:0.00}s (Time.realtimeSinceStartup)");
                }

                GUILayout.Space(6);
            }

            // ---- RunClock HUD ----
            if (_homeShowRunClock)
            {
                GUILayout.Space(10);
                if (_runClockHud != null)
                    _runClockHud.DrawHubGUI();
                else
                    GUILayout.Label("DebugRunClockHUD: missing (add component to scene if you want clock controls)");

                GUILayout.Space(6);
            }

            // ---- Lane HUD ----
            if (_homeShowLanes)
            {
                GUILayout.Space(10);
                if (_combatLaneHud != null)
                    _combatLaneHud.DrawHubGUI();
                else
                    GUILayout.Label("DebugCombatLaneHUD: missing (add component to scene if you want lane spawn debug)");

                GUILayout.Space(6);
            }

            // ---- Save/Load HUD ----
            if (_homeShowSaveLoad)
            {
                GUILayout.Space(10);
                if (_gs != null) _saveLoadHUD.Draw(_gs);
                else GUILayout.Label("SaveLoadHUD: GameServices is null");

                GUILayout.Space(6);
            }

            // ---- Day34: Wave HUD ----
            if (_homeShowWave)
            {
                GUILayout.Space(10);
                if (_waveHud != null)
                    _waveHud.DrawHubGUI();
                else
                    GUILayout.Label("DebugWaveHUD: missing (add component to scene if you want wave counters)");

                GUILayout.Space(6);
            }

            GUILayout.EndScrollView();
        }

        private bool TryFindBuildingFromHover(out BuildingId bid, out BuildingState bs)
        {
            if (_lockedTargetBuilding.Value != 0 && _gs?.WorldState?.Buildings != null && _gs.WorldState.Buildings.Exists(_lockedTargetBuilding))
            {
                bid = _lockedTargetBuilding;
                bs = _gs.WorldState.Buildings.Get(bid);
                return true;
            }

            // NEW: uu ti?n cached hover (v? b?m n?t th? chu?t da r?i world)
            if (TryGetCachedBuilding(out bid, out bs))
                return true;
            bid = default;
            bs = default;

            if (_gs?.WorldState?.Buildings == null || _gs.GridMap == null) return false;
            if (!MouseCellSharedState.HasValue) return false;

            var cell = MouseCellSharedState.Cell;
            var occ = _gs.GridMap.Get(cell);

            // Case 1: hover on building cell
            if (occ.Kind == CellOccupancyKind.Building && occ.Building.Value != 0 && _gs.WorldState.Buildings.Exists(occ.Building))
            {
                bid = occ.Building;
                bs = _gs.WorldState.Buildings.Get(bid);
                return true;
            }

            // Case 2: hover on site cell -> use TargetBuilding
            if (occ.Kind == CellOccupancyKind.Site && occ.Site.Value != 0 && _gs.WorldState.Sites != null && _gs.WorldState.Sites.Exists(occ.Site))
            {
                var st = _gs.WorldState.Sites.Get(occ.Site);
                if (st.TargetBuilding.Value != 0 && _gs.WorldState.Buildings.Exists(st.TargetBuilding))
                {
                    bid = st.TargetBuilding;
                    bs = _gs.WorldState.Buildings.Get(bid);
                    return true;
                }
            }

            return false;
        }

        private void EnsureHp(ref BuildingState bs)
        {
            if (bs.MaxHP <= 0)
            {
                int mhp = 100;
                try { mhp = Mathf.Max(1, _gs.DataRegistry.GetBuilding(bs.DefId).MaxHp); } catch { }
                bs.MaxHP = mhp;
                if (bs.HP <= 0) bs.HP = bs.MaxHP;
            }

            if (bs.HP > bs.MaxHP) bs.HP = bs.MaxHP;
            if (bs.HP < 0) bs.HP = 0;
        }

        private void Quick_DamageBuildingUnderMouse(int damage)
        {
            if (_gs == null) return;

            if (!TryFindBuildingFromHover(out var bid, out var bs))
            {
                _gs.NotificationService?.Push("dbg_damage_none", "Debug", "No building under mouse (need MouseCellSharedState).",
                    NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            EnsureHp(ref bs);

            bs.HP -= Mathf.Max(1, damage);
            if (bs.HP < 0) bs.HP = 0;

            _gs.WorldState.Buildings.Set(bid, bs);

            _gs.NotificationService?.Push("dbg_damage_ok", "Debug",
                $"Damaged {bs.DefId} #{bid.Value}: {bs.HP}/{bs.MaxHP}",
                NotificationSeverity.Warning, default, 0.1f, true);
        }

        private void Quick_SetHpUnderMouse(int hp)
        {
            if (_gs == null) return;

            if (!TryFindBuildingFromHover(out var bid, out var bs))
            {
                _gs.NotificationService?.Push("dbg_sethp_none", "Debug", "No building under mouse.",
                    NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            EnsureHp(ref bs);

            bs.HP = Mathf.Clamp(hp, 0, bs.MaxHP);
            _gs.WorldState.Buildings.Set(bid, bs);

            _gs.NotificationService?.Push("dbg_sethp_ok", "Debug",
                $"Set HP {bs.DefId} #{bid.Value}: {bs.HP}/{bs.MaxHP}",
                NotificationSeverity.Info, default, 0.1f, true);
        }

        private void Quick_HealBuildingUnderMouse()
        {
            if (_gs == null) return;

            if (!TryFindBuildingFromHover(out var bid, out var bs))
            {
                _gs.NotificationService?.Push("dbg_heal_none", "Debug", "No building under mouse.",
                    NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            EnsureHp(ref bs);

            bs.HP = bs.MaxHP;
            _gs.WorldState.Buildings.Set(bid, bs);

            _gs.NotificationService?.Push("dbg_heal_ok", "Debug",
                $"Healed {bs.DefId} #{bid.Value}: {bs.HP}/{bs.MaxHP}",
                NotificationSeverity.Info, default, 0.1f, true);
        }

        private void Quick_CreateRepairOrderUnderMouse()
        {
            if (_gs == null) return;

            if (!TryFindBuildingFromHover(out var bid, out var bs))
            {
                _gs.NotificationService?.Push("dbg_repair_none", "Debug", "No building under mouse.",
                    NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            int id = _gs.BuildOrderService.CreateRepairOrder(bid);
            if (id > 0)
                _gs.NotificationService?.Push("dbg_repair_ok", "Debug", $"Repair order #{id} created for #{bid.Value}",
                    NotificationSeverity.Info, default, 0.1f, true);
            else
                _gs.NotificationService?.Push("dbg_repair_fail", "Debug", "Repair order not created (full HP / invalid / duplicate).",
                    NotificationSeverity.Warning, default, 0.25f, true);
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

        private void Quick_SaveRun()
        {
            if (_gs?.SaveService == null)
            {
                _gs?.NotificationService?.Push("dbg_save_missing", "Debug", "SaveService missing.", NotificationSeverity.Warning, default, 0.25f, true);
                return;
            }

            var r = _gs.SaveService.SaveRun(_gs.WorldState, _gs.RunClock);
            var sev = r.Code == SaveResultCode.Ok ? NotificationSeverity.Info : NotificationSeverity.Warning;
            _gs.NotificationService?.Push("dbg_save_run", "Save/Load", $"Save: {r.Code} | {r.Message}", sev, default, 0.25f, false);
        }

        private void Quick_LoadApply()
        {
            if (_gs?.SaveService == null)
            {
                _gs?.NotificationService?.Push("dbg_load_missing", "Debug", "SaveService missing.", NotificationSeverity.Warning, default, 0.25f, true);
                return;
            }

            var r = _gs.SaveService.LoadRun(out var dto);
            if (r.Code != SaveResultCode.Ok || dto == null)
            {
                _gs.NotificationService?.Push("dbg_load_run", "Save/Load", $"Load: {r.Code} | {r.Message}", NotificationSeverity.Warning, default, 0.3f, false);
                return;
            }

            if (SaveLoadApplier.TryApply(_gs, dto, out var err))
                _gs.NotificationService?.Push("dbg_load_apply_ok", "Save/Load", "Load+Apply: OK", NotificationSeverity.Info, default, 0.25f, true);
            else
                _gs.NotificationService?.Push("dbg_load_apply_fail", "Save/Load", "Load+Apply FAIL: " + err, NotificationSeverity.Error, default, 0.5f, false);
        }

        private void Quick_SaveLoadRoundTrip()
        {
            if (_gs?.SaveService == null)
            {
                _gs?.NotificationService?.Push("dbg_saveload_missing", "Debug", "SaveService missing.", NotificationSeverity.Warning, default, 0.25f, true);
                return;
            }

            var sr = _gs.SaveService.SaveRun(_gs.WorldState, _gs.RunClock);
            if (sr.Code != SaveResultCode.Ok)
            {
                _gs.NotificationService?.Push("dbg_saveload_save_fail", "Save/Load", $"Save failed: {sr.Code} | {sr.Message}", NotificationSeverity.Warning, default, 0.3f, false);
                return;
            }

            var lr = _gs.SaveService.LoadRun(out var dto);
            if (lr.Code != SaveResultCode.Ok || dto == null)
            {
                _gs.NotificationService?.Push("dbg_saveload_load_fail", "Save/Load", $"Load failed: {lr.Code} | {lr.Message}", NotificationSeverity.Warning, default, 0.3f, false);
                return;
            }

            if (SaveLoadApplier.TryApply(_gs, dto, out var err))
                _gs.NotificationService?.Push("dbg_saveload_ok", "Save/Load", "Quick Save+Load: OK", NotificationSeverity.Info, default, 0.25f, true);
            else
                _gs.NotificationService?.Push("dbg_saveload_apply_fail", "Save/Load", "Quick Save+Load FAIL: " + err, NotificationSeverity.Error, default, 0.5f, false);
        }

        private void Quick_DeleteSave()
        {
            if (_gs?.SaveService == null)
            {
                _gs?.NotificationService?.Push("dbg_delete_save_missing", "Debug", "SaveService missing.", NotificationSeverity.Warning, default, 0.25f, true);
                return;
            }

            _gs.SaveService.DeleteRunSave();
            _gs.NotificationService?.Push("dbg_delete_save_ok", "Save/Load", "Deleted run save.", NotificationSeverity.Info, default, 0.2f, true);
        }

        private void Quick_RunSaveLoadMatrix()
        {
            if (_gs == null) return;
            bool ok = QaSaveLoadScenario8.Run(_gs, out var summary);
            _gs.NotificationService?.Push("dbg_saveload_matrix", "Save/Load", summary, ok ? NotificationSeverity.Info : NotificationSeverity.Warning, default, 0.5f, false);
        }

        private void Quick_RunInternalSaveLoadCi()
        {
            if (_gs == null) return;
            var rep = QaInternalCiRunner.RunB(_gs, writeReport: true);
            string body = (rep.passed ? "PASS: " : "FAIL: ") + rep.summary;
            if (!string.IsNullOrEmpty(rep.reportPath)) body += " | Report: " + rep.reportPath;
            _gs.NotificationService?.Push("dbg_saveload_ci", "Save/Load", body, rep.passed ? NotificationSeverity.Info : NotificationSeverity.Warning, default, 0.5f, false);
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

        private void DrawEnemyPresetButtons()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Swarmling", GUILayout.Width(100))) _quickEnemyDefId = "Swarmling";
            if (GUILayout.Button("Raider", GUILayout.Width(100))) _quickEnemyDefId = "Raider";
            if (GUILayout.Button("Bruiser", GUILayout.Width(100))) _quickEnemyDefId = "Bruiser";
            if (GUILayout.Button("Archer", GUILayout.Width(100))) _quickEnemyDefId = "Archer";
            if (GUILayout.Button("Sapper", GUILayout.Width(100))) _quickEnemyDefId = "Sapper";
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SiegeBrute", GUILayout.Width(100))) _quickEnemyDefId = "SiegeBrute";
            GUILayout.Label("Selected: " + _quickEnemyDefId);
            GUILayout.EndHorizontal();
        }

        private void DrawQuickLaneButtons()
        {
            GUILayout.BeginHorizontal();
            bool any = false;
            if (_gs?.RunStartRuntime?.Lanes != null)
            {
                foreach (var kv in _gs.RunStartRuntime.Lanes)
                {
                    any = true;
                    int laneId = kv.Key;
                    if (GUILayout.Button($"Lane {laneId}", GUILayout.Width(85)))
                        Quick_SpawnEnemy(_quickEnemyDefId, laneId, _quickSpawnCount);
                }
            }

            if (!any)
            {
                GUILayout.Label("No lane cache; use manual lane in Advanced.");
            }
            GUILayout.EndHorizontal();
        }

        private void Quick_CompleteHoveredBuildingIfSiteOrRepairTarget()
        {
            if (TryFindActiveSiteFromHover(out _, out _))
            {
                Quick_CompleteCurrentSiteUnderMouse();
                return;
            }

            if (!TryFindBuildingFromHover(out var bid, out var bs))
                return;

            EnsureHp(ref bs);
            bs.HP = bs.MaxHP;
            _gs.WorldState.Buildings.Set(bid, bs);
            _gs.NotificationService?.Push("dbg_finish_hovered", "Debug", $"Finished/Healed hovered building #{bid.Value}.", NotificationSeverity.Info, default, 0.15f, true);
        }

        private bool TryResolveTowerForBuilding(BuildingId bid, BuildingState bs, out TowerId tid, out TowerState ts)
        {
            tid = default;
            ts = default;
            if (_gs?.WorldState?.Towers == null || _gs.DataRegistry == null) return false;
            if (bid.Value == 0) return false;

            int bw = 1, bh = 1;
            try
            {
                if (_gs.DataRegistry.TryGetBuilding(bs.DefId, out var def) && def != null)
                {
                    bw = Mathf.Max(1, def.SizeX);
                    bh = Mathf.Max(1, def.SizeY);
                }
            }
            catch { }

            var towerCell = new CellPos(bs.Anchor.X + (bw / 2), bs.Anchor.Y + (bh / 2));
            foreach (var id in _gs.WorldState.Towers.Ids)
            {
                if (!_gs.WorldState.Towers.Exists(id)) continue;
                var t = _gs.WorldState.Towers.Get(id);
                if (t.Cell.X == towerCell.X && t.Cell.Y == towerCell.Y)
                {
                    tid = id;
                    ts = t;
                    return true;
                }
            }

            return false;
        }

        private void Quick_DrainTowerUnderMouse()
        {
            if (!TryFindBuildingFromHover(out var bid, out var bs) || !TryResolveTowerForBuilding(bid, bs, out var tid, out var ts))
            {
                _gs?.NotificationService?.Push("dbg_tower_drain_none", "Debug", "Current building target has no linked tower.", NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            int amount = 30;
            if (!int.TryParse(_quickAmmoDrainAmtStr, out amount) || amount <= 0) amount = 30;
            ts.Ammo = Mathf.Max(0, ts.Ammo - amount);
            _gs.WorldState.Towers.Set(tid, ts);

            try
            {
                bs.Ammo = ts.Ammo;
                _gs.WorldState.Buildings.Set(bid, bs);
            }
            catch { }

            _gs.AmmoService?.NotifyTowerAmmoChanged(tid, ts.Ammo, ts.AmmoCap);
            _gs.NotificationService?.Push("dbg_tower_drain_one", "Debug", $"{bs.DefId} #{bid.Value} ammo -> {ts.Ammo}/{ts.AmmoCap}", NotificationSeverity.Info, default, 0.1f, true);
        }

        private void Quick_RefillTowerUnderMouse()
        {
            if (!TryFindBuildingFromHover(out var bid, out var bs) || !TryResolveTowerForBuilding(bid, bs, out var tid, out var ts))
            {
                _gs?.NotificationService?.Push("dbg_tower_refill_none", "Debug", "Current building target has no linked tower.", NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            ts.Ammo = ts.AmmoCap;
            _gs.WorldState.Towers.Set(tid, ts);

            try
            {
                bs.Ammo = ts.Ammo;
                _gs.WorldState.Buildings.Set(bid, bs);
            }
            catch { }

            _gs.AmmoService?.NotifyTowerAmmoChanged(tid, ts.Ammo, ts.AmmoCap);
            _gs.NotificationService?.Push("dbg_tower_refill_one", "Debug", $"{bs.DefId} #{bid.Value} ammo -> {ts.Ammo}/{ts.AmmoCap}", NotificationSeverity.Info, default, 0.1f, true);
        }
    }
}


