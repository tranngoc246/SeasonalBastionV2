namespace SeasonalBastion.Contracts
{
    public enum JobArchetype
    {
        Leisure,
        Inspect,

        Harvest,
        HaulBasic,
        HaulToForge,

        BuildDeliver,
        BuildWork,
        RepairWork,

        CraftAmmo,
        HaulAmmoToArmory,
        ResupplyTower
    }

    public enum JobStatus { Created, Claimed, InProgress, Completed, Failed, Cancelled }

    public struct Job
    {
        public JobId Id;
        public JobArchetype Archetype;
        public JobStatus Status;

        public NpcId ClaimedBy;

        public BuildingId Workplace;      // source workplace (HQ/warehouse/forge/armory/producer)
        public BuildingId SourceBuilding; // optional
        public BuildingId DestBuilding;   // optional
        public SiteId Site;               // optional
        public TowerId Tower;             // optional

        public ResourceType ResourceType; // optional
        public int Amount;                // optional

        public CellPos TargetCell;        // optional convenience
        public float CreatedAt;
    }
}
