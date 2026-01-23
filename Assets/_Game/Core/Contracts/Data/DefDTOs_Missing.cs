using System;

namespace SeasonalBastion.Contracts
{
    [Flags]
    public enum WorkRoleFlags
    {
        None = 0,

        // Producer jobs
        Harvest = 1 << 0,

        // Logistics jobs
        HaulBasic = 1 << 1,

        // Construction jobs (Sprint 2+)
        Build = 1 << 2,

        // Craft / ammo pipeline (Sprint 3+)
        Craft = 1 << 3,

        // Armory / resupply pipeline (Sprint 3+)
        Armory = 1 << 4,
    }

    public sealed class BuildingDef
    {
        public string DefId = "";
        public int SizeX = 1;
        public int SizeY = 1;
        public int BaseLevel = 1;

        public bool IsHQ = false;
        public bool IsWarehouse = false;
        public bool IsProducer = false;
        public bool IsHouse = false;
        public bool IsForge = false;
        public bool IsArmory = false;
        public bool IsTower = false;

        // Day13: workplace role gating (LOCKED)
        // HQ: Build|HaulBasic (VS#1 uses HaulBasic)
        // Producer: Harvest
        // Warehouse: HaulBasic
        public WorkRoleFlags WorkRoles = WorkRoleFlags.None;

        public StorageCapsByLevel CapWood;
        public StorageCapsByLevel CapFood;
        public StorageCapsByLevel CapStone;
        public StorageCapsByLevel CapIron;
        public StorageCapsByLevel CapAmmo;

        // VS2 Day18: construction delivery gate (L1 build)
        public CostDef[] BuildCostsL1;
        public int BuildChunksL1;
    }

    [Serializable]
    public struct StorageCapsByLevel
    {
        public int L1;
        public int L2;
        public int L3;

        public int Get(int level)
        {
            return level switch
            {
                1 => L1,
                2 => L2,
                3 => L3,
                _ => L1
            };
        }
    }

    public sealed class EnemyDef
    {
        public string DefId = "";

        public int MaxHp = 1;
        public float MoveSpeed = 1f;

        // VS2 combat stats (v0.1)
        public int DamageToHQ = 1;
        public int DamageToBuildings = 1;
        public float Range = 0f; // 0 = melee

        // Optional boss tags
        public bool IsBoss = false;
        public int BossYear = 0;
        public Season BossSeason = Season.Spring;
        public int BossDay = 0;

        // Optional aura
        public float AuraSlowRofPct = 0f;
    }

    public sealed class WaveDef
    {
        public string DefId = "";
        public int WaveIndex = 0;

        // Calendar routing (v0.1)
        public int Year = 1;
        public Season Season = Season.Autumn;
        public int Day = 1;

        public bool IsBoss = false;
        public WaveEntryDef[] Entries;
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

        public ResourceType OutputType;
        public int OutputAmount = 1; // e.g., ammo count

        public CostDef[] ExtraInputs;
        public float CraftTimeSec = 0f;
    }

    [Serializable]
    public struct UnlockDef
    {
        public int Year;
        public Season Season;
        public int Day;
    }

    [Serializable]
    public struct WaveEntryDef
    {
        public string EnemyId;
        public int Count;
    }

    public sealed class NpcDef
    {
        public string DefId = "";
        public string Role = "";

        public float BaseMoveSpeed = 1f;
        public float RoadSpeedMultiplier = 1f;

        // Core carry (used by hauling/production in later days)
        public StorageCapsByLevel CarryCore;
    }

    public sealed class TowerDef
    {
        public string DefId = "";
        public int Tier = 1;

        public int MaxHp = 1;
        public float Range = 1f;
        public float Rof = 1f;
        public int Damage = 1;

        // Optional effects
        public float SlowPct = 0f;
        public float SlowSec = 0f;
        public string Aoe = "";
        public int DotDps = 0;
        public int DotSec = 0;

        // Ammo
        public int AmmoMax = 0;
        public int AmmoPerShot = 1;
        public float NeedsAmmoThresholdPct = 0.25f;

        // Build
        public CostDef[] BuildCost;
        public int BuildChunks = 1;
        public UnlockDef Unlock;
    }


    /// <summary>Minimal cost definition used by build / site / validators.</summary>
    public sealed class CostDef
    {
        public ResourceType Resource;
        public int Amount;
    }
}
