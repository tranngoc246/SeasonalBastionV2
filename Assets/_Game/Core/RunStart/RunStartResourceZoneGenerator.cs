using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.RunStart
{
    internal static class RunStartResourceZoneGenerator
    {
        internal static bool TryGenerateZones(
            GameServices s,
            StartMapConfigDto cfg,
            int seed,
            out List<ZoneState> zones,
            out string error)
        {
            zones = new List<ZoneState>();
            error = null;

            if (s?.GridMap == null)
            {
                error = "GenerateZones requires GridMap.";
                return false;
            }

            if (cfg?.resourceGeneration == null)
            {
                error = "resourceGeneration missing.";
                return false;
            }

            if (!TryResolveHqAnchor(s, out var hqAnchor))
            {
                error = "Unable to resolve HQ anchor for resource generation.";
                return false;
            }

            int nextZoneId = 1;
            int rootSeed = Mix(seed, cfg.resourceGeneration.seedOffset, 7919);

            if (cfg.resourceGeneration.starterRules != null)
            {
                for (int i = 0; i < cfg.resourceGeneration.starterRules.Length; i++)
                {
                    var rule = cfg.resourceGeneration.starterRules[i];
                    if (rule == null) continue;
                    if (!TryParseResourceType(rule.resourceType, out var rt)) continue;

                    int count = PickRange(rule.countMin, rule.countMax, Mix(rootSeed, i, 101));
                    GenerateZonesForRule(s, hqAnchor, rt, rule, count, Mix(rootSeed, i, 211), isStarter: true, ref nextZoneId, zones);
                }
            }

            if (cfg.resourceGeneration.bonusRules != null)
            {
                for (int i = 0; i < cfg.resourceGeneration.bonusRules.Length; i++)
                {
                    var rule = cfg.resourceGeneration.bonusRules[i];
                    if (rule == null) continue;
                    if (!TryParseResourceType(rule.resourceType, out var rt)) continue;

                    int count = PickRange(rule.countMin, rule.countMax, Mix(rootSeed, i, 307));
                    GenerateZonesForRule(s, hqAnchor, rt, rule, count, Mix(rootSeed, i, 401), isStarter: false, ref nextZoneId, zones);
                }
            }

            return true;
        }

        private static void GenerateZonesForRule(
            GameServices s,
            CellPos hqAnchor,
            ResourceType rt,
            ResourceSpawnRuleDto rule,
            int count,
            int seed,
            bool isStarter,
            ref int nextZoneId,
            List<ZoneState> zones)
        {
            if (count <= 0)
                return;

            int minSeparation = GetMinSeparation(rt);
            int crossTypeMinSeparation = isStarter ? 2 : 5;
            for (int i = 0; i < count; i++)
            {
                int localSeed = Mix(seed, i, rt.GetHashCode());
                int w = PickRange(rule.rectWidthMin, rule.rectWidthMax, Mix(localSeed, 17, 23));
                int h = PickRange(rule.rectHeightMin, rule.rectHeightMax, Mix(localSeed, 29, 31));

                if (!TryPickZoneRect(s, hqAnchor, rt, rule, localSeed, w, h, minSeparation, crossTypeMinSeparation, zones, out int xMin, out int yMin, out int xMax, out int yMax))
                    continue;

                var z = new ZoneState
                {
                    Id = nextZoneId++,
                    Resource = rt
                };

                for (int y = yMin; y <= yMax; y++)
                    for (int x = xMin; x <= xMax; x++)
                        z.Cells.Add(new CellPos(x, y));

                if (z.Cells.Count > 0)
                    zones.Add(z);
            }
        }

        private static bool TryPickZoneRect(
            GameServices s,
            CellPos hqAnchor,
            ResourceType rt,
            ResourceSpawnRuleDto rule,
            int seed,
            int width,
            int height,
            int minSeparation,
            int crossTypeMinSeparation,
            List<ZoneState> existingZones,
            out int xMin,
            out int yMin,
            out int xMax,
            out int yMax)
        {
            xMin = yMin = xMax = yMax = 0;
            if (s?.GridMap == null)
                return false;

            int mapW = s.GridMap.Width;
            int mapH = s.GridMap.Height;
            if (width <= 0 || height <= 0 || width > mapW || height > mapH)
                return false;

            const int maxAttempts = 256;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                int aSeed = Mix(seed, attempt, 1237);
                int x = PickRange(0, mapW - width, Mix(aSeed, 11, 13));
                int y = PickRange(0, mapH - height, Mix(aSeed, 17, 19));

                if (!IsRectCandidateValid(s, hqAnchor, rt, rule, x, y, width, height, minSeparation, crossTypeMinSeparation, existingZones))
                    continue;

                xMin = x;
                yMin = y;
                xMax = x + width - 1;
                yMax = y + height - 1;
                return true;
            }

            // Deterministic fallback sweep: if random probing misses, scan the map and take the best valid candidate.
            int bestScore = int.MaxValue;
            bool found = false;
            for (int y = 0; y <= mapH - height; y++)
            {
                for (int x = 0; x <= mapW - width; x++)
                {
                    if (!IsRectCandidateValid(s, hqAnchor, rt, rule, x, y, width, height, minSeparation, crossTypeMinSeparation, existingZones))
                        continue;

                    int rectCx = x + (width - 1) / 2;
                    int rectCy = y + (height - 1) / 2;
                    int dist = Manhattan(hqAnchor, new CellPos(rectCx, rectCy));
                    int score = System.Math.Abs(dist - rule.minDistanceFromHQ) * 4 + (Mix(seed, x, y) & 7);
                    if (!found || score < bestScore)
                    {
                        found = true;
                        bestScore = score;
                        xMin = x;
                        yMin = y;
                        xMax = x + width - 1;
                        yMax = y + height - 1;
                    }
                }
            }

            return found;
        }

        private static bool IsRectCandidateValid(
            GameServices s,
            CellPos hqAnchor,
            ResourceType rt,
            ResourceSpawnRuleDto rule,
            int x,
            int y,
            int width,
            int height,
            int minSeparation,
            int crossTypeMinSeparation,
            List<ZoneState> existingZones)
        {
            int rectCx = x + (width - 1) / 2;
            int rectCy = y + (height - 1) / 2;
            int dist = Manhattan(hqAnchor, new CellPos(rectCx, rectCy));
            if (dist < rule.minDistanceFromHQ || dist > rule.maxDistanceFromHQ)
                return false;

            if (!IsRectValid(s, x, y, width, height))
                return false;

            if (!IsSeparatedEnough(rt, x, y, width, height, minSeparation, crossTypeMinSeparation, existingZones))
                return false;

            return true;
        }

        private static bool IsRectValid(GameServices s, int x, int y, int width, int height)
        {
            for (int yy = y; yy < y + height; yy++)
            {
                for (int xx = x; xx < x + width; xx++)
                {
                    var c = new CellPos(xx, yy);
                    if (!s.GridMap.IsInside(c))
                        return false;

                    var occ = s.GridMap.Get(c).Kind;
                    if (occ == CellOccupancyKind.Road || occ == CellOccupancyKind.Building || occ == CellOccupancyKind.Site)
                        return false;
                }
            }

            return true;
        }

        private static bool IsSeparatedEnough(ResourceType rt, int x, int y, int width, int height, int minSeparation, int crossTypeMinSeparation, List<ZoneState> existingZones)
        {
            if (existingZones == null || existingZones.Count == 0)
                return true;

            int newXMin = x;
            int newYMin = y;
            int newXMax = x + width - 1;
            int newYMax = y + height - 1;

            for (int i = 0; i < existingZones.Count; i++)
            {
                var z = existingZones[i];
                if (z == null || z.Cells == null || z.Cells.Count == 0)
                    continue;

                int required = z.Resource == rt ? minSeparation : crossTypeMinSeparation;
                if (required <= 0)
                    continue;

                ComputeBounds(z.Cells, out int zxMin, out int zyMin, out int zxMax, out int zyMax);
                if (RectsOverlapWithGap(newXMin, newYMin, newXMax, newYMax, zxMin, zyMin, zxMax, zyMax, required))
                    return false;
            }

            return true;
        }

        private static void ComputeBounds(List<CellPos> cells, out int xMin, out int yMin, out int xMax, out int yMax)
        {
            xMin = int.MaxValue;
            yMin = int.MaxValue;
            xMax = int.MinValue;
            yMax = int.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                if (c.X < xMin) xMin = c.X;
                if (c.Y < yMin) yMin = c.Y;
                if (c.X > xMax) xMax = c.X;
                if (c.Y > yMax) yMax = c.Y;
            }
        }

        private static bool RectsOverlapWithGap(int axMin, int ayMin, int axMax, int ayMax, int bxMin, int byMin, int bxMax, int byMax, int gap)
        {
            axMin -= gap;
            ayMin -= gap;
            axMax += gap;
            ayMax += gap;

            return !(axMax < bxMin || axMin > bxMax || ayMax < byMin || ayMin > byMax);
        }

        private static int GetMinSeparation(ResourceType rt)
        {
            return rt switch
            {
                ResourceType.Wood => 7,
                ResourceType.Food => 7,
                ResourceType.Stone => 9,
                ResourceType.Iron => 10,
                _ => 6
            };
        }

        private static bool TryResolveHqAnchor(GameServices s, out CellPos hqAnchor)
        {
            hqAnchor = default;

            foreach (var id in s.WorldState.Buildings.Ids)
            {
                if (!s.WorldState.Buildings.Exists(id)) continue;
                var st = s.WorldState.Buildings.Get(id);
                if (!st.IsConstructed) continue;
                if (DefIdTierUtil.IsBase(st.DefId, "bld_hq"))
                {
                    hqAnchor = st.Anchor;
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseResourceType(string text, out ResourceType rt)
        {
            if (Enum.TryParse(text, true, out rt) && rt != ResourceType.None)
                return true;

            rt = ResourceType.None;
            return false;
        }

        private static int PickRange(int min, int max, int seed)
        {
            if (max < min)
                return min;

            int span = max - min + 1;
            return min + ((seed & 0x7fffffff) % span);
        }

        private static int Mix(int a, int b, int c)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + a;
                h = h * 31 + b;
                h = h * 31 + c;
                return h;
            }
        }

        private static int Manhattan(CellPos a, CellPos b)
        {
            int dx = a.X - b.X; if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
            return dx + dy;
        }
    }
}
