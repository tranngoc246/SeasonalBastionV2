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

        // Global modifiers
        ref RunModifiers RunMods { get; }  // from Part 12
    }
}
