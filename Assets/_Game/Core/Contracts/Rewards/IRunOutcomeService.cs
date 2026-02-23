using System;

namespace SeasonalBastion.Contracts
{
    public interface IRunOutcomeService
    {
        RunOutcome Outcome { get; }
        void ResetOutcome();
        void Defeat();
        void Victory();
        void Abort();

        event Action<RunOutcome> OnRunEnded;
    }
}
