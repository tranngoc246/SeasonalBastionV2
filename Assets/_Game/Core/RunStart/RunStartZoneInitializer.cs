using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.RunStart
{
    internal static class RunStartZoneInitializer
    {
        public static void Apply(GameServices s, StartMapConfigDto cfg)
        {
            var zs = s?.WorldState?.Zones;
            if (zs == null) return;

            zs.Clear();

            bool addedAny = false;
            if (cfg != null && cfg.zones != null && cfg.zones.Length > 0)
            {
                int id = 1;
                for (int i = 0; i < cfg.zones.Length; i++)
                {
                    var z = cfg.zones[i];
                    if (z == null || z.cellsRect == null) continue;
                    if (!TryMapZoneTypeToResource(z.type, out var rt)) continue;

                    AddRectZone(zs, id++, rt, z.cellsRect.xMin, z.cellsRect.yMin, z.cellsRect.xMax, z.cellsRect.yMax);
                    addedAny = true;
                }
            }

            if (!addedAny)
            {
                AddRectZone(zs, 1, ResourceType.Wood, 14, 40, 24, 50);
                AddRectZone(zs, 2, ResourceType.Food, 40, 14, 50, 24);
                AddRectZone(zs, 3, ResourceType.Stone, 14, 14, 24, 24);
                AddRectZone(zs, 4, ResourceType.Iron, 40, 40, 50, 50);
            }
        }

        private static bool TryMapZoneTypeToResource(string zoneType, out ResourceType rt)
        {
            if (string.Equals(zoneType, "FarmPlots", StringComparison.OrdinalIgnoreCase)) { rt = ResourceType.Food; return true; }
            if (string.Equals(zoneType, "ForestTiles", StringComparison.OrdinalIgnoreCase)) { rt = ResourceType.Wood; return true; }
            if (string.Equals(zoneType, "QuarryTiles", StringComparison.OrdinalIgnoreCase)) { rt = ResourceType.Stone; return true; }
            if (string.Equals(zoneType, "IronVeinTiles", StringComparison.OrdinalIgnoreCase)) { rt = ResourceType.Iron; return true; }

            rt = default;
            return false;
        }

        private static void AddRectZone(IZoneStore zs, int id, ResourceType rt, int xMin, int yMin, int xMax, int yMax)
        {
            var z = new ZoneState { Id = id, Resource = rt };

            for (int y = yMin; y <= yMax; y++)
                for (int x = xMin; x <= xMax; x++)
                    z.Cells.Add(new CellPos(x, y));

            zs.Add(z);
        }
    }
}
