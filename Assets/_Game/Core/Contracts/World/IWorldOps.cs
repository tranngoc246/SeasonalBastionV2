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
    public interface IWorldOps
    {
        BuildingId CreateBuilding(string buildingDefId, CellPos anchor, Dir4 rotation);
        void DestroyBuilding(BuildingId id);

        NpcId CreateNpc(string npcDefId, CellPos spawn);
        void DestroyNpc(NpcId id);

        EnemyId CreateEnemy(string enemyDefId, CellPos spawn, int lane);
        void DestroyEnemy(EnemyId id);

        SiteId CreateBuildSite(string buildingDefId, CellPos anchor, Dir4 rotation);
        void DestroyBuildSite(SiteId id);
    }
}
