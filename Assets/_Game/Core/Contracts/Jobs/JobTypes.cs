// AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1)
// Source: PART25_Technical_InterfacesPack_Services_Events_DTOs_LOCKED_SPEC_v0.1.md
// Notes:
// - Contracts only: interfaces/enums/structs/DTO/events.
// - Do not put runtime logic here.
// - Namespace kept unified to minimize cross-namespace friction.

using System;
using System.Collections.Generic;

namespace SeasonalBastion.Contracts
{
    public enum JobArchetype
    {
        Leisure,
        Inspect,

        Harvest,
        HaulBasic,

        BuildDeliver,
        BuildWork,

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
