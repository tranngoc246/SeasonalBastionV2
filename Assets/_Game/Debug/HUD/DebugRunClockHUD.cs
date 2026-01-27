using UnityEngine;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.DebugTools
{
    /// <summary>
    /// Minimal RunClock debug panel:
    /// - Show Year/Season/Day/Phase + remaining seconds
    /// - Buttons for time scale (0/1/2/3)
    /// - VS3 Day32: quick jump to Winter Year2 for Victory test
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
            float el = impl != null ? impl.DayElapsedSeconds : 0f;

            var outcomeSvc = _gs?.RunOutcomeService;
            var outcome = outcomeSvc != null ? outcomeSvc.Outcome : RunOutcome.Ongoing;

            GUILayout.Label("=== RunClock (VS3 Day32) ===");
            GUILayout.Label($"Year: {year}  | Season: {clock.CurrentSeason}  | Day: {clock.DayIndex}  | Phase: {clock.CurrentPhase}");
            GUILayout.Label($"TimeScale: {clock.TimeScale:0.##}   (DefendSpeedUnlocked: {clock.DefendSpeedUnlocked})");
            GUILayout.Label($"RunOutcome: {outcome}");
            GUILayout.Label("Victory rule: End of Winter (Day 4) of Year 2");

            if (impl != null)
                GUILayout.Label($"DayTimer: {el:0.0}s / {len:0.0}s   (Remaining: {rem:0.0}s)");
            else
                GUILayout.Label("(RunClockService not found: cannot show day timer / jump)");

            GUILayout.Space(4);

            // TimeScale controls
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Pause (0x)", GUILayout.Width(110))) clock.SetTimeScale(0f);
            if (GUILayout.Button("1x", GUILayout.Width(60))) clock.SetTimeScale(1f);
            if (GUILayout.Button("2x", GUILayout.Width(60))) clock.SetTimeScale(2f);
            if (GUILayout.Button("3x", GUILayout.Width(60))) clock.SetTimeScale(3f);
            GUILayout.EndHorizontal();

            if (impl != null)
            {
                GUILayout.Space(6);
                GUILayout.Label("Quick jump (for acceptance tests):");

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Jump: Winter Y2 D4 (Start)", GUILayout.Width(220)))
                {
                    impl.LoadSnapshot(
                        yearIndex: 2,
                        seasonText: Season.Winter.ToString(),
                        dayIndex: 4,
                        dayTimerSeconds: 0f,
                        timeScale: 1f
                    );
                }

                if (GUILayout.Button("Jump: Winter Y2 D4 (Near End)", GUILayout.Width(220)))
                {
                    // Set timer near end so DayEnded triggers quickly -> Victory
                    float nearEnd = Mathf.Max(0f, impl.DayLengthSeconds - 0.2f);
                    impl.LoadSnapshot(
                        yearIndex: 2,
                        seasonText: Season.Winter.ToString(),
                        dayIndex: 4,
                        dayTimerSeconds: nearEnd,
                        timeScale: 1f
                    );
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(6);
                GUILayout.Label("Force Season/Day (Year giữ nguyên):");

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Spring D1", GUILayout.Width(90))) impl.ForceSeasonDay(Season.Spring, 1);
                if (GUILayout.Button("Summer D1", GUILayout.Width(90))) impl.ForceSeasonDay(Season.Summer, 1);
                if (GUILayout.Button("Autumn D1", GUILayout.Width(90))) impl.ForceSeasonDay(Season.Autumn, 1);
                if (GUILayout.Button("Winter D1", GUILayout.Width(90))) impl.ForceSeasonDay(Season.Winter, 1);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Prev Day", GUILayout.Width(90)))
                {
                    int d = clock.DayIndex - 1;
                    if (d < 1) d = 1;
                    impl.ForceSeasonDay(clock.CurrentSeason, d);
                }
                if (GUILayout.Button("Next Day", GUILayout.Width(90)))
                {
                    int d = clock.DayIndex + 1;
                    impl.ForceSeasonDay(clock.CurrentSeason, d);
                }
                GUILayout.EndHorizontal();
            }
        }
    }
}
