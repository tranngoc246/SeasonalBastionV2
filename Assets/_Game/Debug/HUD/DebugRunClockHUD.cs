using UnityEngine;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.DebugTools
{
    /// <summary>
    /// Minimal RunClock debug panel:
    /// - Show Year/Season/Day/Phase + remaining seconds
    /// - Buttons for time scale (0/1/2/3)
    /// 
    /// Drawn by DebugHUDHub (no standalone hotkey).
    /// </summary>
    public sealed class DebugRunClockHUD : MonoBehaviour
    {
        [SerializeField] private bool _hubControlled;
        private GameServices _gs;

        private void Awake()
        {
            _gs = FindObjectOfType<GameBootstrap>()?.Services;
        }

        public void SetHubControlled(bool v) => _hubControlled = v;

        private void TryResolveServices()
        {
            if (_gs != null) return;
            var bootstrap = FindObjectOfType<GameBootstrap>();
            if (bootstrap != null) _gs = bootstrap.Services;
        }

        public void DrawHubGUI()
        {
            TryResolveServices();

            var clock = _gs?.RunClock;
            if (clock == null)
            {
                GUILayout.Label("RunClock: missing");
                return;
            }

            var impl = clock as RunClockService;
            int year = impl != null ? impl.YearIndex : 1;
            float len = impl != null ? impl.DayLengthSeconds : 0f;
            float rem = impl != null ? impl.DayRemainingSeconds : 0f;
            float el  = impl != null ? impl.DayElapsedSeconds : 0f;

            GUILayout.Label("=== RunClock ===");
            GUILayout.Label($"Year: {year}  | Season: {clock.CurrentSeason}  | Day: {clock.DayIndex}  | Phase: {clock.CurrentPhase}");
            GUILayout.Label($"TimeScale: {clock.TimeScale:0.##}   (DefendSpeedUnlocked: {clock.DefendSpeedUnlocked})");

            if (impl != null)
            {
                GUILayout.Label($"DayTimer: {el:0.0}s / {len:0.0}s   (Remaining: {rem:0.0}s)");
            }
            else
            {
                GUILayout.Label("(RunClockService not found: cannot show day timer)");
            }

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Pause (0x)", GUILayout.Width(110))) clock.SetTimeScale(0f);
            if (GUILayout.Button("1x", GUILayout.Width(60))) clock.SetTimeScale(1f);
            if (GUILayout.Button("2x", GUILayout.Width(60))) clock.SetTimeScale(2f);
            if (GUILayout.Button("3x", GUILayout.Width(60))) clock.SetTimeScale(3f);
            GUILayout.EndHorizontal();

            GUILayout.Label("Rule: Enter Defend (Autumn/Winter) -> auto set 1x. Defend >1x only if unlocked.");
        }
    }
}
