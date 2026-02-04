using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class WorldState : IWorldState
    {
        public IBuildingStore Buildings { get; } = new BuildingStore();
        public INpcStore Npcs { get; } = new NpcStore();
        public ITowerStore Towers { get; } = new TowerStore();
        public IEnemyStore Enemies { get; } = new EnemyStore();
        public IBuildSiteStore Sites { get; } = new BuildSiteStore();
        public IZoneStore Zones { get; } = new ZoneStore();
        public IResourcePileStore Piles { get; } = new ResourcePileStore();

        private RunModifiers _mods;
        public ref RunModifiers RunMods => ref _mods;
    }
}
