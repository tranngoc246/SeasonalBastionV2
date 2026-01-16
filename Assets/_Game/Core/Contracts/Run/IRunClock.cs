// AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1)
// Source: PART25_Technical_InterfacesPack_Services_Events_DTOs_LOCKED_SPEC_v0.1.md
// Notes:
// - Contracts only: interfaces/enums/structs/DTO/events.
// - Do not put runtime logic here.
// - Namespace kept unified to minimize cross-namespace friction.

using System;
using System.Collections.Generic;

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
        event System.Action<Season,int> OnSeasonDayChanged;
        event System.Action<Phase> OnPhaseChanged;
        event System.Action OnDayEnded;
    }
}
