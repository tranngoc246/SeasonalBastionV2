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
    public interface IJobBoard
    {
        JobId Enqueue(Job job);
        bool TryPeekForWorkplace(BuildingId workplace, out Job job); // deterministic order
        bool TryClaim(JobId id, NpcId npc);

        bool TryGet(JobId id, out Job job);
        void Update(Job job);        // status transitions
        void Cancel(JobId id);

        int CountForWorkplace(BuildingId workplace);
    }
}
