using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    /// <summary>
    /// M3: Road placement tool.
    /// - LMB to place road on hovered cell
    /// - ESC (via ToolModeController) cancels
    /// Notes:
    /// - We rely on PlacementService.CanPlaceRoad for legality.
    /// - Visuals updated via RoadRuntimeView.
    /// </summary>
    public sealed class RoadPlacementController : MonoBehaviour
    {
        private GameServices _s;
        private WorldSelectionController _mapper;
        private RoadRuntimeView _roadsView;
        private bool _active;
        private UIDocument _hudDoc;
        private UIDocument _panelsDoc;
        private UIDocument _modalsDoc;

        public bool IsActive => _active;

        public void Bind(
            GameServices s,
            WorldSelectionController mapper,
            RoadRuntimeView roadsView,
            UIDocument hudDoc,
            UIDocument panelsDoc,
            UIDocument modalsDoc)
        {
            _s = s;
            _mapper = mapper;
            _roadsView = roadsView;

            _hudDoc = hudDoc;
            _panelsDoc = panelsDoc;
            _modalsDoc = modalsDoc;
        }

        public void Begin()
        {
            _active = true;
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
        }

        private void Update()
        {
            if (!_active) return;
            if (_s == null || _s.PlacementService == null || _s.GridMap == null) return;
            if (_mapper == null) return;
            if (Mouse.current == null) return;

            if (!Mouse.current.leftButton.wasPressedThisFrame)
                return;

            if (UiBlocker.IsPointerOverBlockingUi(Mouse.current.position.ReadValue(), _hudDoc, _panelsDoc, _modalsDoc))
                return;

            if (!_mapper.TryScreenToCell(Mouse.current.position.ReadValue(), out var cell))
                return;

            if (!_s.PlacementService.CanPlaceRoad(cell))
            {
                _s.NotificationService?.Push(
                    key: "road_fail",
                    title: "Can't place road",
                    body: "Invalid cell.",
                    severity: NotificationSeverity.Warning,
                    payload: default,
                    cooldownSeconds: 0.5f,
                    dedupeByKey: false
                );
                return;
            }

            _s.PlacementService.PlaceRoad(cell);
            _roadsView?.SetRoad(cell, true);
        }
    }
}
