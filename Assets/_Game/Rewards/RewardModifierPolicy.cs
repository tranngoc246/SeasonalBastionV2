using System;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    internal sealed class RewardModifierPolicy
    {
        private readonly IWorldState _worldState;
        private readonly IDataRegistry _dataRegistry;

        public RewardModifierPolicy(IWorldState worldState, IDataRegistry dataRegistry)
        {
            _worldState = worldState;
            _dataRegistry = dataRegistry;
        }

        public void ApplyReward(string rewardId)
        {
            if (string.IsNullOrWhiteSpace(rewardId) || _worldState == null)
                return;

            ref var mods = ref _worldState.RunMods;
            switch (rewardId)
            {
                case RewardService.RewardBuildSpeedId:
                    mods.BuildSpeedMultiplier = MaxOrDefault(mods.BuildSpeedMultiplier, 1f) * 1.15f;
                    Debug.Log($"[RewardService] Applied modifier: BuildSpeedMultiplier={mods.BuildSpeedMultiplier:0.###}");
                    break;

                case RewardService.RewardAmmoCapacityId:
                    mods.TowerAmmoCapacityBonus += 5;
                    ApplyTowerAmmoCapacityBonus(mods.TowerAmmoCapacityBonus);
                    Debug.Log($"[RewardService] Applied modifier: TowerAmmoCapacityBonus={mods.TowerAmmoCapacityBonus}");
                    break;

                case RewardService.RewardTowerReloadId:
                    mods.TowerReloadSpeedMultiplier = MaxOrDefault(mods.TowerReloadSpeedMultiplier, 1f) * 1.12f;
                    Debug.Log($"[RewardService] Applied modifier: TowerReloadSpeedMultiplier={mods.TowerReloadSpeedMultiplier:0.###}");
                    break;

                case RewardService.RewardNpcMoveSpeedId:
                    mods.NpcMoveSpeedMultiplier = MaxOrDefault(mods.NpcMoveSpeedMultiplier, 1f) * 1.10f;
                    Debug.Log($"[RewardService] Applied modifier: NpcMoveSpeedMultiplier={mods.NpcMoveSpeedMultiplier:0.###}");
                    break;

                default:
                    Debug.LogWarning($"[RewardService] Unknown reward id '{rewardId}' ignored.");
                    break;
            }
        }

        public void ResetRunModifiers()
        {
            if (_worldState == null)
                return;

            ref var mods = ref _worldState.RunMods;
            mods.BuildSpeedMultiplier = 1f;
            mods.TowerAmmoCapacityBonus = 0;
            mods.TowerReloadSpeedMultiplier = 1f;
            mods.NpcMoveSpeedMultiplier = 1f;
            ApplyTowerAmmoCapacityBonus(mods.TowerAmmoCapacityBonus);
        }

        private void ApplyTowerAmmoCapacityBonus(int totalBonus)
        {
            if (_worldState?.Towers == null)
                return;

            foreach (var towerId in _worldState.Towers.Ids)
            {
                if (!_worldState.Towers.Exists(towerId))
                    continue;

                var tower = _worldState.Towers.Get(towerId);
                int baseCap = ResolveBaseTowerAmmoCap(tower.Cell);
                tower.AmmoCap = Math.Max(0, baseCap + totalBonus);
                if (tower.Ammo > tower.AmmoCap)
                    tower.Ammo = tower.AmmoCap;
                _worldState.Towers.Set(towerId, tower);
            }
        }

        private int ResolveBaseTowerAmmoCap(CellPos towerCell)
        {
            if (_worldState?.Buildings == null || _dataRegistry == null)
                return 0;

            foreach (var buildingId in _worldState.Buildings.Ids)
            {
                if (!_worldState.Buildings.Exists(buildingId))
                    continue;

                var building = _worldState.Buildings.Get(buildingId);
                if (!building.IsConstructed)
                    continue;

                if (!_dataRegistry.TryGetBuilding(building.DefId, out var buildingDef) || buildingDef == null || !buildingDef.IsTower)
                    continue;

                int width = Math.Max(1, buildingDef.SizeX);
                int height = Math.Max(1, buildingDef.SizeY);
                if (!FootprintContainsCell(building.Anchor, width, height, towerCell))
                    continue;

                if (_dataRegistry.TryGetTower(building.DefId, out var towerDef) && towerDef != null)
                    return Math.Max(0, towerDef.AmmoMax);

                return 0;
            }

            return 0;
        }

        private static float MaxOrDefault(float value, float fallback)
            => value > 0f ? value : fallback;

        private static bool FootprintContainsCell(CellPos anchor, int sizeX, int sizeY, CellPos cell)
        {
            int width = sizeX <= 0 ? 1 : sizeX;
            int height = sizeY <= 0 ? 1 : sizeY;
            return cell.X >= anchor.X && cell.X < (anchor.X + width)
                && cell.Y >= anchor.Y && cell.Y < (anchor.Y + height);
        }
    }
}
