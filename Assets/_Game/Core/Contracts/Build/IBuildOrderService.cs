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
    public interface IBuildOrderService
    {
        int CreatePlaceOrder(string buildingDefId, CellPos anchor, Dir4 rotation);
        int CreateUpgradeOrder(BuildingId building);
        int CreateRepairOrder(BuildingId building);

        bool TryGet(int orderId, out BuildOrder order);
        void Cancel(int orderId);

        // tick drives generating jobs (deliver/work)
        void Tick(float dt);

        event System.Action<int> OnOrderCompleted;
    }
}
