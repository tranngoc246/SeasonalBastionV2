using System;
using System.Collections.Generic;

namespace SeasonalBastion.Contracts
{
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
