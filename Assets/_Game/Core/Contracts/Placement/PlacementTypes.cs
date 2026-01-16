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
