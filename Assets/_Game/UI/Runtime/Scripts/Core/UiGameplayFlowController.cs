using SeasonalBastion.Contracts;
using SeasonalBastion.UI.Navigation;

namespace SeasonalBastion.UI
{
    public sealed class UiGameplayFlowController
    {
        private readonly UIStateStore _store;
        private readonly PanelRegistry _panels;
        private readonly ModalStackController _modals;
        private readonly GameServices _services;

        private string _pendingPlacementDefId;

        public UiGameplayFlowController(UIStateStore store, PanelRegistry panels, ModalStackController modals, object services)
        {
            _store = store;
            _panels = panels;
            _modals = modals;
            _services = services as GameServices;

            var bus = _services?.EventBus;
            if (bus == null) return;

            bus.Subscribe<UiOpenBuildPanelRequestedEvent>(OnOpenBuildPanelRequested);
            bus.Subscribe<UiCloseBuildPanelRequestedEvent>(OnCloseBuildPanelRequested);
            bus.Subscribe<UiBeginPlaceBuildingEvent>(OnBeginPlaceBuilding);
            bus.Subscribe<UiInspectSelectionRequestedEvent>(OnInspectSelectionRequested);
            bus.Subscribe<UiClearInspectRequestedEvent>(OnClearInspectRequested);
            bus.Subscribe<UiOpenModalRequestedEvent>(OnOpenModalRequested);
            bus.Subscribe<UiToolModeRequestedEvent>(OnToolModeRequested);
            bus.Subscribe<UiPlacementStartedEvent>(OnPlacementStarted);
            bus.Subscribe<UiPlacementFinishedEvent>(OnPlacementFinished);
            bus.Subscribe<UiInspectActionRequestedEvent>(OnInspectActionRequested);
        }

        public void Dispose()
        {
            var bus = _services?.EventBus;
            if (bus == null) return;

            bus.Unsubscribe<UiOpenBuildPanelRequestedEvent>(OnOpenBuildPanelRequested);
            bus.Unsubscribe<UiCloseBuildPanelRequestedEvent>(OnCloseBuildPanelRequested);
            bus.Unsubscribe<UiBeginPlaceBuildingEvent>(OnBeginPlaceBuilding);
            bus.Unsubscribe<UiInspectSelectionRequestedEvent>(OnInspectSelectionRequested);
            bus.Unsubscribe<UiClearInspectRequestedEvent>(OnClearInspectRequested);
            bus.Unsubscribe<UiOpenModalRequestedEvent>(OnOpenModalRequested);
            bus.Unsubscribe<UiToolModeRequestedEvent>(OnToolModeRequested);
            bus.Unsubscribe<UiPlacementStartedEvent>(OnPlacementStarted);
            bus.Unsubscribe<UiPlacementFinishedEvent>(OnPlacementFinished);
            bus.Unsubscribe<UiInspectActionRequestedEvent>(OnInspectActionRequested);
        }

        private void OnOpenBuildPanelRequested(UiOpenBuildPanelRequestedEvent _)
        {
            if (_store.HasModal || _store.IsPlacementActive) return;
            _store.SetToolMode(UiToolMode.Select);
            _panels.Show(UiKeys.Panel_Build);
        }

        private void OnCloseBuildPanelRequested(UiCloseBuildPanelRequestedEvent _)
        {
            if (_panels.IsOpen(UiKeys.Panel_Build))
                _panels.HideCurrent();
            _store.SetToolMode(UiToolMode.Select);
        }

        private void OnBeginPlaceBuilding(UiBeginPlaceBuildingEvent ev)
        {
            if (_store.HasModal || string.IsNullOrEmpty(ev.DefId)) return;
            _pendingPlacementDefId = ev.DefId;
            _store.ClearSelection();
            _panels.HideCurrent();
            _store.SetPlacementActive(true);
            _store.SetToolMode(UiToolMode.BuildPlacement);
        }

        private void OnInspectSelectionRequested(UiInspectSelectionRequestedEvent ev)
        {
            if (_store.HasModal || _store.IsPlacementActive || _store.ToolMode != UiToolMode.Select)
                return;

            var selectionKind = (SelectionKind)ev.Kind;
            var selection = selectionKind switch
            {
                SelectionKind.Building when ev.Id > 0 => SelectionRef.Building(ev.Id),
                SelectionKind.ResourcePatch when ev.Id > 0 => SelectionRef.ResourcePatch(ev.Id),
                _ => SelectionRef.None,
            };

            if (selection.IsNone)
            {
                _store.ClearSelection();
                if (_panels.IsOpen(UiKeys.Panel_Inspect))
                    _panels.HideCurrent();
                return;
            }

            _store.Select(selection);
            _panels.Show(UiKeys.Panel_Inspect);
        }

        private void OnClearInspectRequested(UiClearInspectRequestedEvent _)
        {
            _store.ClearSelection();
            if (_panels.IsOpen(UiKeys.Panel_Inspect))
                _panels.HideCurrent();
        }

        private void OnOpenModalRequested(UiOpenModalRequestedEvent ev)
        {
            if (string.IsNullOrEmpty(ev.ModalKey)) return;
            _modals.Push(ev.ModalKey);
        }

        private void OnToolModeRequested(UiToolModeRequestedEvent ev)
        {
            if (_store.HasModal) return;

            if (ev.Mode == UiToolMode.Select)
            {
                _store.SetToolMode(UiToolMode.Select);
                _store.SetPlacementActive(false);
                _pendingPlacementDefId = null;
                return;
            }

            _store.ClearSelection();
            if (_panels.IsOpen(UiKeys.Panel_Inspect) || _panels.IsOpen(UiKeys.Panel_Build))
                _panels.HideCurrent();

            _store.SetPlacementActive(false);
            _pendingPlacementDefId = null;
            _store.SetToolMode(ev.Mode);
        }

        private void OnPlacementStarted(UiPlacementStartedEvent ev)
        {
            _pendingPlacementDefId = ev.DefId;
            _store.SetPlacementActive(true);
            _store.SetToolMode(UiToolMode.BuildPlacement);
        }

        private void OnPlacementFinished(UiPlacementFinishedEvent ev)
        {
            _pendingPlacementDefId = null;
            _store.SetPlacementActive(false);
            _store.SetToolMode(UiToolMode.Select);

            if (!ev.Confirmed)
                _panels.Show(UiKeys.Panel_Build);
        }

        private void OnInspectActionRequested(UiInspectActionRequestedEvent ev)
        {
            if (_services == null || ev.TargetId <= 0) return;

            var bid = new BuildingId(ev.TargetId);
            switch (ev.Action)
            {
                case "Upgrade":
                    if (_services.BuildOrderService != null)
                        _services.BuildOrderService.CreateUpgradeOrder(bid);
                    break;
                case "Repair":
                    if (_services.BuildOrderService != null)
                        _services.BuildOrderService.CreateRepairOrder(bid);
                    break;
                case "AssignNpc":
                    _modals.Push(UiKeys.Modal_AssignNpc);
                    break;
                case "CancelConstruction":
                    if (_services.BuildOrderService != null)
                        _services.BuildOrderService.CancelByBuilding(bid);
                    break;
            }
        }
    }
}
