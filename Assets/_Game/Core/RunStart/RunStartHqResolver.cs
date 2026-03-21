using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.RunStart
{
    internal static class RunStartHqResolver
    {
        public static void BuildLaneRuntime(GameServices s, StartMapConfigRootDto cfg)
        {
            if (s?.RunStartRuntime == null) return;
            if (!TryResolveHQTargetCell(s, out _)) return;
            if (cfg.spawnGates == null) return;

            for (int i = 0; i < cfg.spawnGates.Length; i++)
            {
                var g = cfg.spawnGates[i];
                if (g == null || g.cell == null) continue;

                int laneId = g.lane;
                var start = new CellPos(g.cell.x, g.cell.y);
                var dir = RunStartPlacementHelper.ParseDir4(g.dirToHQ);

                if (TryResolveHQTargetCellAdjacent(s, dir, out var hqAdjTarget))
                    s.RunStartRuntime.Lanes[laneId] = new LaneRuntime(laneId, start, dir, hqAdjTarget);
            }
        }

        internal static bool TryResolveHQTargetCellAdjacent(GameServices s, Dir4 dirToHQ, out CellPos target)
        {
            target = default;
            if (s == null || s.WorldState == null || s.DataRegistry == null || s.GridMap == null) return false;
            if (!TryFindHq(s, out var hq, out int w, out int h)) return false;

            int midX = (w - 1) / 2;
            int midY = (h - 1) / 2;

            CellPos pref = dirToHQ switch
            {
                Dir4.N => new CellPos(hq.Anchor.X + midX, hq.Anchor.Y - 1),
                Dir4.S => new CellPos(hq.Anchor.X + midX, hq.Anchor.Y + h),
                Dir4.E => new CellPos(hq.Anchor.X - 1, hq.Anchor.Y + midY),
                Dir4.W => new CellPos(hq.Anchor.X + w, hq.Anchor.Y + midY),
                _ => new CellPos(hq.Anchor.X + midX, hq.Anchor.Y - 1),
            };

            if (IsGoodTargetCell(s, pref))
            {
                target = pref;
                return true;
            }

            var candidates = new[]
            {
                new CellPos(hq.Anchor.X + midX, hq.Anchor.Y - 1),
                new CellPos(hq.Anchor.X - 1, hq.Anchor.Y + midY),
                new CellPos(hq.Anchor.X + midX, hq.Anchor.Y + h),
                new CellPos(hq.Anchor.X + w, hq.Anchor.Y + midY),
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (!IsGoodTargetCell(s, candidates[i])) continue;
                target = candidates[i];
                return true;
            }

            return false;
        }

        internal static bool TryResolveHQTargetCell(GameServices s, out CellPos target)
        {
            target = default;
            if (s == null || s.WorldState == null || s.DataRegistry == null) return false;
            if (!TryFindHq(s, out var hq, out int w, out int h)) return false;

            target = new CellPos(hq.Anchor.X + (w - 1) / 2, hq.Anchor.Y + (h - 1) / 2);
            return true;
        }

        private static bool TryFindHq(GameServices s, out BuildingState hq, out int w, out int h)
        {
            hq = default;
            w = 1;
            h = 1;

            BuildingId best = default;
            int bestId = int.MaxValue;

            foreach (var id in s.WorldState.Buildings.Ids)
            {
                if (!s.WorldState.Buildings.Exists(id)) continue;
                var st = s.WorldState.Buildings.Get(id);
                if (!st.IsConstructed) continue;

                bool isHQ = false;
                try
                {
                    var def = s.DataRegistry.GetBuilding(st.DefId);
                    isHQ = def.IsHQ;
                }
                catch { }

                if (!isHQ && !string.Equals(st.DefId, RunStartPlacementHelper.HqDefId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (id.Value < bestId)
                {
                    best = id;
                    bestId = id.Value;
                    hq = st;
                }
            }

            if (best.Value == 0) return false;

            try
            {
                var def = s.DataRegistry.GetBuilding(hq.DefId);
                w = Math.Max(1, def.SizeX);
                h = Math.Max(1, def.SizeY);
            }
            catch { }

            return true;
        }

        private static bool IsGoodTargetCell(GameServices s, CellPos c)
        {
            var grid = s.GridMap;
            if (!grid.IsInside(c)) return false;

            var occ = grid.Get(c).Kind;
            if (occ == CellOccupancyKind.Building || occ == CellOccupancyKind.Site)
                return false;

            return true;
        }
    }
}
