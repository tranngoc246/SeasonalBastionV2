using UnityEngine;

namespace SeasonalBastion.DebugTools
{
    /// <summary>
    /// Day34: show wave counters (alive/spawned/planned) + resolve timers.
    /// Hub-controlled (only drawn by DebugHUDHub).
    /// </summary>
    public sealed class DebugWaveHUD : MonoBehaviour
    {
        [SerializeField] private GameBootstrap _bootstrap;
        [SerializeField] private bool _hubControlled;

        private GameServices _gs;

        public void SetHubControlled(bool v) => _hubControlled = v;

        private void Awake()
        {
            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
        }

        private void TryResolve()
        {
            if (_bootstrap == null) return;
            _gs ??= _bootstrap.Services;
        }

        public void DrawHubGUI()
        {
            TryResolve();
            if (_gs == null)
            {
                GUILayout.Label("GameServices = null");
                return;
            }

            var cs = _gs.CombatService as CombatService;
            if (cs == null)
            {
                GUILayout.Label("CombatService (impl) not found");
                return;
            }

            GUILayout.Space(10);
            GUILayout.Label("Day34: Wave Debug");

            GUILayout.Label($"Combat Active: {cs.IsActive}");
            GUILayout.Label($"Wave Active: {cs.HasActiveWave}  | WaveId: {(cs.ActiveWaveId ?? "(none)")}");
            GUILayout.Label($"Spawned: {cs.ActiveWaveSpawned}/{cs.ActiveWavePlanned}  | SpawnDone: {cs.ActiveWaveSpawnDone}");
            GUILayout.Label($"AliveEnemies: {cs.AliveEnemyCount}");
            GUILayout.Label($"ResolveTimer: {cs.ActiveWaveResolveElapsed:0.0}s / {cs.ActiveWaveResolveTimeout:0.0}s");
            GUILayout.Label($"IsBossWave: {cs.ActiveWaveIsBoss}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Force Resolve Wave", GUILayout.Width(180)))
                cs.ForceResolveWave();
            GUILayout.EndHorizontal();
        }
    }
}
