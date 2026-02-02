using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SeasonalBastion
{
    /// <summary>
    /// Day4: Click on world -> resolve cell -> read GridMap occupancy -> SelectedBuilding.
    /// No prefab/collider dependency. Deterministic & grid-authority.
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

        private GameServices _s;

        /// <summary>
        /// When true, this controller will ignore world clicks.
        /// Used by UI tools (build/road/remove) to avoid double-handling LMB.
        /// </summary>
        public bool BlockSelection { get; set; }

        public BuildingId SelectedBuilding { get; private set; }
        public bool HasSelection { get; private set; }

        public void Bind(GameServices s)
        {
            _s = s;
            if (_camera == null) _camera = Camera.main;
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

            // Ignore click on UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (_camera == null) return;

            if (!TryScreenToCell(_camera, Mouse.current.position.ReadValue(), out var cell))
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
                // click empty => clear (toi thieu)
                ClearSelection();
            }
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
    }
}
