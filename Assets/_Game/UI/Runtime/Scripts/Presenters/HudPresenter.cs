using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    /// <summary>
    /// Full HUD presenter restored on top of the new flexible layout.
    /// Keeps all logic inside the UI layer and preserves existing runtime integration.
    /// </summary>
    public sealed class HudPresenter : UiPresenterBase
    {
        private GameServices _services;

        private Label _lblTime;
        private Label _lblPhase;
        private Label _lblPopulationSummary;

        private Label _wood;
        private Label _stone;
        private Label _iron;
        private Label _food;
        private Label _ammo;
        private Label _ammoState;

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
        private readonly List<VisualElement> _notificationItems = new(8);

        private int _yearIndex = 1;
        private Season _season = Season.Spring;
        private int _dayIndex = 1;
        private Phase _phase = Phase.Build;

        protected override void OnBind()
        {
            _services = Ctx?.Services as GameServices;

            _lblTime = Root.Q<Label>("LblTime");
            _lblPhase = Root.Q<Label>("LblPhase");
            _lblPopulationSummary = Root.Q<Label>("LblPopulationSummary");

            _wood = Root.Q<Label>("LblResWood");
            _stone = Root.Q<Label>("LblResStone");
            _iron = Root.Q<Label>("LblResIron");
            _food = Root.Q<Label>("LblResFood");
            _ammo = Root.Q<Label>("LblResAmmo");
            _ammoState = Root.Q<Label>("LblAmmoState");

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

            RegisterButtonCallbacks();
            RegisterRuntimeBindings();

            PullClockFromService();
            RefreshClockLabels();
            RefreshResources();
            RefreshPopulationSummary();
            RefreshNotifications();
            RefreshSpeedHighlight();
            RefreshAmmoState();
        }

        protected override void OnUnbind()
        {
            UnregisterButtonCallbacks();
            UnregisterRuntimeBindings();
            ClearNotifications();

            _services = null;
            _lblTime = null;
            _lblPhase = null;
            _lblPopulationSummary = null;
            _wood = null;
            _stone = null;
            _iron = null;
            _food = null;
            _ammo = null;
            _ammoState = null;
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
        }

        protected override void OnRefresh()
        {
            PullClockFromService();
            RefreshClockLabels();
            RefreshResources();
            RefreshPopulationSummary();
            RefreshNotifications();
            RefreshSpeedHighlight();
            RefreshAmmoState();
        }

        private void RegisterButtonCallbacks()
        {
            if (_btnBuild != null)
                _btnBuild.clicked += OnBuildClicked;

            if (_btnSettings != null)
                _btnSettings.clicked += OnSettingsClicked;

            if (_btnPause != null)
                _btnPause.clicked += OnPauseClicked;

            if (_btnSpeed1 != null)
                _btnSpeed1.clicked += OnSpeed1Clicked;

            if (_btnSpeed2 != null)
                _btnSpeed2.clicked += OnSpeed2Clicked;

            if (_btnSpeed3 != null)
                _btnSpeed3.clicked += OnSpeed3Clicked;

            if (_btnToolRoad != null)
                _btnToolRoad.clicked += OnToolRoadClicked;

            if (_btnToolRemove != null)
                _btnToolRemove.clicked += OnToolRemoveClicked;

            if (_btnToolCancel != null)
                _btnToolCancel.clicked += OnToolCancelClicked;
        }

        private void UnregisterButtonCallbacks()
        {
            if (_btnBuild != null)
                _btnBuild.clicked -= OnBuildClicked;

            if (_btnSettings != null)
                _btnSettings.clicked -= OnSettingsClicked;

            if (_btnPause != null)
                _btnPause.clicked -= OnPauseClicked;

            if (_btnSpeed1 != null)
                _btnSpeed1.clicked -= OnSpeed1Clicked;

            if (_btnSpeed2 != null)
                _btnSpeed2.clicked -= OnSpeed2Clicked;

            if (_btnSpeed3 != null)
                _btnSpeed3.clicked -= OnSpeed3Clicked;

            if (_btnToolRoad != null)
                _btnToolRoad.clicked -= OnToolRoadClicked;

            if (_btnToolRemove != null)
                _btnToolRemove.clicked -= OnToolRemoveClicked;

            if (_btnToolCancel != null)
                _btnToolCancel.clicked -= OnToolCancelClicked;
        }

        private void RegisterRuntimeBindings()
        {
            if (_services?.EventBus != null)
            {
                _services.EventBus.Subscribe<DayStartedEvent>(OnDayStarted);
                _services.EventBus.Subscribe<SeasonDayChangedEvent>(OnSeasonDayChanged);
                _services.EventBus.Subscribe<YearChangedEvent>(OnYearChanged);
                _services.EventBus.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
                _services.EventBus.Subscribe<TimeScaleChangedEvent>(OnTimeScaleChanged);
                _services.EventBus.Subscribe<ResourceDeliveredEvent>(OnResourceDelivered);
                _services.EventBus.Subscribe<ResourceSpentEvent>(OnResourceSpent);
                _services.EventBus.Subscribe<BuildingPlacedEvent>(OnBuildingPlaced);
                _services.EventBus.Subscribe<BuildingUpgradedEvent>(OnBuildingUpgraded);
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
                _services.EventBus.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
                _services.EventBus.Unsubscribe<TimeScaleChangedEvent>(OnTimeScaleChanged);
                _services.EventBus.Unsubscribe<ResourceDeliveredEvent>(OnResourceDelivered);
                _services.EventBus.Unsubscribe<ResourceSpentEvent>(OnResourceSpent);
                _services.EventBus.Unsubscribe<BuildingPlacedEvent>(OnBuildingPlaced);
                _services.EventBus.Unsubscribe<BuildingUpgradedEvent>(OnBuildingUpgraded);
            }

            if (_services?.NotificationService != null)
                _services.NotificationService.NotificationsChanged -= OnNotificationsChanged;
        }

        private void PullClockFromService()
        {
            var clock = _services?.RunClock;
            if (clock == null)
                return;

            _season = clock.CurrentSeason;
            _dayIndex = Math.Max(1, clock.DayIndex);
            _phase = clock.CurrentPhase;

            if (clock is RunClockService runtimeClock)
                _yearIndex = Math.Max(1, runtimeClock.YearIndex);
        }

        private void RefreshClockLabels()
        {
            if (_lblTime != null)
                _lblTime.text = $"Year {_yearIndex} • {_season} D{_dayIndex}";

            if (_lblPhase != null)
                _lblPhase.text = _phase.ToString();
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

        private void RefreshPopulationSummary()
        {
            if (_lblPopulationSummary == null)
                return;

            var populationService = _services?.PopulationService;
            if (populationService == null)
            {
                _lblPopulationSummary.text = "Pop 0/0 • Need 0/day";
                return;
            }

            var state = populationService.State;
            var summary = $"Pop {state.PopulationCurrent}/{state.PopulationCap} • Need {state.DailyFoodNeed}/day";
            if (state.StarvedToday || state.StarvationDays > 0)
                summary += $" • Starving ({state.StarvationDays})";

            _lblPopulationSummary.text = summary;
        }

        private void RefreshSpeedHighlight()
        {
            var timeScale = _services?.RunClock != null ? _services.RunClock.TimeScale : 1f;

            SetButtonActive(_btnPause, timeScale <= 0.01f);
            SetButtonActive(_btnSpeed1, Math.Abs(timeScale - 1f) < 0.01f);
            SetButtonActive(_btnSpeed2, Math.Abs(timeScale - 2f) < 0.01f);
            SetButtonActive(_btnSpeed3, Math.Abs(timeScale - 3f) < 0.01f);
        }

        private void RefreshAmmoState()
        {
            var ammoService = _services?.AmmoService;
            if (ammoService == null)
            {
                if (_ammoState != null)
                    _ammoState.text = "Unknown";
                return;
            }

            if (_ammoState == null)
                return;

            if (ammoService.Debug_TowersWithoutAmmo > 0)
                _ammoState.text = "Alert";
            else if (ammoService.PendingRequests > 0)
                _ammoState.text = "Low";
            else
                _ammoState.text = "Stable";
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

        private void OnBuildClicked()
        {
            _services?.EventBus?.Publish(new UiOpenBuildPanelRequestedEvent());
        }

        private void OnSettingsClicked()
        {
            PublishToolMode(UiToolMode.Select);
            _services?.EventBus?.Publish(new UiOpenModalRequestedEvent(UiKeys.Modal_Settings));
        }

        private void OnPauseClicked()
        {
            SetSpeed(0f);
        }

        private void OnSpeed1Clicked()
        {
            SetSpeed(1f);
        }

        private void OnSpeed2Clicked()
        {
            SetSpeed(2f);
        }

        private void OnSpeed3Clicked()
        {
            SetSpeed(3f);
        }

        private void OnToolRoadClicked()
        {
            PublishToolMode(UiToolMode.Road);
        }

        private void OnToolRemoveClicked()
        {
            PublishToolMode(UiToolMode.Remove);
        }

        private void OnToolCancelClicked()
        {
            PublishToolMode(UiToolMode.Select);
        }

        private void PublishToolMode(UiToolMode mode)
        {
            _services?.EventBus?.Publish(new UiToolModeRequestedEvent(mode));
        }

        private void SetSpeed(float scale)
        {
            var clock = _services?.RunClock;
            if (clock == null)
                return;

            if (clock.CurrentPhase != Phase.Build && !clock.DefendSpeedUnlocked && scale > 1.01f)
            {
                _services?.NotificationService?.Push(
                    key: "ui.speed.locked",
                    title: "Tốc độ bị khóa",
                    body: "Trong pha phòng thủ, tốc độ hiện chỉ có thể giữ ở 1x.",
                    severity: NotificationSeverity.Warning,
                    payload: default,
                    cooldownSeconds: 3f,
                    dedupeByKey: true);

                clock.SetTimeScale(1f);
                RefreshSpeedHighlight();
                return;
            }

            clock.SetTimeScale(scale);
            RefreshSpeedHighlight();
        }

        private void OnDayStarted(DayStartedEvent eventData)
        {
            _yearIndex = Math.Max(1, eventData.YearIndex);
            _season = eventData.Season;
            _dayIndex = Math.Max(1, eventData.DayIndex);
            _phase = eventData.Phase;
            RefreshClockLabels();
            RefreshPopulationSummary();
            RefreshAmmoState();
        }

        private void OnSeasonDayChanged(SeasonDayChangedEvent eventData)
        {
            _season = eventData.Season;
            _dayIndex = Math.Max(1, eventData.DayIndex);
            PullClockFromService();
            RefreshClockLabels();
        }

        private void OnYearChanged(YearChangedEvent eventData)
        {
            _yearIndex = Math.Max(1, eventData.ToYear);
            RefreshClockLabels();
        }

        private void OnPhaseChanged(PhaseChangedEvent eventData)
        {
            _phase = eventData.To;
            PullClockFromService();
            RefreshClockLabels();
        }

        private void OnTimeScaleChanged(TimeScaleChangedEvent _)
        {
            RefreshSpeedHighlight();
        }

        private void OnResourceDelivered(ResourceDeliveredEvent _)
        {
            RefreshResourceDrivenUi();
        }

        private void OnResourceSpent(ResourceSpentEvent _)
        {
            RefreshResourceDrivenUi();
        }

        private void OnBuildingPlaced(BuildingPlacedEvent _)
        {
            RefreshResourceDrivenUi();
        }

        private void OnBuildingUpgraded(BuildingUpgradedEvent _)
        {
            RefreshResourceDrivenUi();
        }

        private void RefreshResourceDrivenUi()
        {
            RefreshResources();
            RefreshPopulationSummary();
            RefreshAmmoState();
        }

        private void OnNotificationsChanged()
        {
            RefreshNotifications();
        }
    }
}
