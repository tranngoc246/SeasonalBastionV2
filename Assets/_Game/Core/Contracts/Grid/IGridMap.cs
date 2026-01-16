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
    }
}
