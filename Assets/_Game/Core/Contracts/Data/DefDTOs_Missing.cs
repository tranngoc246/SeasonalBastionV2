// PATCH v0.1.1 — Missing contract types to unblock compilation (Unity 2022.3)
// This patch only adds placeholder contract DTOs/Ids/States referenced by Part 25 interfaces.
// Keep these as pure data (no UnityEngine, no runtime logic).
using System;
using System.Collections.Generic;

namespace SeasonalBastion.Contracts
{

/// <summary>
/// Minimal DEF DTO placeholders referenced by IDataRegistry and other contracts.
/// Replace/expand later with your real JSON schema (Part 1/17).
/// </summary>
public sealed class BuildingDef
{
    public string DefId = "";
    public int SizeX = 1;
    public int SizeY = 1;
    public int BaseLevel = 1;
}

public sealed class EnemyDef
{
    public string DefId = "";
    public int MaxHp = 1;
    public float MoveSpeed = 1f;
}

public sealed class WaveDef
{
    public string DefId = "";
    public int WaveIndex = 0;
}

public sealed class RewardDef
{
    public string DefId = "";
    public string Title = "";
}

public sealed class RecipeDef
{
    public string DefId = "";
    public ResourceType InputType;
    public int InputAmount = 1;
    public int OutputAmount = 1; // e.g., ammo count
}

/// <summary>Minimal cost definition used by build / site / validators.</summary>
public sealed class CostDef
{
    public ResourceType Resource;
    public int Amount;
}

}
