using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public static partial class GameServicesFactory
    {
        private static void ComposeBuild(GameServices services)
        {
            services.BuildWorkplaceResolver = new BuildOrderWorkplaceResolver(services);
            services.BuildOrderService = new BuildOrderService(services);

            if (services.PlacementService is PlacementService ps)
                ps.BindBuildOrders(services.BuildOrderService);
        }

        private static void ComposeCombatAndRewards(GameServices services)
        {
            services.AmmoService = new AmmoService(services);
            services.CombatService = new CombatService(services);
            services.WaveCalendarResolver = new WaveCalendarResolver(services.DataRegistry);

            services.RewardService = new RewardService(services);
            services.RunOutcomeService = new RunOutcomeService(services.EventBus, services.WorldState, services.DataRegistry);
        }

        private static void ComposeSave(GameServices services)
        {
            services.SaveService = new SaveService(new SaveMigrator(), services.DataRegistry, services.GridMap, services.PopulationService, services);
            _ = new SaveAutosaveService(services);
        }
    }
}
