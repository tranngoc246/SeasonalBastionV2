using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
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
        private readonly List<VisualElement> _notificationItems = new(8);

        private GameServices _services;
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

        private VisualElement _notificationPanel;
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
            _services = Ctx?.Services as GameServices;

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

            _notificationPanel = Root.Q<VisualElement>("NotiStack");

            var resourceBarRoot = Root.Q<VisualElement>("HudResourceBar");
            _resourceBarPresenter = resourceBarRoot != null ? new ResourceBarPresenter(resourceBarRoot) : null;
            _resourceBarPresenter?.BindMockData();

            RegisterButtonCallbacks();
            RegisterRuntimeSubscriptions();
            RefreshResourceValues();
        }

        protected override void OnUnbind()
        {
            UnregisterRuntimeSubscriptions();
            UnregisterButtonCallbacks();
            ClearNotifications();

            _services = null;
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
            _notificationPanel = null;
            _resourceBarPresenter = null;
        }

        protected override void OnRefresh()
        {
            RefreshResourceValues();
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

        public void SetResourceValue(string labelName, string value)
        {
            _resourceBarPresenter?.UpdateValue(labelName, value);
        }

        public void SetResourceItem(ResourceItemViewModel item)
        {
            _resourceBarPresenter?.UpdateItem(item);
        }

        public IReadOnlyList<ResourceItemViewModel> GetMockResourceItems()
        {
            return _resourceBarPresenter?.CreateMockSnapshot();
        }

        public void SetSpeedSelection(int speedLevel, bool paused)
        {
            SetButtonActive(_btnPause, paused);
            SetButtonActive(_btnSpeed1, !paused && speedLevel == 1);
            SetButtonActive(_btnSpeed2, !paused && speedLevel == 2);
            SetButtonActive(_btnSpeed3, !paused && speedLevel == 3);
        }

        public void SetNotifications(IReadOnlyList<HudNotificationItem> items)
        {
            if (_notificationPanel == null)
                return;

            ClearNotifications();

            if (items == null)
                return;

            for (var index = 0; index < items.Count; index++)
            {
                var itemData = items[index];
                if (itemData == null)
                    continue;

                var item = new VisualElement();
                item.AddToClassList("hud-root__notification-item");
                item.pickingMode = PickingMode.Ignore;

                switch (itemData.Variant)
                {
                    case HudNotificationVariant.Warning:
                        item.AddToClassList("hud-root__notification-item--warning");
                        break;
                    case HudNotificationVariant.Error:
                        item.AddToClassList("hud-root__notification-item--error");
                        break;
                    default:
                        item.AddToClassList("hud-root__notification-item--info");
                        break;
                }

                var title = new Label(itemData.Title ?? string.Empty);
                title.AddToClassList("hud-root__notification-title");
                title.pickingMode = PickingMode.Ignore;
                item.Add(title);

                if (!string.IsNullOrWhiteSpace(itemData.Body))
                {
                    var body = new Label(itemData.Body);
                    body.AddToClassList("hud-root__notification-body");
                    body.pickingMode = PickingMode.Ignore;
                    item.Add(body);
                }

                _notificationPanel.Add(item);
                _notificationItems.Add(item);
            }
        }

        public VisualElement GetNotificationPanel()
        {
            return _notificationPanel;
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

        private void RegisterRuntimeSubscriptions()
        {
            if (_services?.EventBus == null)
                return;

            _services.EventBus.Subscribe<ResourceDeliveredEvent>(OnResourceChanged);
            _services.EventBus.Subscribe<ResourceSpentEvent>(OnResourceChanged);
            _services.EventBus.Subscribe<AmmoUsedEvent>(OnAmmoChanged);
        }

        private void UnregisterRuntimeSubscriptions()
        {
            if (_services?.EventBus == null)
                return;

            _services.EventBus.Unsubscribe<ResourceDeliveredEvent>(OnResourceChanged);
            _services.EventBus.Unsubscribe<ResourceSpentEvent>(OnResourceChanged);
            _services.EventBus.Unsubscribe<AmmoUsedEvent>(OnAmmoChanged);
        }

        private void RefreshResourceValues()
        {
            var storage = _services?.StorageService;
            if (storage == null)
                return;

            SetResourceValue("ResourceItemWood", storage.GetTotal(ResourceType.Wood).ToString());
            SetResourceValue("ResourceItemStone", storage.GetTotal(ResourceType.Stone).ToString());
            SetResourceValue("ResourceItemIron", storage.GetTotal(ResourceType.Iron).ToString());
            SetResourceValue("ResourceItemFood", storage.GetTotal(ResourceType.Food).ToString());
            SetResourceValue("ResourceItemAmmo", storage.GetTotal(ResourceType.Ammo).ToString());
        }

        private void OnResourceChanged(ResourceDeliveredEvent _) => RefreshResourceValues();
        private void OnResourceChanged(ResourceSpentEvent _) => RefreshResourceValues();
        private void OnAmmoChanged(AmmoUsedEvent _) => RefreshResourceValues();

        private void ClearNotifications()
        {
            for (var index = 0; index < _notificationItems.Count; index++)
                _notificationItems[index]?.RemoveFromHierarchy();

            _notificationItems.Clear();
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

    public sealed class HudNotificationItem
    {
        public string Title { get; set; }
        public string Body { get; set; }
        public HudNotificationVariant Variant { get; set; } = HudNotificationVariant.Info;
    }

    public enum HudNotificationVariant
    {
        Info,
        Warning,
        Error,
    }
}
