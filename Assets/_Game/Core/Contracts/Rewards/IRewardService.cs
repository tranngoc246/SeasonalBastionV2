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
    public interface IRewardService
    {
        bool IsSelectionActive { get; }
        RewardOffer CurrentOffer { get; }

        // called by RunClock end-of-defend-day
        RewardOffer GenerateOffer(int dayIndex, int seed);

        void StartSelection(RewardOffer offer);
        void Choose(int slotIndex); // 0..2

        event System.Action OnSelectionStarted;
        event System.Action<string> OnRewardChosen;
        event System.Action OnSelectionEnded;
    }
}
