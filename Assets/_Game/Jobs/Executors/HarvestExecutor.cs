// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class HarvestExecutor : IJobExecutor
    {
        private readonly GameServices _s;
        public HarvestExecutor(GameServices s){ _s = s; }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            // TODO:
            // - move to producer target
            // - work timer
            // - on complete: add to producer local storage
            // - mark job completed
            return false;
        }
    }
}
