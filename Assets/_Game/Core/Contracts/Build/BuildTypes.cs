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
