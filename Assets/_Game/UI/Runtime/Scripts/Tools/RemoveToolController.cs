using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    /// <summary>
    /// M3: Remove tool (minimal).
    /// Current scope: remove road cells.
    /// (Building delete is intentionally deferred to later milestone.)
    /// </summary>
    public sealed class RemoveToolController : MonoBehaviour
    {
        private GameServices _s;
        private WorldSelectionController _mapper;
        private RoadRuntimeView _roadsView;
        private bool _active;
        private UIDocument _hudDoc;
        private UIDocument _panelsDoc;
        private UIDocument _modalsDoc;

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
                key: "tool_remove",
                title: "REMOVE",
                body: "LMB: remove road • ESC: cancel",
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
            if (_s == null || _s.GridMap == null) return;
            if (_mapper == null) return;
            if (Mouse.current == null) return;

            if (!Mouse.current.leftButton.wasPressedThisFrame)
                return;

            if (UiBlocker.IsPointerOverBlockingUi(Mouse.current.position.ReadValue(), _hudDoc, _panelsDoc, _modalsDoc))
                return;

            if (!_mapper.TryScreenToCell(Mouse.current.position.ReadValue(), out var cell))
                return;

            if (!_s.GridMap.IsInside(cell))
                return;

            // Remove road only (safe)
            if (_s.GridMap.IsRoad(cell))
            {
                _s.GridMap.SetRoad(cell, false);
                _roadsView?.SetRoad(cell, false);

                _s.NotificationService?.Push(
                    key: "road_removed",
                    title: "Road removed",
                    body: $"({cell.X},{cell.Y})",
                    severity: NotificationSeverity.Info,
                    payload: default,
                    cooldownSeconds: 0.15f,
                    dedupeByKey: false
                );
            }
        }
    }
}
