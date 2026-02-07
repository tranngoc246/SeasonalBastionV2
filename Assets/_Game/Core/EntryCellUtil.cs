using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// Deterministic entry/driveway resolver.
    /// Entry cell = middle of front edge, 1 cell outside footprint (same rule as RunStartApplier).
    /// HQ: has 4 entries (N/E/S/W) -> pick nearest to "from".
    /// </summary>
    public static class EntryCellUtil
    {
        public static CellPos GetApproachCellForBuilding(GameServices s, in BuildingState b, CellPos from)
        {
            int w = 1, h = 1;
            bool isHQ = false;

            try
            {
                var def = s.DataRegistry.GetBuilding(b.DefId);
                w = Math.Max(1, def.SizeX);
                h = Math.Max(1, def.SizeY);
                isHQ = def.IsHQ;
            }
            catch { /* fallback */ }

            if (isHQ)
            {
                // HQ has 4 entry cells regardless of rotation
                var eN = ComputeEntryOutsideFootprint(b.Anchor, w, h, Dir4.N);
                var eS = ComputeEntryOutsideFootprint(b.Anchor, w, h, Dir4.S);
                var eE = ComputeEntryOutsideFootprint(b.Anchor, w, h, Dir4.E);
                var eW = ComputeEntryOutsideFootprint(b.Anchor, w, h, Dir4.W);
                return PickNearestInside(s, from, b.Anchor, eN, eS, eE, eW);
            }

            var entry = ComputeEntryOutsideFootprint(b.Anchor, w, h, b.Rotation);
            return PickNearestInside(s, from, b.Anchor, entry);
        }

        public static CellPos GetApproachCellForSite(GameServices s, in BuildSiteState site, CellPos from)
        {
            int w = 1, h = 1;
            bool isHQ = false;

            try
            {
                var def = s.DataRegistry.GetBuilding(site.BuildingDefId);
                w = Math.Max(1, def.SizeX);
                h = Math.Max(1, def.SizeY);
                isHQ = def.IsHQ;
            }
            catch { /* fallback */ }

            if (isHQ)
            {
                var eN = ComputeEntryOutsideFootprint(site.Anchor, w, h, Dir4.N);
                var eS = ComputeEntryOutsideFootprint(site.Anchor, w, h, Dir4.S);
                var eE = ComputeEntryOutsideFootprint(site.Anchor, w, h, Dir4.E);
                var eW = ComputeEntryOutsideFootprint(site.Anchor, w, h, Dir4.W);
                return PickNearestInside(s, from, site.Anchor, eN, eS, eE, eW);
            }

            var entry = ComputeEntryOutsideFootprint(site.Anchor, w, h, site.Rotation);
            return PickNearestInside(s, from, site.Anchor, entry);
        }

        private static CellPos PickNearestInside(GameServices s, CellPos from, CellPos fallbackAnchor, params CellPos[] candidates)
        {
            if (candidates == null || candidates.Length == 0) return fallbackAnchor;

            int best = int.MaxValue;
            CellPos bestPos = fallbackAnchor;

            for (int i = 0; i < candidates.Length; i++)
            {
                var c = candidates[i];

                // if out of bounds -> skip (fallback to anchor)
                if (s.GridMap != null && !s.GridMap.IsInside(c))
                    continue;

                int d = Manhattan(from, c);
                if (d < best)
                {
                    best = d;
                    bestPos = c;
                }
            }

            // if all out of bounds -> fallback
            if (best == int.MaxValue) return fallbackAnchor;
            return bestPos;
        }

        private static int Manhattan(CellPos a, CellPos b)
        {
            int dx = a.X - b.X; if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
            return dx + dy;
        }

        // Same logic as RunStartApplier.ComputeEntryOutsideFootprint (private there).
        private static CellPos ComputeEntryOutsideFootprint(CellPos anchor, int w, int h, Dir4 rot)
        {
            int cx = w / 2;
            int cy = h / 2;

            return rot switch
            {
                Dir4.N => new CellPos(anchor.X + cx, anchor.Y + h),
                Dir4.S => new CellPos(anchor.X + cx, anchor.Y - 1),
                Dir4.E => new CellPos(anchor.X + w, anchor.Y + cy),
                Dir4.W => new CellPos(anchor.X - 1, anchor.Y + cy),
                _ => new CellPos(anchor.X + cx, anchor.Y + h),
            };
        }
    }
}
