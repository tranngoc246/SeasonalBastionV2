using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// Run outcome evaluator.
    /// NOTE: Do NOT implement internal interfaces (e.g. IResettable) because Rewards asmdef
    /// may not have access. New runs should reset outcome via scene reload (M0-M2) or by
    /// constructing a fresh GameServices container.
    /// </summary>
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

        /// <summary>
        /// Optional manual reset (not via interface), useful if you ever restart runs without scene reload.
        /// </summary>
        public void ResetOutcome()
        {
            Outcome = RunOutcome.Ongoing;
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

            // Defeat rule: HQ HP <= 0
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

            // Victory = survive hết Winter (day cuối) của Year 2.
            // (Mini demo có thể rút gọn ở milestone sau.)
            if (e.YearIndex == 2 && e.Season == Season.Winter && e.DayIndex >= 4)
            {
                Victory();
            }
        }

        private bool IsHQ(string defId)
        {
            if (string.IsNullOrEmpty(defId)) return false;

            // Fallback hard-coded by canonical base id
            if (DefIdTierUtil.IsBase(defId, "bld_hq")) return true;

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
            _bus?.Publish(new RunEndedEvent(Outcome));
            OnRunEnded?.Invoke(Outcome);
        }

        public void Victory()
        {
            if (Outcome != RunOutcome.Ongoing) return;
            Outcome = RunOutcome.Victory;
            _bus?.Publish(new RunEndedEvent(Outcome));
            OnRunEnded?.Invoke(Outcome);
        }

        public void Abort()
        {
            if (Outcome != RunOutcome.Ongoing) return;
            Outcome = RunOutcome.Abort;
            _bus?.Publish(new RunEndedEvent(Outcome));
            OnRunEnded?.Invoke(Outcome);
        }
    }
}
