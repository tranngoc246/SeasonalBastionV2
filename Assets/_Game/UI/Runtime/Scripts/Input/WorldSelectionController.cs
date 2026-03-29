using SeasonalBastion.Contracts;
using SeasonalBastion.UI.Services;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Input
{
    /// <summary>
    /// P0.2: Click world -> select building -> Inspect panel.
    /// - Deterministic: pick theo IGridMap occupancy (không collider).
    /// - Gate: block khi pointer đang ở UI (ui-block-world) hoặc modal stack mở.
    /// - Nếu click vào Site: chọn TargetBuilding của site (placeholder building dưới công trình đang xây).
    /// </summary>
    public sealed class WorldSelectionController : MonoBehaviour
    {
        [Header("UI System (optional, auto-find if null)")]
        [SerializeField] private UiSystem _uiSystem;

        [Header("Optional: Services Provider (implements IUiServicesProvider)")]
        [SerializeField] private MonoBehaviour _servicesProvider;

        [Header("Optional: Placement controller (skip selection while placing/tool active)")]
        [SerializeField] private PlacementInputController _placement;

        [Header("World mapping")]
        [SerializeField] private Camera _cameraOverride;
        [SerializeField] private Grid _grid;
        [SerializeField] private bool _useXZ = false; // 2D XY => false
        [SerializeField] private float _planeZ = 0f;  // XY plane z
        [SerializeField] private float _planeY = 0f;  // XZ plane y

        [Header("Behavior")]
        [SerializeField] private bool _toggleOffWhenClickSame = true;
        [SerializeField] private bool _clearSelectionWhenClickEmpty = true;

        private UIStateStore _store;
        private IInputGate _gate;

        private IGridMap _gridMap;
        private IWorldState _world;
        private ResourcePatchService _resourcePatches;

        private Camera _cam;
        private bool _bound;
        private bool _warned;

        private void Awake()
        {
            _cam = _cameraOverride != null ? _cameraOverride : Camera.main;
        }

        private void OnEnable()
        {
            TryBind();
        }

        private void Update()
        {
            if (!_bound)
            {
                TryBind();
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null) return;

            // Only react on click
            if (!mouse.leftButton.wasPressedThisFrame) return;

            // Gate: modal open or pointer over blocking ui
            if (_gate != null && !_gate.IsWorldInputAllowed)
                return;

            // Extra safety: do realtime hit-test this frame (avoid script execution order issues)
            if (IsPointerOverBlockingUiNow(mouse.position.ReadValue()))
                return;

            // Skip selection while placing building / road tool is active
            if (_placement != null && _placement.IsWorldActionActive)
                return;

            if (!TryGetCellUnderMouse(out var cell))
            {
                if (_clearSelectionWhenClickEmpty) _store?.ClearSelection();
                return;
            }

            if (_gridMap == null || !_gridMap.IsInside(cell))
            {
                if (_clearSelectionWhenClickEmpty) _store?.ClearSelection();
                return;
            }

            var occ = _gridMap.Get(cell);

            // Building cell -> select building
            if (occ.Kind == CellOccupancyKind.Building && occ.Building.Value != 0)
            {
                SelectBuilding(occ.Building.Value);
                return;
            }

            // Site cell -> select target building (placeholder) if available
            if (occ.Kind == CellOccupancyKind.Site && occ.Site.Value != 0 && _world?.Sites != null)
            {
                if (_world.Sites.Exists(occ.Site))
                {
                    var site = _world.Sites.Get(occ.Site);
                    if (site.TargetBuilding.Value != 0)
                    {
                        SelectBuilding(site.TargetBuilding.Value);
                        return;
                    }
                }
            }

            // Resource patch cell -> select patch
            if (_resourcePatches != null && _resourcePatches.TryGetPatchAtCell(cell, out var patch))
            {
                SelectResourcePatch(patch.Id);
                return;
            }

            // Else: clear selection
            if (_clearSelectionWhenClickEmpty) _store?.ClearSelection();
        }

        private void SelectBuilding(int id)
        {
            if (_store == null) return;

            if (_toggleOffWhenClickSame && _store.Selected.Kind == SelectionKind.Building && _store.Selected.Id == id)
                _store.ClearSelection();
            else
                _store.SelectBuilding(id);
        }

        private void SelectResourcePatch(int id)
        {
            if (_store == null) return;

            if (_toggleOffWhenClickSame && _store.Selected.Kind == SelectionKind.ResourcePatch && _store.Selected.Id == id)
                _store.ClearSelection();
            else
                _store.SelectResourcePatch(id);
        }

        private void TryBind()
        {
            if (_bound) return;

            if (_uiSystem == null)
            {
                _uiSystem = FindObjectOfType<UiSystem>();
                if (_uiSystem == null) return;
            }

            if (_placement == null)
                _placement = FindObjectOfType<PlacementInputController>(); // optional

            // UI context must exist (UiBootstrap.Start() will Initialize UiSystem)
            var ctx = _uiSystem.Ctx;
            if (ctx == null)
                return;

            _store = ctx.Store;
            _gate = ctx.InputGate;

            // Resolve services
            object servicesObj = ctx.Services;
            if (servicesObj == null)
                servicesObj = UiServicesProviderUtil.TryGetServicesFrom(_servicesProvider);

            var s = servicesObj as GameServices;
            if (s == null)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[WorldSelectionController] Missing GameServices. Check UiBootstrap._servicesProvider or UiSystem.Ctx.Services.");
                }
                return;
            }

            _gridMap = s.GridMap;
            _world = s.WorldState;
            _resourcePatches = s.ResourcePatchService;

            if (_gridMap == null || _world == null || _store == null)
                return;


            // World mapping fallbacks
            if (_cam == null) _cam = _cameraOverride != null ? _cameraOverride : Camera.main;
            if (_grid == null) _grid = FindObjectOfType<Grid>();

            _bound = true;
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

            if (_grid != null)
            {
                var c = _grid.WorldToCell(world);
                cell = new CellPos(c.x, c.y);
                return true;
            }

            // fallback: floor world
            cell = new CellPos(Mathf.FloorToInt(world.x), _useXZ ? Mathf.FloorToInt(world.z) : Mathf.FloorToInt(world.y));
            return true;
        }

        private bool IsPointerOverBlockingUiNow(Vector2 screenPos)
        {
            var ctx = _uiSystem != null ? _uiSystem.Ctx : null;
            if (ctx == null) return false;

            // Order: overlay -> modals -> panels -> hud (topmost first)
            return IsOverBlocking(ctx.DocOverlay, screenPos)
                   || IsOverBlocking(ctx.DocModals, screenPos)
                   || IsOverBlocking(ctx.DocPanels, screenPos)
                   || IsOverBlocking(ctx.DocHud, screenPos);
        }

        private static bool IsOverBlocking(UIDocument doc, Vector2 screen)
        {
            if (doc == null) return false;

            var root = doc.rootVisualElement;
            if (root == null) return false;

            var panel = root.panel;
            if (panel == null) return false;

            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, screen);
            var picked = panel.Pick(panelPos) as VisualElement;
            if (picked == null) return false;

            return UiElementUtil.HasClassInHierarchy(picked, UiKeys.Class_BlockWorld);
        }
    }
}