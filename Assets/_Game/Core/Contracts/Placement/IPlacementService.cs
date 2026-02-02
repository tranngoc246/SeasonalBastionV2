namespace SeasonalBastion.Contracts
{
    public interface IPlacementService
    {
        PlacementResult ValidateBuilding(string buildingDefId, CellPos anchor, Dir4 rotation);
        BuildingId CommitBuilding(string buildingDefId, CellPos anchor, Dir4 rotation);

        // Road placement
        bool CanPlaceRoad(CellPos c);
        void PlaceRoad(CellPos c);

        bool CanRemoveRoad(CellPos c);
        void RemoveRoad(CellPos c);
    }
}
