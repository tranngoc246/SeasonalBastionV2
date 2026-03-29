using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.RunStart
{
    internal static class RunStartRuntimeCacheBuilder
    {
        internal static void ApplyRuntimeMetadata(GameServices s, StartMapConfigDto cfg)
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

        internal static void ApplyRuntimeZonesFromWorld(GameServices s)
        {
            if (s?.RunStartRuntime == null || s?.WorldState?.Zones == null)
                return;

            s.RunStartRuntime.Zones.Clear();

            foreach (var z in EnumerateZones(s.WorldState.Zones))
            {
                if (z == null || z.Cells == null || z.Cells.Count == 0)
                    continue;

                ComputeBounds(z.Cells, out int xMin, out int yMin, out int xMax, out int yMax);
                string zoneId = $"zone_{z.Id}";
                string type = ResourceTypeToZoneType(z.Resource);
                int cellCount = z.Cells.Count;
                s.RunStartRuntime.Zones[zoneId] = new ZoneRect(zoneId, type, ownerBuildingHint: null, new IntRect(xMin, yMin, xMax, yMax), cellCount);
            }
        }

        private static IEnumerable<ZoneState> EnumerateZones(IZoneStore zs)
        {
            if (zs?.Zones == null)
                yield break;

            for (int i = 0; i < zs.Zones.Count; i++)
            {
                var z = zs.Zones[i];
                if (z == null)
                    continue;

                yield return z;
            }
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

        private static string ResourceTypeToZoneType(ResourceType rt)
        {
            return rt switch
            {
                ResourceType.Food => "FarmPlots",
                ResourceType.Wood => "ForestTiles",
                ResourceType.Stone => "QuarryTiles",
                ResourceType.Iron => "IronVeinTiles",
                _ => rt.ToString()
            };
        }
    }
}
