using SeasonalBastion.UI.Input;
using SeasonalBastion.UI.Navigation;
using SeasonalBastion.UI.Overlay;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI
{
    /// <summary>
    /// UIContext: mọi presenter truy cập qua đây thay vì singleton/static.
    /// Services là object để bạn map sang GameServices thật sau này.
    /// </summary>
    public sealed class UIContext
    {
        public object Services { get; }
        public UIStateStore Store { get; }
        public IInputGate InputGate { get; }

        public PanelRegistry Panels { get; }
        public ModalStackController Modals { get; }
        public UiGameplayFlowController Flow { get; }

        public ToastController Toasts { get; }
        public TooltipController Tooltips { get; }

        public UIDocument DocHud { get; }
        public UIDocument DocPanels { get; }
        public UIDocument DocModals { get; }
        public UIDocument DocOverlay { get; }

        public UIContext(
            object services,
            UIStateStore store,
            IInputGate inputGate,
            PanelRegistry panels,
            ModalStackController modals,
            UiGameplayFlowController flow,
            ToastController toasts,
            TooltipController tooltips,
            UIDocument docHud,
            UIDocument docPanels,
            UIDocument docModals,
            UIDocument docOverlay)
        {
            Services = services;
            Store = store;
            InputGate = inputGate;
            Panels = panels;
            Modals = modals;
            Flow = flow;
            Toasts = toasts;
            Tooltips = tooltips;
            DocHud = docHud;
            DocPanels = docPanels;
            DocModals = docModals;
            DocOverlay = docOverlay;
        }
    }
}