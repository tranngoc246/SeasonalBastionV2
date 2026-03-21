namespace SeasonalBastion
{
    internal static class DataRegistryLoader
    {
        internal static void LoadAll(DataRegistry registry, DefsCatalog catalog)
        {
            if (registry == null) return;

            registry.ClearAll();

            registry.LoadBuildingsInternal(catalog != null ? catalog.Buildings : null);
            registry.LoadNpcsInternal(catalog != null ? catalog.Npcs : null);
            registry.LoadTowersInternal(catalog != null ? catalog.Towers : null);
            registry.LoadEnemiesInternal(catalog != null ? catalog.Enemies : null);
            registry.LoadRecipesInternal(catalog != null ? catalog.Recipes : null);
            registry.LoadWavesInternal(catalog != null ? catalog.Waves : null);
            registry.LoadRewardsInternal(catalog != null ? catalog.Rewards : null);
            registry.LoadBalanceInternal(catalog != null ? catalog.Balance : null);
            registry.LoadBuildablesGraphInternal(catalog != null ? catalog.BuildablesGraph : null);

            registry.ReportLoadErrorsIfAny();
        }
    }
}
