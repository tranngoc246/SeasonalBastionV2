using SeasonalBastion.Contracts;

namespace SeasonalBastion.Tests.EditMode
{
    public abstract class RegressionTestBase
    {
        protected static GameServices MakeServices(
            IEventBus bus,
            IDataRegistry data,
            INotificationService noti,
            IRunClock clock,
            IRunOutcomeService outcome,
            IWorldState world = null,
            IGridMap grid = null,
            IPlacementService placement = null)
        {
            var services = new GameServices
            {
                EventBus = bus,
                DataRegistry = data,
                NotificationService = noti,
                RunClock = clock,
                RunOutcomeService = outcome,
                WorldState = world,
                GridMap = grid,
                TerrainMap = grid != null ? new TerrainMap(grid.Width, grid.Height) : null,
                RuntimeMapSize = grid != null ? new MapSize(grid.Width, grid.Height) : default,
                PlacementService = placement
            };

            if (services.TerrainMap != null)
            {
                for (int y = 0; y < services.TerrainMap.Height; y++)
                    for (int x = 0; x < services.TerrainMap.Width; x++)
                        services.TerrainMap.Set(new CellPos(x, y), TerrainType.Land);
            }

            if (services.GridMap != null)
                services.Pathfinder = new NpcPathfinder(services.GridMap, services.TerrainMap);

            services.ApplyRunStartConfig = (s, cfg) =>
            {
                bool ok = SeasonalBastion.RunStart.RunStartFacade.TryApply(s, cfg, out var error);
                return (ok, error);
            };

            return services;
        }
    }
}
