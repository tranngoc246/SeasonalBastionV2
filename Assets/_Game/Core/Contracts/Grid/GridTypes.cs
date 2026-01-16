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
    public enum CellOccupancyKind { Empty, Road, Building, Site }

    public readonly struct CellOccupancy
    {
        public readonly CellOccupancyKind Kind;
        public readonly BuildingId Building;
        public readonly SiteId Site;
        public CellOccupancy(CellOccupancyKind k, BuildingId b, SiteId s)
        { Kind=k; Building=b; Site=s; }
    }
}
