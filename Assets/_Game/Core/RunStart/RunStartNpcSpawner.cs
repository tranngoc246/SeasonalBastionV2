using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.RunStart
{
    internal static class RunStartNpcSpawner
    {
        internal static void SpawnInitialNpcs(GameServices s, StartMapConfigDto cfg, RunStartBuildContext ctx)
        {
            if (cfg.initialNpcs == null) return;

            for (int i = 0; i < cfg.initialNpcs.Length; i++)
            {
                var n = cfg.initialNpcs[i];
                if (n == null || n.spawnCell == null || string.IsNullOrEmpty(n.npcDefId)) continue;

                BuildingId workplace = default;
                if (!string.IsNullOrEmpty(n.assignedWorkplaceDefId))
                    ctx.DefIdToBuildingId.TryGetValue(n.assignedWorkplaceDefId, out workplace);

                var desired = new CellPos(n.spawnCell.x, n.spawnCell.y);
                var spawn = ResolveSpawnCell(s, desired);

                var st = new NpcState
                {
                    DefId = n.npcDefId,
                    Cell = spawn,
                    Workplace = workplace,
                    CurrentJob = default,
                    IsIdle = true
                };

                var id = s.WorldState.Npcs.Create(st);
                st.Id = id;
                s.WorldState.Npcs.Set(id, st);
            }
        }

        private static CellPos ResolveSpawnCell(GameServices s, CellPos desired)
        {
            if (s?.GridMap == null)
                return desired;

            if (IsPreferredSpawnCell(s, desired))
                return desired;

            var empty = FindNearbyCell(s, desired, CellOccupancyKind.Empty);
            if (empty.HasValue)
                return empty.Value;

            var road = FindNearbyCell(s, desired, CellOccupancyKind.Road);
            if (road.HasValue)
                return road.Value;

            if (s.GridMap.IsInside(desired))
                return desired;

            int x = Math.Clamp(desired.X, 0, s.GridMap.Width - 1);
            int y = Math.Clamp(desired.Y, 0, s.GridMap.Height - 1);
            return new CellPos(x, y);
        }

        private static bool IsPreferredSpawnCell(GameServices s, CellPos cell)
        {
            if (!s.GridMap.IsInside(cell)) return false;
            return s.GridMap.Get(cell).Kind == CellOccupancyKind.Empty;
        }

        private static CellPos? FindNearbyCell(GameServices s, CellPos desired, CellOccupancyKind wanted)
        {
            const int maxR = 8;

            bool IsMatch(CellPos c)
            {
                if (!s.GridMap.IsInside(c)) return false;
                return s.GridMap.Get(c).Kind == wanted;
            }

            for (int r = 1; r <= maxR; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    int ax = r - Math.Abs(dy);
                    int dx1 = -ax;
                    int dx2 = ax;

                    var c1 = new CellPos(desired.X + dx1, desired.Y + dy);
                    if (IsMatch(c1)) return c1;

                    if (dx2 != dx1)
                    {
                        var c2 = new CellPos(desired.X + dx2, desired.Y + dy);
                        if (IsMatch(c2)) return c2;
                    }
                }
            }

            return null;
        }
    }
}
