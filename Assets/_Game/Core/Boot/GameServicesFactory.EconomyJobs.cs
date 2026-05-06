using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public static partial class GameServicesFactory
    {
        private static void ComposeEconomyAndJobs(GameServices services)
        {
            services.StorageService = new StorageService(services.WorldState, services.DataRegistry, services.EventBus);
            services.ResourceFlowService = new ResourceFlowService(services.WorldState, services.WorldIndex, services.StorageService, services.Pathfinder);
            services.PopulationService = new PopulationService(
                services.EventBus,
                services.DataRegistry,
                services.RunClock,
                services.NotificationService,
                services.WorldState,
                services.GridMap,
                services.StorageService,
                services.RunOutcomeService);

            services.ClaimService = new ClaimService();
            services.JobWorkplacePolicy = new JobWorkplacePolicy(services.DataRegistry);
            var executorRegistry = new JobExecutorRegistry(services);
            services.JobScheduler = new JobScheduler(services, services.WorldState, services.JobBoard, services.ClaimService, executorRegistry, services.EventBus, services.DataRegistry, services.NotificationService, services.JobWorkplacePolicy);

            // P0: disable ProducerLoopService to avoid duplicate/invalid Harvest jobs (JobScheduler is the single source)
            services.ProducerLoopService = null;
        }
    }
}
