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

            if (worldIndex != null && !Contains(worldState, dataRegistry, worldIndex, buildingId, building, def))
                throw new InvalidOperationException($"WorldIndex is missing building {buildingId.Value} ({building.DefId}).");
        }

        private static bool Contains(IWorldState worldState, IDataRegistry dataRegistry, IWorldIndex worldIndex, BuildingId buildingId, in BuildingState building, BuildingDef def)
        {
            if (Contains(worldIndex.Warehouses, buildingId)
                || Contains(worldIndex.Producers, buildingId)
                || Contains(worldIndex.Houses, buildingId)
                || Contains(worldIndex.Forges, buildingId)
                || Contains(worldIndex.Armories, buildingId))
            {
                return true;
            }

            var towerValidation = TowerBackingValidator.TryValidateBuildingHasCorrectTower(worldState, dataRegistry, worldIndex, buildingId);
            if (towerValidation.IsValid)
                return true;

            if (!string.IsNullOrEmpty(towerValidation.Error))
                LogInvariantMismatch(towerValidation.Error);

            return false;
        }

        private static bool Contains(System.Collections.Generic.IReadOnlyList<BuildingId> ids, BuildingId buildingId)
        {
            if (ids == null) return false;
            for (int i = 0; i < ids.Count; i++)
                if (ids[i].Value == buildingId.Value)
                    return true;
            return false;
        }


        private static void LogInvariantMismatch(string message)
        {
            UnityEngine.Debug.LogWarning($"[BuildOrderInvariantHelper] {message}");
        }

        private static BuildingDef SafeGetBuildingDef(IDataRegistry dataRegistry, string defId)
        {
            if (dataRegistry == null || string.IsNullOrWhiteSpace(defId))
                return null;

            try { return dataRegistry.GetBuilding(defId); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BuildOrderInvariantHelper] Failed to resolve BuildingDef '{defId}' while asserting build invariant: {ex}");
                return null;
            }
        }
    }
}
