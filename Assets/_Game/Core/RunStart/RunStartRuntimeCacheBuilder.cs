using SeasonalBastion.Contracts;

namespace SeasonalBastion.RunStart
{
    internal static class RunStartRuntimeCacheBuilder
    {
        public static void Apply(GameServices s, StartMapConfigDto cfg)
        {
            if (s?.RunStartRuntime == null || cfg?.map == null) return;

            s.RunStartRuntime.MapWidth = cfg.map.width;
            s.RunStartRuntime.MapHeight = cfg.map.height;

            if (cfg.map.buildableRect != null)
            {
                s.RunStartRuntime.BuildableRect = new IntRect(
                    cfg.map.buildableRect.xMin,
                    cfg.map.buildableRect.yMin,
                    cfg.map.buildableRect.xMax,
                    cfg.map.buildableRect.yMax);
            }

            s.RunStartRuntime.SpawnGates.Clear();
            s.RunStartRuntime.Zones.Clear();
            s.RunStartRuntime.Lanes.Clear();
            s.RunStartRuntime.LockedInvariants.Clear();

            if (cfg.lockedInvariants != null)
            {
                for (int i = 0; i < cfg.lockedInvariants.Length; i++)
                {
                    var t = cfg.lockedInvariants[i];
                    if (!string.IsNullOrEmpty(t))
                        s.RunStartRuntime.LockedInvariants.Add(t);
                }
            }

            if (cfg.spawnGates != null)
            {
                for (int i = 0; i < cfg.spawnGates.Length; i++)
                {
                    var g = cfg.spawnGates[i];
                    if (g == null || g.cell == null) continue;
                    s.RunStartRuntime.SpawnGates.Add(new SpawnGate(g.lane, new CellPos(g.cell.x, g.cell.y), RunStartPlacementHelper.ParseDir4(g.dirToHQ)));
                }
            }

            if (cfg.zones != null)
            {
                for (int i = 0; i < cfg.zones.Length; i++)
                {
                    var z = cfg.zones[i];
                    if (z == null || z.cellsRect == null || string.IsNullOrEmpty(z.zoneId)) continue;
                    var rect = new IntRect(z.cellsRect.xMin, z.cellsRect.yMin, z.cellsRect.xMax, z.cellsRect.yMax);
                    s.RunStartRuntime.Zones[z.zoneId] = new ZoneRect(z.zoneId, z.type, z.ownerBuildingHint, rect, z.cellCount);
                }
            }
        }
    }
}
