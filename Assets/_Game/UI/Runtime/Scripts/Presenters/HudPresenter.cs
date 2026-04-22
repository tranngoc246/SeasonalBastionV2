using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    /// <summary>
    /// HUD presenter with UI-only orchestration.
    /// Keeps runtime bindings inside UI layer and does not modify core systems.
    /// </summary>
    public sealed class HudPresenter : UiPresenterBase
    {
        private GameServices _services;

        private Label _lblTime;
        private Label _lblDayYear;

        private Label _wood;
        private Label _stone;
        private Label _iron;
        private Label _food;
        private Label _ammo;

        private Button _btnBuild;
        private Button _btnRoad;
        private Button _btnRemove;
        private Button _btnSettings;

        private VisualElement _notificationPanel;
        private readonly List<VisualElement> _notificationItems = new(8);

        private int _yearIndex = 1;
        private int _dayIndex = 1;

        protected override void OnBind()
        {
            _services = Ctx?.Services as GameServices;

            _lblTime = Root.Q<Label>("LblTime");
            _lblDayYear = Root.Q<Label>("LblDayYear");

            _wood = Root.Q<Label>("LblResWood");
            _stone = Root.Q<Label>("LblResStone");
            _iron = Root.Q<Label>("LblResIron");
            _food = Root.Q<Label>("LblResFood");
            _ammo = Root.Q<Label>("LblResAmmo");

            _btnBuild = Root.Q<Button>("BtnBuild");
            _btnRoad = Root.Q<Button>("BtnToolRoad");
            _btnRemove = Root.Q<Button>("BtnToolRemove");
            _btnSettings = Root.Q<Button>("BtnSettings");

            _notificationPanel = Root.Q<VisualElement>("NotiStack") ?? Root.Q<VisualElement>("NotificationPanel");

            RegisterButtonCallbacks();
            RegisterRuntimeBindings();

            PullClockFromService();
            RefreshClockLabels();
            RefreshResources();
            RefreshNotifications();
        }

        protected override void OnUnbind()
        {
            UnregisterButtonCallbacks();
            UnregisterRuntimeBindings();
            ClearNotifications();

            _services = null;
            _lblTime = null;
            _lblDayYear = null;
            _wood = null;
            _stone = null;
            _iron = null;
            _food = null;
            _ammo = null;
            _btnBuild = null;
            _btnRoad = null;
            _btnRemove = null;
            _btnSettings = null;
            _notificationPanel = null;
        }

        protected override void OnRefresh()
        {
            PullClockFromService();
            RefreshClockLabels();
            RefreshResources();
            RefreshNotifications();
        }

        private void RegisterButtonCallbacks()
        {
            if (_btnBuild != null)
                _btnBuild.clicked += OnBuildClicked;

            if (_btnRoad != null)
                _btnRoad.clicked += OnRoadClicked;

            if (_btnRemove != null)
                _btnRemove.clicked += OnRemoveClicked;

            if (_btnSettings != null)
                _btnSettings.clicked += OnSettingsClicked;
        }

        private void UnregisterButtonCallbacks()
        {
            if (_btnBuild != null)
                _btnBuild.clicked -= OnBuildClicked;

            if (_btnRoad != null)
                _btnRoad.clicked -= OnRoadClicked;

            if (_btnRemove != null)
                _btnRemove.clicked -= OnRemoveClicked;

            if (_btnSettings != null)
                _btnSettings.clicked -= OnSettingsClicked;
        }

        private void RegisterRuntimeBindings()
        {
            if (_services?.EventBus != null)
            {
                _services.EventBus.Subscribe<DayStartedEvent>(OnDayStarted);
                _services.EventBus.Subscribe<SeasonDayChangedEvent>(OnSeasonDayChanged);
                _services.EventBus.Subscribe<YearChangedEvent>(OnYearChanged);
                _services.EventBus.Subscribe<ResourceDeliveredEvent>(OnResourceDelivered);
                _services.EventBus.Subscribe<ResourceSpentEvent>(OnResourceSpent);
                _services.EventBus.Subscribe<BuildingPlacedEvent>(OnBuildingStateChanged);
                _services.EventBus.Subscribe<BuildingUpgradedEvent>(OnBuildingStateChanged);
            }

            if (_services?.NotificationService != null)
                _services.NotificationService.NotificationsChanged += OnNotificationsChanged;
        }

        private void UnregisterRuntimeBindings()
        {
            if (_services?.EventBus != null)
            {
                _services.EventBus.Unsubscribe<DayStartedEvent>(OnDayStarted);
                _services.EventBus.Unsubscribe<SeasonDayChangedEvent>(OnSeasonDayChanged);
                _services.EventBus.Unsubscribe<YearChangedEvent>(OnYearChanged);
                _services.EventBus.Unsubscribe<ResourceDeliveredEvent>(OnResourceDelivered);
                _services.EventBus.Unsubscribe<ResourceSpentEvent>(OnResourceSpent);
                _services.EventBus.Unsubscribe<BuildingPlacedEvent>(OnBuildingStateChanged);
                _services.EventBus.Unsubscribe<BuildingUpgradedEvent>(OnBuildingStateChanged);
            }

            if (_services?.NotificationService != null)
                _services.NotificationService.NotificationsChanged -= OnNotificationsChanged;
        }

        private void PullClockFromService()
        {
            var runClock = _services?.RunClock;
            if (runClock == null)
                return;

            _dayIndex = Math.Max(1, runClock.DayIndex);

            if (runClock is RunClockService runtimeClock)
                _yearIndex = Math.Max(1, runtimeClock.YearIndex);
        }

        private void RefreshClockLabels()
        {
            if (_lblTime != null)
                _lblTime.text = $"Day {_dayIndex}";

            if (_lblDayYear != null)
                _lblDayYear.text = $"Year {_yearIndex}";
        }

        private void RefreshResources()
        {
            var storage = _services?.StorageService;
            if (storage == null)
            {
                SetLabel(_wood, 0);
                SetLabel(_stone, 0);
                SetLabel(_iron, 0);
                SetLabel(_food, 0);
                SetLabel(_ammo, 0);
                return;
            }

            SetLabel(_wood, storage.GetTotal(ResourceType.Wood));
            SetLabel(_stone, storage.GetTotal(ResourceType.Stone));
            SetLabel(_iron, storage.GetTotal(ResourceType.Iron));
            SetLabel(_food, storage.GetTotal(ResourceType.Food));
            SetLabel(_ammo, storage.GetTotal(ResourceType.Ammo));
        }

        private void RefreshNotifications()
        {
            if (_notificationPanel == null)
                return;

            ClearNotifications();

            var service = _services?.NotificationService;
            if (service == null)
                return;

            var notifications = service.GetVisible();
            if (notifications == null)
                return;

            for (var index = 0; index < notifications.Count; index++)
            {
                var viewModel = notifications[index];
                if (viewModel == null)
                    continue;

                var item = new VisualElement();
                item.AddToClassList("hud-root__notification-item");
                item.pickingMode = PickingMode.Ignore;

                var title = new Label(string.IsNullOrWhiteSpace(viewModel.Title) ? "Notification" : viewModel.Title);
                title.AddToClassList("hud-root__notification-title");
                title.pickingMode = PickingMode.Ignore;
                item.Add(title);

                if (!string.IsNullOrWhiteSpace(viewModel.Body))
                {
                    var body = new Label(viewModel.Body);
                    body.AddToClassList("hud-root__notification-body");
                    body.pickingMode = PickingMode.Ignore;
                    item.Add(body);
                }

                switch (viewModel.Severity)
                {
                    case NotificationSeverity.Warning:
                        item.AddToClassList("hud-root__notification-item--warning");
                        break;
                    case NotificationSeverity.Error:
                        item.AddToClassList("hud-root__notification-item--error");
                        break;
                    default:
                        item.AddToClassList("hud-root__notification-item--info");
                        break;
                }

                _notificationPanel.Add(item);
                _notificationItems.Add(item);
            }
        }

        private void ClearNotifications()
        {
            for (var index = 0; index < _notificationItems.Count; index++)
                _notificationItems[index]?.RemoveFromHierarchy();

            _notificationItems.Clear();
        }

        private static void SetLabel(Label label, int value)
        {
            if (label != null)
                label.text = value.ToString();
        }

        private void OnBuildClicked()
        {
            _services?.EventBus?.Publish(new UiOpenBuildPanelRequestedEvent());
        }

        private void OnRoadClicked()
        {
            _services?.EventBus?.Publish(new UiToolModeRequestedEvent(UiToolMode.Road));
        }

        private void OnRemoveClicked()
        {
            _services?.EventBus?.Publish(new UiToolModeRequestedEvent(UiToolMode.Remove));
        }

        private void OnSettingsClicked()
        {
            _services?.EventBus?.Publish(new UiToolModeRequestedEvent(UiToolMode.Select));
            _services?.EventBus?.Publish(new UiOpenModalRequestedEvent(UiKeys.Modal_Settings));
        }

        private void OnDayStarted(DayStartedEvent eventData)
        {
            _dayIndex = Math.Max(1, eventData.DayIndex);
            _yearIndex = Math.Max(1, eventData.YearIndex);
            RefreshClockLabels();
        }

        private void OnSeasonDayChanged(SeasonDayChangedEvent eventData)
        {
            _dayIndex = Math.Max(1, eventData.DayIndex);
            RefreshClockLabels();
        }

        private void OnYearChanged(YearChangedEvent eventData)
        {
            _yearIndex = Math.Max(1, eventData.ToYear);
            RefreshClockLabels();
        }

        private void OnResourceDelivered(ResourceDeliveredEvent _)
        {
            RefreshResources();
        }

        private void OnResourceSpent(ResourceSpentEvent _)
        {
            RefreshResources();
        }

        private void OnBuildingStateChanged(BuildingPlacedEvent _)
        {
            RefreshResources();
        }

        private void OnBuildingStateChanged(BuildingUpgradedEvent _)
        {
            RefreshResources();
        }

        private void OnNotificationsChanged()
        {
            RefreshNotifications();
        }
    }
}
