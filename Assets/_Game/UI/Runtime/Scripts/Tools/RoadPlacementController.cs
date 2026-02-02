using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    /// <summary>
    /// M3: Road placement tool.
    /// - Hover shows 1-cell preview (valid/invalid)
    /// - LMB to place road on hovered cell
    /// - ESC (via ToolModeController) cancels
    /// Notes:
    /// - PlacementService is authority.
    /// - RoadRuntimeView updates via B-lite events (RoadsDirtyEvent), so we don't push view here.
    /// </summary>
    public sealed class RoadPlacementController : MonoBehaviour
    {
        private GameServices _s;
        private WorldSelectionController _mapper;
        private bool _active;

        private UIDocument _hudDoc;
        private UIDocument _panelsDoc;
        private UIDocument _modalsDoc;

        // hover preview (single sprite)
        private SpriteRenderer _hover;
        private Transform _hoverRoot;

        private readonly Color _valid = new Color(1f, 1f, 0f, 0.55f);          // vàng
        private readonly Color _invalid = new Color(1f, 0.25f, 0.25f, 0.55f);   // đỏ nhạt

        public bool IsActive => _active;

        public void Bind(
            GameServices s,
            WorldSelectionController mapper,
            RoadRuntimeView roadsView, // kept for signature compatibility, not used in B-lite
            UIDocument hudDoc,
            UIDocument panelsDoc,
            UIDocument modalsDoc)
        {
            _s = s;
            _mapper = mapper;

            _hudDoc = hudDoc;
            _panelsDoc = panelsDoc;
            _modalsDoc = modalsDoc;
        }

        public void Begin()
        {
            _active = true;

            EnsureHover();

            _s?.NotificationService?.Push(
                key: "tool_road",
                title: "ROAD",
                body: "LMB: place road • ESC: cancel",
                severity: NotificationSeverity.Info,
                payload: default,
                cooldownSeconds: 1.0f,
                dedupeByKey: true
            );
        }

        public void End()
        {
            _active = false;
            HideHover();
        }

        private void Update()
        {
            if (!_active) return;
            if (_s == null || _s.PlacementService == null || _s.GridMap == null) return;
            if (_mapper == null) return;
            if (Mouse.current == null) return;

            var mousePos = Mouse.current.position.ReadValue();

            // If pointer over UI => hide hover and ignore clicks
            if (UiBlocker.IsPointerOverBlockingUi(mousePos, _hudDoc, _panelsDoc, _modalsDoc))
            {
                HideHover();
                return;
            }

            // Hover preview
            if (_mapper.TryScreenToCell(mousePos, out var cell) && _s.GridMap.IsInside(cell))
            {
                EnsureHover();

                bool can = _s.PlacementService.CanPlaceRoad(cell);
                _hover.color = can ? _valid : _invalid;

                var pos = _mapper.CellToWorldCenter(cell);
                pos.z -= 0.05f; // a bit behind building previews but above roads
                _hover.transform.position = pos;

                float cs = Mathf.Max(0.0001f, _mapper.CellSize);
                _hover.transform.localScale = new Vector3(cs, cs, 1f);

                _hover.gameObject.SetActive(true);

                // Click to place
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    if (!can)
                    {
                        _s.NotificationService?.Push(
                            key: "road_fail",
                            title: "Can't place road",
                            body: "Road must be adjacent to an existing road.",
                            severity: NotificationSeverity.Warning,
                            payload: default,
                            cooldownSeconds: 0.5f,
                            dedupeByKey: false
                        );
                        return;
                    }

                    _s.PlacementService.PlaceRoad(cell);
                    // B-lite: RoadRuntimeView listens to RoadsDirtyEvent, so no SetRoad() here.
                }
            }
            else
            {
                HideHover();
            }
        }

        private void EnsureHover()
        {
            if (_hover != null) return;

            if (_hoverRoot == null)
            {
                var rootGo = new GameObject("__RoadHoverPreview");
                rootGo.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                _hoverRoot = rootGo.transform;
            }

            var go = new GameObject("road_hover_cell");
            go.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            go.transform.SetParent(_hoverRoot, false);

            _hover = go.AddComponent<SpriteRenderer>();
            _hover.sprite = RuntimeSpriteFactory.WhiteSprite;
            _hover.sortingOrder = 4999; // below build preview(5000), above roads(-10)
            _hover.gameObject.SetActive(false);
        }

        private void HideHover()
        {
            if (_hover != null)
                _hover.gameObject.SetActive(false);
        }
    }
}
