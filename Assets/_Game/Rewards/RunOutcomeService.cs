// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class RunOutcomeService : IRunOutcomeService
    {
        private readonly IEventBus _bus;
        public RunOutcome Outcome { get; private set; } = RunOutcome.Ongoing;

        public event System.Action<RunOutcome> OnRunEnded;

        public RunOutcomeService(IEventBus bus){ _bus = bus; }

        public void Reset() => Outcome = RunOutcome.Ongoing;

        public void Defeat()
        {
            if (Outcome != RunOutcome.Ongoing) return;
            Outcome = RunOutcome.Defeat;
            OnRunEnded?.Invoke(Outcome);
            _bus.Publish(new RunEndedEvent(Outcome));
        }

        public void Victory()
        {
            if (Outcome != RunOutcome.Ongoing) return;
            Outcome = RunOutcome.Victory;
            OnRunEnded?.Invoke(Outcome);
            _bus.Publish(new RunEndedEvent(Outcome));
        }

        public void Abort()
        {
            if (Outcome != RunOutcome.Ongoing) return;
            Outcome = RunOutcome.Abort;
            OnRunEnded?.Invoke(Outcome);
            _bus.Publish(new RunEndedEvent(Outcome));
        }

        public void Tick(float dt)
        {
            // TODO: if HQ hp <=0 -> Defeat
        }
    }
}
