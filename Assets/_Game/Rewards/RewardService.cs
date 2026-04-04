using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    public sealed class RewardService : IRewardService
    {
        private readonly GameServices _s;
        private const bool RewardSelectionRuntimeEnabled = false;
        private const string PlaceholderRewardId = "Reward_None";

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
            var ids = BuildDeterministicPlaceholderOffer(seed, dayIndex);
            return new RewardOffer(ids[0], ids[1], ids[2]);
        }

        public void StartSelection(RewardOffer offer)
        {
            CurrentOffer = NormalizeOffer(offer);

            if (!RewardSelectionRuntimeEnabled)
            {
                LogPlaceholderGate("StartSelection blocked: reward runtime is placeholder-only in current project state.");
                IsSelectionActive = false;
                return;
            }

            IsSelectionActive = true;
            OnSelectionStarted?.Invoke();
        }

        public void Choose(int slotIndex)
        {
            if (!RewardSelectionRuntimeEnabled)
            {
                LogPlaceholderGate("Choose ignored: reward selection/apply path is placeholder-only and disabled.");
                IsSelectionActive = false;
                return;
            }

            var chosen = slotIndex == 0 ? CurrentOffer.A : (slotIndex == 1 ? CurrentOffer.B : CurrentOffer.C);
            OnRewardChosen?.Invoke(chosen);

            IsSelectionActive = false;
            OnSelectionEnded?.Invoke();
        }

        private RewardOffer NormalizeOffer(RewardOffer offer)
        {
            string a = string.IsNullOrWhiteSpace(offer.A) ? PlaceholderRewardId : offer.A;
            string b = string.IsNullOrWhiteSpace(offer.B) ? a : offer.B;
            string c = string.IsNullOrWhiteSpace(offer.C) ? b : offer.C;
            return new RewardOffer(a, b, c);
        }

        private string[] BuildDeterministicPlaceholderOffer(int seed, int dayIndex)
        {
            var ids = new List<string>();
            TryAddRewardId(ids, PlaceholderRewardId);
            TryAddRewardId(ids, "Reward_RunComplete");
            TryAddRewardId(ids, "Reward_WinterBossY1");

            if (ids.Count == 0)
                ids.Add(PlaceholderRewardId);

            ids.Sort(StringComparer.OrdinalIgnoreCase);
            int count = ids.Count;
            int baseIndex = Math.Abs((seed * 397) ^ dayIndex);

            string a = ids[baseIndex % count];
            string b = ids[(baseIndex + 1) % count];
            string c = ids[(baseIndex + 2) % count];
            return new[] { a, b, c };
        }

        private void TryAddRewardId(List<string> ids, string rewardId)
        {
            if (ids == null || string.IsNullOrWhiteSpace(rewardId))
                return;
            if (ids.Contains(rewardId))
                return;

            if (_s?.DataRegistry == null)
            {
                ids.Add(rewardId);
                return;
            }

            if (_s.DataRegistry.TryGetReward(rewardId, out var def) && def != null)
                ids.Add(rewardId);
        }

        private void LogPlaceholderGate(string message)
        {
            Debug.LogWarning($"[RewardService] {message}");
            _s?.NotificationService?.Push(
                key: "reward_placeholder_gate",
                title: "Rewards",
                body: "Reward runtime is placeholder-only right now; no gameplay reward was started or applied.",
                severity: NotificationSeverity.Info,
                payload: default,
                cooldownSeconds: 1f,
                dedupeByKey: true);
        }
    }
}
