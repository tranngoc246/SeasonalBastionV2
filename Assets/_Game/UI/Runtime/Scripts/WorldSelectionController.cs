using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    /// <summary>
    /// Day4: Click on world -> resolve cell -> read GridMap occupancy -> SelectedBuilding.
    /// No prefab/collider dependency. Deterministic & grid-authority.
    ///
    /// IMPORTANT (UI Toolkit):
    /// - Do NOT rely on EventSystem.IsPointerOverGameObject() (uGUI-centric).
    /// - Use UiBlocker against UIDocuments (HUD/Panels/Modals) to block world clicks.
    /// </summary>
    public sealed class WorldSelectionController : MonoBehaviour
    {
        [Header("Grid plane mapping")]
        [SerializeField] private bool _useXZ = false; // project dang XY => false
        [SerializeField] private Vector3 _origin = Vector3.zero;
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private float _planeZ = 0f; // XY plane
        [SerializeField] private float _planeY = 0f; // XZ plane

        [Header("Camera")]
        [SerializeField] private Camera _camera;

        [Header("UI Toolkit Documents (optional but recommended)")]
        [SerializeField] private UIDocument _hudDocument;
        [SerializeField] private UIDocument _panelsDocument;
        [SerializeField] private UIDocument _modalsDocument;

        private GameServices _s;

        /// <summary>
        /// When true, this controller will ignore world clicks.
        /// Used by UI tools (build/road/remove) to avoid double-handling LMB.
        /// </summary>
        public bool BlockSelection { get; set; }

        public BuildingId SelectedBuilding { get; private set; }
        public bool HasSelection { get; private set; }

        private bool _uiDocsResolved;

        public void Bind(GameServices s)
        {
            _s = s;
            if (_camera == null) _camera = Camera.main;
            ResolveUiDocumentsIfNeeded();
        }

        public void SetUiDocuments(UIDocument hud, UIDocument panels, UIDocument modals)
        {
            _hudDocument = hud;
            _panelsDocument = panels;
            _modalsDocument = modals;
            _uiDocsResolved = true;
        }

        public void ClearSelection()
        {
            HasSelection = false;
            SelectedBuilding = default;
        }

        /// <summary>
        /// Shared screen->cell mapping for tools.
        /// </summary>
        public bool TryScreenToCell(Vector2 screen, out CellPos cell)
        {
            cell = default;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return false;
            return TryScreenToCell(_camera, screen, out cell);
        }

        /// <summary>
        /// World center position for a cell (used by runtime renderers/previews).
        /// </summary>
        public Vector3 CellToWorldCenter(CellPos cell)
        {
            float half = Mathf.Max(0.0001f, _cellSize) * 0.5f;
            if (_useXZ)
                return _origin + new Vector3(cell.X * _cellSize + half, _planeY, cell.Y * _cellSize + half);
            return _origin + new Vector3(cell.X * _cellSize + half, cell.Y * _cellSize + half, _planeZ);
        }

        public float CellSize => _cellSize;

        private void Update()
        {
            if (_s == null || _s.GridMap == null) return;
            if (Mouse.current == null) return;
            if (BlockSelection) return;

            if (!Mouse.current.leftButton.wasPressedThisFrame) return;

            ResolveUiDocumentsIfNeeded();

            // Ignore click on UI Toolkit (HUD/Panels/Modals)
            var mousePos = Mouse.current.position.ReadValue();
            if (UiBlocker.IsPointerOverBlockingUi(mousePos, _hudDocument, _panelsDocument, _modalsDocument))
                return;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            if (!TryScreenToCell(_camera, mousePos, out var cell))
                return;

            if (!_s.GridMap.IsInside(cell))
                return;

            var occ = _s.GridMap.Get(cell);
            if (occ.Kind == CellOccupancyKind.Building && occ.Building.Value != 0)
            {
                SelectedBuilding = occ.Building;
                HasSelection = true;
            }
            else
            {
                ClearSelection();
            }
        }

        private void ResolveUiDocumentsIfNeeded()
        {
            if (_uiDocsResolved) return;

            // If already assigned in inspector, keep them.
            if (_hudDocument != null && _panelsDocument != null && _modalsDocument != null)
            {
                _uiDocsResolved = true;
                return;
            }

            // Best-effort scene scan (no prefab required).
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

            // If still missing, fallback: pick remaining by sort order (lowest=HUD-ish, highest=Modals-ish)
            if (_hudDocument == null || _panelsDocument == null || _modalsDocument == null)
            {
                UIDocument a = null, b = null, c = null;
                for (int i = 0; i < docs.Length; i++)
                {
                    var d = docs[i];
                    if (d == null) continue;

                    if (a == null || d.sortingOrder < a.sortingOrder) a = d;
                    if (c == null || d.sortingOrder > c.sortingOrder) c = d;
                }

                // choose middle as panels if possible
                if (docs.Length >= 3)
                {
                    b = docs[0];
                    for (int i = 0; i < docs.Length; i++)
                    {
                        var d = docs[i];
                        if (d == null) continue;
                        if (d != a && d != c)
                        {
                            b = d;
                            break;
                        }
                    }
                }

                if (_hudDocument == null) _hudDocument = a;
                if (_modalsDocument == null) _modalsDocument = c;
                if (_panelsDocument == null) _panelsDocument = b;
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

        private bool TryScreenToCell(Camera cam, Vector2 screen, out CellPos cell)
        {
            cell = default;

            Ray ray = cam.ScreenPointToRay(screen);

            Plane plane = _useXZ
                ? new Plane(Vector3.up, new Vector3(0f, _planeY, 0f))
                : new Plane(Vector3.forward, new Vector3(0f, 0f, _planeZ)); // XY @ z=_planeZ

            if (!plane.Raycast(ray, out float enter)) return false;

            Vector3 hit = ray.GetPoint(enter);
            Vector3 local = hit - _origin;

            int x = Mathf.FloorToInt(local.x / Mathf.Max(0.0001f, _cellSize));
            int y = _useXZ
                ? Mathf.FloorToInt(local.z / Mathf.Max(0.0001f, _cellSize))
                : Mathf.FloorToInt(local.y / Mathf.Max(0.0001f, _cellSize));

            cell = new CellPos(x, y);
            return true;
        }

        public void SelectBuilding(BuildingId id)
        {
            if (id.Value == 0) return;
            if (_s == null || _s.WorldState == null) return;

            var bsStore = _s.WorldState.Buildings;
            if (bsStore == null) return;
            if (!bsStore.Exists(id)) return;

            // Minimal: set selected building + optional selection state
            SelectedBuilding = id;
            HasSelection = true;

            // If your HUD listens via EventBus, publish a selection event (optional):
            //_s.EventBus?.Publish(new BuildingSelectedEvent(id));
        }
    }
}
