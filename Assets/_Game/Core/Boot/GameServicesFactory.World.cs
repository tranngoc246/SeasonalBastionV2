using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public static partial class GameServicesFactory
    {
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
    }
}
