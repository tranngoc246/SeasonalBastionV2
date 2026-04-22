using System.Collections.Generic;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    /// <summary>
    /// Basic HUD presenter for UI binding only.
    /// No game business logic, only element lookup and lightweight view updates.
    /// </summary>
    public sealed class HudPresenter : UiPresenterBase
    {
        private static readonly string[] ResourceElementNames =
        {
            "LblResWood",
            "LblResStone",
            "LblResIron",
            "LblResFood",
            "LblResAmmo"
        };

        private readonly Dictionary<string, Label> _resourceLabels = new();

        private Label _lblTime;
        private Label _lblDayYear;
        private VisualElement _notificationPanel;

        private Button _btnBuild;
        private Button _btnRoad;
        private Button _btnRemove;
        private Button _btnSettings;

        protected override void OnBind()
        {
            _lblTime = Root.Q<Label>("LblTime");
            _lblDayYear = Root.Q<Label>("LblDayYear");
            _notificationPanel = Root.Q<VisualElement>("NotificationPanel");

            CacheResourceLabels();

            _btnBuild = Root.Q<Button>("BtnBuild");
            _btnRoad = Root.Q<Button>("BtnToolRoad");
            _btnRemove = Root.Q<Button>("BtnToolRemove");
            _btnSettings = Root.Q<Button>("BtnSettings");

            RegisterButtonCallbacks();
        }

        protected override void OnUnbind()
        {
            UnregisterButtonCallbacks();
            _resourceLabels.Clear();

            _lblTime = null;
            _lblDayYear = null;
            _notificationPanel = null;

            _btnBuild = null;
            _btnRoad = null;
            _btnRemove = null;
            _btnSettings = null;
        }

        protected override void OnRefresh()
        {
            SetTime("Day 1", "Year 1");
            SetResource("LblResWood", "0");
            SetResource("LblResStone", "0");
            SetResource("LblResIron", "0");
            SetResource("LblResFood", "0");
            SetResource("LblResAmmo", "0");
        }

        public void SetTime(string dayText, string yearText)
        {
            if (_lblTime != null)
                _lblTime.text = string.IsNullOrWhiteSpace(dayText) ? "Day 1" : dayText;

            if (_lblDayYear != null)
                _lblDayYear.text = string.IsNullOrWhiteSpace(yearText) ? "Year 1" : yearText;
        }

        public void SetResource(string elementName, string value)
        {
            if (string.IsNullOrWhiteSpace(elementName))
                return;

            if (_resourceLabels.TryGetValue(elementName, out var label) && label != null)
                label.text = string.IsNullOrWhiteSpace(value) ? "0" : value;
        }

        public VisualElement GetNotificationPanel()
        {
            return _notificationPanel;
        }

        private void CacheResourceLabels()
        {
            _resourceLabels.Clear();

            for (var index = 0; index < ResourceElementNames.Length; index++)
            {
                var elementName = ResourceElementNames[index];
                var label = Root.Q<Label>(elementName);
                if (label != null)
                    _resourceLabels[elementName] = label;
            }
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

        private void OnBuildClicked()
        {
        }

        private void OnRoadClicked()
        {
        }

        private void OnRemoveClicked()
        {
        }

        private void OnSettingsClicked()
        {
        }
    }
}
