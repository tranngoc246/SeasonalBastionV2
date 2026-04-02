namespace SeasonalBastion.Contracts
{
    public readonly struct BuildingPlacedEvent
    {
        public readonly string DefId;
        public readonly BuildingId Building;
        public BuildingPlacedEvent(string d, BuildingId b){DefId=d;Building=b;}
    }

    public readonly struct BuildingDestroyedEvent
    {
        public readonly string DefId;
        public readonly BuildingId Building;
        public BuildingDestroyedEvent(string d, BuildingId b){DefId=d;Building=b;}
    }

    public readonly struct BuildingUpgradedEvent
    {
        public readonly string FromDefId;
        public readonly string ToDefId;
        public readonly BuildingId Building;
        public BuildingUpgradedEvent(string from, string to, BuildingId b)
        { FromDefId = from; ToDefId = to; Building = b; }
    }

    public readonly struct RoadPlacedEvent
    {
        public readonly CellPos Cell;
        public RoadPlacedEvent(CellPos c){Cell=c;}
    }

    public readonly struct WorldStateChangedEvent
    {
        public readonly string EntityKind;
        public readonly int EntityId;
        public WorldStateChangedEvent(string entityKind, int entityId)
        {
            EntityKind = entityKind;
            EntityId = entityId;
        }
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
        public readonly int Year;
        public readonly Season Season;
        public readonly int Day;
        public readonly bool IsBoss;
        public readonly bool IsFinalWave;

        public WaveEndedEvent(string waveId, int year, Season season, int day, bool isBoss, bool isFinalWave)
        {
            WaveId = waveId;
            Year = year;
            Season = season;
            Day = day;
            IsBoss = isBoss;
            IsFinalWave = isFinalWave;
        }
    }

    public readonly struct RunEndedEvent
    {
        public readonly RunOutcome Outcome;
        public readonly RunEndReason Reason;
        public RunEndedEvent(RunOutcome outcome, RunEndReason reason)
        {
            Outcome = outcome;
            Reason = reason;
        }
    }

    public readonly struct RewardPickedEvent
    {
        public readonly string RewardId;
        public RewardPickedEvent(string id){RewardId=id;}
    }

    public readonly struct EndSeasonRewardRequested
    {
        public readonly Season Season;
        public readonly int YearIndex;
        public readonly int DayIndex; 
        public EndSeasonRewardRequested(Season s, int yearIndex, int dayIndex) { Season = s; YearIndex = yearIndex; DayIndex = dayIndex; }
    }

    public readonly struct ResourceSpentEvent
    {
        public readonly ResourceType Type;
        public readonly int Amount;
        public readonly BuildingId Source;
        public ResourceSpentEvent(ResourceType t, int amt, BuildingId src) { Type = t; Amount = amt; Source = src; }
    }

    public readonly struct EnemyKilledEvent
    {
        public readonly string EnemyDefId;
        public readonly int Count;
        public EnemyKilledEvent(string defId, int count) { EnemyDefId = defId; Count = count; }
    }

    public readonly struct AmmoUsedEvent
    {
        public readonly int Amount;
        public AmmoUsedEvent(int amt) { Amount = amt; }

        public readonly struct UnlocksChangedEvent
        {
            public readonly int Hash;
            public UnlocksChangedEvent(int hash) { Hash = hash; }
        }
    }
}
