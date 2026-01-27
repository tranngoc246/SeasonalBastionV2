using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.DebugTools
{
    public sealed class DebugRoadTool : MonoBehaviour
    {
        [Header("Bootstrap (required)")]
        [SerializeField] private GameBootstrap _bootstrap;

        [Header("Mapping (single source)")]
        [SerializeField] private DebugBuildingTool _mappingSource;
        [SerializeField] private Camera _cameraOverride;

        [Header("Grid Mapping (fallback)")]
        [SerializeField] private Vector3 _gridOrigin = Vector3.zero;
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private bool _useXZ = false;
        [SerializeField] private float _planeY = 0f;
        [SerializeField] private float _planeZ = 0f;

        [Header("Gizmos")]
        [SerializeField] private bool _drawRoadGizmos = true;
        [SerializeField] private bool _drawHoverPreview = true;
        [SerializeField] private float _gizmoHeight = 0.05f;
        [SerializeField] private float _hoverPreviewScale = 0.85f;

        [SerializeField] private bool _hubControlled;
        public void SetHubControlled(bool v) => _hubControlled = v;
        public void SetEnabledFromHub(bool enabled) { _enabled = enabled; _anyRoadCacheValid = false; _lastHoverValid = false; }

        private InputAction _toggleTool;  // R
        private InputAction _click;       // LMB
        private bool _enabled;

        private IPlacementService _placement;
        private IGridMap _grid;
        private INotificationService _noti;

        private Camera _cam;

        private bool _hasHover;
        private CellPos _hoverCell;
        private bool _hoverInside;
        private bool _hoverIsRoad;
        private bool _hoverCanPlace;
        private bool _hoverCanRemove;
        private bool _hoverWouldSplitIfRemoved;

        private readonly Queue<CellPos> _q = new Queue<CellPos>(256);
        private bool[] _visited;

        private bool _anyRoadCached;
        private bool _anyRoadCacheValid;
        private CellPos _lastHoverCell;
        private bool _lastHoverValid;

        private void Awake()
        {
            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
            _cam = _cameraOverride != null ? _cameraOverride : Camera.main;
            _anyRoadCacheValid = false;
        }


        private void OnEnable()
        {
            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
            if (_cam == null) _cam = _cameraOverride != null ? _cameraOverride : Camera.main;
        }

        private void OnDisable()
        {
            // no-op
        }

        private void Start()
        {
            var services = _bootstrap.Services;

            _placement = services.PlacementService;
            _grid = services.GridMap;
            _noti = services.NotificationService;

            if (_cam == null) _cam = _cameraOverride != null ? _cameraOverride : Camera.main;

            if (_grid != null)
                _visited = new bool[_grid.Width * _grid.Height];

            _anyRoadCacheValid = false;
            _lastHoverValid = false;
        }

        private void Update()
        {
            // Sync mapping
            if (_mappingSource == null) _mappingSource = FindObjectOfType<DebugBuildingTool>();
            if (_mappingSource != null)
            {
                _gridOrigin = _mappingSource.GridOrigin;
                _cellSize = Mathf.Max(0.0001f, _mappingSource.CellSize);
                _useXZ = _mappingSource.UseXZ;
                _planeY = _mappingSource.PlaneY;
                _planeZ = _mappingSource.PlaneZ;
            }

            if (!_enabled || _grid == null)
            {
                _hasHover = false;
                _lastHoverValid = false;
                return;
            }

            if (!TryGetCellUnderMouse(out var cell))
            {
                _hasHover = false;
                _lastHoverValid = false;
                return;
            }

            _hasHover = true;
            _hoverCell = cell;

            if (!_lastHoverValid || _lastHoverCell.X != cell.X || _lastHoverCell.Y != cell.Y)
            {
                _anyRoadCacheValid = false;
                _lastHoverValid = true;
                _lastHoverCell = cell;
            }

            _hoverInside = _grid.IsInside(cell);
            _hoverIsRoad = _hoverInside && _grid.IsRoad(cell);

            if (!_hoverInside)
            {
                _hoverCanPlace = false;
                _hoverCanRemove = false;
                _hoverWouldSplitIfRemoved = false;
                return;
            }

            if (_hoverIsRoad)
            {
                _hoverWouldSplitIfRemoved = !WouldRoadStayConnectedIfRemoved(cell);
                _hoverCanRemove = !_hoverWouldSplitIfRemoved;
                _hoverCanPlace = false;
            }
            else
            {
                _hoverCanPlace = CanPlaceRoadNoIslands(cell);
                _hoverCanRemove = false;
                _hoverWouldSplitIfRemoved = false;
            }
        
            // ---- Input polling (Option B: no InputActions in tool) ----
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (kb != null && kb.rKey.wasPressedThisFrame) OnToggle(default);
            if (_enabled && mouse != null && mouse.leftButton.wasPressedThisFrame) OnClick(default);
}

        private void OnToggle(InputAction.CallbackContext _)
        {
            if (_hubControlled) return;

            _enabled = !_enabled;
            _anyRoadCacheValid = false;
            _lastHoverValid = false;

            _noti?.Push(
                key: "DebugRoadTool",
                title: "Debug",
                body: _enabled ? "Road Tool: ON (R toggle, LMB place/remove)" : "Road Tool: OFF (R toggle)",
                severity: NotificationSeverity.Info,
                payload: default,
                cooldownSeconds: 0.2f,
                dedupeByKey: true
            );
        }

        private void OnClick(InputAction.CallbackContext _)
        {
            if (!_enabled) return;
            if (_placement == null || _grid == null) return;

            if (!TryGetCellUnderMouse(out var cell))
                return;

            if (!_grid.IsInside(cell))
            {
                _noti?.Push("Road_OutOfBounds", "Road", "Out of bounds", NotificationSeverity.Warning, default, 1.5f, true);
                return;
            }

            if (_grid.IsRoad(cell))
            {
                if (!WouldRoadStayConnectedIfRemoved(cell))
                {
                    _noti?.Push("Road_NoIslands_Remove", "Road", "Cannot remove: would create road islands", NotificationSeverity.Warning, default, 1.5f, true);
                    return;
                }

                _grid.SetRoad(cell, false);
                _anyRoadCacheValid = false;
                return;
            }

            if (!CanPlaceRoadNoIslands(cell))
            {
                if (_grid.IsBlocked(cell))
                    _noti?.Push("Road_Blocked", "Road", "Cell is blocked", NotificationSeverity.Warning, default, 1.5f, true);
                else
                    _noti?.Push("Road_NoIslands_Place", "Road", "Cannot place: must connect to existing roads", NotificationSeverity.Warning, default, 1.5f, true);
                return;
            }

            _placement.PlaceRoad(cell);
            _anyRoadCacheValid = false;
        }

        private bool AnyRoadExistsCached()
        {
            if (_anyRoadCacheValid) return _anyRoadCached;

            bool any = false;
            for (int y = 0; y < _grid.Height && !any; y++)
                for (int x = 0; x < _grid.Width; x++)
                    if (_grid.IsRoad(new CellPos(x, y))) { any = true; break; }

            _anyRoadCached = any;
            _anyRoadCacheValid = true;
            return any;
        }

        private bool HasRoadNeighbor4(CellPos c)
        {
            var n = new CellPos(c.X, c.Y + 1);
            var e = new CellPos(c.X + 1, c.Y);
            var s = new CellPos(c.X, c.Y - 1);
            var w = new CellPos(c.X - 1, c.Y);

            return (_grid.IsInside(n) && _grid.IsRoad(n))
                || (_grid.IsInside(e) && _grid.IsRoad(e))
                || (_grid.IsInside(s) && _grid.IsRoad(s))
                || (_grid.IsInside(w) && _grid.IsRoad(w));
        }

        private bool CanPlaceRoadNoIslands(CellPos c)
        {
            if (!_grid.IsInside(c)) return false;
            if (_grid.IsBlocked(c)) return false;
            if (!_placement.CanPlaceRoad(c)) return false;

            if (AnyRoadExistsCached() && !HasRoadNeighbor4(c))
                return false;

            return true;
        }

        private bool WouldRoadStayConnectedIfRemoved(CellPos removed)
        {
            if (!_grid.IsInside(removed)) return true;
            if (!_grid.IsRoad(removed)) return true;

            int total = 0;
            CellPos start = default;
            bool hasStart = false;

            for (int y = 0; y < _grid.Height; y++)
                for (int x = 0; x < _grid.Width; x++)
                {
                    var c = new CellPos(x, y);
                    if (c.X == removed.X && c.Y == removed.Y) continue;
                    if (_grid.IsRoad(c))
                    {
                        total++;
                        if (!hasStart) { start = c; hasStart = true; }
                    }
                }

            if (total == 0) return true;

            EnsureVisitedBuffer();
            System.Array.Clear(_visited, 0, _visited.Length);
            _q.Clear();

            _q.Enqueue(start);
            _visited[Idx(start)] = true;

            int reached = 0;
            while (_q.Count > 0)
            {
                var cur = _q.Dequeue();
                reached++;

                TryEnqueueRoadNeighbor(cur.X, cur.Y + 1, removed);
                TryEnqueueRoadNeighbor(cur.X + 1, cur.Y, removed);
                TryEnqueueRoadNeighbor(cur.X, cur.Y - 1, removed);
                TryEnqueueRoadNeighbor(cur.X - 1, cur.Y, removed);
            }

            return reached == total;
        }

        private void TryEnqueueRoadNeighbor(int x, int y, CellPos removed)
        {
            var n = new CellPos(x, y);
            if (!_grid.IsInside(n)) return;
            if (n.X == removed.X && n.Y == removed.Y) return;
            if (!_grid.IsRoad(n)) return;

            int i = Idx(n);
            if (_visited[i]) return;
            _visited[i] = true;
            _q.Enqueue(n);
        }

        private int Idx(CellPos c) => c.Y * _grid.Width + c.X;

        private void EnsureVisitedBuffer()
        {
            int need = _grid.Width * _grid.Height;
            if (_visited == null || _visited.Length != need)
                _visited = new bool[need];
        }

        private bool TryGetCellUnderMouse(out CellPos cell)
        {
            cell = default;
            if (_cam == null) return false;
            if (Mouse.current == null) return false;

            var ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane plane = _useXZ
                ? new Plane(Vector3.up, new Vector3(0f, _planeY, 0f))
                : new Plane(Vector3.forward, new Vector3(0f, 0f, _planeZ));

            if (!plane.Raycast(ray, out var enter)) return false;

            Vector3 world = ray.GetPoint(enter);
            var local = world - _gridOrigin;

            int x = Mathf.FloorToInt(local.x / _cellSize);
            int y = _useXZ ? Mathf.FloorToInt(local.z / _cellSize) : Mathf.FloorToInt(local.y / _cellSize);

            cell = new CellPos(x, y);
            return true;
        }

        private void OnDrawGizmos()
        {
            if (!_drawRoadGizmos) return;
            if (_grid == null) return;

            Gizmos.color = Color.white;

            for (int y = 0; y < _grid.Height; y++)
                for (int x = 0; x < _grid.Width; x++)
                {
                    var c = new CellPos(x, y);
                    if (!_grid.IsRoad(c)) continue;
                    Gizmos.DrawCube(CellToWorldCenter(c), CellBoxSize(1f));
                }

            if (_drawHoverPreview && _enabled && _hasHover)
                DrawHoverPreview();
        }

        private void DrawHoverPreview()
        {
            var center = CellToWorldCenter(_hoverCell);
            float s = Mathf.Clamp01(_hoverPreviewScale) * _cellSize;

            if (!_hoverInside)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(center, CellBoxSize(_hoverPreviewScale));
                return;
            }

            if (_hoverIsRoad)
            {
                Gizmos.color = _hoverCanRemove ? Color.yellow : Color.red;
                Gizmos.DrawWireCube(center, CellBoxSize(_hoverPreviewScale));

                if (_hoverWouldSplitIfRemoved)
                    DrawX(center, s * 0.5f);

                return;
            }

            Gizmos.color = _hoverCanPlace ? Color.green : Color.red;
            Gizmos.DrawWireCube(center, CellBoxSize(_hoverPreviewScale));
        }

        private void DrawX(Vector3 center, float half)
        {
            Vector3 a, b, c, d;
            if (_useXZ)
            {
                a = center + new Vector3(-half, 0f, -half);
                b = center + new Vector3(half, 0f, half);
                c = center + new Vector3(-half, 0f, half);
                d = center + new Vector3(half, 0f, -half);
            }
            else
            {
                a = center + new Vector3(-half, -half, 0f);
                b = center + new Vector3(half, half, 0f);
                c = center + new Vector3(-half, half, 0f);
                d = center + new Vector3(half, -half, 0f);
            }

            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(c, d);
        }

        private Vector3 CellToWorldCenter(CellPos c)
        {
            float wx = _gridOrigin.x + (c.X + 0.5f) * _cellSize;

            if (_useXZ)
            {
                float wy = _planeY + (_gizmoHeight * 0.5f);
                float wz = _gridOrigin.z + (c.Y + 0.5f) * _cellSize;
                return new Vector3(wx, wy, wz);
            }

            float wy2 = _gridOrigin.y + (c.Y + 0.5f) * _cellSize;
            return new Vector3(wx, wy2, _planeZ);
        }

        private Vector3 CellBoxSize(float scale01)
        {
            float s = Mathf.Clamp01(scale01) * _cellSize;
            return _useXZ ? new Vector3(s, _gizmoHeight, s) : new Vector3(s, s, _gizmoHeight);
        }
    }
}
