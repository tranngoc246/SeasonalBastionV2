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
        [SerializeField] private bool _useXZ = true;
        [SerializeField] private float _planeY = 0f;

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

        private Camera _cam;
        private bool _enabled;

        // Services
        private GameServices _s;
        private IGridMap _grid;
        private IPlacementService _place;
        private IDataRegistry _data;
        private INotificationService _noti;

        // Preview state
        private string _selectedDef;
        private CellPos _hoverCell;
        private bool _hasHover;
        private PlacementResult _lastValidate;
        private Dir4 _rotation = Dir4.N;

        // Cache to avoid re-validating every frame
        private bool _cacheValid;
        private CellPos _cacheCell;
        private string _cacheDef;
        private Dir4 _cacheRot;

        private void Awake()
        {
            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
            _cam = Camera.main;

            // Runtime actions - do not touch InputActions asset
            _toggle = new InputAction("ToggleBuildingTool", InputActionType.Button, "<Keyboard>/b");
            _click = new InputAction("PlaceBuilding", InputActionType.Button, "<Mouse>/leftButton");

            _sel1 = new InputAction("Sel1", InputActionType.Button, "<Keyboard>/1");
            _sel2 = new InputAction("Sel2", InputActionType.Button, "<Keyboard>/2");
            _sel3 = new InputAction("Sel3", InputActionType.Button, "<Keyboard>/3");
            _sel4 = new InputAction("Sel4", InputActionType.Button, "<Keyboard>/4");
            _sel5 = new InputAction("Sel5", InputActionType.Button, "<Keyboard>/5");

            _rotL = new InputAction("RotL", InputActionType.Button, "<Keyboard>/q");
            _rotR = new InputAction("RotR", InputActionType.Button, "<Keyboard>/e");

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

            _toggle.performed += OnToggle;
            _click.performed += OnClick;

            _sel1.performed += OnSel1;
            _sel2.performed += OnSel2;
            _sel3.performed += OnSel3;
            _sel4.performed += OnSel4;
            _sel5.performed += OnSel5;

            _rotL.performed += OnRotL;
            _rotR.performed += OnRotR;
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

            _toggle.Disable();
            _click.Disable();
            _sel1.Disable(); _sel2.Disable(); _sel3.Disable(); _sel4.Disable(); _sel5.Disable();
            _rotL.Disable(); _rotR.Disable();

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

            // Only revalidate when something changes
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

        private void OnSel1(InputAction.CallbackContext _) => Select(_def1);
        private void OnSel2(InputAction.CallbackContext _) => Select(_def2);
        private void OnSel3(InputAction.CallbackContext _) => Select(_def3);
        private void OnSel4(InputAction.CallbackContext _) => Select(_def4);
        private void OnSel5(InputAction.CallbackContext _) => Select(_def5);

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
            {
                _noti?.Push("Build_CommitFailed", "Build", "Commit failed (id=0)", NotificationSeverity.Error, default, 1.0f, true);
                return;
            }
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

            Vector3 world;

            if (_useXZ)
            {
                var ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
                var plane = new Plane(Vector3.up, new Vector3(0f, _planeY, 0f));
                if (!plane.Raycast(ray, out var enter)) return false;
                world = ray.GetPoint(enter);
            }
            else
            {
                var ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
                var plane = new Plane(Vector3.forward, Vector3.zero); // z=0
                if (!plane.Raycast(ray, out var enter)) return false;
                world = ray.GetPoint(enter);
            }

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
            Gizmos.color = new Color(1f, 0.6f, 0f, 1f); // orange
            for (int y = 0; y < _grid.Height; y++)
                for (int x = 0; x < _grid.Width; x++)
                {
                    var c = new CellPos(x, y);
                    var occ = _grid.Get(c);
                    if (occ.Kind != CellOccupancyKind.Site) continue;
                    Gizmos.DrawCube(CellCenter(c), new Vector3(_cellSize, _gizmoHeight, _cellSize));
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

                    Gizmos.DrawCube(CellCenter(c), new Vector3(_cellSize, _gizmoHeight, _cellSize));
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
            catch { /* keep 1x1 */ }

            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                {
                    var c = new CellPos(_hoverCell.X + dx, _hoverCell.Y + dy);

                    bool inside = _grid.IsInside(c);

                    // inside: use ok/fail color
                    // outside: draw faint gray (still visible footprint)
                    if (inside)
                        Gizmos.color = _lastValidate.Ok ? _okColor : _failColor;
                    else
                        Gizmos.color = _failColor; 

                    Gizmos.DrawWireCube(CellCenter(c), new Vector3(_cellSize, _gizmoHeight, _cellSize));
                }
        }

        private void DrawDrivewayHint()
        {
            // Always draw SuggestedRoadCell (even if outside map) for invalid preview debugging.
            var d = _lastValidate.SuggestedRoadCell;

            Gizmos.color = _lastValidate.Ok ? _drivewayColor : Color.red;
            var center = CellCenter(d);
            var s = _cellSize * 0.6f;
            Gizmos.DrawWireCube(center, new Vector3(s, _gizmoHeight, s));
        }

        private void DrawEntryHint()
        {
            // Always draw entry (SuggestedRoadCell), even when invalid, and even if outside map.
            var entry = _lastValidate.SuggestedRoadCell;

            Gizmos.color = _lastValidate.Ok ? Color.yellow : Color.red;
            var center = CellCenter(entry);
            float s2 = _cellSize * 0.6f;
            Gizmos.DrawWireCube(center, new Vector3(s2, _gizmoHeight, s2));
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
                return new Vector3(wx, wy, 0f);
            }
        }
    }
}
