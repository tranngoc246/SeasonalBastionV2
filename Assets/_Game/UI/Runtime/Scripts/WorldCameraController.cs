using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    /// <summary>
    /// WorldCameraController (New Input System)
    /// - Pan: WASD / Arrow keys
    /// - Drag pan: Middle Mouse Button (MMB)
    /// - Zoom: Mouse wheel (orthographic size), unscaled
    /// - Focus: smooth move to building center (unscaled)
    /// - Blocks input when pointer over UI Toolkit docs (HUD/Panels/Modals)
    /// </summary>
    public sealed class WorldCameraController : MonoBehaviour
    {
        [Header("Plane mapping (match WorldSelectionController)")]
        [SerializeField] private bool _useXZ = false; // project dang XY => false
        [SerializeField] private Vector3 _origin = Vector3.zero;
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private float _planeZ = 0f; // XY plane
        [SerializeField] private float _planeY = 0f; // XZ plane

        [Header("Camera")]
        [SerializeField] private Camera _camera;

        [Header("Pan/Zoom")]
        [SerializeField] private float _panSpeed = 12f;          // units per second (scaled by zoom)
        [SerializeField] private float _dragPanMultiplier = 1f;  // drag world delta multiplier
        [SerializeField] private float _zoomSpeed = 14f;         // size delta per second (after scrollScale)
        [SerializeField] private float _scrollScale = 0.01f;     // InputSystem scroll is often ±120 per notch
        [SerializeField] private float _minOrthoSize = 3f;
        [SerializeField] private float _maxOrthoSize = 35f;

        [Header("Focus")]
        [SerializeField] private float _focusSmoothTime = 0.18f; // seconds (unscaled)

        [Header("UI Toolkit Documents (optional)")]
        [SerializeField] private UIDocument _hudDocument;
        [SerializeField] private UIDocument _panelsDocument;
        [SerializeField] private UIDocument _modalsDocument;

        private GameServices _s;
        private WorldSelectionController _mapper;

        private bool _uiDocsResolved;

        // focus state
        private bool _focusing;
        private Vector3 _focusTarget;
        private Vector3 _focusVel;

        // drag pan state (screen-delta based)
        private bool _dragging;
        private Vector2 _dragStartScreen;
        private Vector3 _camStartPos;

        public void Bind(
            GameServices s,
            WorldSelectionController mapper,
            UIDocument hudDoc,
            UIDocument panelsDoc,
            UIDocument modalsDoc)
        {
            _s = s;
            _mapper = mapper;

            _hudDocument = hudDoc;
            _panelsDocument = panelsDoc;
            _modalsDocument = modalsDoc;

            if (_camera == null) _camera = Camera.main;

            // best-effort: align mapping from mapper
            if (_mapper != null)
            {
                // mapper của bạn thường có CellSize (nếu không có, bạn có thể xóa dòng này)
                _cellSize = _mapper.CellSize;
            }

            ResolveUiDocumentsIfNeeded();
        }

        public void FocusWorld(Vector3 world, bool instant = false)
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            var pos = _camera.transform.position;

            if (_useXZ)
                _focusTarget = new Vector3(world.x, pos.y, world.z);
            else
                _focusTarget = new Vector3(world.x, world.y, pos.z);

            if (instant)
            {
                _camera.transform.position = _focusTarget;
                _focusing = false;
                _focusVel = Vector3.zero;
                return;
            }

            _focusing = true;
        }

        public bool TryFocusBuilding(BuildingId id, bool instant = false)
        {
            if (_s == null || _s.WorldState == null || _s.DataRegistry == null) return false;
            if (id.Value == 0) return false;

            var store = _s.WorldState.Buildings;
            if (store == null) return false;
            if (!store.Exists(id)) return false;

            var bs = store.Get(id);
            if (string.IsNullOrEmpty(bs.DefId)) return false;

            BuildingDef def;
            try { def = _s.DataRegistry.GetBuilding(bs.DefId); }
            catch { return false; }

            int w = Mathf.Max(1, def.SizeX);
            int h = Mathf.Max(1, def.SizeY);

            // center in cells (support even sizes)
            float cx = bs.Anchor.X + (w * 0.5f);
            float cy = bs.Anchor.Y + (h * 0.5f);

            Vector3 world = CellToWorld(new Vector2(cx, cy));
            FocusWorld(world, instant);
            return true;
        }

        private void Update()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            if (Mouse.current == null) return;

            ResolveUiDocumentsIfNeeded();

            // block camera input when pointer over UI
            Vector2 mousePos = Mouse.current.position.ReadValue();
            if (UiBlocker.IsPointerOverBlockingUi(mousePos, _hudDocument, _panelsDocument, _modalsDocument))
            {
                // allow focus smoothing to continue, but block user input
                TickFocus(Time.unscaledDeltaTime);
                return;
            }

            float dt = Time.unscaledDeltaTime;

            // ---- ZOOM (mouse wheel) ----
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                CancelFocusOnManualInput();

                // magnitude-based zoom (InputSystem scroll often ±120)
                float zoomDelta = scroll * _scrollScale; // e.g. 120 -> 1.2

                if (_camera.orthographic)
                {
                    float size = _camera.orthographicSize - zoomDelta * (_zoomSpeed * dt);
                    _camera.orthographicSize = Mathf.Clamp(size, _minOrthoSize, _maxOrthoSize);
                }
                else
                {
                    // perspective fallback: dolly along forward
                    _camera.transform.position += _camera.transform.forward * (-zoomDelta * (_zoomSpeed * dt));
                }
            }

            // ---- PAN (WASD/Arrows) ----
            Vector2 move = Vector2.zero;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) move.y += 1;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) move.y -= 1;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) move.x -= 1;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) move.x += 1;
            }

            if (move.sqrMagnitude > 0.001f)
            {
                CancelFocusOnManualInput();

                float zoomScale = _camera.orthographic ? Mathf.Max(1f, _camera.orthographicSize / 10f) : 1f;
                float speed = _panSpeed * zoomScale;

                Vector3 delta;
                if (_useXZ)
                    delta = new Vector3(move.x, 0f, move.y) * (speed * dt);
                else
                    delta = new Vector3(move.x, move.y, 0f) * (speed * dt);

                _camera.transform.position += delta;
            }

            // ---- DRAG PAN (MMB) - screen delta based (stable, no jitter) ----
            if (Mouse.current.middleButton.wasPressedThisFrame)
            {
                CancelFocusOnManualInput();

                _dragging = true;
                _dragStartScreen = mousePos;
                _camStartPos = _camera.transform.position;
            }
            else if (Mouse.current.middleButton.isPressed && _dragging)
            {
                CancelFocusOnManualInput();

                Vector2 deltaPx = mousePos - _dragStartScreen;

                // convert screen pixels -> world units
                // For orthographic camera:
                // worldHeight = 2 * orthoSize
                // unitsPerPixel = worldHeight / Screen.height
                float unitsPerPixel = 1f;

                if (_camera.orthographic)
                {
                    float worldHeight = 2f * _camera.orthographicSize;
                    unitsPerPixel = worldHeight / Mathf.Max(1f, Screen.height);
                }
                else
                {
                    // Perspective fallback: approximate by scaling with distance to plane.
                    // Keep stable: use a constant factor relative to camera distance.
                    float dist = Mathf.Max(1f, Mathf.Abs(_camera.transform.position.z));
                    unitsPerPixel = (dist * 0.0025f);
                }

                Vector2 deltaWorld2 = deltaPx * (unitsPerPixel * _dragPanMultiplier);

                Vector3 offset;
                if (_useXZ)
                    offset = new Vector3(deltaWorld2.x, 0f, deltaWorld2.y);
                else
                    offset = new Vector3(deltaWorld2.x, deltaWorld2.y, 0f);

                // drag to move view => camera moves opposite direction
                _camera.transform.position = _camStartPos - offset;
            }
            else if (Mouse.current.middleButton.wasReleasedThisFrame)
            {
                _dragging = false;
            }

            TickFocus(dt);
        }

        private void TickFocus(float dt)
        {
            if (!_focusing) return;

            var pos = _camera.transform.position;
            pos = Vector3.SmoothDamp(pos, _focusTarget, ref _focusVel, _focusSmoothTime, Mathf.Infinity, dt);
            _camera.transform.position = pos;

            if ((pos - _focusTarget).sqrMagnitude < 0.0004f)
            {
                _camera.transform.position = _focusTarget;
                _focusing = false;
                _focusVel = Vector3.zero;
            }
        }

        private void CancelFocusOnManualInput()
        {
            _focusing = false;
            _focusVel = Vector3.zero;
        }

        private bool TryGetWorldOnPlane(Vector2 screen, out Vector3 world)
        {
            world = default;

            Ray ray = _camera.ScreenPointToRay(screen);

            Plane plane = _useXZ
                ? new Plane(Vector3.up, new Vector3(0f, _planeY, 0f))
                : new Plane(Vector3.forward, new Vector3(0f, 0f, _planeZ));

            if (!plane.Raycast(ray, out float enter)) return false;
            world = ray.GetPoint(enter);
            return true;
        }

        private Vector3 CellToWorld(Vector2 cell)
        {
            float half = Mathf.Max(0.0001f, _cellSize) * 0.5f;

            if (_useXZ)
                return _origin + new Vector3(cell.x * _cellSize + half, _planeY, cell.y * _cellSize + half);

            return _origin + new Vector3(cell.x * _cellSize + half, cell.y * _cellSize + half, _planeZ);
        }

        private void ResolveUiDocumentsIfNeeded()
        {
            if (_uiDocsResolved) return;

            if (_hudDocument != null && _panelsDocument != null && _modalsDocument != null)
            {
                _uiDocsResolved = true;
                return;
            }

#if UNITY_2023_1_OR_NEWER
            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var docs = Object.FindObjectsOfType<UIDocument>(true);
#endif
            if (docs == null || docs.Length == 0)
            {
                _uiDocsResolved = true;
                return;
            }

            foreach (var d in docs)
            {
                if (d == null) continue;
                var n = d.gameObject.name;

                if (_hudDocument == null && ContainsAny(n, "HUD", "Hud"))
                    _hudDocument = d;

                if (_panelsDocument == null && ContainsAny(n, "Panels", "Panel"))
                    _panelsDocument = d;

                if (_modalsDocument == null && ContainsAny(n, "Modals", "Modal"))
                    _modalsDocument = d;
            }

            _uiDocsResolved = true;
        }

        private static bool ContainsAny(string s, params string[] keys)
        {
            if (string.IsNullOrEmpty(s) || keys == null) return false;
            for (int i = 0; i < keys.Length; i++)
            {
                var k = keys[i];
                if (string.IsNullOrEmpty(k)) continue;
                if (s.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}
