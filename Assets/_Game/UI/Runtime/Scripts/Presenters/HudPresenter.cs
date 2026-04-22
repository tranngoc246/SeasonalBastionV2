using System;
using System.Collections.Generic;
using SeasonalBastion.UI.Views;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    /// <summary>
    /// Lightweight HUD presenter.
    /// Responsible for element queries, button wiring, and simple view updates only.
    /// Runtime orchestration should be handled by a separate binder/controller.
    /// </summary>
    public sealed class HudPresenter : UiPresenterBase
    {
        private Label _lblTime;
        private Label _lblPhase;
        private Label _lblPopulationSummary;

        private Button _btnBuild;
        private Button _btnSettings;
        private Button _btnPause;
        private Button _btnSpeed1;
        private Button _btnSpeed2;
        private Button _btnSpeed3;
        private Button _btnToolRoad;
        private Button _btnToolRemove;
        private Button _btnToolCancel;

        private NotificationView _notificationView;
        private ResourceBarPresenter _resourceBarPresenter;

        public event Action BuildClicked;
        public event Action SettingsClicked;
        public event Action PauseClicked;
        public event Action Speed1Clicked;
        public event Action Speed2Clicked;
        public event Action Speed3Clicked;
        public event Action RoadClicked;
        public event Action RemoveClicked;
        public event Action CancelClicked;

        protected override void OnBind()
        {
            _lblTime = Root.Q<Label>("LblTime");
            _lblPhase = Root.Q<Label>("LblPhase");
            _lblPopulationSummary = Root.Q<Label>("LblPopulationSummary");

            _btnBuild = Root.Q<Button>("BtnBuild");
            _btnSettings = Root.Q<Button>("BtnSettings");
            _btnPause = Root.Q<Button>("BtnPause");
            _btnSpeed1 = Root.Q<Button>("BtnSpeed1");
            _btnSpeed2 = Root.Q<Button>("BtnSpeed2");
            _btnSpeed3 = Root.Q<Button>("BtnSpeed3");
            _btnToolRoad = Root.Q<Button>("BtnToolRoad");
            _btnToolRemove = Root.Q<Button>("BtnToolRemove");
            _btnToolCancel = Root.Q<Button>("BtnToolCancel");

            var notificationRoot = Root.Q<VisualElement>("HudNotificationPanel");
            _notificationView = new NotificationView();
            _notificationView.Initialize(notificationRoot);

            var resourceBarRoot = Root.Q<VisualElement>("HudResourceBar");
            _resourceBarPresenter = resourceBarRoot != null ? new ResourceBarPresenter(resourceBarRoot) : null;
            _resourceBarPresenter?.BindMockData();

            RegisterButtonCallbacks();
        }

        protected override void OnUnbind()
        {
            UnregisterButtonCallbacks();
            _notificationView?.Clear();

            _lblTime = null;
            _lblPhase = null;
            _lblPopulationSummary = null;
            _btnBuild = null;
            _btnSettings = null;
            _btnPause = null;
            _btnSpeed1 = null;
            _btnSpeed2 = null;
            _btnSpeed3 = null;
            _btnToolRoad = null;
            _btnToolRemove = null;
            _btnToolCancel = null;
            _notificationView = null;
            _resourceBarPresenter = null;
        }

        protected override void OnRefresh()
        {
        }

        public void SetTimeText(string value)
        {
            if (_lblTime != null)
                _lblTime.text = value ?? string.Empty;
        }

        public void SetPhaseText(string value)
        {
            if (_lblPhase != null)
                _lblPhase.text = value ?? string.Empty;
        }

        public void SetPopulationSummaryText(string value)
        {
            if (_lblPopulationSummary != null)
                _lblPopulationSummary.text = value ?? string.Empty;
        }

        public void SetResourceValue(string itemName, string value)
        {
            _resourceBarPresenter?.UpdateValue(itemName, value);
        }

        public void SetResourceItem(ResourceItemViewModel itemViewModel)
        {
            _resourceBarPresenter?.UpdateItem(itemViewModel);
        }

        public void SetSpeedSelection(int speedLevel, bool paused)
        {
            SetButtonActive(_btnPause, paused);
            SetButtonActive(_btnSpeed1, !paused && speedLevel == 1);
            SetButtonActive(_btnSpeed2, !paused && speedLevel == 2);
            SetButtonActive(_btnSpeed3, !paused && speedLevel == 3);
        }

        public void ShowNotification(string message)
        {
            _notificationView?.Show(message);
        }

        public void ShowNotification(string message, NotificationKind kind)
        {
            _notificationView?.Show(message, kind);
        }

        public void SetNotifications(IReadOnlyList<NotificationItemViewModel> items)
        {
            _notificationView?.SetNotifications(items);
        }

        private void RegisterButtonCallbacks()
        {
            if (_btnBuild != null)
                _btnBuild.clicked += HandleBuildClicked;

            if (_btnSettings != null)
                _btnSettings.clicked += HandleSettingsClicked;

            if (_btnPause != null)
                _btnPause.clicked += HandlePauseClicked;

            if (_btnSpeed1 != null)
                _btnSpeed1.clicked += HandleSpeed1Clicked;

            if (_btnSpeed2 != null)
                _btnSpeed2.clicked += HandleSpeed2Clicked;

            if (_btnSpeed3 != null)
                _btnSpeed3.clicked += HandleSpeed3Clicked;

            if (_btnToolRoad != null)
                _btnToolRoad.clicked += HandleRoadClicked;

            if (_btnToolRemove != null)
                _btnToolRemove.clicked += HandleRemoveClicked;

            if (_btnToolCancel != null)
                _btnToolCancel.clicked += HandleCancelClicked;
        }

        private void UnregisterButtonCallbacks()
        {
            if (_btnBuild != null)
                _btnBuild.clicked -= HandleBuildClicked;

            if (_btnSettings != null)
                _btnSettings.clicked -= HandleSettingsClicked;

            if (_btnPause != null)
                _btnPause.clicked -= HandlePauseClicked;

            if (_btnSpeed1 != null)
                _btnSpeed1.clicked -= HandleSpeed1Clicked;

            if (_btnSpeed2 != null)
                _btnSpeed2.clicked -= HandleSpeed2Clicked;

            if (_btnSpeed3 != null)
                _btnSpeed3.clicked -= HandleSpeed3Clicked;

            if (_btnToolRoad != null)
                _btnToolRoad.clicked -= HandleRoadClicked;

            if (_btnToolRemove != null)
                _btnToolRemove.clicked -= HandleRemoveClicked;

            if (_btnToolCancel != null)
                _btnToolCancel.clicked -= HandleCancelClicked;
        }

        private static void SetButtonActive(Button button, bool isActive)
        {
            if (button == null)
                return;

            const string activeClass = "hud-root__speed-button--active";
            if (isActive)
                button.AddToClassList(activeClass);
            else
                button.RemoveFromClassList(activeClass);
        }

        private void HandleBuildClicked() => BuildClicked?.Invoke();
        private void HandleSettingsClicked() => SettingsClicked?.Invoke();
        private void HandlePauseClicked() => PauseClicked?.Invoke();
        private void HandleSpeed1Clicked() => Speed1Clicked?.Invoke();
        private void HandleSpeed2Clicked() => Speed2Clicked?.Invoke();
        private void HandleSpeed3Clicked() => Speed3Clicked?.Invoke();
        private void HandleRoadClicked() => RoadClicked?.Invoke();
        private void HandleRemoveClicked() => RemoveClicked?.Invoke();
        private void HandleCancelClicked() => CancelClicked?.Invoke();
    }
}
