using SeasonalBastion.Contracts;
using SeasonalBastion.RunStart;

namespace SeasonalBastion
{
    /// <summary>
    /// Runtime service container (composition root fills these references).
    /// IMPORTANT: This is NOT a contracts type. It lives in runtime (Game.Core).
    /// </summary>
    public sealed class GameServices
    {
        // Core
        public IEventBus EventBus;
        public IDataRegistry DataRegistry;
        public IDataValidator DataValidator;
        public IRunClock RunClock;
        public INotificationService NotificationService;
        public IUnlockService UnlockService;
        public SeasonMetricsService SeasonMetrics;
        public TutorialHintsService TutorialHints;
        public BalanceService Balance;

        // World/Grid
        public IWorldState WorldState;
        public IWorldOps WorldOps;
        public IWorldIndex WorldIndex;
        public IGridMap GridMap;
        public IPlacementService PlacementService;
        public ResourcePatchService ResourcePatchService;

        // VS2: cached data loaded from StartMapConfig at StartNewRun()
        public RunStartRuntime RunStartRuntime;

        // Day14: simple mover/pathfinding (runtime-only)
        public GridAgentMoverLite AgentMover;
        public NpcPathfinder Pathfinder;

        // Economy/Jobs
        public ITickable ProducerLoopService; // Day36: producer loop (enqueue Harvest)
        public IStorageService StorageService;
        public IResourceFlowService ResourceFlowService;
        public IPopulationService PopulationService;
        public IClaimService ClaimService;
        public IJobBoard JobBoard;
        public IJobScheduler JobScheduler;

        // Build/Ammo/Combat
        public IBuildOrderService BuildOrderService;
        public IBuildWorkplaceResolver BuildWorkplaceResolver;
        public IBuildJobOrchestrator BuildJobOrchestrator;
        public IJobWorkplacePolicy JobWorkplacePolicy;
        public IAmmoService AmmoService;
        public ICombatService CombatService;
        public IWaveCalendarResolver WaveCalendarResolver;

        // Rewards/Save
        public IRewardService RewardService;
        public IRunOutcomeService RunOutcomeService;
        public ISaveService SaveService;

        // Optional
        public IAudioService AudioService;
        public IFXService FXService;
    }
}
