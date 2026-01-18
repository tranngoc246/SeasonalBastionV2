namespace SeasonalBastion.Contracts
{
    public enum PlacementFailReason
    {
        None,
        OutOfBounds,
        Overlap,
        NoRoadConnection,        // entry too far (driveway len=1)
        InvalidRotation,
        BlockedBySite,
        Unknown
    }

    public readonly struct PlacementResult
    {
        public readonly bool Ok;
        public readonly PlacementFailReason Reason;
        public readonly CellPos SuggestedRoadCell; // driveway conversion target (if any)
        public PlacementResult(bool ok, PlacementFailReason r, CellPos drivewayTarget)
        { Ok=ok; Reason=r; SuggestedRoadCell=drivewayTarget; }
    }
}
