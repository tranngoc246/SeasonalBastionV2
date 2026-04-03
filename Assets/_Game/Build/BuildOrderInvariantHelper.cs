using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal static class BuildOrderInvariantHelper
    {
        public static void AssertBuildInvariant(IWorldState worldState, IGridMap gridMap, IDataRegistry dataRegistry, IWorldIndex worldIndex, BuildingId buildingId)
        {
            if (worldState == null) throw new InvalidOperationException("WorldState is null.");
            if (buildingId.Value == 0) throw new InvalidOperationException("BuildingId is invalid.");
            if (!worldState.Buildings.Exists(buildingId)) throw new InvalidOperationException($"Building {buildingId.Value} is missing.");

            var building = worldState.Buildings.Get(buildingId);
            if (!building.IsConstructed)
                throw new InvalidOperationException($"Building {buildingId.Value} exists but IsConstructed == false.");

            if (worldState.Sites != null)
            {
                foreach (var siteId in worldState.Sites.Ids)
                {
                    if (!worldState.Sites.Exists(siteId)) continue;
                    var site = worldState.Sites.Get(siteId);
                    if (site.TargetBuilding.Value == buildingId.Value)
                        throw new InvalidOperationException($"Site {siteId.Value} still references constructed building {buildingId.Value}.");
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
                        throw new InvalidOperationException($"Grid mismatch at ({cell.X},{cell.Y}) for building {buildingId.Value}: got {occ.Kind} / {occ.Building.Value}.");
                }
            }

            if (worldIndex != null && !Contains(worldIndex, buildingId))
                throw new InvalidOperationException($"WorldIndex is missing building {buildingId.Value} ({building.DefId}).");
        }

        private static bool Contains(IWorldIndex worldIndex, BuildingId buildingId)
        {
            return Contains(worldIndex.Warehouses, buildingId)
                || Contains(worldIndex.Producers, buildingId)
                || Contains(worldIndex.Houses, buildingId)
                || Contains(worldIndex.Forges, buildingId)
                || Contains(worldIndex.Armories, buildingId)
                || ContainsTowerBackingBuilding(worldIndex, buildingId);
        }

        private static bool Contains(System.Collections.Generic.IReadOnlyList<BuildingId> ids, BuildingId buildingId)
        {
            if (ids == null) return false;
            for (int i = 0; i < ids.Count; i++)
                if (ids[i].Value == buildingId.Value)
                    return true;
            return false;
        }

        private static bool ContainsTowerBackingBuilding(IWorldIndex worldIndex, BuildingId buildingId)
        {
            var towers = worldIndex.Towers;
            return towers != null && towers.Count > 0 && buildingId.Value != 0;
        }

        private static BuildingDef SafeGetBuildingDef(IDataRegistry dataRegistry, string defId)
        {
            if (dataRegistry == null || string.IsNullOrWhiteSpace(defId))
                return null;

            try { return dataRegistry.GetBuilding(defId); }
            catch { return null; }
        }
    }
}
