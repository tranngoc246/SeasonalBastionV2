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
    public readonly struct BuildingPlacedEvent
    {
        public readonly string DefId;
        public readonly BuildingId Building;
        public BuildingPlacedEvent(string d, BuildingId b){DefId=d;Building=b;}
    }

    public readonly struct RoadPlacedEvent
    {
        public readonly CellPos Cell;
        public RoadPlacedEvent(CellPos c){Cell=c;}
    }

    public readonly struct NPCAssignedEvent
    {
        public readonly NpcId Npc;
        public readonly BuildingId Workplace;
        public NPCAssignedEvent(NpcId n, BuildingId w){Npc=n;Workplace=w;}
    }

    public readonly struct ResourceDeliveredEvent
    {
        public readonly ResourceType Type;
        public readonly int Amount;
        public readonly BuildingId Dest;
        public ResourceDeliveredEvent(ResourceType t,int a,BuildingId d){Type=t;Amount=a;Dest=d;}
    }

    public readonly struct WaveStartedEvent
    {
        public readonly string WaveId;
        public WaveStartedEvent(string id){WaveId=id;}
    }

    public readonly struct WaveEndedEvent
    {
        public readonly string WaveId;
        public WaveEndedEvent(string id){WaveId=id;}
    }

    public readonly struct RunEndedEvent
    {
        public readonly RunOutcome Outcome;
        public RunEndedEvent(RunOutcome o){Outcome=o;}
    }

    public readonly struct RewardPickedEvent
    {
        public readonly string RewardId;
        public RewardPickedEvent(string id){RewardId=id;}
    }
}
