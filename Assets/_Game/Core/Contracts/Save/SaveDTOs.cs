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

        public int yearIndex;    
        public float dayTimer;   

        public WorldDTO world;
        public BuildDTO build;
        public CombatDTO combat;
        public RewardsDTO rewards;
    }

    public sealed class MetaSaveDTO
    {
        public int schemaVersion;
        public int currency;
        public List<string> unlockIds;
        public Dictionary<string,int> perkLevels;
    }
}
