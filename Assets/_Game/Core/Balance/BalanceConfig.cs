using System;

namespace SeasonalBastion
{
    [Serializable]
    public sealed class BalanceConfig
    {
        public string schema = "balance_v0.1";

        public Movement movement = new();
        public Carry carry = new();
        public Build build = new();
        public Repair repair = new();
        public BuilderWorkplace builderWorkplace = new();
        public WarehouseWorkplace warehouseWorkplace = new();
        public Armory armory = new();
        public Crafting crafting = new();
        public AmmoSupply ammoSupply = new();
        public AmmoMonitor ammoMonitor = new();

        [Serializable] public sealed class Level3Int { public int L1; public int L2; public int L3; }
        [Serializable] public sealed class Level3Float { public float L1 = 1f; public float L2 = 1f; public float L3 = 1f; }

        [Serializable]
        public sealed class Movement
        {
            public float defaultBaseMoveSpeed = 1f;
            public float defaultRoadSpeedMultiplier = 1.3f;
        }

        [Serializable]
        public sealed class Carry
        {
            public Level3Int harvest = new() { L1 = 6, L2 = 8, L3 = 10 };
            public Level3Int haulBasic = new() { L1 = 10, L2 = 14, L3 = 18 };
            public Level3Int builder = new() { L1 = 8, L2 = 12, L3 = 16 };
            public Level3Int armoryAmmo = new() { L1 = 40, L2 = 60, L3 = 80 };
        }

        [Serializable]
        public sealed class Build
        {
            public float workChunkSec = 6f;
            public int fallbackBuildChunksL1 = 2;
        }

        [Serializable]
        public sealed class Repair
        {
            public float workChunkSec = 4f;
            public float healPctPerChunk = 0.15f;
            public float costFactorOfBuildCost = 0.30f;
        }

        [Serializable]
        public sealed class BuilderWorkplace
        {
            public string[] builderDefIds;
            public Level3Float buildSpeedMult = new() { L1 = 1f, L2 = 0.85f, L3 = 0.70f };
            public Level3Float repairTimeMult = new() { L1 = 1f, L2 = 0.85f, L3 = 0.70f };
            public Level3Float repairCostMult = new() { L1 = 1f, L2 = 0.85f, L3 = 0.70f };
        }

        [Serializable]
        public sealed class WarehouseWorkplace
        {
            public string[] warehouseDefIds;
        }

        [Serializable]
        public sealed class Armory
        {
            public Level3Int resupplyTripAmmo = new() { L1 = 20, L2 = 30, L3 = 40 };
        }

        [Serializable]
        public sealed class Crafting
        {
            public string ammoRecipeId = "ForgeAmmo";
        }

        [Serializable]
        public sealed class AmmoSupply
        {
            public int forgeTargetCrafts = 5;
        }

        [Serializable]
        public sealed class AmmoMonitor
        {
            public int lowAmmoPct = 25;
            public float reqCooldownLowSec = 8f;
            public float reqCooldownEmptySec = 4f;
            public float notifyCooldownLowSec = 6f;
            public float notifyCooldownEmptySec = 4f;
        }
    }
}