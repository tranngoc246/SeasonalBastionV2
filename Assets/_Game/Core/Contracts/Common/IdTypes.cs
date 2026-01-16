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
    public readonly struct BuildingId { public readonly int Value; public BuildingId(int v)=>Value=v; }

    public readonly struct NpcId      { public readonly int Value; public NpcId(int v)=>Value=v; }

    public readonly struct TowerId    { public readonly int Value; public TowerId(int v)=>Value=v; }

    public readonly struct EnemyId    { public readonly int Value; public EnemyId(int v)=>Value=v; }

    public readonly struct SiteId     { public readonly int Value; public SiteId(int v)=>Value=v; }

    public readonly struct JobId      { public readonly int Value; public JobId(int v)=>Value=v; }

    public readonly struct NotificationId
    {
        public readonly int Value;
        public NotificationId(int v){Value=v;}
    }
}
