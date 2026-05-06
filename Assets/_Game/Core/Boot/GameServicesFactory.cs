using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public static partial class GameServicesFactory
    {
        public static GameServices Create(DefsCatalog catalog, MapSize? runtimeMapSize = null)
        {
            var services = new GameServices();
            var mapSize = runtimeMapSize ?? MapSize.Default;

            ComposeCore(services, catalog);
            ComposeRunStartAndWorld(services);
            ComposeGrid(services, mapSize);
            ComposeEconomyAndJobs(services);
            ComposeBuild(services);
            ComposeCombatAndRewards(services);
            ComposeSave(services);

            return services;
        }
    }
}
