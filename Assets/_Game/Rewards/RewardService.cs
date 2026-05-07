using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    public sealed class RewardService : IRewardService
    {
        private readonly IWorldState _worldState;
        private readonly IEventBus _bus;
        private readonly RewardModifierPolicy _modifierPolicy;
        private static readonly int[] DaysPerSeason = { 6, 6, 4, 4 };

        internal const string RewardBuildSpeedId = "Reward_BuildSpeed";
        internal const string RewardAmmoCapacityId = "Reward_AmmoCapacity";
        internal const string RewardTowerReloadId = "Reward_TowerReload";
        internal const string RewardNpcMoveSpeedId = "Reward_NpcMoveSpeed";

        private readonly List<string> _pickedRewardDefIds = new();
        private readonly string[] _rewardPool =
        {
            RewardBuildSpeedId,
            RewardAmmoCapacityId,
            RewardTowerReloadId,
            RewardNpcMoveSpeedId,
        };

        private int _offerSequence;

        public bool IsSelectionActive { get; private set; }
        public RewardOffer CurrentOffer { get; private set; }
        public RewardOffer LastGeneratedOffer { get; private set; }
        public IReadOnlyList<string> PickedRewardDefIds => _pickedRewardDefIds;

        public event Action OnSelectionStarted;
        public event Action<string> OnRewardChosen;
        public event Action OnSelectionEnded;

        public RewardService(IWorldState worldState, IDataRegistry dataRegistry, IEventBus eventBus)
        {
            _worldState = worldState;
            _bus = eventBus;
            _modifierPolicy = new RewardModifierPolicy(worldState, dataRegistry);

            if (_bus != null)
            {
                _bus.Subscribe<DayEndedEvent>(OnDayEnded);
                _bus.Subscribe<EndSeasonRewardRequested>(OnEndSeasonRewardRequested);
            }

            CurrentOffer = default;
            LastGeneratedOffer = default;
        }

        private void OnDayEnded(DayEndedEvent ev)
        {
            int idx = (int)ev.Season;
            int max = (idx >= 0 && idx < DaysPerSeason.Length) ? DaysPerSeason[idx] : 1;
            if (ev.DayIndex == max)
                _bus?.Publish(new EndSeasonRewardRequested(ev.Season, ev.YearIndex, ev.DayIndex));
        }

        private void OnEndSeasonRewardRequested(EndSeasonRewardRequested ev)
        {
            TriggerSeasonEndReward(ev.Season, ev.YearIndex, ev.DayIndex);
        }

        public RewardOffer GenerateOffer(int dayIndex, int seed)
        {
            int eventSeed = CombineSeed(seed, dayIndex, _offerSequence);
            var ids = BuildDeterministicOffer(eventSeed);
            var offer = new RewardOffer(ids[0], ids[1], ids[2]);
            LastGeneratedOffer = offer;
            _offerSequence++;
            return offer;
        }

        public void StartSelection(RewardOffer offer)
        {
            CurrentOffer = NormalizeOffer(offer);
            IsSelectionActive = true;
            Debug.Log($"[RewardService] Selection started: {CurrentOffer.A}, {CurrentOffer.B}, {CurrentOffer.C}");
            OnSelectionStarted?.Invoke();
        }

        public void Choose(int slotIndex)
        {
            if (!IsSelectionActive)
            {
                Debug.LogWarning("[RewardService] Choose ignored because no reward selection is active.");
                return;
            }

            string chosen = slotIndex switch
            {
                <= 0 => CurrentOffer.A,
                1 => CurrentOffer.B,
                _ => CurrentOffer.C,
            };

            ApplyReward(chosen, appendToHistory: true);
            OnRewardChosen?.Invoke(chosen);
            _bus?.Publish(new RewardPickedEvent(chosen));
            IsSelectionActive = false;
            OnSelectionEnded?.Invoke();
        }

        public void TriggerWaveEndReward(string waveId, int year, Season season, int day, bool isBoss, bool isFinalWave)
        {
            int runSeed = GetRunSeed();
            int reasonSeed = CombineSeed(runSeed, year, (int)season, day, waveId != null ? waveId.GetHashCode() : 0, isBoss ? 1 : 0, isFinalWave ? 1 : 0, 101);
            var offer = GenerateOffer(dayIndex: day, seed: reasonSeed);
            StartSelection(offer);

            Debug.Log($"[RewardService] Wave-end reward triggered wave={waveId} y={year} season={season} day={day} offer=[{offer.A}, {offer.B}, {offer.C}]");
        }

        public void TriggerSeasonEndReward(Season season, int yearIndex, int dayIndex)
        {
            int runSeed = GetRunSeed();
            int reasonSeed = CombineSeed(runSeed, yearIndex, (int)season, dayIndex, 202);
            var offer = GenerateOffer(dayIndex, reasonSeed);
            StartSelection(offer);

            Debug.Log($"[RewardService] Season-end reward triggered y={yearIndex} season={season} day={dayIndex} offer=[{offer.A}, {offer.B}, {offer.C}]");
        }

        public void LoadPickedRewards(IReadOnlyList<string> rewardIds)
        {
            _pickedRewardDefIds.Clear();
            _modifierPolicy.ResetRunModifiers();

            if (rewardIds == null)
                return;

            for (int i = 0; i < rewardIds.Count; i++)
            {
                var rewardId = rewardIds[i];
                if (string.IsNullOrWhiteSpace(rewardId))
                    continue;

                ApplyReward(rewardId, appendToHistory: true);
            }

            IsSelectionActive = false;
            CurrentOffer = default;
        }

        private RewardOffer NormalizeOffer(RewardOffer offer)
        {
            string a = NormalizeRewardId(offer.A, 0);
            string b = NormalizeRewardId(offer.B, 1);
            string c = NormalizeRewardId(offer.C, 2);
            return new RewardOffer(a, b, c);
        }

        private string NormalizeRewardId(string rewardId, int fallbackOffset)
        {
            if (!string.IsNullOrWhiteSpace(rewardId))
                return rewardId;

            if (_rewardPool.Length == 0)
                return RewardBuildSpeedId;

            int idx = fallbackOffset % _rewardPool.Length;
            if (idx < 0) idx += _rewardPool.Length;
            return _rewardPool[idx];
        }

        private string[] BuildDeterministicOffer(int eventSeed)
        {
            var ids = new List<string>(_rewardPool.Length);
            for (int i = 0; i < _rewardPool.Length; i++)
                ids.Add(_rewardPool[i]);

            ids.Sort(StringComparer.Ordinal);
            ShuffleDeterministic(ids, eventSeed);

            return new[]
            {
                ids[0 % ids.Count],
                ids[1 % ids.Count],
                ids[2 % ids.Count],
            };
        }

        private static void ShuffleDeterministic(List<string> ids, int seed)
        {
            if (ids == null || ids.Count <= 1)
                return;

            unchecked
            {
                uint state = (uint)seed;
                if (state == 0u)
                    state = 0x9E3779B9u;

                for (int i = ids.Count - 1; i > 0; i--)
                {
                    state = state * 1664525u + 1013904223u;
                    int j = (int)(state % (uint)(i + 1));
                    (ids[i], ids[j]) = (ids[j], ids[i]);
                }
            }
        }

        private void ApplyReward(string rewardId, bool appendToHistory)
        {
            if (string.IsNullOrWhiteSpace(rewardId))
                return;

            if (appendToHistory)
                _pickedRewardDefIds.Add(rewardId);

            Debug.Log($"[RewardService] Chosen reward: {rewardId}");
            _modifierPolicy.ApplyReward(rewardId);
        }

        private int GetRunSeed()
        {
            return 0;
        }

        private static int CombineSeed(params int[] values)
        {
            unchecked
            {
                int hash = 17;
                if (values != null)
                {
                    for (int i = 0; i < values.Length; i++)
                        hash = (hash * 31) + values[i];
                }
                return hash;
            }
        }
    }
}
