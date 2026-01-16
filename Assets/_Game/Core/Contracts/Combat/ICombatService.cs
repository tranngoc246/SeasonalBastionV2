// AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1)
// Source: PART25_Technical_InterfacesPack_Services_Events_DTOs_LOCKED_SPEC_v0.1.md
// Notes:
// - Contracts only: interfaces/enums/structs/DTO/events.
// - Do not put runtime logic here.
// - Namespace kept unified to minimize cross-namespace friction.

using System;
using System.Collections.Generic;

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

        event System.Action<string> OnWaveStarted;
        event System.Action<string> OnWaveEnded;
    }
}
