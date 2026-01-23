namespace SeasonalBastion.Contracts
{
    /// <summary>
    /// Emitted when PlacementService auto-creates a driveway road cell during CommitBuilding().
    /// BuildOrderService listens to map orderId -> roadCell for cancel rollback (Day21).
    /// </summary>
    public readonly struct BuildOrderAutoRoadCreatedEvent
    {
        public readonly int OrderId;
        public readonly CellPos RoadCell;

        public BuildOrderAutoRoadCreatedEvent(int orderId, CellPos roadCell)
        {
            OrderId = orderId;
            RoadCell = roadCell;
        }
    }
}
