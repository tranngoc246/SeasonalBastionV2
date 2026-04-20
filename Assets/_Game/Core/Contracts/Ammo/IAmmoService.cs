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
    public interface IAmmoService
    {
        // Tower monitor
        void NotifyTowerAmmoChanged(TowerId tower, int current, int max);

        // Request queue (armory uses this)
        void EnqueueRequest(AmmoRequest req);
        bool TryDequeueNext(out AmmoRequest req); // urgent first

        // Crafting
        bool TryStartCraft(BuildingId forge);
        void Tick(float dt);

        // Debug / observability
        int PendingRequests { get; }
        int Debug_TotalTowers { get; }
        int Debug_TowersWithoutAmmo { get; }
        int Debug_ActiveResupplyJobs { get; }
        int Debug_ArmoryAvailableAmmo { get; }
        string Debug_ArmoryStatus { get; }
        string Debug_ResupplyStatus { get; }
    }
}
