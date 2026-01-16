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
    public interface IResourceFlowService
    {
        // pick nearest source with enough amount (or any amount)
        bool TryPickSource(CellPos from, ResourceType type, int minAmount, out StoragePick pick);

        // pick nearest destination with enough space
        bool TryPickDest(CellPos from, ResourceType type, int minSpace, out StoragePick pick);

        // atomic transfer (server-authoritative in sim)
        int Transfer(BuildingId src, BuildingId dst, ResourceType type, int amount);
    }
}
