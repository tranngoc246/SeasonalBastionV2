using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class BuildOrderCostTracker
    {
        public List<CostDef> CloneCostsOrEmpty(CostDef[] arr)
        {
            if (arr == null || arr.Length == 0) return new List<CostDef>(0);

            var list = new List<CostDef>(arr.Length);
            for (int i = 0; i < arr.Length; i++)
            {
                var c = arr[i];
                if (c == null) continue;

                int amt = c.Amount;
                if (amt <= 0) continue;

                list.Add(new CostDef { Resource = c.Resource, Amount = amt });
            }
            return list;
        }

        public List<CostDef> BuildDeliveredMirror(CostDef[] arr)
        {
            if (arr == null || arr.Length == 0) return new List<CostDef>(0);

            var list = new List<CostDef>(arr.Length);
            for (int i = 0; i < arr.Length; i++)
            {
                var c = arr[i];
                if (c == null) continue;
                if (c.Amount <= 0) continue;

                list.Add(new CostDef { Resource = c.Resource, Amount = 0 });
            }
            return list;
        }
    }
}
