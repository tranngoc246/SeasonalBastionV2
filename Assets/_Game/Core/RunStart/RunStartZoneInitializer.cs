using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.RunStart
{
    internal static class RunStartZoneInitializer
    {
        internal static void ApplyZones(GameServices s, StartMapConfigDto cfg)
        {
            var zs = s?.WorldState?.Zones;
            if (zs == null) return;

            zs.Clear();

            string mode = cfg?.resourceGeneration?.mode;
            if (string.IsNullOrWhiteSpace(mode))
                mode = "AuthoredOnly";

            bool authoredOnly = string.Equals(mode, "AuthoredOnly", StringComparison.OrdinalIgnoreCase);
            bool hybrid = string.Equals(mode, "Hybrid", StringComparison.OrdinalIgnoreCase);
            bool generatedOnly = string.Equals(mode, "GeneratedOnly", StringComparison.OrdinalIgnoreCase);

            bool addedAny = false;

            if (generatedOnly)
            {
                addedAny = TryApplyGeneratedZones(s, cfg, out _);
            }
            else if (hybrid)
            {
                addedAny = TryApplyGeneratedZones(s, cfg, out _);
                if (!addedAny)
                    addedAny = ApplyAuthoredZones(zs, cfg);
            }
            else if (authoredOnly || cfg?.resourceGeneration != null)
            {
                addedAny = ApplyAuthoredZones(zs, cfg);
            }
            else
            {
                addedAny = ApplyAuthoredZones(zs, cfg);
            }

            if (!addedAny)
                ApplyLegacyFallbackZones(zs);
        }

        private static bool TryApplyGeneratedZones(GameServices s, StartMapConfigDto cfg, out string error)
        {
            error = null;
            if (cfg?.resourceGeneration == null)
                return false;

            if (!RunStartResourceZoneGenerator.TryGenerateZones(s, cfg, ResolveSeed(s), out var zones, out error))
                return false;

            if (zones == null || zones.Count == 0)
                return false;

            ApplyZoneStates(s.WorldState.Zones, zones);
            return true;
        }

        private static bool ApplyAuthoredZones(IZoneStore zs, StartMapConfigDto cfg)
        {
            bool addedAny = false;
            if (cfg == null || cfg.zones == null || cfg.zones.Length == 0)
                return false;

            int id = 1;
            for (int i = 0; i < cfg.zones.Length; i++)
            {
                var z = cfg.zones[i];
                if (z == null || z.cellsRect == null) continue;
                if (!TryMapZoneTypeToResource(z.type, out var rt)) continue;

                AddRectZone(zs, id++, rt, z.cellsRect.xMin, z.cellsRect.yMin, z.cellsRect.xMax, z.cellsRect.yMax);
                addedAny = true;
            }

            return addedAny;
        }

        private static void ApplyLegacyFallbackZones(IZoneStore zs)
        {
            AddRectZone(zs, 1, ResourceType.Wood, 14, 40, 24, 50);
            AddRectZone(zs, 2, ResourceType.Food, 40, 14, 50, 24);
            AddRectZone(zs, 3, ResourceType.Stone, 14, 14, 24, 24);
            AddRectZone(zs, 4, ResourceType.Iron, 40, 40, 50, 50);
        }

        private static void ApplyZoneStates(IZoneStore zs, List<ZoneState> zones)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (z == null || z.Cells == null || z.Cells.Count == 0)
                    continue;

                zs.Add(z);
            }
        }

        private static int ResolveSeed(GameServices s)
        {
            return s?.RunStartRuntime != null ? s.RunStartRuntime.Seed : 0;
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
