namespace SeasonalBastion.Contracts
{
    public enum BuildOrderKind { PlaceNew, Upgrade, Repair }

    public struct BuildOrder
    {
        public int OrderId;
        public BuildOrderKind Kind;

        public string BuildingDefId;
        public BuildingId TargetBuilding; // for upgrade/repair
        public SiteId Site;               // for PlaceNew

        public CostDef RequiredCost;
        public CostProgress Delivered;
        public float WorkSecondsRequired;
        public float WorkSecondsDone;

        public bool Completed;
    }

    public struct CostProgress
    {
        public int Wood, Food, Stone, Iron, Ammo;
    }
}
