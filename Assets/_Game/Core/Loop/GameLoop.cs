using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class GameLoop : IDisposable
    {
        private readonly GameServices _s;

        public GameLoop(GameServices services)
        {
            _s = services ?? throw new ArgumentNullException(nameof(services));
        }

        public void StartNewRun(int seed)
        {
            if (_s.RunOutcomeService is IResettable resetOutcome) resetOutcome.Reset();

            // Deterministic initial run clock state
            _s.RunClock.ForceSeasonDay(Season.Spring, dayIndex: 1);
            _s.RunClock.SetTimeScale(1f);

            // TODO: generate start map + spawn initial buildings/NPCs using WorldOps.
        }

        public void Tick(float dt) => TickOrder.TickAll(_s, dt);

        public void Dispose()
        {
        }
    }
}
