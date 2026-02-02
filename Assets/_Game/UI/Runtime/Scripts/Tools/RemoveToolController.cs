using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    public sealed class RemoveToolController : MonoBehaviour
    {
        private GameServices _s;
        private WorldSelectionController _mapper;
        private bool _active;

        private UIDocument _hudDoc;
        private UIDocument _panelsDoc;
        private UIDocument _modalsDoc;

        public void Bind(
            GameServices s,
            WorldSelectionController mapper,
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

        public void End() => _active = false;

        private void Update()
        {
            if (!_active) return;
            if (_s == null || _s.PlacementService == null) return;
            if (_mapper == null) return;
            if (Mouse.current == null) return;

            if (!Mouse.current.leftButton.wasPressedThisFrame)
                return;

            if (UiBlocker.IsPointerOverBlockingUi(Mouse.current.position.ReadValue(), _hudDoc, _panelsDoc, _modalsDoc))
                return;

            if (!_mapper.TryScreenToCell(Mouse.current.position.ReadValue(), out var cell))
                return;

            if (_s.PlacementService.CanRemoveRoad(cell))
            {
                _s.PlacementService.RemoveRoad(cell);

                _s.NotificationService?.Push(
                    key: "road_removed",
                    title: "Road removed",
                    body: $"({cell.X},{cell.Y})",
                    severity: NotificationSeverity.Info,
                    payload: default,
                    cooldownSeconds: 0.35f,
                    dedupeByKey: true
                );
            }
        }
    }
}
