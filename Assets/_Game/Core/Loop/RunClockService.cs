using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class RunClockService : IRunClock, ITickable
    {
        private readonly IEventBus _bus;

        public Season CurrentSeason { get; private set; } = Season.Spring;
        public int DayIndex { get; private set; } = 1;
        public Phase CurrentPhase { get; private set; } = Phase.Build;

        public float TimeScale { get; private set; } = 1f;
        public bool DefendSpeedUnlocked { get; private set; } = false;

        public event Action<Season,int> OnSeasonDayChanged;
        public event Action<Phase> OnPhaseChanged;
        public event Action OnDayEnded;

        private float _dayTimer;

        public float DayTimerSeconds => _dayTimer;

        // VS2 additions (not in contract): used by debug HUD and runtime logic.
        public int YearIndex { get; private set; } = 1; // 1-based
        public float DayElapsedSeconds => _dayTimer;
        public float DayLengthSeconds => GetSecondsPerDay(CurrentSeason);
        public float DayRemainingSeconds => Math.Max(0f, DayLengthSeconds - _dayTimer);

        private const float SecondsPerDayDev = 180f;
        private const float SecondsPerDayDefend = 120f;

        // Index by (int)Season
        private static readonly int[] DaysPerSeason = { 6, 6, 4, 4 };

        public RunClockService(IEventBus bus){ _bus = bus; }

        public void Start(int seed)
        {
            // Reset run clock to deterministic defaults.
            var prevPhase = CurrentPhase;
            CurrentSeason = Season.Spring;
            DayIndex = 1;
            YearIndex = 1;
            _dayTimer = 0f;

            // Phase is derived from season.
            CurrentPhase = Phase.Build;

            // Default speed.
            SetTimeScaleInternal(1f, forced: true);

            // Fire initial events.
            OnSeasonDayChanged?.Invoke(CurrentSeason, DayIndex);
            _bus?.Publish(new SeasonDayChangedEvent(CurrentSeason, DayIndex));

            // Notify phase state on start (useful for systems to latch current phase).
            OnPhaseChanged?.Invoke(CurrentPhase);
            if (prevPhase != CurrentPhase)
                _bus?.Publish(new PhaseChangedEvent(prevPhase, CurrentPhase));

            _bus?.Publish(new DayStartedEvent(CurrentSeason, DayIndex, YearIndex, CurrentPhase));
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (TimeScale <= 0f) return; // paused

            var scaled = dt * TimeScale;
            _dayTimer += scaled;

            var dayLen = GetSecondsPerDay(CurrentSeason);
            // If dt is large (or timescale high), rollover may cross multiple days.
            // Keep it deterministic by processing sequentially.
            while (_dayTimer >= dayLen)
            {
                _dayTimer -= dayLen;
                EndCurrentDay();
                AdvanceToNextDay();
                dayLen = GetSecondsPerDay(CurrentSeason);
            }
        }

        public void SetTimeScale(float scale)
        {
            SetTimeScaleInternal(scale, forced: false);
        }

        public void ForceSeasonDay(Season s, int dayIndex)
        {
            var prevSeason = CurrentSeason;
            var prevPhase = CurrentPhase;

            CurrentSeason = s;
            DayIndex = ClampDayIndex(s, dayIndex);
            _dayTimer = 0f;

            var newPhase = PhaseFromSeason(CurrentSeason);
            if (newPhase != CurrentPhase)
            {
                CurrentPhase = newPhase;
                OnPhaseChanged?.Invoke(CurrentPhase);
                _bus?.Publish(new PhaseChangedEvent(prevPhase, CurrentPhase));

                // If we jumped into defend, apply speed rule.
                ApplyDefendSpeedRuleIfNeeded();
            }

            OnSeasonDayChanged?.Invoke(CurrentSeason, DayIndex);
            _bus?.Publish(new SeasonDayChangedEvent(CurrentSeason, DayIndex));

            if (prevSeason != CurrentSeason)
                _bus?.Publish(new SeasonChangedEvent(prevSeason, CurrentSeason));

            _bus?.Publish(new DayStartedEvent(CurrentSeason, DayIndex, YearIndex, CurrentPhase));
        }

        private void EndCurrentDay()
        {
            // End-of-day callback (contract) + bus event.
            OnDayEnded?.Invoke();
            _bus?.Publish(new DayEndedEvent(CurrentSeason, DayIndex, YearIndex));
        }

        private void AdvanceToNextDay()
        {
            var prevSeason = CurrentSeason;
            var prevPhase = CurrentPhase;
            var prevYear = YearIndex;

            var maxDays = DaysInSeason(CurrentSeason);
            DayIndex++;
            if (DayIndex <= maxDays)
            {
                OnSeasonDayChanged?.Invoke(CurrentSeason, DayIndex);
                _bus?.Publish(new SeasonDayChangedEvent(CurrentSeason, DayIndex));
                _bus?.Publish(new DayStartedEvent(CurrentSeason, DayIndex, YearIndex, CurrentPhase));
                return;
            }

            // Season rollover.
            DayIndex = 1;
            CurrentSeason = NextSeason(prevSeason);

            if (prevSeason == Season.Winter && CurrentSeason == Season.Spring)
            {
                YearIndex++;
                _bus?.Publish(new YearChangedEvent(prevYear, YearIndex));
            }

            _bus?.Publish(new SeasonChangedEvent(prevSeason, CurrentSeason));

            // Phase rollover based on season.
            var newPhase = PhaseFromSeason(CurrentSeason);
            if (newPhase != prevPhase)
            {
                CurrentPhase = newPhase;
                OnPhaseChanged?.Invoke(CurrentPhase);
                _bus?.Publish(new PhaseChangedEvent(prevPhase, CurrentPhase));

                ApplyDefendSpeedRuleIfNeeded();
            }

            OnSeasonDayChanged?.Invoke(CurrentSeason, DayIndex);
            _bus?.Publish(new SeasonDayChangedEvent(CurrentSeason, DayIndex));
            _bus?.Publish(new DayStartedEvent(CurrentSeason, DayIndex, YearIndex, CurrentPhase));
        }

        private void ApplyDefendSpeedRuleIfNeeded()
        {
            // Deliverable B: when entering Defend, auto set 1x.
            if (CurrentPhase == Phase.Defend)
            {
                if (TimeScale > 1f)
                    SetTimeScaleInternal(1f, forced: true);
            }
        }

        private void SetTimeScaleInternal(float scale, bool forced)
        {
            var clamped = ClampTimeScale(scale);

            // Defend: disallow >1 unless unlocked.
            if (CurrentPhase == Phase.Defend && !DefendSpeedUnlocked && clamped > 1f)
                clamped = 1f;

            if (Math.Abs(TimeScale - clamped) < 0.0001f)
                return;

            var prev = TimeScale;
            TimeScale = clamped;
            _bus?.Publish(new TimeScaleChangedEvent(prev, TimeScale, forced));
        }

        private static float ClampTimeScale(float scale)
        {
            // Allow discrete steps: 0 / 1 / 2 / 3 / 5 (5x for debug)
            if (scale <= 0f) return 0f;
            if (scale <= 1f) return 1f;
            if (scale <= 2f) return 2f;
            if (scale <= 3f) return 3f;
            return 5f;
        }

        private static float GetSecondsPerDay(Season s)
        {
            return (s == Season.Spring || s == Season.Summer) ? SecondsPerDayDev : SecondsPerDayDefend;
        }

        private static int DaysInSeason(Season s)
        {
            var idx = (int)s;
            if ((uint)idx >= DaysPerSeason.Length) return 1;
            return DaysPerSeason[idx];
        }

        private static int ClampDayIndex(Season s, int dayIndex)
        {
            var max = DaysInSeason(s);
            if (dayIndex < 1) return 1;
            if (dayIndex > max) return max;
            return dayIndex;
        }

        private static Season NextSeason(Season s)
        {
            return s switch
            {
                Season.Spring => Season.Summer,
                Season.Summer => Season.Autumn,
                Season.Autumn => Season.Winter,
                _ => Season.Spring,
            };
        }

        private static Phase PhaseFromSeason(Season s)
        {
            return (s == Season.Autumn || s == Season.Winter) ? Phase.Defend : Phase.Build;
        }

        public void LoadSnapshot(int yearIndex, string seasonText, int dayIndex, float dayTimerSeconds, float timeScale)
        {
            YearIndex = Math.Max(1, yearIndex);

            if (!Enum.TryParse<Season>(seasonText, out var s))
                s = Season.Spring;

            CurrentSeason = s;
            DayIndex = Math.Max(1, dayIndex);

            // phase rule: Autumn/Winter => Defend, else Build
            CurrentPhase = (CurrentSeason == Season.Autumn || CurrentSeason == Season.Winter) ? Phase.Defend : Phase.Build;

            _dayTimer = Math.Max(0f, dayTimerSeconds);

            // Apply timeScale AFTER phase is set, so Defend clamp rule is correct.
            SetTimeScaleInternal(timeScale, forced: true);

            // Optional: emit event so systems re-latch safely
            _bus?.Publish(new DayStartedEvent(CurrentSeason, DayIndex, YearIndex, CurrentPhase));
        }
    }
}
