// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
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
            _map[JobArchetype.BuildDeliver] = new BuildDeliverExecutor(s);
            _map[JobArchetype.BuildWork] = new BuildWorkExecutor(s);
            _map[JobArchetype.RepairWork] = new RepairWorkExecutor(s);
            _map[JobArchetype.CraftAmmo] = new CraftAmmoExecutor(s);
            _map[JobArchetype.ResupplyTower] = new ResupplyTowerExecutor(s);
            // Leisure/Inspect optional later
        }

        public IJobExecutor Get(JobArchetype a) => _map[a];
    }
}
