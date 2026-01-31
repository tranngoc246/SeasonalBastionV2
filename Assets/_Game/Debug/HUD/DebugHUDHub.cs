using System.Collections.Generic;
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

        [SerializeField] private bool _selfPollHotkeysWhenNoRouter = true;
        private bool _hasRouter;

        private int _lastHotkeyFrame = -1;

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
            // Fallback: nếu scene chưa có DebugInputRouter thì Hub tự poll hotkeys để khỏi “mất phím”.
            if (_selfPollHotkeysWhenNoRouter)
            {
                // Nếu sau này bạn add Router vào scene, Hub sẽ tự tắt fallback.
                _hasRouter = _hasRouter || (FindObjectOfType<DebugInputRouter>(true) != null);

                if (!_hasRouter)
                {
                    var kb = Keyboard.current;
                    if (kb != null) HandleHotkeys(kb);
                }
            }

            // Hub no longer polls hotkeys here (handled by DebugInputRouter).
            // Update only resolves services and auto-finds modules if needed.
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
            GUILayout.BeginArea(new Rect(10, 50, 620, (Screen.height - 20)), GUI.skin.box);

            GUILayout.Label("[DebugHUDHub] F1 UI | F2 Build | F3 Road | F4 NPC | F5 Storage | F6 Noti | F7 Index | Esc None");
            GUILayout.Label($"Mode: {_mode}    Tab: {_tab}");

            GUILayout.Space(6);

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

            GUILayout.EndArea();
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
    }
}