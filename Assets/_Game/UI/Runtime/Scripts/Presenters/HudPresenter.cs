using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    /// <summary>
    /// HUD presenter (runtime binding):
    /// - time/phase from EventBus DayStartedEvent + RunClock
    /// - resources totals from StorageService.GetTotal
    /// - speed buttons -> RunClock.SetTimeScale
    /// - tool buttons -> publish UiToolModeRequestedEvent (Road/Remove/None)
    /// - notifications -> NotificationService.GetVisible()
    /// </summary>
    public sealed class HudPresenter : UiPresenterBase
    {
        private GameServices _s;

        // HUD elements
        private Label _lblTime;
        private Label _lblPhase;
        private Label _lblPopulationSummary;

        private Label _wood, _food, _stone, _iron, _ammo;

        private Button _btnBuild;
        private Button _btnSettings;

        private Button _btnPause;
        private Button _btnS1, _btnS2, _btnS3;

        // Bottom tool buttons (optional)
        private Button _btnToolRoad;
        private Button _btnToolRemove;
        private Button _btnToolCancel;

        private VisualElement _notiHost;
        private readonly List<VisualElement> _notiItems = new(8);

        // cached run state
        private int _yearIndex = 1;
        private Season _season = Season.Spring;
        private int _dayIndex = 1;
        private Phase _phase = Phase.Build;

        protected override void OnBind()
        {
            _s = Ctx?.Services as GameServices;

            _lblTime = Root.Q<Label>("LblTime");
            _lblPhase = Root.Q<Label>("LblPhase");
            _lblPopulationSummary = Root.Q<Label>("LblPopulationSummary");

            _wood = Root.Q<Label>("LblResWood");
            _food = Root.Q<Label>("LblResFood");
            _stone = Root.Q<Label>("LblResStone");
            _iron = Root.Q<Label>("LblResIron");
            _ammo = Root.Q<Label>("LblResAmmo");

            // NOTE: BtnBuild đã chuyển xuống bottom bar nhưng Q theo name vẫn tìm được
            _btnBuild = Root.Q<Button>("BtnBuild");
            _btnSettings = Root.Q<Button>("BtnSettings");

            _btnPause = Root.Q<Button>("BtnPause");
            _btnS1 = Root.Q<Button>("BtnSpeed1");
            _btnS2 = Root.Q<Button>("BtnSpeed2");
            _btnS3 = Root.Q<Button>("BtnSpeed3");

            // Tool buttons trên bottom bar (đúng name theo HUD.uxml bạn đang dùng)
            _btnToolRoad = Root.Q<Button>("BtnToolRoad");
            _btnToolRemove = Root.Q<Button>("BtnToolRemove");
            _btnToolCancel = Root.Q<Button>("BtnToolCancel");

            _notiHost = Root.Q<VisualElement>("NotiStack");

            // UI actions
            if (_btnBuild != null) _btnBuild.clicked += OnBuildClicked;
            if (_btnSettings != null) _btnSettings.clicked += OnSettingsClicked;

            if (_btnPause != null) _btnPause.clicked += OnPauseClicked;
            if (_btnS1 != null) _btnS1.clicked += () => SetSpeed(1f);
            if (_btnS2 != null) _btnS2.clicked += () => SetSpeed(2f);
            if (_btnS3 != null) _btnS3.clicked += () => SetSpeed(3f);

            // Tool actions
            if (_btnToolRoad != null) _btnToolRoad.clicked += OnToolRoad;
            if (_btnToolRemove != null) _btnToolRemove.clicked += OnToolRemove;
            if (_btnToolCancel != null) _btnToolCancel.clicked += OnToolCancel;

            // Subscribe services
            if (_s?.EventBus != null)
            {
                _s.EventBus.Subscribe<DayStartedEvent>(OnDayStarted);
                _s.EventBus.Subscribe<SeasonDayChangedEvent>(OnSeasonDayChanged);
                _s.EventBus.Subscribe<YearChangedEvent>(OnYearChanged);
                _s.EventBus.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
                _s.EventBus.Subscribe<TimeScaleChangedEvent>(OnTimeScaleChanged);

                _s.EventBus.Subscribe<ResourceDeliveredEvent>(OnResourceChanged);
                _s.EventBus.Subscribe<ResourceSpentEvent>(OnResourceChanged);
                _s.EventBus.Subscribe<BuildingPlacedEvent>(OnBuildingChanged);
                _s.EventBus.Subscribe<BuildingUpgradedEvent>(OnBuildingUpgraded);
            }

            if (_s?.NotificationService != null)
            {
                _s.NotificationService.NotificationsChanged += OnNotificationsChanged;
            }

            // Initial refresh
            PullClockFromService();
            RefreshResources();
            RefreshPopulationSummary();
            RefreshNotifications();
            RefreshSpeedHighlight();
        }

        protected override void OnUnbind()
        {
            if (_btnBuild != null) _btnBuild.clicked -= OnBuildClicked;
            if (_btnSettings != null) _btnSettings.clicked -= OnSettingsClicked;

            if (_btnPause != null) _btnPause.clicked -= OnPauseClicked;

            if (_btnToolRoad != null) _btnToolRoad.clicked -= OnToolRoad;
            if (_btnToolRemove != null) _btnToolRemove.clicked -= OnToolRemove;
            if (_btnToolCancel != null) _btnToolCancel.clicked -= OnToolCancel;

            if (_s?.EventBus != null)
            {
                _s.EventBus.Unsubscribe<DayStartedEvent>(OnDayStarted);
                _s.EventBus.Unsubscribe<SeasonDayChangedEvent>(OnSeasonDayChanged);
                _s.EventBus.Unsubscribe<YearChangedEvent>(OnYearChanged);
                _s.EventBus.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
                _s.EventBus.Unsubscribe<TimeScaleChangedEvent>(OnTimeScaleChanged);

                _s.EventBus.Unsubscribe<ResourceDeliveredEvent>(OnResourceChanged);
                _s.EventBus.Unsubscribe<ResourceSpentEvent>(OnResourceChanged);
                _s.EventBus.Unsubscribe<BuildingPlacedEvent>(OnBuildingChanged);
                _s.EventBus.Unsubscribe<BuildingUpgradedEvent>(OnBuildingUpgraded);
            }

            if (_s?.NotificationService != null)
                _s.NotificationService.NotificationsChanged -= OnNotificationsChanged;

            _s = null;
        }

        protected override void OnRefresh()
        {
            RefreshClockLabels();
            RefreshResources();
            RefreshPopulationSummary();
            RefreshNotifications();
            RefreshSpeedHighlight();
        }

        private void PullClockFromService()
        {
            var c = _s?.RunClock;
            if (c == null) return;

            _season = c.CurrentSeason;
            _dayIndex = c.DayIndex;
            _phase = c.CurrentPhase;

            if (c is RunClockService rc)
                _yearIndex = rc.YearIndex;
        }

        private void RefreshClockLabels()
        {
            PullClockFromService();

            if (_lblTime != null)
                _lblTime.text = $"Year {_yearIndex} • {_season} D{_dayIndex}";

            if (_lblPhase != null)
                _lblPhase.text = _phase.ToString();
        }

        private void RefreshResources()
        {
            var st = _s?.StorageService;
            if (st == null)
            {
                SetRes(_wood, 0); SetRes(_food, 0); SetRes(_stone, 0); SetRes(_iron, 0); SetRes(_ammo, 0);
                return;
            }

            SetRes(_wood, st.GetTotal(ResourceType.Wood));
            SetRes(_food, st.GetTotal(ResourceType.Food));
            SetRes(_stone, st.GetTotal(ResourceType.Stone));
            SetRes(_iron, st.GetTotal(ResourceType.Iron));
            SetRes(_ammo, st.GetTotal(ResourceType.Ammo));
        }

        private static void SetRes(Label lbl, int v)
        {
            if (lbl == null) return;
            lbl.text = v.ToString();
        }

        private void RefreshPopulationSummary()
        {
            if (_lblPopulationSummary == null)
                return;

            var pop = _s?.PopulationService;
            if (pop == null)
            {
                _lblPopulationSummary.text = "Pop 0/0 • Need 0/day";
                return;
            }

            var st = pop.State;
            string text = $"Pop {st.PopulationCurrent}/{st.PopulationCap} • Need {st.DailyFoodNeed}/day";
            if (st.StarvedToday || st.StarvationDays > 0)
                text += $" • Starving ({st.StarvationDays})";

            _lblPopulationSummary.text = text;
        }

        private void RefreshSpeedHighlight()
        {
            float ts = _s?.RunClock != null ? _s.RunClock.TimeScale : 1f;

            SetOn(_btnS1, Math.Abs(ts - 1f) < 0.01f);
            SetOn(_btnS2, Math.Abs(ts - 2f) < 0.01f);
            SetOn(_btnS3, Math.Abs(ts - 3f) < 0.01f);
            SetOn(_btnPause, ts <= 0.01f);
        }

        private static void SetOn(Button b, bool on)
        {
            if (b == null) return;
            const string cls = "is-on";
            if (on) b.AddToClassList(cls);
            else b.RemoveFromClassList(cls);
        }

        private void RefreshNotifications()
        {
            if (_notiHost == null) return;

            for (int i = 0; i < _notiItems.Count; i++)
                _notiItems[i]?.RemoveFromHierarchy();
            _notiItems.Clear();

            var ns = _s?.NotificationService;
            if (ns == null) return;

            var list = ns.GetVisible();
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                var vm = list[i];
                if (vm == null) continue;

                var item = new VisualElement();
                item.AddToClassList("noti-item");
                switch (vm.Severity)
                {
                    case NotificationSeverity.Warning: item.AddToClassList("warn"); break;
                    case NotificationSeverity.Error: item.AddToClassList("err"); break;
                }
                item.pickingMode = PickingMode.Ignore;

                var t = new Label(vm.Title ?? ""); t.AddToClassList("noti-title");
                var b = new Label(vm.Body ?? ""); b.AddToClassList("noti-body");

                item.Add(t);
                item.Add(b);

                _notiHost.Add(item);
                _notiItems.Add(item);
            }
        }

        // ===== UI actions =====

        private void OnBuildClicked()
        {
            var panels = Ctx?.Panels;
            if (panels == null) return;

            if (panels.IsOpen(UiKeys.Panel_Build))
            {
                panels.HideCurrent();
                PublishToolMode(UiToolMode.None);
                return;
            }

            // Khi mở build panel, cancel tool mode để tránh ghost/road mode còn chạy
            PublishToolMode(UiToolMode.None);
            panels.Show(UiKeys.Panel_Build);
        }

        private void OnSettingsClicked()
        {
            // Optional: cancel tool mode khi mở settings
            PublishToolMode(UiToolMode.None);
            Ctx?.Modals?.Push(UiKeys.Modal_Settings);
        }

        private void OnPauseClicked()
        {
            PublishToolMode(UiToolMode.None);
            Ctx?.Modals?.Push(UiKeys.Modal_Settings);
        }

        private void OnToolRoad()
        {
            // đóng build panel nếu đang mở
            Ctx?.Panels?.HideCurrent();
            PublishToolMode(UiToolMode.Road);
        }

        private void OnToolRemove()
        {
            Ctx?.Panels?.HideCurrent();
            PublishToolMode(UiToolMode.RemoveRoad);
        }

        private void OnToolCancel()
        {
            Ctx?.Panels?.HideCurrent();
            PublishToolMode(UiToolMode.None);
        }

        private void PublishToolMode(UiToolMode mode)
        {
            var bus = _s?.EventBus;
            if (bus == null) return;
            bus.Publish(new UiToolModeRequestedEvent(mode));
        }

        private void SetSpeed(float scale)
        {
            var c = _s?.RunClock;
            if (c == null) return;

            if (c.CurrentPhase != Phase.Build && !c.DefendSpeedUnlocked && scale > 1.01f)
            {
                _s?.NotificationService?.Push(
                    key: "ui.speed.locked",
                    title: "Speed locked",
                    body: "Defend speed >1x is locked.",
                    severity: NotificationSeverity.Warning,
                    payload: new NotificationPayload(default, default, ""),
                    cooldownSeconds: 1.0f,
                    dedupeByKey: true);
                c.SetTimeScale(1f);
                return;
            }

            c.SetTimeScale(scale);
            RefreshSpeedHighlight();
        }

        // ===== Events =====

        private void OnDayStarted(DayStartedEvent ev)
        {
            _yearIndex = ev.YearIndex;
            _season = ev.Season;
            _dayIndex = ev.DayIndex;
            _phase = ev.Phase;
            RefreshClockLabels();
            RefreshPopulationSummary();
        }

        private void OnSeasonDayChanged(SeasonDayChangedEvent ev)
        {
            _season = ev.Season;
            _dayIndex = ev.DayIndex;
            PullClockFromService();
            RefreshClockLabels();
        }

        private void OnYearChanged(YearChangedEvent ev)
        {
            _yearIndex = ev.ToYear;
            RefreshClockLabels();
        }

        private void OnPhaseChanged(PhaseChangedEvent ev)
        {
            _phase = ev.To;
            PullClockFromService();
            RefreshClockLabels();
        }

        private void OnTimeScaleChanged(TimeScaleChangedEvent ev)
        {
            RefreshSpeedHighlight();
        }

        private void OnResourceChanged(ResourceDeliveredEvent _)
        {
            RefreshResources();
            RefreshPopulationSummary();
        }

        private void OnResourceChanged(ResourceSpentEvent _)
        {
            RefreshResources();
            RefreshPopulationSummary();
        }

        private void OnBuildingChanged(BuildingPlacedEvent _)
        {
            RefreshResources();
            RefreshPopulationSummary();
        }

        private void OnBuildingUpgraded(BuildingUpgradedEvent _)
        {
            RefreshResources();
            RefreshPopulationSummary();
        }

        private void OnNotificationsChanged() => RefreshNotifications();
    }
}