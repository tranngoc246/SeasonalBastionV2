using System.Text;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion.DebugTools
{
    public sealed partial class DebugHUDHub
    {
        private void DrawRewardQuickSection()
        {
            if (_gs?.RewardService == null)
            {
                GUILayout.Label("RewardService: missing");
                return;
            }

            GUILayout.Space(6);
            GUILayout.Label("Rewards / Progression");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Trigger Wave Reward", GUILayout.Width(160)))
            {
                var clock = _gs.RunClock as RunClockService;
                int year = clock != null ? clock.YearIndex : 1;
                Season season = clock != null ? clock.CurrentSeason : Season.Spring;
                int day = clock != null ? clock.DayIndex : 1;
                _gs.RewardService.TriggerWaveEndReward("debug_wave", year, season, day, false, false);
            }
            if (GUILayout.Button("Trigger Season Reward", GUILayout.Width(160)))
            {
                var clock = _gs.RunClock as RunClockService;
                int year = clock != null ? clock.YearIndex : 1;
                Season season = clock != null ? clock.CurrentSeason : Season.Spring;
                int day = clock != null ? clock.DayIndex : 1;
                _gs.RewardService.TriggerSeasonEndReward(season, year, day);
            }
            if (GUILayout.Button("Clear Rewards", GUILayout.Width(120)))
            {
                _gs.RewardService.LoadPickedRewards(System.Array.Empty<string>());
            }
            GUILayout.EndHorizontal();

            var currentOffer = _gs.RewardService.CurrentOffer;
            GUILayout.Label($"Selection Active: {_gs.RewardService.IsSelectionActive}");
            GUILayout.Label($"Offer: {FormatRewardName(currentOffer.A)} | {FormatRewardName(currentOffer.B)} | {FormatRewardName(currentOffer.C)}");

            if (_gs.RewardService.IsSelectionActive)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Pick A", GUILayout.Width(100))) _gs.RewardService.Choose(0);
                if (GUILayout.Button("Pick B", GUILayout.Width(100))) _gs.RewardService.Choose(1);
                if (GUILayout.Button("Pick C", GUILayout.Width(100))) _gs.RewardService.Choose(2);
                GUILayout.EndHorizontal();
            }

            var picked = _gs.RewardService.PickedRewardDefIds;
            if (picked != null && picked.Count > 0)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < picked.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(FormatRewardName(picked[i]));
                }
                GUILayout.Label("Picked: " + sb);
            }
            else
            {
                GUILayout.Label("Picked: (none)");
            }

            ref var mods = ref _gs.WorldState.RunMods;
            GUILayout.Label($"Mods: Build x{mods.BuildSpeedMultiplier:0.##} | Ammo +{mods.TowerAmmoCapacityBonus} | Reload x{mods.TowerReloadSpeedMultiplier:0.##} | NPC Move x{mods.NpcMoveSpeedMultiplier:0.##}");
        }

        private static string FormatRewardName(string rewardId)
        {
            return rewardId switch
            {
                "Reward_BuildSpeed" => "+Build speed",
                "Reward_AmmoCapacity" => "+Ammo capacity",
                "Reward_TowerReload" => "+Tower reload speed",
                "Reward_NpcMoveSpeed" => "+NPC move speed",
                _ => string.IsNullOrWhiteSpace(rewardId) ? "-" : rewardId,
            };
        }
    }
}
