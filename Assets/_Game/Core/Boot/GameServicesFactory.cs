using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public static class GameServicesFactory
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

        private static void ComposeCore(GameServices services, DefsCatalog catalog)
        {
            services.EventBus = new EventBus();
            services.DataRegistry = new DataRegistry(catalog);
            services.DataValidator = new DataValidator();
            var dr = services.DataRegistry as DataRegistry;
            services.Balance = new BalanceService(services, dr != null ? dr.GetBalanceOrNull() : null);
            services.RunClock = new RunClockService(services.EventBus);
            var unlockJson = UnityEngine.Resources.Load<UnityEngine.TextAsset>("UnlockSchedule_v0_1");
            services.UnlockService = new UnlockService(services.RunClock, unlockJson, services.EventBus);
            services.NotificationService = new NotificationService(services.EventBus);
            services.TutorialHints = new TutorialHintsService(services);
            services.SeasonMetrics = new SeasonMetricsService(services.EventBus);
        }

        private static void ComposeRunStartAndWorld(GameServices services)
        {
            services.RunStartRuntime = new RunStartRuntime();

            services.WorldState = new WorldState();
            services.JobBoard = new JobBoard();
            services.WorldIndex = new WorldIndexService(services.WorldState, services.DataRegistry);
            services.WorldOps = new WorldOps(services.WorldState, services.EventBus, services.DataRegistry, services.WorldIndex, services.JobBoard);
            services.WorldIndex.RebuildAll();
        }

        private static void ComposeGrid(GameServices services, MapSize mapSize)
        {
            services.RuntimeMapSize = mapSize;
            services.GridMap = new GridMap(width: mapSize.Width, height: mapSize.Height);
            services.TerrainMap = new TerrainMap(width: mapSize.Width, height: mapSize.Height);
            services.ResourcePatchService = new ResourcePatchService();

            services.Pathfinder = new NpcPathfinder(services.GridMap, services.TerrainMap);
            services.AgentMover = new GridAgentMoverLite(services.GridMap, services.DataRegistry, services.Balance, services.TerrainMap);
            services.EventBus.Subscribe<RoadsDirtyEvent>(_ => services.AgentMover?.NotifyRoadsDirty());

            services.PlacementService = new PlacementService(services.GridMap, services.WorldState, services.DataRegistry, services.WorldIndex, services.EventBus, services.TerrainMap);
            ((PlacementService)services.PlacementService).BindRunStart(services.RunStartRuntime);
        }

        private static void ComposeEconomyAndJobs(GameServices services)
        {
            services.StorageService = new StorageService(services.WorldState, services.DataRegistry, services.EventBus);
            services.ResourceFlowService = new ResourceFlowService(services.WorldState, services.WorldIndex, services.StorageService, services.Pathfinder);
            services.PopulationService = new PopulationService(services);

            services.ClaimService = new ClaimService();
            services.JobWorkplacePolicy = new JobWorkplacePolicy(services.DataRegistry);
            var executorRegistry = new JobExecutorRegistry(services);
            services.JobScheduler = new JobScheduler(services, services.WorldState, services.JobBoard, services.ClaimService, executorRegistry, services.EventBus, services.DataRegistry, services.NotificationService, services.JobWorkplacePolicy);

            // P0: disable ProducerLoopService to avoid duplicate/invalid Harvest jobs (JobScheduler is the single source)
            services.ProducerLoopService = null;
        }

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
