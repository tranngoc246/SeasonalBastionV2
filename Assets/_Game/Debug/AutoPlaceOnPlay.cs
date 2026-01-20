using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.DebugTools
{
    /// <summary>
    /// Auto place roads + buildings right after Play (after GameBootstrap started a run).
    /// - No InputActions touched.
    /// - Uses PlacementService as single source of truth.
    /// - Optional: enforces "no road islands" rule like DebugRoadTool.
    /// </summary>
    public sealed class AutoPlaceOnPlay : MonoBehaviour
    {
        [Header("Bootstrap (required)")]
        [SerializeField] private GameBootstrap _bootstrap;

        [Header("Run")]
        [SerializeField] private bool _runOnce = true;
        [SerializeField] private int _delayFrames = 1;

        [Header("Road rules")]
        [SerializeField] private bool _enforceNoRoadIslands = true;

        [Header("Road Lines (orthogonal)")]
        [SerializeField] private RoadLine[] _roads;

        [Header("Buildings (commit -> creates build orders + sites)")]
        [SerializeField] private BuildingSpawn[] _buildings;

        private bool _didRun;

        private void Awake()
        {
            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
        }

        private void Start()
        {
            StartCoroutine(RunAfterBootstrap());
        }

        private IEnumerator RunAfterBootstrap()
        {
            if (_runOnce && _didRun) yield break;
            if (_bootstrap == null) yield break;

            // Wait a few frames to ensure GameBootstrap.Awake() + StartNewRun already executed.
            for (int i = 0; i < Mathf.Max(0, _delayFrames); i++)
                yield return null;

            var s = _bootstrap.Services;
            if (s == null) yield break;

            var grid = s.GridMap;
            var placement = s.PlacementService;

            if (grid == null || placement == null)
            {
                Debug.LogWarning("[AutoPlaceOnPlay] Missing GridMap / PlacementService.");
                yield break;
            }

            // Ensure PlacementService has BuildOrders bound (in case factory forgot).
            if (placement is SeasonalBastion.PlacementService ps && s.BuildOrderService != null)
                ps.BindBuildOrders(s.BuildOrderService);

            // 1) Place roads
            bool anyRoad = ScanAnyRoad(grid);

            int roadPlaced = 0;
            if (_roads != null)
            {
                for (int i = 0; i < _roads.Length; i++)
                {
                    var ln = _roads[i];
                    foreach (var c in ln.EnumerateCells())
                    {
                        if (!grid.IsInside(c)) continue;
                        if (grid.IsRoad(c)) { anyRoad = true; continue; }

                        if (!placement.CanPlaceRoad(c)) continue;

                        if (_enforceNoRoadIslands && anyRoad && !HasRoadNeighbor4(grid, c))
                            continue;

                        placement.PlaceRoad(c);
                        anyRoad = true;
                        roadPlaced++;
                    }
                }
            }

            // 2) Place buildings
            int bOk = 0, bFail = 0;
            if (_buildings != null)
            {
                for (int i = 0; i < _buildings.Length; i++)
                {
                    var b = _buildings[i];
                    if (string.IsNullOrEmpty(b.DefId)) continue;

                    var vr = placement.ValidateBuilding(b.DefId, b.AnchorCell, b.Rotation);
                    if (!vr.Ok)
                    {
                        bFail++;
                        Debug.LogWarning($"[AutoPlaceOnPlay] Place FAIL: {b.DefId} @({b.Anchor.x},{b.Anchor.y}) rot={b.Rotation} reason={vr.Reason} suggestedRoad=({vr.SuggestedRoadCell.X},{vr.SuggestedRoadCell.Y})");
                        continue;
                    }

                    var id = placement.CommitBuilding(b.DefId, b.AnchorCell, b.Rotation);
                    if (id.Value == 0)
                    {
                        bFail++;
                        Debug.LogWarning($"[AutoPlaceOnPlay] Commit FAIL: {b.DefId} @({b.Anchor.x},{b.Anchor.y}) rot={b.Rotation} (Commit returned default)");
                        continue;
                    }

                    bOk++;
                    Debug.Log($"[AutoPlaceOnPlay] Placed: {b.DefId} -> BuildingId={id.Value} @({b.Anchor.x},{b.Anchor.y}) rot={b.Rotation}");
                }
            }

            Debug.Log($"[AutoPlaceOnPlay] Done. RoadsPlaced={roadPlaced}, BuildingsOk={bOk}, BuildingsFail={bFail}");

            _didRun = true;
        }

        private static bool ScanAnyRoad(IGridMap grid)
        {
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (grid.IsRoad(new CellPos(x, y)))
                        return true;
            return false;
        }

        private static bool HasRoadNeighbor4(IGridMap grid, CellPos c)
        {
            var n = new CellPos(c.X, c.Y + 1);
            var e = new CellPos(c.X + 1, c.Y);
            var s = new CellPos(c.X, c.Y - 1);
            var w = new CellPos(c.X - 1, c.Y);

            return (grid.IsInside(n) && grid.IsRoad(n))
                || (grid.IsInside(e) && grid.IsRoad(e))
                || (grid.IsInside(s) && grid.IsRoad(s))
                || (grid.IsInside(w) && grid.IsRoad(w));
        }

        // ----------------------------
        // Inspector DTOs
        // ----------------------------

        [System.Serializable]
        public struct RoadLine
        {
            public Vector2Int From;   // inspector-friendly
            public Vector2Int To;     // inspector-friendly

            public IEnumerable<CellPos> EnumerateCells()
            {
                int fx = From.x, fy = From.y;
                int tx = To.x, ty = To.y;

                int dx = tx - fx;
                int dy = ty - fy;

                if (dx != 0 && dy != 0)
                    yield break; 

                int sx = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
                int sy = dy == 0 ? 0 : (dy > 0 ? 1 : -1);

                int len = Mathf.Abs(dx) + Mathf.Abs(dy);

                int cx = fx, cy = fy;
                yield return new CellPos(cx, cy);

                for (int i = 0; i < len; i++)
                {
                    cx += sx; cy += sy;
                    yield return new CellPos(cx, cy);
                }
            }
        }

        [System.Serializable]
        public struct BuildingSpawn
        {
            public string DefId;
            public Vector2Int Anchor; // inspector-friendly
            public Dir4 Rotation;

            public CellPos AnchorCell => new CellPos(Anchor.x, Anchor.y);
        }

    }
}
