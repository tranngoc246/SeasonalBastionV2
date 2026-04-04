using System;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    internal static class TowerBackingValidator
    {
        internal readonly struct Result
        {
            public readonly bool IsValid;
            public readonly TowerId TowerId;
            public readonly CellPos ExpectedTowerCell;
            public readonly string Error;

            public Result(bool isValid, TowerId towerId, CellPos expectedTowerCell, string error)
            {
                IsValid = isValid;
                TowerId = towerId;
                ExpectedTowerCell = expectedTowerCell;
                Error = error;
            }
        }

        public static bool ValidateBuildingHasCorrectTower(IWorldState worldState, IDataRegistry dataRegistry, IWorldIndex worldIndex, BuildingId buildingId, out string error)
        {
            return TryValidateBuildingHasCorrectTower(worldState, dataRegistry, worldIndex, buildingId).IsValidOrError(out error);
        }

        public static Result TryValidateBuildingHasCorrectTower(IWorldState worldState, IDataRegistry dataRegistry, IWorldIndex worldIndex, BuildingId buildingId)
        {
            if (buildingId.Value == 0)
                return Fail(default, default, "BuildingId is invalid.");

            if (worldState?.Buildings == null || !worldState.Buildings.Exists(buildingId))
                return Fail(default, default, $"Building {buildingId.Value} is missing.");

            var building = worldState.Buildings.Get(buildingId);
            BuildingDef def = SafeGetBuildingDef(dataRegistry, building.DefId);
            if (def == null)
                return Fail(default, default, $"Cannot validate tower backing for building {buildingId.Value} because BuildingDef '{building.DefId}' could not be resolved.");

            if (!def.IsTower)
                return new Result(true, default, default, null);

            int w = Math.Max(1, def.SizeX);
            int h = Math.Max(1, def.SizeY);
            var expectedTowerCell = new CellPos(building.Anchor.X + (w / 2), building.Anchor.Y + (h / 2));

            if (worldIndex?.Towers == null)
                return Fail(default, expectedTowerCell, $"Tower backing missing for building {buildingId.Value} ({building.DefId}): WorldIndex.Towers is unavailable.");

            if (worldState.Towers == null)
                return Fail(default, expectedTowerCell, $"Tower backing missing for building {buildingId.Value} ({building.DefId}): Tower store is unavailable.");

            bool foundTowerAtExpectedCell = false;
            bool foundIndexedTowerAtExpectedCell = false;
            bool foundIndexedTowerMissingFromStore = false;
            bool sawIndexedTowerClaimedByDifferentBuilding = false;

            for (int i = 0; i < worldIndex.Towers.Count; i++)
            {
                var towerId = worldIndex.Towers[i];
                if (!worldState.Towers.Exists(towerId))
                {
                    foundIndexedTowerMissingFromStore = true;
                    continue;
                }

                var tower = worldState.Towers.Get(towerId);
                bool sameCell = tower.Cell.X == expectedTowerCell.X && tower.Cell.Y == expectedTowerCell.Y;
                if (!sameCell)
                    continue;

                foundIndexedTowerAtExpectedCell = true;
                var ownership = TryResolveOwningTowerBuilding(worldState, dataRegistry, tower.Cell, towerId);
                if (!ownership.HasValue)
                    return Fail(towerId, expectedTowerCell, $"Tower backing mismatch for building {buildingId.Value} ({building.DefId}): indexed tower {towerId.Value} exists at expected cell ({expectedTowerCell.X},{expectedTowerCell.Y}) but no constructed tower building footprint owns that cell.");

                if (ownership.Value.Value != buildingId.Value)
                {
                    sawIndexedTowerClaimedByDifferentBuilding = true;
                    return Fail(towerId, expectedTowerCell, $"Tower backing mismatch for building {buildingId.Value} ({building.DefId}): indexed tower {towerId.Value} at expected cell ({expectedTowerCell.X},{expectedTowerCell.Y}) belongs to building {ownership.Value.Value}, not {buildingId.Value}.");
                }

                return new Result(true, towerId, expectedTowerCell, null);
            }

            foreach (var towerId in worldState.Towers.Ids)
            {
                if (!worldState.Towers.Exists(towerId))
                    continue;

                var tower = worldState.Towers.Get(towerId);
                if (tower.Cell.X != expectedTowerCell.X || tower.Cell.Y != expectedTowerCell.Y)
                    continue;

                foundTowerAtExpectedCell = true;
                var ownership = TryResolveOwningTowerBuilding(worldState, dataRegistry, tower.Cell, towerId);
                if (!ownership.HasValue)
                    return Fail(towerId, expectedTowerCell, $"Tower backing mismatch for building {buildingId.Value} ({building.DefId}): tower {towerId.Value} exists at expected cell ({expectedTowerCell.X},{expectedTowerCell.Y}) but no constructed tower building footprint owns that cell.");

                if (ownership.Value.Value != buildingId.Value)
                {
                    return Fail(towerId, expectedTowerCell, $"Tower backing mismatch for building {buildingId.Value} ({building.DefId}): tower {towerId.Value} at expected cell ({expectedTowerCell.X},{expectedTowerCell.Y}) belongs to building {ownership.Value.Value}, not {buildingId.Value}.");
                }

                return Fail(towerId, expectedTowerCell, $"Tower backing mismatch for building {buildingId.Value} ({building.DefId}): tower {towerId.Value} exists for this building at expected cell ({expectedTowerCell.X},{expectedTowerCell.Y}) but WorldIndex.Towers is missing it.");
            }

            if (foundIndexedTowerMissingFromStore)
            {
                return Fail(default, expectedTowerCell, $"Tower backing missing for building {buildingId.Value} ({building.DefId}): WorldIndex.Towers references a tower missing from TowerStore.");
            }

            if (sawIndexedTowerClaimedByDifferentBuilding)
            {
                return Fail(default, expectedTowerCell, $"Tower backing mismatch for building {buildingId.Value} ({building.DefId}): indexed tower ownership resolved to a different building.");
            }

            if (foundIndexedTowerAtExpectedCell)
            {
                return Fail(default, expectedTowerCell, $"Tower backing missing for building {buildingId.Value} ({building.DefId}): indexed tower exists at expected cell ({expectedTowerCell.X},{expectedTowerCell.Y}) but ownership could not be resolved exactly.");
            }

            if (foundTowerAtExpectedCell)
            {
                return Fail(default, expectedTowerCell, $"Tower backing missing for building {buildingId.Value} ({building.DefId}): tower exists at expected cell ({expectedTowerCell.X},{expectedTowerCell.Y}) but failed exact ownership validation.");
            }

            return Fail(default, expectedTowerCell, $"Tower backing missing for building {buildingId.Value} ({building.DefId}): no tower found at expected cell ({expectedTowerCell.X},{expectedTowerCell.Y}).");
        }

        private static BuildingId? TryResolveOwningTowerBuilding(IWorldState worldState, IDataRegistry dataRegistry, CellPos towerCell, TowerId towerId)
        {
            if (worldState?.Buildings == null)
                return null;

            BuildingId owner = default;
            int matches = 0;

            foreach (var buildingId in worldState.Buildings.Ids)
            {
                if (!worldState.Buildings.Exists(buildingId))
                    continue;

                var building = worldState.Buildings.Get(buildingId);
                if (!building.IsConstructed)
                    continue;

                var def = SafeGetBuildingDef(dataRegistry, building.DefId);
                if (def == null || !def.IsTower)
                    continue;

                int w = Math.Max(1, def.SizeX);
                int h = Math.Max(1, def.SizeY);
                bool insideFootprint = towerCell.X >= building.Anchor.X
                    && towerCell.X < building.Anchor.X + w
                    && towerCell.Y >= building.Anchor.Y
                    && towerCell.Y < building.Anchor.Y + h;

                if (!insideFootprint)
                    continue;

                var expectedTowerCell = new CellPos(building.Anchor.X + (w / 2), building.Anchor.Y + (h / 2));
                if (expectedTowerCell.X != towerCell.X || expectedTowerCell.Y != towerCell.Y)
                {
                    Debug.LogWarning($"[TowerBackingValidator] Tower backing footprint mismatch: tower {towerId.Value} at ({towerCell.X},{towerCell.Y}) falls inside tower building {buildingId.Value} ({building.DefId}) footprint anchored at ({building.Anchor.X},{building.Anchor.Y}) but expected anchor-derived tower cell is ({expectedTowerCell.X},{expectedTowerCell.Y}).");
                    continue;
                }

                matches++;
                owner = buildingId;
                if (matches > 1)
                {
                    Debug.LogWarning($"[TowerBackingValidator] Tower backing ownership is ambiguous for tower {towerId.Value} at ({towerCell.X},{towerCell.Y}): multiple constructed tower buildings claim the same footprint/cell.");
                    return null;
                }
            }

            return matches == 1 ? owner : null;
        }

        private static BuildingDef SafeGetBuildingDef(IDataRegistry dataRegistry, string defId)
        {
            if (dataRegistry == null || string.IsNullOrWhiteSpace(defId))
                return null;

            try { return dataRegistry.GetBuilding(defId); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TowerBackingValidator] Failed to resolve BuildingDef '{defId}' while validating tower backing: {ex.Message}");
                return null;
            }
        }

        private static Result Fail(TowerId towerId, CellPos expectedTowerCell, string error)
        {
            return new Result(false, towerId, expectedTowerCell, error);
        }

        private static bool IsValidOrError(this Result result, out string error)
        {
            error = result.Error;
            return result.IsValid;
        }
    }
}
