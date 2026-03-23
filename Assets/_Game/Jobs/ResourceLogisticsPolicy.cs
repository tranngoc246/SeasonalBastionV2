using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class ResourceLogisticsPolicy
    {
        internal bool IsWarehouseWorkplace(string defId)
        {
            return DefIdTierUtil.IsBase(defId, "bld_warehouse")
                || DefIdTierUtil.IsBase(defId, "bld_hq");
        }

        internal bool IsHarvestProducer(string defId)
        {
            return DefIdTierUtil.IsBase(defId, "bld_farmhouse")
                || DefIdTierUtil.IsBase(defId, "bld_lumbercamp")
                || DefIdTierUtil.IsBase(defId, "bld_quarry")
                || DefIdTierUtil.IsBase(defId, "bld_ironhut");
        }

        internal ResourceType HarvestResourceType(string defId)
        {
            if (DefIdTierUtil.IsBase(defId, "bld_farmhouse")) return ResourceType.Food;
            if (DefIdTierUtil.IsBase(defId, "bld_lumbercamp")) return ResourceType.Wood;
            if (DefIdTierUtil.IsBase(defId, "bld_quarry")) return ResourceType.Stone;
            if (DefIdTierUtil.IsBase(defId, "bld_ironhut")) return ResourceType.Iron;
            return ResourceType.Food;
        }

        internal int HarvestLocalCap(string defId, int level)
        {
            if (DefIdTierUtil.IsBase(defId, "bld_farmhouse")) return level == 1 ? 30 : level == 2 ? 60 : 90;
            if (DefIdTierUtil.IsBase(defId, "bld_lumbercamp")) return level == 1 ? 40 : level == 2 ? 80 : 120;
            if (DefIdTierUtil.IsBase(defId, "bld_quarry")) return level == 1 ? 40 : level == 2 ? 80 : 120;
            if (DefIdTierUtil.IsBase(defId, "bld_ironhut")) return level == 1 ? 30 : level == 2 ? 60 : 90;
            return 0;
        }

        internal int DestCap(string defId, int level, ResourceType rt)
        {
            if (DefIdTierUtil.IsBase(defId, "bld_warehouse"))
            {
                return rt switch
                {
                    ResourceType.Wood or ResourceType.Food or ResourceType.Stone or ResourceType.Iron
                        => level == 1 ? 300 : level == 2 ? 600 : 1000,
                    _ => 0
                };
            }

            if (DefIdTierUtil.IsBase(defId, "bld_hq"))
            {
                return rt switch
                {
                    ResourceType.Wood or ResourceType.Food or ResourceType.Stone or ResourceType.Iron
                        => level == 1 ? 120 : level == 2 ? 180 : 240,
                    _ => 0
                };
            }

            return 0;
        }

        internal int GetAmountFromBuilding(in BuildingState bs, ResourceType rt)
        {
            return rt switch
            {
                ResourceType.Wood => bs.Wood,
                ResourceType.Food => bs.Food,
                ResourceType.Stone => bs.Stone,
                ResourceType.Iron => bs.Iron,
                ResourceType.Ammo => bs.Ammo,
                _ => 0
            };
        }

        internal int NormalizeLevel(int level) => level <= 0 ? 1 : (level > 3 ? 3 : level);
    }
}
