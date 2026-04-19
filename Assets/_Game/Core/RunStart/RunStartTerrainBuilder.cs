using SeasonalBastion.Contracts;

namespace SeasonalBastion.RunStart
{
    internal static class RunStartTerrainBuilder
    {
        internal static bool ApplyTerrain(GameServices s, StartMapConfigDto cfg, out string error)
        {
            error = null;

            if (s?.TerrainMap == null || cfg?.map == null)
            {
                error = "TerrainMap or StartMapConfig.map missing.";
                return false;
            }

            s.TerrainMap.ClearAll();

            for (int y = 0; y < s.TerrainMap.Height; y++)
            {
                for (int x = 0; x < s.TerrainMap.Width; x++)
                    s.TerrainMap.Set(new CellPos(x, y), TerrainType.Land);
            }

            if (cfg.terrainRects == null || cfg.terrainRects.Length == 0)
                return true;

            for (int i = 0; i < cfg.terrainRects.Length; i++)
            {
                var tr = cfg.terrainRects[i];
                if (tr == null || tr.rect == null || string.IsNullOrWhiteSpace(tr.terrain))
                    continue;

                if (!TryParseTerrain(tr.terrain, out var terrain))
                {
                    error = $"terrainRects[{i}].terrain='{tr.terrain}' unsupported.";
                    return false;
                }

                int xMin = tr.rect.xMin;
                int yMin = tr.rect.yMin;
                int xMax = tr.rect.xMax;
                int yMax = tr.rect.yMax;

                if (xMin > xMax || yMin > yMax)
                {
                    error = $"terrainRects[{i}] invalid rect min/max.";
                    return false;
                }

                if (!s.TerrainMap.IsInside(new CellPos(xMin, yMin)) || !s.TerrainMap.IsInside(new CellPos(xMax, yMax)))
                {
                    error = $"terrainRects[{i}] out of bounds: ({xMin},{yMin})..({xMax},{yMax}).";
                    return false;
                }

                for (int y = yMin; y <= yMax; y++)
                {
                    for (int x = xMin; x <= xMax; x++)
                        s.TerrainMap.Set(new CellPos(x, y), terrain);
                }
            }

            return true;
        }

        private static bool TryParseTerrain(string text, out TerrainType terrain)
        {
            if (System.Enum.TryParse(text, ignoreCase: true, out terrain))
                return true;

            terrain = TerrainType.Sea;
            return false;
        }
    }
}
