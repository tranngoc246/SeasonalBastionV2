using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.RunStart
{
    internal static class RunStartPlacementHelper
    {
        internal const string HqDefId = "bld_hq_t1";

        internal static string ResolveBuildingDefIdOrNull(GameServices s, string defId)
        {
            if (string.IsNullOrEmpty(defId) || s?.DataRegistry == null) return null;
            return HasBuildingDef(s, defId) ? defId : null;
        }

        internal static bool TryPickValidAnchor(
            GameServices s,
            string buildingDefId,
            CellPos desiredAnchor,
            int w,
            int h,
            Dir4 rot,
            out CellPos finalAnchor)
        {
            finalAnchor = desiredAnchor;
            if (s == null || s.GridMap == null || string.IsNullOrEmpty(buildingDefId)) return false;

            var placement = s.PlacementService;
            if (placement == null) return true;

            bool hasBuildableRect = s.RunStartRuntime != null && (s.RunStartRuntime.BuildableRect.XMax != 0 || s.RunStartRuntime.BuildableRect.YMax != 0);
            var rect = hasBuildableRect ? s.RunStartRuntime.BuildableRect : default;

            bool IsFootprintInBuildable(CellPos a)
            {
                if (!hasBuildableRect) return true;
                if (!rect.Contains(a)) return false;
                return rect.Contains(new CellPos(a.X + w - 1, a.Y + h - 1));
            }

            bool IsCandidateOk(CellPos a)
            {
                if (!IsFootprintInBuildable(a)) return false;
                var pr = placement.ValidateBuilding(buildingDefId, a, rot);
                return pr.Ok;
            }

            if (IsCandidateOk(desiredAnchor))
            {
                finalAnchor = desiredAnchor;
                return true;
            }

            const int maxR = 24;
            for (int r = 1; r <= maxR; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    int ax = r - Math.Abs(dy);
                    int dx1 = -ax;
                    int dx2 = ax;

                    var c1 = new CellPos(desiredAnchor.X + dx1, desiredAnchor.Y + dy);
                    if (IsCandidateOk(c1)) { finalAnchor = c1; return true; }

                    if (dx2 != dx1)
                    {
                        var c2 = new CellPos(desiredAnchor.X + dx2, desiredAnchor.Y + dy);
                        if (IsCandidateOk(c2)) { finalAnchor = c2; return true; }
                    }
                }
            }

            return false;
        }

        internal static bool HasBuildingDef(GameServices s, string id)
        {
            try { s.DataRegistry.GetBuilding(id); return true; }
            catch { return false; }
        }

        internal static Dir4 ParseDir4(string s)
        {
            if (string.IsNullOrEmpty(s)) return Dir4.N;
            char c = char.ToUpperInvariant(s[0]);
            return c switch
            {
                'N' => Dir4.N,
                'E' => Dir4.E,
                'S' => Dir4.S,
                'W' => Dir4.W,
                _ => Dir4.N
            };
        }

        internal static void PromoteRunStartEntryRoads(GameServices s, BuildingState b, int w, int h)
        {
            if (s == null || s.GridMap == null) return;

            if (string.Equals(b.DefId, HqDefId, StringComparison.OrdinalIgnoreCase))
            {
                PromoteRoadIfPossible(s, ComputeEntryOutsideFootprint(b.Anchor, w, h, Dir4.N));
                PromoteRoadIfPossible(s, ComputeEntryOutsideFootprint(b.Anchor, w, h, Dir4.S));
                PromoteRoadIfPossible(s, ComputeEntryOutsideFootprint(b.Anchor, w, h, Dir4.E));
                PromoteRoadIfPossible(s, ComputeEntryOutsideFootprint(b.Anchor, w, h, Dir4.W));
                return;
            }

            PromoteRoadIfPossible(s, ComputeEntryOutsideFootprint(b.Anchor, w, h, b.Rotation));
        }

        internal static void PromoteRoadIfPossible(GameServices s, CellPos cell)
        {
            if (!s.GridMap.IsInside(cell)) return;

            var occ = s.GridMap.Get(cell);
            if (occ.Kind == CellOccupancyKind.Building) return;
            if (occ.Kind == CellOccupancyKind.Site) return;

            if (!s.GridMap.IsRoad(cell))
                s.GridMap.SetRoad(cell, true);
        }

        internal static CellPos ComputeEntryOutsideFootprint(CellPos anchor, int w, int h, Dir4 rot)
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
