using SeasonalBastion.Contracts;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUILayout.Space(8);
            GUILayout.Label("Day43: Debug Cheats");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn Today (calendar)", GUILayout.Width(180)))
            {
                // Spawn wave theo calendar hiện tại (nếu bạn muốn ép)
                var year = (_gs.RunClock is RunClockService rc) ? rc.YearIndex : 1;
                var season = _gs.RunClock.CurrentSeason;
                var day = _gs.RunClock.DayIndex;

                // Resolver đang fallback Year1+scale nếu Year>1, nên gọi StartDayWaves sẽ hoạt động.
                cs.OnDefendPhaseStarted();
            }

            if (GUILayout.Button("Kill All Enemies", GUILayout.Width(180)))
                cs.KillAllEnemies();
            GUILayout.EndHorizontal();

            // Give resources
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Give +200 Wood/Stone/Food +100 Iron", GUILayout.Width(280)))
                GiveCoreResources(_gs, wood: 200, stone: 200, food: 200, iron: 100);

            if (GUILayout.Button("Give +300 Ammo (Armory)", GUILayout.Width(180)))
                GiveAmmo(_gs, ammo: 300);
            GUILayout.EndHorizontal();
#endif

            GUILayout.EndHorizontal();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void GiveCoreResources(GameServices gs, int wood, int stone, int food, int iron)
        {
            if (gs == null || gs.WorldState == null || gs.StorageService == null || gs.DataRegistry == null) return;

            // find HQ (IsHQ)
            BuildingId hq = default;
            foreach (var id in gs.WorldState.Buildings.Ids)
            {
                var st = gs.WorldState.Buildings.Get(id);
                if (gs.DataRegistry.TryGetBuilding(st.DefId, out var def) && def != null && def.IsHQ)
                {
                    hq = id;
                    break;
                }
            }
            if (hq.Value == 0) return;

            gs.StorageService.Add(hq, ResourceType.Wood, wood);
            gs.StorageService.Add(hq, ResourceType.Stone, stone);
            gs.StorageService.Add(hq, ResourceType.Food, food);
            gs.StorageService.Add(hq, ResourceType.Iron, iron);
        }

        private static void GiveAmmo(GameServices gs, int ammo)
        {
            if (gs == null || gs.WorldState == null || gs.StorageService == null || gs.DataRegistry == null) return;

            // find Armory (defId == bld_armory_t1)
            BuildingId arm = default;
            foreach (var id in gs.WorldState.Buildings.Ids)
            {
                var st = gs.WorldState.Buildings.Get(id);
                if (!string.IsNullOrEmpty(st.DefId) && st.DefId == "bld_armory_t1")
                { arm = id; break; }
            }
            if (arm.Value == 0) return;

            gs.StorageService.Add(arm, ResourceType.Ammo, ammo);
        }
#endif
    }
}
