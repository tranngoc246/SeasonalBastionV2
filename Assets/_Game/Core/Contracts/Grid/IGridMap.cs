namespace SeasonalBastion.Contracts
{
    public interface IGridMap
    {
        int Width { get; }
        int Height { get; }

        bool IsInside(CellPos c);
        CellOccupancy Get(CellPos c);

        bool IsRoad(CellPos c);
        bool IsBlocked(CellPos c);

        // Mutations should be controlled by services (Placement/BuildSite),
        // but GridMap provides low-level apply methods.
        void SetRoad(CellPos c, bool isRoad);
        void SetBuilding(CellPos c, BuildingId id);
        void ClearBuilding(CellPos c);

        void SetSite(CellPos c, SiteId id);
        void ClearSite(CellPos c);

        void ClearAll();
    }
}
