// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class JobScheduler : IJobScheduler
    {
        private readonly IWorldState _w;
        private readonly IJobBoard _board;
        private readonly IClaimService _claims;
        private readonly JobExecutorRegistry _exec;
        private readonly IEventBus _bus;

        public int AssignedThisTick { get; private set; }

        public JobScheduler(IWorldState w, IJobBoard board, IClaimService claims, JobExecutorRegistry exec, IEventBus bus)
        { _w = w; _board = board; _claims = claims; _exec = exec; _bus = bus; }

        public void Tick(float dt)
        {
            AssignedThisTick = 0;

            // 1) assign jobs to idle NPCs (by workplace)
            // 2) tick current jobs (executor)
            // 3) cleanup completed/failed, release claims
            // TODO: deterministic iteration order (npc id ascending)
        }

        public bool TryAssign(NpcId npc, out Job assigned)
        {
            assigned = default;
            // TODO: find workplace, peek job, claim, set NPC state
            return false;
        }
    }
}
