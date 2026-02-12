using System;

namespace SeasonalBastion.Contracts
{
    public interface ICombatService
    {
        bool IsActive { get; }
        void OnDefendPhaseStarted();
        void OnDefendPhaseEnded();

        void Tick(float dt);

        // Debug
        void SpawnWave(string waveDefId);
        void KillAllEnemies();

        // VS3: Wave debug helper (align with CombatService.ForceResolveWave)
        void ForceResolveWave();

        event Action<string> OnWaveStarted;
        event Action<string> OnWaveEnded;
    }
}