using System;

namespace SeasonalBastion.Contracts
{
    public interface IRunClock
    {
        Season CurrentSeason { get; }
        int DayIndex { get; }             // 1-based or 0-based but must be consistent
        Phase CurrentPhase { get; }

        float TimeScale { get; }          // current applied
        bool DefendSpeedUnlocked { get; } // dev/user setting

        void SetTimeScale(float scale);   // 0,1,2,3 (or 0.5)
        void ForceSeasonDay(Season s, int dayIndex);

        // Time events
        event Action<Season,int> OnSeasonDayChanged;
        event Action<Phase> OnPhaseChanged;
        event Action OnDayEnded;
    }
}
