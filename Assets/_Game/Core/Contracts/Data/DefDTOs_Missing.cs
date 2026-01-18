namespace SeasonalBastion.Contracts
{
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
