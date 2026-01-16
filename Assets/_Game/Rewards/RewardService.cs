// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class RewardService : IRewardService
    {
        private readonly GameServices _s;

        public bool IsSelectionActive { get; private set; }
        public RewardOffer CurrentOffer { get; private set; }

        public event System.Action OnSelectionStarted;
        public event System.Action<string> OnRewardChosen;
        public event System.Action OnSelectionEnded;

        public RewardService(GameServices s){ _s = s; }

        public RewardOffer GenerateOffer(int dayIndex, int seed)
        {
            // TODO: deterministic pool pick
            return new RewardOffer("reward.x", "reward.y", "reward.z");
        }

        public void StartSelection(RewardOffer offer)
        {
            IsSelectionActive = true;
            CurrentOffer = offer;
            OnSelectionStarted?.Invoke();
        }

        public void Choose(int slotIndex)
        {
            // TODO: apply reward effect to RunMods or defs modifiers
            var chosen = slotIndex == 0 ? CurrentOffer.A : (slotIndex == 1 ? CurrentOffer.B : CurrentOffer.C);
            OnRewardChosen?.Invoke(chosen);

            IsSelectionActive = false;
            OnSelectionEnded?.Invoke();
        }
    }
}
