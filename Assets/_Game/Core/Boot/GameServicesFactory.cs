using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public static class GameServicesFactory
    {
        public static GameServices Create(DefsCatalog catalog)
        {
            var services = new GameServices();

            // Core
            services.EventBus = new EventBus();
            services.DataRegistry = new DataRegistry(catalog);
            services.DataValidator = new DataValidator();
            services.RunClock = new RunClockService(services.EventBus);
            services.NotificationService = new NotificationService(services.EventBus);

            // World
            services.WorldState = new WorldState();
            services.WorldOps = new WorldOps(services.WorldState, services.EventBus);
            services.WorldIndex = new WorldIndexService(services.WorldState, services.DataRegistry);
            // Keep derived lists in sync (idempotent; safe with construction flow).
            services.WorldIndex.RebuildAll();
            services.EventBus.Subscribe<BuildingPlacedEvent>(ev => services.WorldIndex.OnBuildingCreated(ev.Building));


            // Grid
            services.GridMap = new GridMap(width: 64, height: 64);
            services.PlacementService = new PlacementService(services.GridMap, services.WorldState, services.DataRegistry, services.WorldIndex, services.EventBus);

            // Economy
            services.StorageService = new StorageService(services.WorldState, services.DataRegistry, services.EventBus);
            services.ResourceFlowService = new ResourceFlowService(services.WorldState, services.WorldIndex, services.StorageService);

            // Jobs
            services.ClaimService = new ClaimService();
            services.JobBoard = new JobBoard();
            var executorRegistry = new JobExecutorRegistry(services);
            services.JobScheduler = new JobScheduler(
                services.WorldState,
                services.JobBoard,
                services.ClaimService,
                executorRegistry,
                services.EventBus,
                services.DataRegistry,
                services.NotificationService);

            // Build
            services.BuildOrderService = new BuildOrderService(services);

            // bind build orders into placement (so CommitBuilding routes to construction)
            if (services.PlacementService is PlacementService ps)
                ps.BindBuildOrders(services.BuildOrderService);

            // Ammo & Combat
            services.AmmoService = new AmmoService(services);
            services.CombatService = new CombatService(services);

            // Rewards & Outcome
            services.RewardService = new RewardService(services);
            services.RunOutcomeService = new RunOutcomeService(services.EventBus);

            // Save
            services.SaveService = new SaveService(new SaveMigrator(), services.DataRegistry);

            return services;
        }
    }
}
