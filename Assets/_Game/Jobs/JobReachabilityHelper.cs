using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public static class JobReachabilityHelper
    {
        public static bool IsReachable(GameServices s, CellPos from, CellPos to)
        {
            if (s?.Pathfinder == null)
                return true;

            return s.Pathfinder.TryEstimateCost(from, to, out _);
        }

        public static bool IsReachable(IPathfinderRuntime pathfinder, CellPos from, CellPos to)
        {
            if (pathfinder == null)
                return true;

            return pathfinder.TryEstimateCost(from, to, out _);
        }

        public static bool IsSiteEntryReachable(GameServices s, in BuildSiteState site, CellPos from)
        {
            var entry = EntryCellUtil.GetApproachCellForSite(s, site, from);
            return IsReachable(s, from, entry);
        }

        public static bool IsSiteEntryReachable(IDataRegistry dataRegistry, IGridMap gridMap, IPathfinderRuntime pathfinder, in BuildSiteState site, CellPos from)
        {
            var entry = EntryCellUtil.GetApproachCellForSite(dataRegistry, gridMap, site, from);
            return IsReachable(pathfinder, from, entry);
        }

        public static bool IsBuildingEntryReachable(GameServices s, in BuildingState building, CellPos from)
        {
            var entry = EntryCellUtil.GetApproachCellForBuilding(s, building, from);
            return IsReachable(s, from, entry);
        }

        public static bool IsBuildingEntryReachable(IDataRegistry dataRegistry, IGridMap gridMap, IPathfinderRuntime pathfinder, in BuildingState building, CellPos from)
        {
            var entry = EntryCellUtil.GetApproachCellForBuilding(dataRegistry, gridMap, building, from);
            return IsReachable(pathfinder, from, entry);
        }
    }
}
