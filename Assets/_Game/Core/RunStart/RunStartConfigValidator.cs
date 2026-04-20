using SeasonalBastion.Contracts;

namespace SeasonalBastion.RunStart
{
    internal static class RunStartConfigValidator
    {
        internal static bool ValidateConfig(GameServices s, StartMapConfigDto cfg, out string error)
        {
            error = null;
            if (s == null) { error = "services=null"; return false; }
            if (cfg == null || cfg.map == null) { error = "StartMapConfig missing map"; return false; }

            if (s.GridMap != null)
            {
                if (cfg.map.width != s.GridMap.Width || cfg.map.height != s.GridMap.Height)
                {
                    error = $"StartMapConfig map size {cfg.map.width}x{cfg.map.height} != GridMap {s.GridMap.Width}x{s.GridMap.Height}";
                    return false;
                }
            }

            if (s.TerrainMap != null)
            {
                if (cfg.map.width != s.TerrainMap.Width || cfg.map.height != s.TerrainMap.Height)
                {
                    error = $"StartMapConfig map size {cfg.map.width}x{cfg.map.height} != TerrainMap {s.TerrainMap.Width}x{s.TerrainMap.Height}";
                    return false;
                }
            }

            return ValidateStartMapHeader(cfg, out error);
        }

        internal static bool ValidateStartMapHeader(StartMapConfigDto cfg, out string error)
        {
            error = null;

            if (cfg.schemaVersion != 1)
            {
                error = $"StartMapConfig schemaVersion={cfg.schemaVersion} unsupported (expect 1).";
                return false;
            }

            if (cfg.coordSystem == null)
            {
                error = "StartMapConfig missing coordSystem.";
                return false;
            }

            if (!string.Equals(cfg.coordSystem.origin, "bottom-left", System.StringComparison.OrdinalIgnoreCase))
            {
                error = $"coordSystem.origin='{cfg.coordSystem.origin}' (expect 'bottom-left').";
                return false;
            }

            if (!string.Equals(cfg.coordSystem.indexing, "0-based", System.StringComparison.OrdinalIgnoreCase))
            {
                error = $"coordSystem.indexing='{cfg.coordSystem.indexing}' (expect '0-based').";
                return false;
            }

            if (cfg.lockedInvariants == null || cfg.lockedInvariants.Length == 0)
            {
                error = "StartMapConfig missing lockedInvariants (expect non-empty).";
                return false;
            }

            if (!ValidateTerrainRects(cfg.terrainRects, cfg.map, out error))
                return false;

            if (!ValidateResourceGeneration(cfg.resourceGeneration, out error))
                return false;

            return true;
        }

        private static bool ValidateTerrainRects(TerrainRectDto[] terrainRects, MapDto map, out string error)
        {
            error = null;
            if (terrainRects == null)
                return true;

            for (int i = 0; i < terrainRects.Length; i++)
            {
                var tr = terrainRects[i];
                if (tr == null || tr.rect == null || string.IsNullOrWhiteSpace(tr.terrain))
                    continue;

                if (!System.Enum.TryParse<TerrainType>(tr.terrain, ignoreCase: true, out _))
                {
                    error = $"terrainRects[{i}].terrain='{tr.terrain}' unsupported (expect Sea | Shore | Land).";
                    return false;
                }

                if (tr.rect.xMin > tr.rect.xMax || tr.rect.yMin > tr.rect.yMax)
                {
                    error = $"terrainRects[{i}] invalid rect min/max.";
                    return false;
                }

                if (tr.rect.xMin < 0 || tr.rect.yMin < 0 || tr.rect.xMax >= map.width || tr.rect.yMax >= map.height)
                {
                    error = $"terrainRects[{i}] out of bounds for map {map.width}x{map.height}.";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateResourceGeneration(ResourceGenerationDto rg, out string error)
        {
            error = null;
            if (rg == null)
                return true;

            if (string.IsNullOrWhiteSpace(rg.mode))
                return true;

            bool authoredOnly = string.Equals(rg.mode, "AuthoredOnly", System.StringComparison.OrdinalIgnoreCase);
            bool hybrid = string.Equals(rg.mode, "Hybrid", System.StringComparison.OrdinalIgnoreCase);
            bool generatedOnly = string.Equals(rg.mode, "GeneratedOnly", System.StringComparison.OrdinalIgnoreCase);
            if (!authoredOnly && !hybrid && !generatedOnly)
            {
                error = $"resourceGeneration.mode='{rg.mode}' unsupported (expect AuthoredOnly | Hybrid | GeneratedOnly).";
                return false;
            }

            if ((hybrid || generatedOnly) && (rg.starterRules == null || rg.starterRules.Length == 0))
            {
                error = $"resourceGeneration.mode='{rg.mode}' requires non-empty starterRules.";
                return false;
            }

            if (!ValidateSpawnRules(rg.starterRules, "starterRules", out error))
                return false;

            if (!ValidateSpawnRules(rg.bonusRules, "bonusRules", out error))
                return false;

            return true;
        }

        private static bool ValidateSpawnRules(ResourceSpawnRuleDto[] rules, string label, out string error)
        {
            error = null;
            if (rules == null)
                return true;

            for (int i = 0; i < rules.Length; i++)
            {
                var r = rules[i];
                if (r == null)
                    continue;

                if (!TryParseResourceType(r.resourceType, out _))
                {
                    error = $"resourceGeneration.{label}[{i}].resourceType='{r.resourceType}' unsupported.";
                    return false;
                }

                if (r.countMin < 0 || r.countMax < 0 || r.countMin > r.countMax)
                {
                    error = $"resourceGeneration.{label}[{i}] invalid count range ({r.countMin}..{r.countMax}).";
                    return false;
                }

                if (r.minDistanceFromHQ < 0 || r.maxDistanceFromHQ < 0 || r.minDistanceFromHQ > r.maxDistanceFromHQ)
                {
                    error = $"resourceGeneration.{label}[{i}] invalid HQ distance range ({r.minDistanceFromHQ}..{r.maxDistanceFromHQ}).";
                    return false;
                }

                if (r.rectWidthMin <= 0 || r.rectWidthMax <= 0 || r.rectWidthMin > r.rectWidthMax)
                {
                    error = $"resourceGeneration.{label}[{i}] invalid rectWidth range ({r.rectWidthMin}..{r.rectWidthMax}).";
                    return false;
                }

                if (r.rectHeightMin <= 0 || r.rectHeightMax <= 0 || r.rectHeightMin > r.rectHeightMax)
                {
                    error = $"resourceGeneration.{label}[{i}] invalid rectHeight range ({r.rectHeightMin}..{r.rectHeightMax}).";
                    return false;
                }
            }

            return true;
        }

        private static bool TryParseResourceType(string text, out ResourceType rt)
        {
            if (System.Enum.TryParse<ResourceType>(text, ignoreCase: true, out rt) && rt != ResourceType.None)
                return true;

            rt = ResourceType.None;
            return false;
        }
    }
}
