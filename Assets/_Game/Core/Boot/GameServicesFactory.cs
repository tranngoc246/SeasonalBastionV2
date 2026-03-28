using SeasonalBastion.Contracts;
using SeasonalBastion.RunStart;

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
            var dr = services.DataRegistry as DataRegistry;
            services.Balance = new BalanceService(services, dr != null ? dr.GetBalanceOrNull() : null);
            services.RunClock = new RunClockService(services.EventBus);
            var unlockJson = UnityEngine.Resources.Load<UnityEngine.TextAsset>("UnlockSchedule_v0_1");
            services.UnlockService = new UnlockService(services.RunClock, unlockJson, services.EventBus);
            services.NotificationService = new NotificationService(services.EventBus);
            services.TutorialHints = new TutorialHintsService(services);
            services.SeasonMetrics = new SeasonMetricsService(services.EventBus);

            // RunStart
            services.RunStartRuntime = new RunStartRuntime();

            // World
            services.WorldState = new WorldState();
            services.WorldOps = new WorldOps(services.WorldState, services.EventBus);
            services.WorldIndex = new WorldIndexService(services.WorldState, services.DataRegistry);
            // Keep derived lists in sync (idempotent; safe with construction flow).
            services.WorldIndex.RebuildAll();
            services.EventBus.Subscribe<BuildingPlacedEvent>(ev => services.WorldIndex.OnBuildingCreated(ev.Building));

            // Grid
            services.GridMap = new GridMap(width: 64, height: 64);

            // Day14: simple mover (cell-by-cell)
            services.AgentMover = new GridAgentMoverLite(services.GridMap, services.DataRegistry, services.Balance);
            services.EventBus.Subscribe<RoadsDirtyEvent>(_ => services.AgentMover?.NotifyRoadsDirty());

            services.PlacementService = new PlacementService(services.GridMap, services.WorldState, services.DataRegistry, services.WorldIndex, services.EventBus);
            ((PlacementService)services.PlacementService).BindRunStart(services.RunStartRuntime);

            // Economy
            services.StorageService = new StorageService(services.WorldState, services.DataRegistry, services.EventBus);
            services.ResourceFlowService = new ResourceFlowService(services.WorldState, services.WorldIndex, services.StorageService, services.GridMap);
            services.PopulationService = new PopulationService(services);
            
            // Jobs
            services.ClaimService = new ClaimService();
            services.JobBoard = new JobBoard();
            services.JobWorkplacePolicy = new JobWorkplacePolicy(services.DataRegistry);
            var executorRegistry = new JobExecutorRegistry(services);
            services.JobScheduler = new JobScheduler( services, services.WorldState, services.JobBoard, services.ClaimService, executorRegistry, services.EventBus, services.DataRegistry, services.NotificationService, services.JobWorkplacePolicy);

            //services.ProducerLoopService = new ProducerLoopService(services.WorldState, services.DataRegistry, services.StorageService, services.JobBoard, services.NotificationService, services.RunClock);
            // P0: disable ProducerLoopService to avoid duplicate/invalid Harvest jobs (JobScheduler is the single source)
            services.ProducerLoopService = null;

            // Build
            services.BuildWorkplaceResolver = new BuildOrderWorkplaceResolver(services);
            services.BuildOrderService = new BuildOrderService(services);

            // bind build orders into placement (so CommitBuilding routes to construction)
            if (services.PlacementService is PlacementService ps)
                ps.BindBuildOrders(services.BuildOrderService);

            // Ammo & Combat
            services.AmmoService = new AmmoService(services);
            services.CombatService = new CombatService(services);
            services.WaveCalendarResolver = new WaveCalendarResolver(services.DataRegistry);

            // Rewards & Outcome
            services.RewardService = new RewardService(services);
            services.RunOutcomeService = new RunOutcomeService(services.EventBus, services.WorldState, services.DataRegistry);

            // Save
            var saveMigrator = new SaveMigrator();
            services.SaveService = new SaveService(new SaveMigrator(), services.DataRegistry, services.GridMap, services.PopulationService);

            return services;
        }
    }
}
