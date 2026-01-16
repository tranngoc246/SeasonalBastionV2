// PATCH v0.1.1 — Missing contract types to unblock compilation (Unity 2022.3)
// This patch only adds placeholder contract DTOs/Ids/States referenced by Part 25 interfaces.
// Keep these as pure data (no UnityEngine, no runtime logic).
using System;
using System.Collections.Generic;

namespace SeasonalBastion.Contracts
{

/// <summary>Minimal DTO groupings referenced by save/load contracts. Expand later.</summary>
public sealed class WorldDTO
{
    public List<BuildingState> Buildings = new();
    public List<NpcState> Npcs = new();
    public List<TowerState> Towers = new();
    public List<EnemyState> Enemies = new();
}

public sealed class BuildDTO
{
    public List<BuildSiteState> Sites = new();
}

public sealed class CombatDTO
{
    public int CurrentWaveIndex;
    public bool IsDefendActive;
}

public sealed class RewardsDTO
{
    public List<string> PickedRewardDefIds = new();
}

}
