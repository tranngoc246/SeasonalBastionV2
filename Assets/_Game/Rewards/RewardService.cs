using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class RewardService : IRewardService
    {
        private readonly GameServices _s;

        public bool IsSelectionActive { get; private set; }
        public RewardOffer CurrentOffer { get; private set; }

        public event Action OnSelectionStarted;
        public event Action<string> OnRewardChosen;
        public event Action OnSelectionEnded;

        private readonly IEventBus _bus;
        private static readonly int[] DaysPerSeason = { 6, 6, 4, 4 };

        public RewardService(GameServices s)
        {
            _s = s;
            _bus = s != null ? s.EventBus : null;

            // Day35: end-season reward hook (placeholder event)
            if (_bus != null)
                _bus.Subscribe<DayEndedEvent>(OnDayEnded);
        }

        private void OnDayEnded(DayEndedEvent ev)
        {
            // end season when dayIndex == maxDays of that season
            int idx = (int)ev.Season;
            int max = (idx >= 0 && idx < DaysPerSeason.Length) ? DaysPerSeason[idx] : 1;

            if (ev.DayIndex == max)
            {
                _bus?.Publish(new EndSeasonRewardRequested(ev.Season, ev.YearIndex, ev.DayIndex));
            }
        }

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
