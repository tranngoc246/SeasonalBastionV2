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
