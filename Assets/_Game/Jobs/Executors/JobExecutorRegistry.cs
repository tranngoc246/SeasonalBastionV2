using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class JobExecutorRegistry
    {
        private readonly System.Collections.Generic.Dictionary<JobArchetype, IJobExecutor> _map = new();

        public JobExecutorRegistry(GameServices s)
        {
            _map[JobArchetype.Harvest] = new HarvestExecutor(s.WorldState, s.StorageService, s.AgentMover, s.NotificationService, s.ClaimService, s.ResourcePatchService, s.Pathfinder, s.DataRegistry, s.GridMap);
            _map[JobArchetype.HaulBasic] = new HaulBasicExecutor(s.WorldState, s.StorageService, s.AgentMover, s.WorldIndex, s.Pathfinder, s.Balance, s.DataRegistry, s.GridMap);
            _map[JobArchetype.HaulToForge] = new HaulToForgeExecutor(s.WorldState, s.StorageService, s.ResourceFlowService, s.AgentMover, s.ClaimService, s.Balance, s.DataRegistry, s.GridMap);
            _map[JobArchetype.BuildDeliver] = new BuildDeliverExecutor(s.WorldState, s.StorageService, s.WorldIndex, s.AgentMover, s.Balance, s.ClaimService, s.EventBus, s.DataRegistry, s.GridMap, s.Pathfinder);
            _map[JobArchetype.BuildWork] = new BuildWorkExecutor(s);
            _map[JobArchetype.RepairWork] = new RepairWorkExecutor(s.WorldState, s.AgentMover, s.DataRegistry, s.StorageService, s.WorldIndex, s.Balance, s.GridMap);
            _map[JobArchetype.CraftAmmo] = new CraftAmmoExecutor(s.WorldState, s.StorageService, s.AgentMover, s.DataRegistry, s.EventBus, s.Balance, s.GridMap);
            _map[JobArchetype.HaulAmmoToArmory] = new HaulAmmoToArmoryExecutor(s.WorldState, s.StorageService, s.AgentMover, s.Balance, s.DataRegistry, s.GridMap);
            _map[JobArchetype.ResupplyTower] = new ResupplyTowerExecutor(s.WorldState, s.StorageService, s.AgentMover, s.DataRegistry, s.GridMap, s.AmmoService);
        }

        public IJobExecutor Get(JobArchetype a) => _map[a];
    }
}
