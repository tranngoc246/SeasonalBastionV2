// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class RunClockService : IRunClock
    {
        private readonly IEventBus _bus;

        public Season CurrentSeason { get; private set; } = Season.Spring;
        public int DayIndex { get; private set; } = 1;
        public Phase CurrentPhase { get; private set; } = Phase.Build;

        public float TimeScale { get; private set; } = 1f;
        public bool DefendSpeedUnlocked { get; private set; } = false;

        public event System.Action<Season,int> OnSeasonDayChanged;
        public event System.Action<Phase> OnPhaseChanged;
        public event System.Action OnDayEnded;

        private float _dayTimer;

        public RunClockService(IEventBus bus){ _bus = bus; }

        public void Start(int seed)
        {
            // TODO reset; store seed if needed
            CurrentSeason = Season.Spring;
            DayIndex = 1;
            CurrentPhase = Phase.Build;
            TimeScale = 1f;
            _dayTimer = 0;
            OnSeasonDayChanged?.Invoke(CurrentSeason, DayIndex);
            OnPhaseChanged?.Invoke(CurrentPhase);
        }

        public void Tick(float dt)
        {
            var scaled = dt * TimeScale;
            _dayTimer += scaled;

            // TODO: use Deliverable B pacing numbers
            // if day ends: advance day, raise OnDayEnded
        }

        public void SetTimeScale(float scale) => TimeScale = scale;

        public void ForceSeasonDay(Season s, int dayIndex)
        {
            CurrentSeason = s;
            DayIndex = dayIndex;
            OnSeasonDayChanged?.Invoke(CurrentSeason, DayIndex);
        }
    }
}
