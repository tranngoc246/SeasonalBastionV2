using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class JobExecutorRegistry
    {
        private readonly System.Collections.Generic.Dictionary<JobArchetype, IJobExecutor> _map = new();

        public JobExecutorRegistry(GameServices s)
        {
            _map[JobArchetype.Harvest] = new HarvestExecutor(s);
            _map[JobArchetype.HaulBasic] = new HaulBasicExecutor(s);
            _map[JobArchetype.HaulToForge] = new HaulToForgeExecutor(s);
            _map[JobArchetype.BuildDeliver] = new BuildDeliverExecutor(s);
            _map[JobArchetype.BuildWork] = new BuildWorkExecutor(s);
            _map[JobArchetype.CraftAmmo] = new CraftAmmoExecutor(s);
            _map[JobArchetype.HaulAmmoToArmory] = new HaulAmmoToArmoryExecutor(s);
            _map[JobArchetype.ResupplyTower] = new ResupplyTowerExecutor(s);
            _map[JobArchetype.RepairWork] = new RepairWorkExecutor(s);
        }

        public IJobExecutor Get(JobArchetype a) => _map[a];
    }
}
