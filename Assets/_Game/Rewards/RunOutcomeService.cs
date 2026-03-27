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
        public RunEndReason Reason { get; private set; } = RunEndReason.None;

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
            Reason = RunEndReason.None;
        }

        private void EnsureSubscribed()
        {
            if (_subscribed) return;
            _subscribed = true;

            _bus?.Subscribe<WaveEndedEvent>(OnWaveEnded);
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
                    DefeatInternal(RunEndReason.HqDestroyed);
                    return;
                }
            }
        }

        private void OnWaveEnded(WaveEndedEvent e)
        {
            if (Outcome != RunOutcome.Ongoing) return;
            if (e.Year != 2) return;
            if (!e.IsFinalWave) return;

            VictoryInternal(RunEndReason.FinalWaveCleared);
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

        public void Defeat() => DefeatInternal(RunEndReason.HqDestroyed);

        public void Victory() => VictoryInternal(RunEndReason.FinalWaveCleared);

        public void Abort()
        {
            if (Outcome != RunOutcome.Ongoing) return;
            Outcome = RunOutcome.Abort;
            Reason = RunEndReason.Aborted;
            _bus?.Publish(new RunEndedEvent(Outcome, Reason));
            OnRunEnded?.Invoke(Outcome);
        }

        private void DefeatInternal(RunEndReason reason)
        {
            if (Outcome != RunOutcome.Ongoing) return;
            Outcome = RunOutcome.Defeat;
            Reason = reason;
            _bus?.Publish(new RunEndedEvent(Outcome, Reason));
            OnRunEnded?.Invoke(Outcome);
        }

        private void VictoryInternal(RunEndReason reason)
        {
            if (Outcome != RunOutcome.Ongoing) return;
            Outcome = RunOutcome.Victory;
            Reason = reason;
            _bus?.Publish(new RunEndedEvent(Outcome, Reason));
            OnRunEnded?.Invoke(Outcome);
        }
    }
}
