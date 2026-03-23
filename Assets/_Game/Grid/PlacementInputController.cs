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

        [Header("Ghost sprite (optional)")]
        [SerializeField] private bool _useGhostSprite = true;
        [SerializeField] private Sprite _ghostSprite;
        [SerializeField, Range(0.1f, 0.8f)] private float _ghostAlpha = 0.30f;
        [SerializeField, Range(0.5f, 1f)] private float _ghostFill = 0.92f; // footprint fill
        [SerializeField] private string _ghostSortingLayer = "Entities";

        [Header("Road paint")]
        [SerializeField] private bool _paintRoadWhileHolding = true;

        private IEventBus _bus;
        private IPlacementService _placement;
        private INotificationService _noti;
        private IGridMap _gridMap;
        private IDataRegistry _data;
        private IRunClock _clock;

        private Camera _cam;
        private bool _bound;

        private string _placeDefId;
        private Dir4 _rot = Dir4.N;
        private UiToolMode _tool = UiToolMode.None;

        // Expose state for UI world selection controller
        public bool IsInPlacementMode => !string.IsNullOrEmpty(_placeDefId);
        public UiToolMode ActiveToolMode => _tool;
        public bool IsWorldActionActive => IsInPlacementMode || _tool != UiToolMode.None;

        private CellPos _lastPaint = new CellPos(int.MinValue, int.MinValue);

        // preview caching
        private readonly List<Vector3Int> _prevCells = new(64);
        private bool _hasPrevDriveway;
        private Vector3Int _prevDriveway;
        private string _prevDef;
        private Dir4 _prevRot;
        private UiToolMode _prevTool;
        private CellPos _prevCell;

        // ghost sr
        private SpriteRenderer _ghostSr;

        private void Awake()
        {
            _cam = _cameraOverride != null ? _cameraOverride : Camera.main;
            EnsureGhost();
        }

        private void OnEnable()
        {
            TryBind();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ClearPreview();
            SetGhostVisible(false);
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
            if (IsPointerOverBlockingUi())
            {
                ClearPreview();
                SetGhostVisible(false);
                return;
            }

            if (!TryGetCellUnderMouse(out var cell))
            {
                ClearPreview();
                SetGhostVisible(false);
                return;
            }

            if (_gridMap == null || !_gridMap.IsInside(cell))
            {
                ClearPreview();
                SetGhostVisible(false);
                return;
            }

            // Build phase gate (optional)
            if (_clock != null && _clock.CurrentPhase != Phase.Build)
            {
                ClearPreview();
                SetGhostVisible(false);
                return;
            }

            // --- PREVIEW ---
            UpdatePreview(cell);

            var mouse = Mouse.current;
            if (mouse == null) return;

            // --- ACTION ---
            if (!string.IsNullOrEmpty(_placeDefId))
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    var vr = _placement.ValidateBuilding(_placeDefId, cell, _rot);
                    if (!vr.Ok)
                    {
                        _noti?.Push(
                            key: "place.fail",
                            title: "Place failed",
                            body: $"{vr.FailReason}",
                            severity: NotificationSeverity.Warning,
                            payload: new NotificationPayload(default, default, _placeDefId),
                            cooldownSeconds: 0.2f,
                            dedupeByKey: false);
                        return;
                    }

                    var bid = _placement.CommitBuilding(_placeDefId, cell, _rot);
                    if (bid.Value == 0)
                    {
                        _noti?.Push("place.commit.fail", "Place failed", "Commit returned default.",
                            NotificationSeverity.Error, new NotificationPayload(default, default, _placeDefId), 0.2f, false);
                        return;
                    }

                    // exit placement after success
                    _noti?.Push("place.ok", "Building placed", $"Id={bid.Value}", NotificationSeverity.Info,
                        new NotificationPayload(default, default, ""), 0.2f, false);

                    _placeDefId = null;
                    _tool = UiToolMode.None;
                    ClearPreview();
                    SetGhostVisible(false);
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
                        TryPlaceRoad(cell);
                    }
                }
                else if (mouse.leftButton.wasPressedThisFrame)
                {
                    _lastPaint = cell;
                    TryPlaceRoad(cell);
                }
                return;
            }

            if (_tool == UiToolMode.RemoveRoad)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (_placement.CanRemoveRoad(cell))
                        _placement.RemoveRoad(cell);
                }
            }
        }

        private void CancelAll()
        {
            _tool = UiToolMode.None;
            _placeDefId = null;
            _rot = Dir4.N;
            ClearPreview();
            SetGhostVisible(false);
        }

        // ---------------- Preview ----------------

        private void UpdatePreview(CellPos cell)
        {
            // Only re-draw if something changed
            if (_prevCell.X == cell.X && _prevCell.Y == cell.Y &&
                _prevTool == _tool &&
                _prevRot == _rot &&
                string.Equals(_prevDef, _placeDefId, StringComparison.Ordinal))
            {
                return;
            }

            _prevCell = cell;
            _prevTool = _tool;
            _prevRot = _rot;
            _prevDef = _placeDefId;

            ClearPreview(); // clear previous tiles

            if (_previewTilemap == null) return;

            // BUILDING preview
            if (!string.IsNullOrEmpty(_placeDefId))
            {
                int w = 1, h = 1;
                BuildingDef def = null;
                if (_data != null)
                    _data.TryGetBuilding(_placeDefId, out def);
                if (def != null)
                {
                    w = Mathf.Max(1, def.SizeX);
                    h = Mathf.Max(1, def.SizeY);
                }

                var vr = _placement.ValidateBuilding(_placeDefId, cell, _rot);
                var tileFoot = vr.Ok ? _tileOk : _tileBad;

                // footprint tiles (anchor is bottom-left in PlacementService)
                for (int dy = 0; dy < h; dy++)
                {
                    for (int dx = 0; dx < w; dx++)
                    {
                        var c = new CellPos(cell.X + dx, cell.Y + dy);
                        if (_gridMap != null && !_gridMap.IsInside(c)) continue;

                        var v = new Vector3Int(c.X, c.Y, 0);
                        if (tileFoot != null)
                        {
                            _previewTilemap.SetTile(v, tileFoot);
                            _prevCells.Add(v);
                        }
                    }
                }

                // driveway / entry (SuggestedRoadCell)
                if (_tileDriveway != null && _gridMap != null && _gridMap.IsInside(vr.SuggestedRoadCell))
                {
                    _prevDriveway = new Vector3Int(vr.SuggestedRoadCell.X, vr.SuggestedRoadCell.Y, 0);
                    _previewTilemap.SetTile(_prevDriveway, _tileDriveway);
                    _hasPrevDriveway = true;
                }

                // ghost sprite (optional)
                UpdateGhost(cell, w, h, vr.Ok);
                return;
            }

            // ROAD preview
            if (_tool == UiToolMode.Road)
            {
                bool ok = _placement.CanPlaceRoad(cell);
                var t = ok ? _tileOk : _tileBad;
                if (t != null)
                {
                    var v = new Vector3Int(cell.X, cell.Y, 0);
                    _previewTilemap.SetTile(v, t);
                    _prevCells.Add(v);
                }
                SetGhostVisible(false);
                return;
            }

            // REMOVE ROAD preview
            if (_tool == UiToolMode.RemoveRoad)
            {
                bool ok = _placement.CanRemoveRoad(cell);
                var t = ok ? _tileOk : _tileBad;
                if (t != null)
                {
                    var v = new Vector3Int(cell.X, cell.Y, 0);
                    _previewTilemap.SetTile(v, t);
                    _prevCells.Add(v);
                }
                SetGhostVisible(false);
                return;
            }

            // No tool
            SetGhostVisible(false);
        }

        private void ClearPreview()
        {
            if (_previewTilemap == null) return;

            for (int i = 0; i < _prevCells.Count; i++)
                _previewTilemap.SetTile(_prevCells[i], null);
            _prevCells.Clear();

            if (_hasPrevDriveway)
            {
                _previewTilemap.SetTile(_prevDriveway, null);
                _hasPrevDriveway = false;
            }
        }

        private void EnsureGhost()
        {
            if (!_useGhostSprite) return;

            var go = new GameObject("GhostBuilding");
            go.transform.SetParent(transform, false);

            _ghostSr = go.AddComponent<SpriteRenderer>();
            _ghostSr.sortingLayerName = _ghostSortingLayer;
            _ghostSr.sortingOrder = 9999; // will be overridden by y-sort
            _ghostSr.sprite = _ghostSprite;
            _ghostSr.enabled = false;
        }

        private void UpdateGhost(CellPos anchor, int sizeX, int sizeY, bool ok)
        {
            if (!_useGhostSprite || _ghostSr == null) return;

            // choose sprite
            if (_ghostSr.sprite == null) _ghostSr.sprite = _ghostSprite;
            if (_ghostSr.sprite == null)
            {
                // if no sprite, just hide
                _ghostSr.enabled = false;
                return;
            }

            // position at center of footprint
            Vector3 pos = FootprintCenterWorld(anchor, sizeX, sizeY);
            _ghostSr.transform.position = pos;

            // y-sort
            _ghostSr.sortingOrder = -Mathf.RoundToInt(pos.y * 100f);

            // color
            var c = ok ? new Color(0.2f, 1f, 0.2f, _ghostAlpha) : new Color(1f, 0.2f, 0.2f, _ghostAlpha);
            _ghostSr.color = c;

            // scale to footprint
            ApplyScaleToFootprint(_ghostSr, sizeX, sizeY, _ghostFill);

            _ghostSr.enabled = true;
        }

        private void SetGhostVisible(bool visible)
        {
            if (_ghostSr == null) return;
            _ghostSr.enabled = visible;
        }

        private Vector3 FootprintCenterWorld(CellPos anchor, int sizeX, int sizeY)
        {
            Vector3 cellSize = _grid != null ? _grid.cellSize : Vector3.one;

            // world position of anchor cell center
            Vector3 anchorCenter = CellToWorldCenter(anchor);

            float ox = (sizeX * 0.5f - 0.5f) * cellSize.x;
            float oy = (sizeY * 0.5f - 0.5f) * cellSize.y;

            return anchorCenter + new Vector3(ox, oy, 0f);
        }

        private Vector3 CellToWorldCenter(CellPos c)
        {
            if (_grid != null)
            {
                var v = new Vector3Int(c.X, c.Y, 0);
                return _grid.GetCellCenterWorld(v);
            }
            return new Vector3(c.X + 0.5f, c.Y + 0.5f, 0f);
        }

        private void ApplyScaleToFootprint(SpriteRenderer sr, int sizeX, int sizeY, float fill)
        {
            if (sr == null || sr.sprite == null) return;

            Vector3 cellSize = _grid != null ? _grid.cellSize : Vector3.one;

            float targetW = Mathf.Max(0.01f, sizeX * cellSize.x * fill);
            float targetH = Mathf.Max(0.01f, sizeY * cellSize.y * fill);

            Vector3 native = sr.sprite.bounds.size;
            float nativeW = Mathf.Max(0.0001f, native.x);
            float nativeH = Mathf.Max(0.0001f, native.y);

            sr.transform.localScale = new Vector3(targetW / nativeW, targetH / nativeH, 1f);
        }

        // ---------------- Placement actions ----------------

        private void TryPlaceRoad(CellPos cell)
        {
            if (_placement.CanPlaceRoad(cell))
                _placement.PlaceRoad(cell);
            else
                _noti?.Push("road.fail", "Road", "Cannot place road here (must connect to existing road).",
                    NotificationSeverity.Warning, new NotificationPayload(default, default, ""), 0.15f, true);
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
            _tool = UiToolMode.None;
            _rot = Dir4.N;

            ClearPreview();
            SetGhostVisible(false);
        }

        private void OnToolModeRequested(UiToolModeRequestedEvent ev)
        {
            // toggle
            _tool = (_tool == ev.Mode) ? UiToolMode.None : ev.Mode;
            _placeDefId = null;
            _rot = Dir4.N;

            ClearPreview();
            SetGhostVisible(false);
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

        private bool IsPointerOverBlockingUi()
        {
            if (_hudDoc == null && _panelsDoc == null && _modalsDoc == null) return false;

            var mouse = Mouse.current;
            if (mouse == null) return false;

            Vector2 screen = mouse.position.ReadValue();

            return IsOverBlocking(_modalsDoc, screen) || IsOverBlocking(_panelsDoc, screen) || IsOverBlocking(_hudDoc, screen);
        }

        private bool IsOverBlocking(UIDocument doc, Vector2 screen)
        {
            if (doc == null) return false;
            var root = doc.rootVisualElement;
            if (root == null) return false;

            var panel = root.panel;
            if (panel == null) return false;

            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, screen);
            var picked = panel.Pick(panelPos) as VisualElement;
            if (picked == null) return false;

            var cur = picked;
            while (cur != null)
            {
                if (cur.ClassListContains(_blockClass)) return true;
                cur = cur.parent;
            }

            return false;
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
                try { var v = prop.GetValue(mb); if (v != null) return v; } catch { }
            }

            var m = t.GetMethod("GetServices", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m != null && m.GetParameters().Length == 0)
            {
                try { var v = m.Invoke(mb, null); if (v != null) return v; } catch { }
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
                try { var v = p.GetValue(obj) as T; if (v != null) return v; } catch { }
            }

            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null)
            {
                try { var v = f.GetValue(obj) as T; if (v != null) return v; } catch { }
            }

            return null;
        }
    }
}