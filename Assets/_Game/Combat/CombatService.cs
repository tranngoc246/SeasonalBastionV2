// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class CombatService : ICombatService
    {
        private readonly GameServices _s;
        private readonly WaveDirector _waves;

        public bool IsActive { get; private set; }

        public event System.Action<string> OnWaveStarted;
        public event System.Action<string> OnWaveEnded;

        public CombatService(GameServices s)
        {
            _s = s;
            _waves = new WaveDirector(s);
        }

        public void OnDefendPhaseStarted()
        {
            IsActive = true;
            _waves.StartDayWaves(_s.RunClock.DayIndex);
        }

        public void OnDefendPhaseEnded()
        {
            IsActive = false;
            // TODO: cleanup enemies?
        }

        public void Tick(float dt)
        {
            if (!IsActive) return;

            _waves.Tick(dt);
            // TODO: tick enemies movement/attack
            // TODO: tick towers targeting/firing (ammo consume)
            // TODO: check wave end
        }

        public void SpawnWave(string waveDefId)
        {
            // TODO: dev hook
        }

        public void KillAllEnemies()
        {
            // TODO: clear enemies store
        }
    }
}
