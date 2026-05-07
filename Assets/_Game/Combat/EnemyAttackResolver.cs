using System;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    internal sealed class EnemyAttackResolver
    {
        private readonly IWorldState _worldState;
        private readonly IGridMap _gridMap;
        private readonly IDataRegistry _dataRegistry;
        private readonly IRunOutcomeService _runOutcomeService;
        private readonly EnemyTargetResolver _targetResolver;
        private readonly Func<int> _getYearIndexOr1;
        private readonly float _defaultAttackIntervalSec;

        public EnemyAttackResolver(
            IWorldState worldState,
            IGridMap gridMap,
            IDataRegistry dataRegistry,
            IRunOutcomeService runOutcomeService,
            EnemyTargetResolver targetResolver,
            Func<int> getYearIndexOr1,
            float defaultAttackIntervalSec)
        {
            _worldState = worldState;
            _gridMap = gridMap;
            _dataRegistry = dataRegistry;
            _runOutcomeService = runOutcomeService;
            _targetResolver = targetResolver;
            _getYearIndexOr1 = getYearIndexOr1;
            _defaultAttackIntervalSec = defaultAttackIntervalSec;
        }

        public void TryAttackHQ(ref EnemyState enemy, EnemyDef def, ref float cooldown)
        {
            if (cooldown > 0f)
                return;
            if (_worldState?.Buildings == null)
                return;

            _targetResolver.EnsureHqCached();
            var hqId = _targetResolver.GetCachedHqId();
            if (hqId.Value == 0 || !_worldState.Buildings.Exists(hqId))
                return;

            int year = _getYearIndexOr1();
            float multiplier = YearScaling.EnemyDamageMul(year);
            int damage = Mathf.Max(0, Mathf.RoundToInt(def.DamageToHQ * multiplier));
            if (damage <= 0)
            {
                cooldown = _defaultAttackIntervalSec;
                return;
            }

            var hq = _worldState.Buildings.Get(hqId);
            int hp = Mathf.Max(0, hq.HP - damage);
            hq.HP = hp;
            _worldState.Buildings.Set(hqId, hq);

            if (hp <= 0)
                _runOutcomeService?.Defeat();

            cooldown = _defaultAttackIntervalSec;
        }

        public void TryAttackBuilding(BuildingId buildingId, int damage, ref float cooldown)
        {
            if (cooldown > 0f)
                return;
            if (damage <= 0)
            {
                cooldown = _defaultAttackIntervalSec;
                return;
            }

            if (_worldState?.Buildings == null || _gridMap == null || _dataRegistry == null)
                return;
            if (buildingId.Value == 0 || !_worldState.Buildings.Exists(buildingId))
                return;

            var building = _worldState.Buildings.Get(buildingId);
            if (!building.IsConstructed)
            {
                cooldown = _defaultAttackIntervalSec;
                return;
            }

            int hp = Mathf.Max(0, building.HP - damage);
            building.HP = hp;
            if (hp <= 0)
            {
                building.IsConstructed = false;
                ClearDestroyedBuildingFootprint(building);
            }

            _worldState.Buildings.Set(buildingId, building);
            cooldown = _defaultAttackIntervalSec;
        }

        public void TryAttackAdjacentBlockingBuilding(ref EnemyState enemy, EnemyDef def, ref float cooldown)
        {
            if (cooldown > 0f || _gridMap == null)
                return;

            int damage = Mathf.Max(0, def.DamageToBuildings);
            if (damage <= 0)
            {
                cooldown = _defaultAttackIntervalSec;
                return;
            }

            var cell = enemy.Cell;
            var north = new CellPos(cell.X, cell.Y + 1);
            var east = new CellPos(cell.X + 1, cell.Y);
            var south = new CellPos(cell.X, cell.Y - 1);
            var west = new CellPos(cell.X - 1, cell.Y);

            if (TryAttackIfBuildingAt(north, damage, ref cooldown)) return;
            if (TryAttackIfBuildingAt(east, damage, ref cooldown)) return;
            if (TryAttackIfBuildingAt(south, damage, ref cooldown)) return;
            if (TryAttackIfBuildingAt(west, damage, ref cooldown)) return;

            cooldown = _defaultAttackIntervalSec;
        }

        private bool TryAttackIfBuildingAt(CellPos cell, int damage, ref float cooldown)
        {
            var occupancy = _gridMap.Get(cell);
            if (occupancy.Kind != CellOccupancyKind.Building || occupancy.Building.Value == 0)
                return false;

            TryAttackBuilding(occupancy.Building, damage, ref cooldown);
            return true;
        }

        private void ClearDestroyedBuildingFootprint(BuildingState building)
        {
            if (_dataRegistry.TryGetBuilding(building.DefId, out var def) && def != null)
            {
                int width = Mathf.Max(1, def.SizeX);
                int height = Mathf.Max(1, def.SizeY);
                for (int dy = 0; dy < height; dy++)
                    for (int dx = 0; dx < width; dx++)
                        _gridMap.ClearBuilding(new CellPos(building.Anchor.X + dx, building.Anchor.Y + dy));
                return;
            }

            _gridMap.ClearBuilding(building.Anchor);
        }
    }
}
