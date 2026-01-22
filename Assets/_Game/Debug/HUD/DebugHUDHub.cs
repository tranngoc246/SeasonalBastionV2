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
        [SerializeField] private DebugRunClockHUD _clockHud;

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

        private GameServices _gs;

        private void Awake()
        {
            DebugHubState.Enabled = true;

            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
            _gs = _bootstrap != null ? _bootstrap.Services : null;

            if (_buildTool == null) _buildTool = FindObjectOfType<DebugBuildingTool>(true);
            if (_roadTool == null) _roadTool = FindObjectOfType<DebugRoadTool>(true);
            if (_npcTool == null) _npcTool = FindObjectOfType<DebugNpcTool>(true);

            if (_storageHud == null) _storageHud = FindObjectOfType<DebugStorageHUD>(true);
            if (_notiHud == null) _notiHud = FindObjectOfType<DebugNotificationsHUD>(true);
            if (_worldIndexHud == null) _worldIndexHud = FindObjectOfType<DebugWorldIndexHUD>(true);
            if (_clockHud == null) _clockHud = FindObjectOfType<DebugRunClockHUD>(true);

            // Hub-control: disable standalone HUD + disable standalone toggle hotkeys
            if (_buildTool != null) _buildTool.SetHubControlled(true);
            if (_roadTool != null) _roadTool.SetHubControlled(true);
            if (_npcTool != null) _npcTool.SetHubControlled(true);

            if (_storageHud != null) _storageHud.SetHubControlled(true);
            if (_notiHud != null) _notiHud.SetHubControlled(true);
            if (_worldIndexHud != null) _worldIndexHud.SetHubControlled(true);
            if (_clockHud != null) _clockHud.SetHubControlled(true);

            ApplyMode(_mode);
        }

        private void OnDestroy()
        {
            DebugHubState.Enabled = false;
        }

        private void Update()
        {
            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
            _gs ??= _bootstrap != null ? _bootstrap.Services : null;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb[_toggleUiKey].wasPressedThisFrame)
                _showUi = !_showUi;

            if (kb[_clearModeKey].wasPressedThisFrame)
                ApplyMode(DebugHubMode.None);

            if (kb[_modeBuildKey].wasPressedThisFrame) { ApplyMode(DebugHubMode.Build); _tab = DebugHubTab.Home; }
            if (kb[_modeRoadKey].wasPressedThisFrame) { ApplyMode(DebugHubMode.Road); _tab = DebugHubTab.Home; }
            if (kb[_modeNpcKey].wasPressedThisFrame) { ApplyMode(DebugHubMode.Npc); _tab = DebugHubTab.Npc; }
            if (kb[_modeStorageKey].wasPressedThisFrame) { ApplyMode(DebugHubMode.Storage); _tab = DebugHubTab.Storage; }

            if (kb[_tabNotiKey].wasPressedThisFrame) _tab = DebugHubTab.Notifications;
            if (kb[_tabWorldIndexKey].wasPressedThisFrame) _tab = DebugHubTab.WorldIndex;
        }

        private void ApplyMode(DebugHubMode m)
        {
            _mode = m;

            if (_buildTool != null) _buildTool.SetEnabledFromHub(m == DebugHubMode.Build);
            if (_roadTool != null) _roadTool.SetEnabledFromHub(m == DebugHubMode.Road);
            if (_npcTool != null) _npcTool.SetEnabledFromHub(m == DebugHubMode.Npc);
            if (_storageHud != null) _storageHud.SetEnabledFromHub(m == DebugHubMode.Storage);
        }

        private void OnGUI()
        {
            if (!_showUi) return;

            GUILayout.BeginArea(new Rect(10, 10, 620, 720), GUI.skin.box);

            GUILayout.Label("[DebugHUDHub] F1 UI | F2 Build | F3 Road | F4 NPC | F5 Storage | F6 Noti | F7 Index | Esc None");
            GUILayout.Label($"Mode: {_mode}    Tab: {_tab}");

            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Home", GUILayout.Width(90))) _tab = DebugHubTab.Home;
            if (GUILayout.Button("Noti (F6)", GUILayout.Width(120))) _tab = DebugHubTab.Notifications;
            if (GUILayout.Button("Index (F7)", GUILayout.Width(120))) _tab = DebugHubTab.WorldIndex;
            if (GUILayout.Button("NPC", GUILayout.Width(90))) _tab = DebugHubTab.Npc;
            if (GUILayout.Button("Storage", GUILayout.Width(110))) _tab = DebugHubTab.Storage;
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

        private void DrawHome()
        {
            GUILayout.Label("Active Modules:");
            GUILayout.Label($"BuildTool: {(_buildTool != null ? "OK" : "missing")}  | RoadTool: {(_roadTool != null ? "OK" : "missing")}  | NpcTool: {(_npcTool != null ? "OK" : "missing")}");
            GUILayout.Label($"NotiHUD: {(_notiHud != null ? "OK" : "missing")}  | WorldIndexHUD: {(_worldIndexHud != null ? "OK" : "missing")}  | StorageHUD: {(_storageHud != null ? "OK" : "missing")}");
            GUILayout.Label($"RunClockHUD: {(_clockHud != null ? "OK" : "missing")}");

            GUILayout.Space(8);
            GUILayout.Label("Notes:");
            GUILayout.Label("- All old standalone toggle keys are disabled (B/R/N/H/I/S).");
            GUILayout.Label("- Tools only respond to inputs when their mode is active.");

            GUILayout.Space(10);
            if (_clockHud != null) _clockHud.DrawHubGUI();
        }
    }
}
