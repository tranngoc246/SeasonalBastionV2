using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal static class BuildOrderInvariantHelper
    {
        public static void AssertBuildInvariant(IWorldState worldState, IGridMap gridMap, IDataRegistry dataRegistry, IWorldIndex worldIndex, BuildingId buildingId)
        {
            if (!ConstructedBuildingInvariantValidator.Validate(worldState, gridMap, dataRegistry, worldIndex, buildingId, out var error))
                throw new InvalidOperationException(error);
        }
    }
}
