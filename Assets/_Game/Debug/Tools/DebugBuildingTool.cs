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
        [SerializeField] private string _def1 = "HQ";
        [SerializeField] private string _def2 = "House";
        [SerializeField] private string _def3 = "Farm";
        [SerializeField] private string _def4 = "Lumber";
        [SerializeField] private string _def5 = "Warehouse";

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

        private InputAction _toggle;     // B
        private InputAction _click;      // LMB
        private InputAction _sel1, _sel2, _sel3, _sel4, _sel5; // 1..5
        private InputAction _rotL, _rotR; // Q/E

        private InputAction _cancel;  // X (cancel build)
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

            _toggle = new InputAction("ToggleBuildingTool", InputActionType.Button, "<Keyboard>/b");
            _click = new InputAction("PlaceBuilding", InputActionType.Button, "<Mouse>/leftButton");

            _sel1 = new InputAction("Sel1", InputActionType.Button, "<Keyboard>/1");
            _sel2 = new InputAction("Sel2", InputActionType.Button, "<Keyboard>/2");
            _sel3 = new InputAction("Sel3", InputActionType.Button, "<Keyboard>/3");
            _sel4 = new InputAction("Sel4", InputActionType.Button, "<Keyboard>/4");
            _sel5 = new InputAction("Sel5", InputActionType.Button, "<Keyboard>/5");

            _rotL = new InputAction("RotL", InputActionType.Button, "<Keyboard>/q");
            _rotR = new InputAction("RotR", InputActionType.Button, "<Keyboard>/e");


            _cancel = new InputAction("CancelBuild", InputActionType.Button, "<Keyboard>/x");
            _selectedDef = _def1;
        }

        private void Start()
        {
            _s = _bootstrap.Services;
            _grid = _s.GridMap;
            _place = _s.PlacementService;
            _data = _s.DataRegistry;
            _noti = _s.NotificationService;

            if (_cam == null) _cam = Camera.main;
        }

        private void OnEnable()
        {
            _toggle.Enable();
            _click.Enable();
            _sel1.Enable(); _sel2.Enable(); _sel3.Enable(); _sel4.Enable(); _sel5.Enable();
            _rotL.Enable(); _rotR.Enable();

            _cancel.Enable();
            _toggle.performed += OnToggle;
            _click.performed += OnClick;

            _sel1.performed += OnSel1;
            _sel2.performed += OnSel2;
            _sel3.performed += OnSel3;
            _sel4.performed += OnSel4;
            _sel5.performed += OnSel5;

            _rotL.performed += OnRotL;
            _rotR.performed += OnRotR;
            _cancel.performed += OnCancel;
        }

        private void OnDisable()
        {
            _toggle.performed -= OnToggle;
            _click.performed -= OnClick;

            _sel1.performed -= OnSel1;
            _sel2.performed -= OnSel2;
            _sel3.performed -= OnSel3;
            _sel4.performed -= OnSel4;
            _sel5.performed -= OnSel5;

            _rotL.performed -= OnRotL;
            _rotR.performed -= OnRotR;

            _cancel.performed -= OnCancel;
            _toggle.Disable();
            _click.Disable();
            _sel1.Disable(); _sel2.Disable(); _sel3.Disable(); _sel4.Disable(); _sel5.Disable();
            _rotL.Disable(); _rotR.Disable();

            _cancel.Disable();
            _hasHover = false;
            _cacheValid = false;
        }

        private void Update()
        {
            if (!_enabled || _place == null)
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
