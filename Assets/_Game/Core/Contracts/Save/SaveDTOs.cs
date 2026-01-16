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
    public sealed class RunSaveDTO
    {
        public int schemaVersion;
        public int seed;

        public string season;
        public int dayIndex;
        public float timeScale;

        public WorldDTO world;
        public BuildDTO build;
        public CombatDTO combat;
        public RewardsDTO rewards;
    }

    public sealed class MetaSaveDTO
    {
        public int schemaVersion;
        public int currency;
        public System.Collections.Generic.List<string> unlockIds;
        public System.Collections.Generic.Dictionary<string,int> perkLevels;
    }
}
