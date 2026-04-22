using SeasonalBastion.Contracts;
using SeasonalBastion.UI.Input;
using SeasonalBastion.UI.Navigation;
using SeasonalBastion.UI.Overlay;
using SeasonalBastion.UI.Presenters;
using SeasonalBastion.UI.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI
{
    public sealed class UiSystem : MonoBehaviour
    {
        public UIContext Ctx { get; private set; }
        public IInputGate InputGate => Ctx?.InputGate;

        private UIDocument _hud;
        private UIDocument _panels;
        private UIDocument _modals;
        private UIDocument _overlay;

        private IUiPauseController _pauseController;

        private UiHitTest _hitTest;
        private InputGate _gate;
        private UIStateStore _store;

        private PanelRegistry _panelRegistry;
        private ModalStackController _modalStack;
        private UiGameplayFlowController _flow;

        private ToastController _toasts;
        private TooltipController _tooltips;

        private HudPresenter _hudPresenter;
        private HudRuntimeBinder _hudRuntimeBinder;
        private BuildPanelPresenter _buildPresenter;
        private InspectPanelPresenter _inspectPresenter;
        private SettingsModalPresenter _settingsModal;
        private ConfirmModalPresenter _confirmModal;
        private AssignNpcModalPresenter _assignNpcModal;
        private RunEndedModalPresenter _runEndedModal;
        private RewardSelectionModalPresenter _rewardSelectionModal;

        private float _inspectRefreshTimer;
        private bool _initialized;

        public void Initialize(
            UIDocument hud,
            UIDocument panels,
            UIDocument modals,
            UIDocument overlay,
            object services,
            IUiPauseController pauseController)
        {
            if (_initialized) return;
            _initialized = true;

            _hud = hud;
            _panels = panels;
            _modals = modals;
            _overlay = overlay;

            _pauseController = pauseController;

            _store = new UIStateStore();

            _gate = new InputGate(_store);
            _hitTest = new UiHitTest(_gate);

            _panelRegistry = new PanelRegistry(_store);
            _modalStack = new ModalStackController(_store, _pauseController);
            _flow = new UiGameplayFlowController(_store, _panelRegistry, _modalStack, services);

            _toasts = new ToastController();
            _tooltips = new TooltipController();

            Ctx = new UIContext(
                services,
                _store,
                _gate,
                _panelRegistry,
                _modalStack,
                _flow,
                _toasts,
                _tooltips,
                _hud,
                _panels,
                _modals,
                _overlay
            );

            BindTopologyAndPresenters();
            WireBasicFlow();
        }

        private void BindTopologyAndPresenters()
        {
            var hudRoot = _hud ? _hud.rootVisualElement : null;
            var panelsRoot = _panels ? _panels.rootVisualElement : null;
            var modalsRoot = _modals ? _modals.rootVisualElement : null;
            var overlayRoot = _overlay ? _overlay.rootVisualElement : null;

            var leftDock = UiElementUtil.GetOrCreateChild(panelsRoot, "LeftDockHost");
            var rightDock = UiElementUtil.GetOrCreateChild(panelsRoot, "RightDockHost");

            var scrim = UiElementUtil.GetOrCreateChild(modalsRoot, "Scrim");
            var modalHost = UiElementUtil.GetOrCreateChild(modalsRoot, "ModalHost");

            var toastHost = UiElementUtil.GetOrCreateChild(overlayRoot, "ToastHost");
            var tooltipHost = UiElementUtil.GetOrCreateChild(overlayRoot, "TooltipHost");

            scrim?.AddToClassList(UiKeys.Class_BlockWorld);

            _modalStack.Bind(modalsRoot, scrim, modalHost);
            _toasts.Bind(toastHost);
            _tooltips.Bind(tooltipHost);

            _hitTest.SetDocuments(_overlay, _modals, _panels, _hud);

            _hudPresenter = new HudPresenter();
            _buildPresenter = new BuildPanelPresenter();
            _inspectPresenter = new InspectPanelPresenter();
            _settingsModal = new SettingsModalPresenter();
            _confirmModal = new ConfirmModalPresenter();
            _assignNpcModal = new AssignNpcModalPresenter();
            _runEndedModal = new RunEndedModalPresenter();
            _rewardSelectionModal = new RewardSelectionModalPresenter();

            _hudPresenter.Bind(Ctx, hudRoot);
            _hudRuntimeBinder = new HudRuntimeBinder(_hudPresenter, Ctx?.Services);
            _hudRuntimeBinder.Bind();

            var buildRoot = UiElementUtil.GetOrCreateChild(leftDock, "BuildPanel");
            var inspectRoot = UiElementUtil.GetOrCreateChild(rightDock, "InspectPanel");

            BindPanelsBackgroundDismiss(panelsRoot, buildRoot, inspectRoot);

            _buildPresenter.Bind(Ctx, buildRoot);
            _inspectPresenter.Bind(Ctx, inspectRoot);

            _panelRegistry.Register(UiKeys.Panel_Build, _buildPresenter, buildRoot);
            _panelRegistry.Register(UiKeys.Panel_Inspect, _inspectPresenter, inspectRoot);

            var settingsRoot = UiElementUtil.GetOrCreateChild(modalHost, "SettingsModal");
            var confirmRoot = UiElementUtil.GetOrCreateChild(modalHost, "ConfirmModal");
            var assignRoot = UiElementUtil.GetOrCreateChild(modalHost, "AssignNpcModal");
            var runEndedRoot = UiElementUtil.GetOrCreateChild(modalHost, "RunEndedModal");
            var rewardSelectionRoot = UiElementUtil.GetOrCreateChild(modalHost, "RewardSelectionModal");

            _settingsModal.Bind(Ctx, settingsRoot);
            _confirmModal.Bind(Ctx, confirmRoot);
            _assignNpcModal.Bind(Ctx, assignRoot);
            _runEndedModal.Bind(Ctx, runEndedRoot);
            _rewardSelectionModal.Bind(Ctx, rewardSelectionRoot);

            _modalStack.Register(UiKeys.Modal_Settings, _settingsModal, settingsRoot);
            _modalStack.Register(UiKeys.Modal_Confirm, _confirmModal, confirmRoot);
            _modalStack.Register(UiKeys.Modal_AssignNpc, _assignNpcModal, assignRoot);
            _modalStack.Register(UiKeys.Modal_RunEnded, _runEndedModal, runEndedRoot, dismissOnScrim: false);
            _modalStack.Register(UiKeys.Modal_RewardSelection, _rewardSelectionModal, rewardSelectionRoot, dismissOnScrim: false);

            UiElementUtil.SetVisible(buildRoot, false);
            UiElementUtil.SetVisible(inspectRoot, false);
            UiElementUtil.SetVisible(settingsRoot, false);
            UiElementUtil.SetVisible(confirmRoot, false);
            UiElementUtil.SetVisible(assignRoot, false);
            UiElementUtil.SetVisible(runEndedRoot, false);
            UiElementUtil.SetVisible(rewardSelectionRoot, false);
        }

        private void WireBasicFlow()
        {
            _store.ModalStackChanged += _ =>
            {
                // handled by ModalStackController internals
            };
        }

        private void BindPanelsBackgroundDismiss(VisualElement panelsRoot, VisualElement buildRoot, VisualElement inspectRoot)
        {
            if (panelsRoot == null) return;

            panelsRoot.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (Ctx?.Panels == null || Ctx?.Store == null) return;
                if (evt.target is not VisualElement target) return;

                if (Ctx.Panels.IsOpen(UiKeys.Panel_Inspect)
                    && inspectRoot != null
                    && inspectRoot.resolvedStyle.display != DisplayStyle.None
                    && !inspectRoot.worldBound.Contains(evt.position)
                    && !inspectRoot.Contains(target))
                {
                    (Ctx.Services as GameServices)?.EventBus?.Publish(new UiClearInspectRequestedEvent());
                    evt.StopPropagation();
                    return;
                }

                if (Ctx.Panels.IsOpen(UiKeys.Panel_Build)
                    && buildRoot != null
                    && buildRoot.resolvedStyle.display != DisplayStyle.None
                    && !buildRoot.worldBound.Contains(evt.position)
                    && !buildRoot.Contains(target))
                {
                    (Ctx.Services as GameServices)?.EventBus?.Publish(new UiCloseBuildPanelRequestedEvent());
                    evt.StopPropagation();
                }
            }, TrickleDown.TrickleDown);
        }

        private void Update()
        {
            if (!_initialized) return;

            _hitTest.UpdatePointerBlocking();
            _toasts.Tick(Time.unscaledDeltaTime);
            _tooltips.Tick(Time.unscaledDeltaTime);
            _hudRuntimeBinder?.Refresh();

            // Keep Inspect panel reactive
            _inspectRefreshTimer += Time.unscaledDeltaTime;
            if (_inspectRefreshTimer >= 0.25f)
            {
                _inspectRefreshTimer = 0f;
                if (Ctx?.Panels != null && Ctx.Store != null && Ctx.Panels.IsOpen(UiKeys.Panel_Inspect) && !Ctx.Store.Selected.IsNone)
                    _inspectPresenter?.Refresh();
            }
        }

        private void OnDestroy()
        {
            if (!_initialized) return;

            _hudRuntimeBinder?.Unbind();
            _hudPresenter?.Unbind();
            _buildPresenter?.Unbind();
            _inspectPresenter?.Unbind();
            _settingsModal?.Unbind();
            _confirmModal?.Unbind();
            _assignNpcModal?.Unbind();
            _runEndedModal?.Unbind();
            _rewardSelectionModal?.Unbind();

            _flow?.Dispose();
            _store?.ClearModals();
        }
    }
}