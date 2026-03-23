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

    [Serializable]
    public struct CellPosI32
    {
        public int x;
        public int y;
        public CellPosI32(int x, int y) { this.x = x; this.y = y; }
    }

    public sealed class WorldDTO
    {
        public List<BuildingState> Buildings = new();
        public List<NpcState> Npcs = new();
        public List<TowerState> Towers = new();
        public List<EnemyState> Enemies = new();
        public List<CellPosI32> Roads = new();
    }

    public sealed class BuildDTO
    {
        public List<BuildSiteState> Sites = new();
    }

    public sealed class CombatDTO
    {
        public int CurrentWaveIndex;
        public bool IsDefendActive;
    }

    public sealed class RewardsDTO
    {
        public List<string> PickedRewardDefIds = new();
    }

}
