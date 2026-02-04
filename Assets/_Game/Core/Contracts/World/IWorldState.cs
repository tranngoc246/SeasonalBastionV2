namespace SeasonalBastion.Contracts
{
    public interface IWorldState
    {
        // Stores
        IBuildingStore Buildings { get; }
        INpcStore Npcs { get; }
        ITowerStore Towers { get; }
        IEnemyStore Enemies { get; }
        IBuildSiteStore Sites { get; }

        IZoneStore Zones { get; }
        IResourcePileStore Piles { get; }

        ref RunModifiers RunMods { get; }  
    }
}
