using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public static class ConstructedBuildingInvariantValidator
    {
        public static bool Validate(IWorldState worldState, IGridMap gridMap, IDataRegistry dataRegistry, IWorldIndex worldIndex, BuildingId buildingId, out string error)
        {
            error = null;

            if (worldState == null)
                return Fail(buildingId, "WorldState is null.", out error);

            if (buildingId.Value == 0)
                return Fail(buildingId, "BuildingId is invalid.", out error);

            if (worldState.Buildings == null || !worldState.Buildings.Exists(buildingId))
                return Fail(buildingId, $"Building {buildingId.Value} is missing.", out error);

            var building = worldState.Buildings.Get(buildingId);
            if (!building.IsConstructed)
                return Fail(buildingId, $"Building {buildingId.Value} exists but IsConstructed == false.", out error);

            if (worldState.Sites != null)
            {
                foreach (var siteId in worldState.Sites.Ids)
                {
                    if (!worldState.Sites.Exists(siteId)) continue;
                    var site = worldState.Sites.Get(siteId);
                    if (site.TargetBuilding.Value == buildingId.Value)
                        return Fail(buildingId, $"Site {siteId.Value} still references constructed building {buildingId.Value}.", out error);
                }
            }

            var def = SafeGetBuildingDef(dataRegistry, building.DefId);
            int w = Math.Max(1, def?.SizeX ?? 1);
            int h = Math.Max(1, def?.SizeY ?? 1);

            if (gridMap != null)
            {
                for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                {
                    var cell = new CellPos(building.Anchor.X + dx, building.Anchor.Y + dy);
                    var occ = gridMap.Get(cell);
                    if (occ.Kind != CellOccupancyKind.Building || occ.Building.Value != buildingId.Value)
                    {
                        return Fail(buildingId, $"Grid mismatch at ({cell.X},{cell.Y}) for building {buildingId.Value}: got {occ.Kind} / {occ.Building.Value}.", out error);
                    }
                }
            }

            if (worldIndex != null)
            {
                if (ContainsBuildingId(worldIndex.Warehouses, buildingId)
                    || ContainsBuildingId(worldIndex.Producers, buildingId)
                    || ContainsBuildingId(worldIndex.Houses, buildingId)
                    || ContainsBuildingId(worldIndex.Forges, buildingId)
                    || ContainsBuildingId(worldIndex.Armories, buildingId))
                {
                    return true;
                }

                var towerValidation = TowerBackingValidator.TryValidateBuildingHasCorrectTower(worldState, dataRegistry, worldIndex, buildingId);
                if (towerValidation.IsValid)
                    return true;

                if (!string.IsNullOrEmpty(towerValidation.Error))
                {
                    error = towerValidation.Error;
                    return false;
                }

                return Fail(buildingId, $"WorldIndex is missing building {buildingId.Value} ({building.DefId}).", out error);
            }

            return true;
        }

        private static BuildingDef SafeGetBuildingDef(IDataRegistry dataRegistry, string defId)
        {
            if (dataRegistry == null || string.IsNullOrWhiteSpace(defId))
                return null;

            try { return dataRegistry.GetBuilding(defId); }
            catch
            {
                return null;
            }
        }

        private static bool ContainsBuildingId(System.Collections.Generic.IReadOnlyList<BuildingId> ids, BuildingId buildingId)
        {
            if (ids == null) return false;
            for (int i = 0; i < ids.Count; i++)
                if (ids[i].Value == buildingId.Value)
                    return true;
            return false;
        }

        private static bool Fail(BuildingId buildingId, string reason, out string error)
        {
            error = reason;
            return false;
        }
    }
}
