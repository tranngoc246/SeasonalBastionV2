using SeasonalBastion.Contracts;

namespace SeasonalBastion.RunStart
{
    internal static class RunStartStorageInitializer
    {
        internal static void ApplyStartingStorage(GameServices s)
        {
            if (s.WorldState == null || s.DataRegistry == null || s.StorageService == null) return;

            BuildingId hq = default;

            foreach (var bid in s.WorldState.Buildings.Ids)
            {
                var st = s.WorldState.Buildings.Get(bid);
                try
                {
                    var def = s.DataRegistry.GetBuilding(st.DefId);
                    if (def.IsHQ)
                    {
                        hq = bid;
                        break;
                    }
                }
                catch { }
            }

            if (hq.Value == 0)
            {
                foreach (var bid in s.WorldState.Buildings.Ids) { hq = bid; break; }
            }

            if (hq.Value == 0) return;

            s.StorageService.Add(hq, ResourceType.Wood, 30);
            s.StorageService.Add(hq, ResourceType.Stone, 20);
            s.StorageService.Add(hq, ResourceType.Food, 10);
            s.StorageService.Add(hq, ResourceType.Iron, 0);
            s.StorageService.Add(hq, ResourceType.Ammo, 0);
        }
    }
}
