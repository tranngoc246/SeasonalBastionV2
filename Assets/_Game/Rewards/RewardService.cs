using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    public sealed class RewardService : IRewardService
    {
        private readonly GameServices _s;
        private readonly IEventBus _bus;
        private static readonly int[] DaysPerSeason = { 6, 6, 4, 4 };

        private const string RewardBuildSpeed = "Reward_BuildSpeed";
        private const string RewardAmmoCapacity = "Reward_AmmoCapacity";
        private const string RewardTowerReload = "Reward_TowerReload";
        private const string RewardNpcMoveSpeed = "Reward_NpcMoveSpeed";

        private readonly List<string> _pickedRewardDefIds = new();
        private readonly string[] _rewardPool =
        {
            RewardBuildSpeed,
            RewardAmmoCapacity,
            RewardTowerReload,
            RewardNpcMoveSpeed,
        };

        private int _offerSequence;

        public bool IsSelectionActive { get; private set; }
        public RewardOffer CurrentOffer { get; private set; }
        public RewardOffer LastGeneratedOffer { get; private set; }
        public IReadOnlyList<string> PickedRewardDefIds => _pickedRewardDefIds;

        public event Action OnSelectionStarted;
        public event Action<string> OnRewardChosen;
        public event Action OnSelectionEnded;

        public RewardService(GameServices s)
        {
            _s = s;
            _bus = s != null ? s.EventBus : null;

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
            ResetRunModifiers();

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
                return RewardBuildSpeed;

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

            ref var mods = ref _s.WorldState.RunMods;

            switch (rewardId)
            {
                case RewardBuildSpeed:
                    mods.BuildSpeedMultiplier = MaxOrDefault(mods.BuildSpeedMultiplier, 1f) * 1.15f;
                    Debug.Log($"[RewardService] Chosen reward: {rewardId}");
                    Debug.Log($"[RewardService] Applied modifier: BuildSpeedMultiplier={mods.BuildSpeedMultiplier:0.###}");
                    break;

                case RewardAmmoCapacity:
                    mods.TowerAmmoCapacityBonus += 5;
                    ApplyTowerAmmoCapacityBonus(mods.TowerAmmoCapacityBonus);
                    Debug.Log($"[RewardService] Chosen reward: {rewardId}");
                    Debug.Log($"[RewardService] Applied modifier: TowerAmmoCapacityBonus={mods.TowerAmmoCapacityBonus}");
                    break;

                case RewardTowerReload:
                    mods.TowerReloadSpeedMultiplier = MaxOrDefault(mods.TowerReloadSpeedMultiplier, 1f) * 1.12f;
                    Debug.Log($"[RewardService] Chosen reward: {rewardId}");
                    Debug.Log($"[RewardService] Applied modifier: TowerReloadSpeedMultiplier={mods.TowerReloadSpeedMultiplier:0.###}");
                    break;

                case RewardNpcMoveSpeed:
                    mods.NpcMoveSpeedMultiplier = MaxOrDefault(mods.NpcMoveSpeedMultiplier, 1f) * 1.10f;
                    Debug.Log($"[RewardService] Chosen reward: {rewardId}");
                    Debug.Log($"[RewardService] Applied modifier: NpcMoveSpeedMultiplier={mods.NpcMoveSpeedMultiplier:0.###}");
                    break;

                default:
                    Debug.LogWarning($"[RewardService] Unknown reward id '{rewardId}' ignored.");
                    break;
            }
        }

        private void ApplyTowerAmmoCapacityBonus(int totalBonus)
        {
            if (_s?.WorldState?.Towers == null)
                return;

            foreach (var towerId in _s.WorldState.Towers.Ids)
            {
                if (!_s.WorldState.Towers.Exists(towerId))
                    continue;

                var tower = _s.WorldState.Towers.Get(towerId);
                int baseCap = ResolveBaseTowerAmmoCap(tower.Cell);
                tower.AmmoCap = Math.Max(0, baseCap + totalBonus);
                if (tower.Ammo > tower.AmmoCap)
                    tower.Ammo = tower.AmmoCap;
                _s.WorldState.Towers.Set(towerId, tower);
            }
        }

        private int ResolveBaseTowerAmmoCap(CellPos towerCell)
        {
            if (_s?.WorldState?.Buildings == null || _s?.DataRegistry == null)
                return 0;

            foreach (var buildingId in _s.WorldState.Buildings.Ids)
            {
                if (!_s.WorldState.Buildings.Exists(buildingId))
                    continue;

                var building = _s.WorldState.Buildings.Get(buildingId);
                if (!building.IsConstructed)
                    continue;

                if (!_s.DataRegistry.TryGetBuilding(building.DefId, out var buildingDef) || buildingDef == null || !buildingDef.IsTower)
                    continue;

                int width = Math.Max(1, buildingDef.SizeX);
                int height = Math.Max(1, buildingDef.SizeY);
                if (!FootprintContainsCell(building.Anchor, width, height, towerCell))
                    continue;

                if (_s.DataRegistry.TryGetTower(building.DefId, out var towerDef) && towerDef != null)
                    return Math.Max(0, towerDef.AmmoMax);

                return 0;
            }

            return 0;
        }

        private void ResetRunModifiers()
        {
            ref var mods = ref _s.WorldState.RunMods;
            mods.BuildSpeedMultiplier = 1f;
            mods.TowerAmmoCapacityBonus = 0;
            mods.TowerReloadSpeedMultiplier = 1f;
            mods.NpcMoveSpeedMultiplier = 1f;
            ApplyTowerAmmoCapacityBonus(mods.TowerAmmoCapacityBonus);
        }

        private int GetRunSeed()
        {
            return 0;
        }

        private static float MaxOrDefault(float value, float fallback)
        {
            return value > 0f ? value : fallback;
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

        private static bool FootprintContainsCell(CellPos anchor, int sizeX, int sizeY, CellPos c)
        {
            int w = sizeX <= 0 ? 1 : sizeX;
            int h = sizeY <= 0 ? 1 : sizeY;

            return c.X >= anchor.X && c.X < (anchor.X + w)
                && c.Y >= anchor.Y && c.Y < (anchor.Y + h);
        }
    }
}
