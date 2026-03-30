using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.RunStart
{
    internal static class RunStartZoneInitializer
    {
        private const string ModeAuthoredOnly = "AuthoredOnly";
        private const string ModeHybrid = "Hybrid";
        private const string ModeGeneratedOnly = "GeneratedOnly";

        private const string AppliedGenerated = "Generated";
        private const string AppliedAuthored = "AuthoredFallback";
        private const string AppliedLegacy = "LegacyFallback";

        internal static void ApplyZones(GameServices s, StartMapConfigDto cfg)
        {
            var zs = s?.WorldState?.Zones;
            if (zs == null) return;

            zs.Clear();
            ResetGenerationDebugState(s, cfg);

            string mode = NormalizeRequestedMode(cfg?.resourceGeneration?.mode);
            bool addedAny = false;

            if (string.Equals(mode, ModeGeneratedOnly, StringComparison.OrdinalIgnoreCase))
            {
                addedAny = TryApplyGeneratedZones(s, cfg, out _);
                if (!addedAny)
                {
                    addedAny = TryApplyAuthoredFallback(s, cfg);
                    if (!addedAny)
                        ApplyLegacyFallbackZones(s);
                }
            }
            else if (string.Equals(mode, ModeHybrid, StringComparison.OrdinalIgnoreCase))
            {
                addedAny = TryApplyGeneratedZones(s, cfg, out _);
                if (!addedAny)
                {
                    addedAny = TryApplyAuthoredFallback(s, cfg);
                    if (!addedAny)
                        ApplyLegacyFallbackZones(s);
                }
            }
            else
            {
                addedAny = TryApplyAuthoredFallback(s, cfg);
                if (!addedAny)
                    ApplyLegacyFallbackZones(s);
            }
        }

        private static void ResetGenerationDebugState(GameServices s, StartMapConfigDto cfg)
        {
            if (s?.RunStartRuntime == null)
                return;

            s.RunStartRuntime.ResourceGenerationModeRequested = NormalizeRequestedMode(cfg?.resourceGeneration?.mode);
            s.RunStartRuntime.ResourceGenerationModeApplied = null;
            s.RunStartRuntime.ResourceGenerationFailureReason = null;
            s.RunStartRuntime.OpeningQualityBand = "Unknown";
        }

        private static bool TryApplyGeneratedZones(GameServices s, StartMapConfigDto cfg, out string error)
        {
            error = null;
            if (cfg?.resourceGeneration == null)
            {
                error = "resourceGeneration missing.";
                RecordGenerationFailure(s, error);
                return false;
            }

            if (!RunStartResourceZoneGenerator.TryGenerateZones(s, cfg, ResolveSeed(s), out var zones, out error))
            {
                RecordGenerationFailure(s, error);
                return false;
            }

            if (zones == null || zones.Count == 0)
            {
                error = "Generated resource zone list was empty.";
                RecordGenerationFailure(s, error);
                return false;
            }

            ApplyZoneStates(s.WorldState.Zones, zones);
            RecordAppliedMode(s, AppliedGenerated, "GeneratedUsable");
            return true;
        }

        private static bool TryApplyAuthoredFallback(GameServices s, StartMapConfigDto cfg)
        {
            bool addedAny = ApplyAuthoredZones(s?.WorldState?.Zones, cfg);
            if (!addedAny)
                return false;

            RecordAppliedMode(s, AppliedAuthored, "AuthoredFallback");
            return true;
        }

        private static bool ApplyAuthoredZones(IZoneStore zs, StartMapConfigDto cfg)
        {
            bool addedAny = false;
            if (zs == null || cfg == null || cfg.zones == null || cfg.zones.Length == 0)
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

        private static void ApplyLegacyFallbackZones(GameServices s)
        {
            var zs = s?.WorldState?.Zones;
            if (zs == null)
                return;

            AddRectZone(zs, 1, ResourceType.Wood, 14, 40, 24, 50);
            AddRectZone(zs, 2, ResourceType.Food, 40, 14, 50, 24);
            AddRectZone(zs, 3, ResourceType.Stone, 14, 14, 24, 24);
            AddRectZone(zs, 4, ResourceType.Iron, 40, 40, 50, 50);
            RecordAppliedMode(s, AppliedLegacy, "LegacyFallback");
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

        private static void RecordGenerationFailure(GameServices s, string reason)
        {
            if (s?.RunStartRuntime == null)
                return;

            s.RunStartRuntime.ResourceGenerationFailureReason = string.IsNullOrWhiteSpace(reason) ? "Unknown generation failure." : reason;
            s.RunStartRuntime.OpeningQualityBand = "GenerationFailed";
        }

        private static void RecordAppliedMode(GameServices s, string appliedMode, string qualityBand)
        {
            if (s?.RunStartRuntime == null)
                return;

            s.RunStartRuntime.ResourceGenerationModeApplied = appliedMode;
            s.RunStartRuntime.OpeningQualityBand = qualityBand;
        }

        private static string NormalizeRequestedMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
                return ModeAuthoredOnly;

            if (string.Equals(mode, ModeGeneratedOnly, StringComparison.OrdinalIgnoreCase))
                return ModeGeneratedOnly;
            if (string.Equals(mode, ModeHybrid, StringComparison.OrdinalIgnoreCase))
                return ModeHybrid;
            return ModeAuthoredOnly;
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
