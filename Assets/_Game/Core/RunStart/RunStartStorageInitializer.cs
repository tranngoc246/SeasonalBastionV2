using SeasonalBastion.Contracts;

namespace SeasonalBastion.RunStart
{
    internal static class RunStartStorageInitializer
    {
        internal static bool ApplyStartingStorage(GameServices s, out string error)
        {
            error = null;
            if (s.WorldState == null || s.DataRegistry == null || s.StorageService == null)
            {
                error = "RunStart storage init missing WorldState/DataRegistry/StorageService.";
                return false;
            }

            BuildingId hq = default;

            foreach (var bid in s.WorldState.Buildings.Ids)
            {
                var st = s.WorldState.Buildings.Get(bid);
                if (s.DataRegistry.TryGetBuilding(st.DefId, out var def) && def != null && def.IsHQ)
                {
                    hq = bid;
                    break;
                }
            }

            if (hq.Value == 0)
            {
                error = "RunStart storage init could not find a constructed HQ.";
                return false;
            }

            s.StorageService.Add(hq, ResourceType.Wood, 30);
            s.StorageService.Add(hq, ResourceType.Stone, 20);
            s.StorageService.Add(hq, ResourceType.Food, 10);
            s.StorageService.Add(hq, ResourceType.Iron, 0);
            s.StorageService.Add(hq, ResourceType.Ammo, 0);
            return true;
        }
    }
}
