using System;
using System.Collections.Generic;
using System.Reflection;
using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    /// <summary>
    /// Runtime placement controller + preview ghost:
    /// - Building placement preview: footprint tiles (OK/BAD) + driveway cell tile + ghost sprite (green/red)
    /// - Road placement preview: OK/BAD tile on hovered cell
    /// - Remove road preview: OK/BAD tile on hovered cell
    /// No hard reference to GameBootstrap/GameServices types (avoid asmdef cycles).
    /// </summary>
    public sealed class PlacementInputController : MonoBehaviour
    {
        [Header("Services Source (drag GameBootstrap or any component exposing Services/GetServices)")]
        [SerializeField] private MonoBehaviour _servicesSource;

        [Header("World mapping")]
        [SerializeField] private Camera _cameraOverride;
        [SerializeField] private Grid _grid;                 // must match your Grid/Tilemaps
        [SerializeField] private bool _useXZ = false;        // 2D topdown XY => false
        [SerializeField] private float _planeZ = 0f;         // XY plane z
        [SerializeField] private float _planeY = 0f;         // XZ plane y

        [Header("UI gating (optional)")]
        [SerializeField] private UIDocument _hudDoc;
        [SerializeField] private UIDocument _panelsDoc;
        [SerializeField] private UIDocument _modalsDoc;
        [SerializeField] private string _blockClass = "ui-block-world";

        [Header("Preview Tilemap (required for footprint/driveway preview)")]
        [SerializeField] private Tilemap _previewTilemap;
        [SerializeField] private TileBase _tileOk;
        [SerializeField] private TileBase _tileBad;
        [SerializeField] private TileBase _tileDriveway;
        [SerializeField] private TileBase _tileEntryValid;
        [SerializeField] private TileBase _tileEntryInvalid;
        [SerializeField] private TileBase _tileFrontNorth;
        [SerializeField] private TileBase _tileFrontEast;
        [SerializeField] private TileBase _tileFrontSouth;
        [SerializeField] private TileBase _tileFrontWest;

        [Header("Ghost sprite (optional)")]
        [SerializeField] private bool _useGhostSprite = true;
        [SerializeField] private Sprite _ghostSprite;
        [SerializeField, Range(0.1f, 0.8f)] private float _ghostAlpha = 0.30f;
        [SerializeField, Range(0.5f, 1f)] private float _ghostFill = 0.92f; // footprint fill
        [SerializeField] private string _ghostSortingLayer = "Entities";

        [Header("Front marker sprite (optional)")]
        [SerializeField] private bool _useFrontArrowSprite = true;
        [SerializeField] private Sprite _frontArrowSprite;
        [SerializeField, Range(0.2f, 1.2f)] private float _frontArrowWidthCells = 0.35f;
        [SerializeField, Range(0.2f, 1.2f)] private float _frontArrowLengthCells = 0.85f;
        [SerializeField] private string _frontArrowSortingLayer = "Entities";

        [Header("Road paint")]
        [SerializeField] private bool _paintRoadWhileHolding = true;

        private IEventBus _bus;
        private IPlacementService _placement;
        private INotificationService _noti;
        private IGridMap _gridMap;
        private IDataRegistry _data;
        private IRunClock _clock;
        private PlacementUiGate _uiGate;
        private PlacementPreviewRenderer _previewRenderer;
        private PlacementActionController _actionController;

        private Camera _cam;
        private bool _bound;

        private string _placeDefId;
        private Dir4 _rot = Dir4.N;
        private UiToolMode _tool = UiToolMode.Select;

        // Expose state for UI world selection controller
        public bool IsInPlacementMode => !string.IsNullOrEmpty(_placeDefId);
        public UiToolMode ActiveToolMode => _tool;
        public bool IsWorldActionActive => IsInPlacementMode || _tool != UiToolMode.Select;

        private CellPos _lastPaint = new CellPos(int.MinValue, int.MinValue);
        private PlacementFailReason _lastPlacementFailReason = PlacementFailReason.None;
        private CellPos _lastPlacementFailCell = new CellPos(int.MinValue, int.MinValue);


        private void Awake()
        {
            _cam = _cameraOverride != null ? _cameraOverride : Camera.main;
            _uiGate = new PlacementUiGate(_hudDoc, _panelsDoc, _modalsDoc, _blockClass);
        }

        private void OnEnable()
        {
            TryBind();
        }

        private void OnDisable()
        {
            Unsubscribe();
            _previewRenderer?.HideAll();
        }

        private void Update()
        {
            if (!_bound)
            {
                TryBind();
                return;
            }

            // Escape cancels any tool/build placement
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                CancelAll();
                return;
            }

            // rotate in building mode
            if (!string.IsNullOrEmpty(_placeDefId) && kb != null)
            {
                if (kb.qKey.wasPressedThisFrame) _rot = TurnLeft(_rot);
                if (kb.eKey.wasPressedThisFrame) _rot = TurnRight(_rot);
            }

            // If pointer over UI -> clear preview to avoid confusing state
            if (_uiGate.IsPointerOverBlockingUi())
            {
                HidePlacementPreview();
                return;
            }

            if (!TryGetCellUnderMouse(out var cell))
            {
                HidePlacementPreview();
                return;
            }

            if (_gridMap == null || !_gridMap.IsInside(cell))
            {
                HidePlacementPreview();
                return;
            }

            // Build phase gate (optional)
            if (_clock != null && _clock.CurrentPhase != Phase.Build)
            {
                HidePlacementPreview();
                return;
            }

            // --- PREVIEW ---
            var previewResult = _previewRenderer?.UpdatePreview(cell, _placeDefId, _rot, _tool);
            if (previewResult.HasValue)
                MaybePushPlacementPreviewHint(previewResult.Value, cell);

            var mouse = Mouse.current;
            if (mouse == null) return;

            // --- ACTION ---
            if (!string.IsNullOrEmpty(_placeDefId))
            {
                if (mouse.leftButton.wasPressedThisFrame && _actionController != null && _actionController.TryCommitBuilding(_placeDefId, cell, _rot))
                {
                    _placeDefId = null;
                    _tool = UiToolMode.Select;
                    _previewRenderer?.HideAll();
                }
                return;
            }

            if (_tool == UiToolMode.Road)
            {
                if (_paintRoadWhileHolding && mouse.leftButton.isPressed)
                {
                    if (_lastPaint.X != cell.X || _lastPaint.Y != cell.Y)
                    {
                        _lastPaint = cell;
                        _actionController?.TryPlaceRoad(cell);
                    }
                }
                else if (mouse.leftButton.wasPressedThisFrame)
                {
                    _lastPaint = cell;
                    _actionController?.TryPlaceRoad(cell);
                }
                return;
            }

            if (_tool == UiToolMode.Remove)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                    _actionController?.TryRemoveRoad(cell);
            }
        }

        private void CancelAll()
        {
            string cancelledDefId = _placeDefId;
            bool wasPlacement = !string.IsNullOrEmpty(_placeDefId);
            ResetPlacementState();
            if (wasPlacement)
                _bus?.Publish(new UiPlacementFinishedEvent(cancelledDefId, false));
            HidePlacementPreview();
        }

        private void ResetPlacementState()
        {
            _tool = UiToolMode.Select;
            _placeDefId = null;
            _rot = Dir4.N;
            _lastPlacementFailReason = PlacementFailReason.None;
            _lastPlacementFailCell = new CellPos(int.MinValue, int.MinValue);
        }

        private void HidePlacementPreview()
        {
            _previewRenderer?.HideAll();
        }

        // ---------------- Placement actions ----------------

        private void MaybePushPlacementPreviewHint(PlacementResult vr, CellPos cell)
        {
            if (_noti == null) return;

            if (vr.Ok)
            {
                _lastPlacementFailReason = PlacementFailReason.None;
                _lastPlacementFailCell = cell;
                return;
            }

            if (vr.FailReason == _lastPlacementFailReason && cell.X == _lastPlacementFailCell.X && cell.Y == _lastPlacementFailCell.Y)
                return;

            _lastPlacementFailReason = vr.FailReason;
            _lastPlacementFailCell = cell;

            _noti.Push(
                key: $"place.preview.{vr.FailReason}",
                title: "Placement",
                body: GetPlacementPreviewMessage(vr.FailReason),
                severity: NotificationSeverity.Warning,
                payload: new NotificationPayload(default, default, _placeDefId ?? ""),
                cooldownSeconds: 0.2f,
                dedupeByKey: true);
        }

        private static string GetPlacementPreviewMessage(PlacementFailReason reason)
        {
            return reason switch
            {
                PlacementFailReason.NoRoadConnection => "Invalid placement: missing road/entry connection.",
                PlacementFailReason.Overlap => "Invalid placement: overlaps road or building.",
                PlacementFailReason.BlockedBySite => "Invalid placement: blocked by construction site.",
                PlacementFailReason.OutOfBounds => "Invalid placement: out of bounds.",
                PlacementFailReason.InvalidRotation => "Invalid placement: rotation not allowed here.",
                _ => "Invalid placement."
            };
        }

        // ---------------- Bind & Events ----------------

        private void TryBind()
        {
            if (_bound) return;

            object services = ResolveServicesObject();
            if (services == null) return;

            _bus = ReadMember<IEventBus>(services, "EventBus");
            _placement = ReadMember<IPlacementService>(services, "PlacementService");
            _noti = ReadMember<INotificationService>(services, "NotificationService");
            _gridMap = ReadMember<IGridMap>(services, "GridMap");
            _data = ReadMember<IDataRegistry>(services, "DataRegistry");
            _clock = ReadMember<IRunClock>(services, "RunClock");

            if (_bus == null || _placement == null || _gridMap == null)
                return;

            _previewRenderer = new PlacementPreviewRenderer(
                _grid,
                _gridMap,
                _data,
                _placement,
                _previewTilemap,
                _tileOk,
                _tileBad,
                _tileDriveway,
                _tileEntryValid,
                _tileEntryInvalid,
                _tileFrontNorth,
                _tileFrontEast,
                _tileFrontSouth,
                _tileFrontWest,
                _useGhostSprite,
                _ghostSprite,
                _ghostAlpha,
                _ghostFill,
                _ghostSortingLayer,
                _useFrontArrowSprite,
                _frontArrowSprite,
                _frontArrowWidthCells,
                _frontArrowLengthCells,
                _frontArrowSortingLayer,
                transform);
            _previewRenderer.EnsureObjects();
            _actionController = new PlacementActionController(_placement, _noti, _bus);

            Subscribe();
            _bound = true;
        }

        private void Subscribe()
        {
            _bus.Subscribe<UiBeginPlaceBuildingEvent>(OnBeginPlaceBuilding);
            _bus.Subscribe<UiToolModeRequestedEvent>(OnToolModeRequested);
        }

        private void Unsubscribe()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<UiBeginPlaceBuildingEvent>(OnBeginPlaceBuilding);
            _bus.Unsubscribe<UiToolModeRequestedEvent>(OnToolModeRequested);
        }

        private void OnBeginPlaceBuilding(UiBeginPlaceBuildingEvent ev)
        {
            _placeDefId = ev.DefId;
            _tool = UiToolMode.BuildPlacement;
            _rot = Dir4.N;
            _lastPlacementFailReason = PlacementFailReason.None;
            _lastPlacementFailCell = new CellPos(int.MinValue, int.MinValue);

            HidePlacementPreview();
            _bus?.Publish(new UiPlacementStartedEvent(ev.DefId));
        }

        private void OnToolModeRequested(UiToolModeRequestedEvent ev)
        {
            bool wasPlacement = !string.IsNullOrEmpty(_placeDefId);
            string cancelledDefId = _placeDefId;

            _tool = ev.Mode;
            if (ev.Mode != UiToolMode.BuildPlacement)
                _placeDefId = null;
            _rot = Dir4.N;
            _lastPlacementFailReason = PlacementFailReason.None;
            _lastPlacementFailCell = new CellPos(int.MinValue, int.MinValue);

            if (wasPlacement && ev.Mode != UiToolMode.BuildPlacement)
                _bus?.Publish(new UiPlacementFinishedEvent(cancelledDefId, false));

            HidePlacementPreview();
        }

        // ---------------- Input helpers ----------------

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

            cell = new CellPos(Mathf.FloorToInt(world.x), _useXZ ? Mathf.FloorToInt(world.z) : Mathf.FloorToInt(world.y));
            return true;
        }

        private static Dir4 TurnLeft(Dir4 d) => d switch { Dir4.N => Dir4.W, Dir4.W => Dir4.S, Dir4.S => Dir4.E, _ => Dir4.N };
        private static Dir4 TurnRight(Dir4 d) => d switch { Dir4.N => Dir4.E, Dir4.E => Dir4.S, Dir4.S => Dir4.W, _ => Dir4.N };

        // ---------------- reflection helpers ----------------

        private object ResolveServicesObject()
        {
            if (_servicesSource != null)
            {
                var s = TryExtractServicesFromMono(_servicesSource);
                if (s != null) return s;
            }

            var all = FindObjectsOfType<MonoBehaviour>();
            for (int i = 0; i < all.Length; i++)
            {
                var mb = all[i];
                if (mb == null) continue;

                var s = TryExtractServicesFromMono(mb);
                if (s != null)
                {
                    _servicesSource = mb;
                    return s;
                }
            }

            return null;
        }

        private static object TryExtractServicesFromMono(MonoBehaviour mb)
        {
            var t = mb.GetType();

            var prop = t.GetProperty("Services", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null)
            {
                try { var v = prop.GetValue(mb); if (v != null) return v; } catch (Exception ex) { UnityEngine.Debug.LogWarning($"[PlacementInputController] Failed to read Services property from {t.Name}: {ex}"); }
            }

            var m = t.GetMethod("GetServices", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m != null && m.GetParameters().Length == 0)
            {
                try { var v = m.Invoke(mb, null); if (v != null) return v; } catch (Exception ex) { UnityEngine.Debug.LogWarning($"[PlacementInputController] Failed to invoke GetServices on {t.Name}: {ex}"); }
            }

            return null;
        }

        private static T ReadMember<T>(object obj, string name) where T : class
        {
            if (obj == null) return null;

            var t = obj.GetType();

            var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null)
            {
                try { var v = p.GetValue(obj) as T; if (v != null) return v; } catch (Exception ex) { UnityEngine.Debug.LogWarning($"[PlacementInputController] Failed to read member '{name}' from {t.Name}: {ex}"); }
            }

            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null)
            {
                try { var v = f.GetValue(obj) as T; if (v != null) return v; } catch (Exception ex) { UnityEngine.Debug.LogWarning($"[PlacementInputController] Failed to read field '{name}' from {t.Name}: {ex}"); }
            }

            return null;
        }
    }
}