using UnityEngine;
using UnityEngine.InputSystem;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.DebugTools
{
    public sealed class DebugBuildingTool : MonoBehaviour
    {
        [Header("Bootstrap (required)")]
        [SerializeField] private GameBootstrap _bootstrap;

        [Header("Defs (ids must exist in DataRegistry)")]
        [SerializeField] private string _def1 = "bld_armory_t1";
        [SerializeField] private string _def2 = "bld_forge_t1";
        [SerializeField] private string _def3 = "bld_farmhouse_t1";
        [SerializeField] private string _def4 = "bld_lumbercamp_t1";
        [SerializeField] private string _def5 = "bld_warehouse_t1";

        [Header("Grid Mapping")]
        [SerializeField] private Vector3 _gridOrigin = Vector3.zero;
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private bool _useXZ = false; // 2D: false
        [SerializeField] private float _planeY = 0f;
        [SerializeField] private float _planeZ = 0f; // 2D XY plane at z = planeZ

        [SerializeField] private bool _hubControlled;
        public void SetHubControlled(bool v) => _hubControlled = v;
        public void SetEnabledFromHub(bool enabled) { _enabled = enabled; _cacheValid = false; _hasHover = false; _lastValidate = default; }

        public Vector3 GridOrigin => _gridOrigin;
        public float CellSize => _cellSize;
        public bool UseXZ => _useXZ;
        public float PlaneY => _planeY;
        public float PlaneZ => _planeZ;

        [Header("Gizmos")]
        [SerializeField] private bool _drawPreview = true;
        [SerializeField] private bool _drawPlacedBuildings = true;
        [SerializeField] private bool _drawDrivewayHint = true;
        [SerializeField] private float _gizmoHeight = 0.06f;

        [Header("Preview Colors")]
        [SerializeField] private Color _okColor = new Color(0f, 1f, 0f, 1f);
        [SerializeField] private Color _failColor = new Color(1f, 0f, 0f, 1f);
        [SerializeField] private Color _drivewayColor = new Color(1f, 1f, 0f, 1f);
        private Camera _cam;
        private bool _enabled;

        private GameServices _s;
        private IGridMap _grid;
        private IPlacementService _place;
        private IDataRegistry _data;
        private INotificationService _noti;

        private string _selectedDef;
        private CellPos _hoverCell;
        private bool _hasHover;
        private PlacementResult _lastValidate;
        private Dir4 _rotation = Dir4.N;

        private bool _cacheValid;
        private CellPos _cacheCell;
        private string _cacheDef;
        private Dir4 _cacheRot;

        private void Awake()
        {
            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
            _cam = Camera.main;
            _cacheValid = false;
        }

        private void Start()
        {
            _s = _bootstrap.Services;
            var unlock = _s.UnlockService;
            _grid = _s.GridMap;
            _place = _s.PlacementService;
            _data = _s.DataRegistry;
            _noti = _s.NotificationService;

            if (_cam == null) _cam = Camera.main;
        }

        private void OnEnable()
        {
            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
            if (_cam == null) _cam = Camera.main;
        }

        private void OnDisable()
        {
            // no-op (hotkeys handled by DebugHUDHub + polling)
        }

        private void Update()
        {
            ResolveServices();

            if (!_enabled)
            {
                _hasHover = false;
                _cacheValid = false;
                _lastValidate = default;
                return;
            }

            if (_place == null || _grid == null)
            {
                _hasHover = false;
                _cacheValid = false;
                _lastValidate = default;
                return;
            }

            if (!TryGetCellUnderMouse(out var c))
            {
                _hasHover = false;
                _cacheValid = false;
                _lastValidate = default;
                return;
            }

            _hasHover = true;
            _hoverCell = c;

            if (!_cacheValid || _cacheCell.X != c.X || _cacheCell.Y != c.Y || _cacheRot != _rotation || _cacheDef != _selectedDef)
            {
                _lastValidate = _place.ValidateBuilding(_selectedDef, _hoverCell, _rotation);
                _cacheValid = true;
                _cacheCell = c;
                _cacheDef = _selectedDef;
                _cacheRot = _rotation;
            }
        
            // ---- Input polling (Option B: no InputActions in tool) ----
            if (_enabled)
            {
                var kb = Keyboard.current;
                var mouse = Mouse.current;
                if (kb != null)
                {
                    if (kb.digit1Key.wasPressedThisFrame) OnSel1(default);
                    if (kb.digit2Key.wasPressedThisFrame) OnSel2(default);
                    if (kb.digit3Key.wasPressedThisFrame) OnSel3(default);
                    if (kb.digit4Key.wasPressedThisFrame) OnSel4(default);
                    if (kb.digit5Key.wasPressedThisFrame) OnSel5(default);

                    if (kb.qKey.wasPressedThisFrame) OnRotL(default);
                    if (kb.eKey.wasPressedThisFrame) OnRotR(default);

                    if (kb.xKey.wasPressedThisFrame) OnCancel(default);
                    if (kb.kKey.wasPressedThisFrame) OnDamage(default);
                    if (kb.rKey.wasPressedThisFrame) OnRepair(default);

                    // standalone toggle (only if not hub-controlled)
                    if (kb.bKey.wasPressedThisFrame) OnToggle(default);
                }

                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                    OnClick(default);
            }
}

        private void ResolveServices()
        {
            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
            if (_bootstrap == null) return;

            _s ??= _bootstrap.Services;
            if (_s == null) return;

            _grid ??= _s.GridMap;
            _place ??= _s.PlacementService;
            _noti ??= _s.NotificationService;

            if (_cam == null) _cam = Camera.main;
        }

        private void OnToggle(InputAction.CallbackContext _)
        {
            if (_hubControlled) return;

            _enabled = !_enabled;
            _cacheValid = false;

            _noti?.Push(
                key: "DebugBuildingTool",
                title: "Debug",
                body: _enabled ? "Building Tool: ON (1-5 select, Q/E rotate, LMB place)" : "Building Tool: OFF",
                severity: NotificationSeverity.Info,
                payload: default,
                cooldownSeconds: 0.2f,
                dedupeByKey: true
            );
        }

        private void OnSel1(InputAction.CallbackContext _) { if (!_enabled) return; Select(_def1); }
        private void OnSel2(InputAction.CallbackContext _) { if (!_enabled) return; Select(_def2); }
        private void OnSel3(InputAction.CallbackContext _) { if (!_enabled) return; Select(_def3); }
        private void OnSel4(InputAction.CallbackContext _) { if (!_enabled) return; Select(_def4); }
        private void OnSel5(InputAction.CallbackContext _) { if (!_enabled) return; Select(_def5); }

        private void Select(string defId)
        {
            if (_s != null && _s.UnlockService != null && !_s.UnlockService.IsUnlocked(defId))
            {
                _noti?.Push("Build_Locked", "Build", $"Locked: {defId}", NotificationSeverity.Warning, default, 0.5f, true);
                return;
            }

            _selectedDef = defId;
            _cacheValid = false;

            _noti?.Push(
                key: "Build_Selected",
                title: "Build",
                body: $"Selected: {defId}",
                severity: NotificationSeverity.Info,
                payload: default,
                cooldownSeconds: 0.2f,
                dedupeByKey: true
            );
        }


        private void OnRotL(InputAction.CallbackContext _)
        {
            if (!_enabled) return;
            Rotate(-1);
        }

        private void OnRotR(InputAction.CallbackContext _)
        {
            if (!_enabled) return;
            Rotate(+1);
        }

        private void Rotate(int dir)
        {
            int v = (int)_rotation;
            v = (v + dir) % 4;
            if (v < 0) v += 4;
            _rotation = (Dir4)v;
            _cacheValid = false;

            _noti?.Push(
                key: "Build_Rotation",
                title: "Build",
                body: $"Rotation: {_rotation} (Q/E)",
                severity: NotificationSeverity.Info,
                payload: default,
                cooldownSeconds: 0.15f,
                dedupeByKey: true
            );
        }

        private void OnCancel(InputAction.CallbackContext _)
        {
            if (!_enabled || !_hasHover) return;
            if (_s == null || _grid == null) return;

            var occ = _grid.Get(_hoverCell);
            if (occ.Kind != CellOccupancyKind.Site || occ.Site.Value == 0) return;

            if (_s.BuildOrderService is BuildOrderService bos)
            {
                bool ok = bos.CancelBySite(occ.Site);
                if (ok) _noti?.Push("Build_Cancel_OK", "Build", $"Cancelled Site {occ.Site.Value}", NotificationSeverity.Info, default, 0.10f, true);
                else _noti?.Push("Build_Cancel_NoOrder", "Build", $"No order for Site {occ.Site.Value}", NotificationSeverity.Warning, default, 0.25f, true);
            }
        }

        private void OnClick(InputAction.CallbackContext _)
        {
            if (!_enabled || !_hasHover || _place == null) return;

            if (_s != null && _s.UnlockService != null && !_s.UnlockService.IsUnlocked(_selectedDef))
            {
                _noti?.Push("Build_Locked", "Build", $"Locked: {_selectedDef}", NotificationSeverity.Warning, default, 0.5f, true);
                return;
            }

            var vr = _place.ValidateBuilding(_selectedDef, _hoverCell, _rotation);
            if (!vr.Ok)
            {
                PushFail(vr.Reason);
                return;
            }

            var id = _place.CommitBuilding(_selectedDef, _hoverCell, _rotation);
            if (id.Value == 0)
                _noti?.Push("Build_CommitFailed", "Build", "Commit failed (id=0)", NotificationSeverity.Error, default, 1.0f, true);
        }

        private void OnDamage(InputAction.CallbackContext _)
        {
            if (!_enabled || !_hasHover) return;
            if (_s == null || _grid == null) return;

            var occ = _grid.Get(_hoverCell);
            if (occ.Kind != CellOccupancyKind.Building || occ.Building.Value == 0) return;
            if (!_s.WorldState.Buildings.Exists(occ.Building)) return;

            var bs = _s.WorldState.Buildings.Get(occ.Building);

            // Day22: requires BuildingState.HP/MaxHP
            if (bs.MaxHP <= 0) bs.MaxHP = 100;
            if (bs.HP <= 0) bs.HP = bs.MaxHP;

            bs.HP -= 50;
            if (bs.HP < 0) bs.HP = 0;

            _s.WorldState.Buildings.Set(occ.Building, bs);

            _noti?.Push("Dbg_Damage", "Build", $"Damage {bs.DefId}: {bs.HP}/{bs.MaxHP}", NotificationSeverity.Warning, default, 0.10f, true);
        }

        private void OnRepair(InputAction.CallbackContext _)
        {
            if (!_enabled || !_hasHover) return;
            if (_s == null || _grid == null) return;

            var occ = _grid.Get(_hoverCell);
            if (occ.Kind != CellOccupancyKind.Building || occ.Building.Value == 0) return;

            int id = _s.BuildOrderService.CreateRepairOrder(occ.Building);
            if (id > 0) _noti?.Push("Dbg_Repair", "Build", $"Repair order #{id} created", NotificationSeverity.Info, default, 0.10f, true);
            else _noti?.Push("Dbg_Repair_Fail", "Build", "Repair order not created (maybe full HP / invalid)", NotificationSeverity.Warning, default, 0.25f, true);
        }

        private void PushFail(PlacementFailReason reason)
        {
            string key = reason switch
            {
                PlacementFailReason.OutOfBounds => "Build_OutOfBounds",
                PlacementFailReason.Overlap => "Build_Overlap",
                PlacementFailReason.BlockedBySite => "Build_BlockedBySite",
                PlacementFailReason.NoRoadConnection => "Build_NoRoad",
                PlacementFailReason.InvalidRotation => "Build_BadRot",
                _ => "Build_Unknown"
            };

            _noti?.Push(key, "Build", reason.ToString(), NotificationSeverity.Warning, default, 1.5f, true);
        }

        private bool TryGetCellUnderMouse(out CellPos cell)
        {
            cell = default;
            if (_cam == null) return false;
            if (Mouse.current == null) return false;

            var ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());

            Plane plane = _useXZ
                ? new Plane(Vector3.up, new Vector3(0f, _planeY, 0f))
                : new Plane(Vector3.forward, new Vector3(0f, 0f, _planeZ)); // XY at z=planeZ

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
            ResolveServices();

            if (!_drawPreview && !_drawPlacedBuildings) return;
            if (_grid == null) return;

            if (_drawPlacedBuildings)
            {
                DrawPlacedSiteCells();
                DrawPlacedBuildingCells();
            }

            if (_drawPreview && _enabled && _hasHover)
            {
                DrawPreviewFootprint();
                DrawEntryHint();
                if (_drawDrivewayHint) DrawDrivewayHint();
            }
        }

        private void DrawPlacedSiteCells()
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 1f);
            for (int y = 0; y < _grid.Height; y++)
                for (int x = 0; x < _grid.Width; x++)
                {
                    var c = new CellPos(x, y);
                    var occ = _grid.Get(c);
                    if (occ.Kind != CellOccupancyKind.Site) continue;
                    Gizmos.DrawCube(CellCenter(c), CellBoxSize(1f));
                }
        }

        private void DrawPlacedBuildingCells()
        {
            Gizmos.color = Color.white;
            for (int y = 0; y < _grid.Height; y++)
                for (int x = 0; x < _grid.Width; x++)
                {
                    var c = new CellPos(x, y);
                    var occ = _grid.Get(c);
                    if (occ.Kind != CellOccupancyKind.Building) continue;
                    Gizmos.DrawCube(CellCenter(c), CellBoxSize(1f));
                }
        }

        private void DrawPreviewFootprint()
        {
            int w = 1, h = 1;
            try
            {
                var def = _data.GetBuilding(_selectedDef);
                w = Mathf.Max(1, def.SizeX);
                h = Mathf.Max(1, def.SizeY);
            }
            catch { }

            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                {
                    var c = new CellPos(_hoverCell.X + dx, _hoverCell.Y + dy);
                    bool inside = _grid.IsInside(c);

                    Gizmos.color = inside ? (_lastValidate.Ok ? _okColor : _failColor) : _failColor;
                    Gizmos.DrawWireCube(CellCenter(c), CellBoxSize(1f));
                }
        }

        private void DrawDrivewayHint()
        {
            var d = _lastValidate.SuggestedRoadCell;
            Gizmos.color = _lastValidate.Ok ? _drivewayColor : Color.red;
            Gizmos.DrawWireCube(CellCenter(d), CellBoxSize(0.6f));
        }

        private void DrawEntryHint()
        {
            var entry = _lastValidate.SuggestedRoadCell;
            Gizmos.color = _lastValidate.Ok ? Color.yellow : Color.red;
            Gizmos.DrawWireCube(CellCenter(entry), CellBoxSize(0.6f));
        }

        private Vector3 CellCenter(CellPos c)
        {
            float wx = _gridOrigin.x + (c.X + 0.5f) * _cellSize;

            if (_useXZ)
            {
                float wy = _planeY + (_gizmoHeight * 0.5f);
                float wz = _gridOrigin.z + (c.Y + 0.5f) * _cellSize;
                return new Vector3(wx, wy, wz);
            }
            else
            {
                float wy = _gridOrigin.y + (c.Y + 0.5f) * _cellSize;
                return new Vector3(wx, wy, _planeZ);
            }
        }

        private Vector3 CellBoxSize(float scale)
        {
            float s = _cellSize * Mathf.Max(0.0001f, scale);
            // XZ: thin by Y, XY: thin by Z
            return _useXZ ? new Vector3(s, _gizmoHeight, s) : new Vector3(s, s, _gizmoHeight);
        }
    }
}
