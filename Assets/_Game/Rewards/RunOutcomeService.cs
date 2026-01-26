// _Game/Rewards/RunOutcomeService.cs
using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class RunOutcomeService : IRunOutcomeService, ITickable
    {
        private readonly IEventBus _bus;
        private readonly IWorldState _world;
        private readonly IDataRegistry _data;

        public RunOutcome Outcome { get; private set; } = RunOutcome.Ongoing;

        public event Action<RunOutcome> OnRunEnded;

        private bool _subscribed;

        public RunOutcomeService(IEventBus bus, IWorldState world, IDataRegistry data)
        {
            _bus = bus;
            _world = world;
            _data = data;

            EnsureSubscribed();
        }

        private void EnsureSubscribed()
        {
            if (_subscribed) return;
            _subscribed = true;

            _bus?.Subscribe<DayEndedEvent>(OnDayEnded);
        }

        public void Tick(float dt)
        {
            if (Outcome != RunOutcome.Ongoing) return;
            if (_world?.Buildings == null) return;

            // Defeat rule: any HQ HP <= 0
            foreach (var id in _world.Buildings.Ids)
            {
                var b = _world.Buildings.Get(id);
                if (!IsHQ(b.DefId)) continue;

                if (b.HP <= 0)
                {
                    Defeat();
                    return;
                }
            }
        }

        private void OnDayEnded(DayEndedEvent e)
        {
            if (Outcome != RunOutcome.Ongoing) return;

            // Victory rule (v0.1): survive hết Winter Y1 (Winter has 4 days in current RunClock)
            if (e.YearIndex == 1 && e.Season == Season.Winter && e.DayIndex >= 4)
            {
                Victory();
            }
        }

        private bool IsHQ(string defId)
        {
            if (string.IsNullOrEmpty(defId)) return false;
            if (defId == "HQ") return true;

            try
            {
                var def = _data.GetBuilding(defId);
                return def.IsHQ;
            }
            catch
            {
                return false;
            }
        }

        public void Defeat()
        {
            if (Outcome != RunOutcome.Ongoing) return;
            Outcome = RunOutcome.Defeat;
            OnRunEnded?.Invoke(Outcome);
        }

        public void Victory()
        {
            if (Outcome != RunOutcome.Ongoing) return;
            Outcome = RunOutcome.Victory;
            OnRunEnded?.Invoke(Outcome);
        }

        public void Abort()
        {
            if (Outcome != RunOutcome.Ongoing) return;
            Outcome = RunOutcome.Abort;
            OnRunEnded?.Invoke(Outcome);
        }
    }
}
