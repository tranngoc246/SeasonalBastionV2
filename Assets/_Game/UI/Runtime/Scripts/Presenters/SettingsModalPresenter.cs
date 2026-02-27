using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    /// <summary>
    /// Settings modal:
    /// - Sliders -> AppSettings
    /// - Resume closes modal
    /// - Save triggers toast/notification
    /// - MainMenu uses GameAppController if present
    /// </summary>
    public sealed class SettingsModalPresenter : UiPresenterBase
    {
        private GameServices _s;

        private Slider _volume;
        private SliderInt _defaultSpeed;

        private Button _btnResume;
        private Button _btnSave;
        private Button _btnMenu;

        private Label _hint;

        protected override void OnBind()
        {
            _s = Ctx?.Services as GameServices;

            Root.AddToClassList(UiKeys.Class_BlockWorld);
            Root.pickingMode = PickingMode.Position;

            _volume = Root.Q<Slider>("SldVolume");
            _defaultSpeed = Root.Q<SliderInt>("SldDefaultSpeed");

            _btnResume = Root.Q<Button>("BtnResume") ?? Root.Q<Button>("BtnClose"); // backward compat
            _btnSave = Root.Q<Button>("BtnSave");
            _btnMenu = Root.Q<Button>("BtnBackToMenu");

            _hint = Root.Q<Label>("LblSettingsHint");

            if (_volume != null)
            {
                _volume.value = AppSettings.MasterVolume;
                _volume.RegisterValueChangedCallback(OnVolumeChanged);
            }

            if (_defaultSpeed != null)
            {
                _defaultSpeed.value = AppSettings.DefaultSpeed;
                _defaultSpeed.RegisterValueChangedCallback(OnDefaultSpeedChanged);
            }

            if (_btnResume != null) _btnResume.clicked += OnResume;
            if (_btnSave != null) _btnSave.clicked += OnSave;
            if (_btnMenu != null) _btnMenu.clicked += OnMenu;

            if (_hint != null) _hint.text = "Click outside to close";
        }

        protected override void OnUnbind()
        {
            if (_volume != null) _volume.UnregisterValueChangedCallback(OnVolumeChanged);
            if (_defaultSpeed != null) _defaultSpeed.UnregisterValueChangedCallback(OnDefaultSpeedChanged);

            if (_btnResume != null) _btnResume.clicked -= OnResume;
            if (_btnSave != null) _btnSave.clicked -= OnSave;
            if (_btnMenu != null) _btnMenu.clicked -= OnMenu;

            _s = null;
        }

        protected override void OnRefresh()
        {
            if (_volume != null) _volume.value = AppSettings.MasterVolume;
            if (_defaultSpeed != null) _defaultSpeed.value = AppSettings.DefaultSpeed;
        }

        private void OnResume()
        {
            Ctx?.Modals?.Pop();
        }

        private void OnSave()
        {
            // In this project AppSettings persists immediately, but keep button as feedback
            _s?.NotificationService?.Push(
                key: "ui.settings.saved",
                title: "Settings",
                body: "Saved",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(default, default, ""),
                cooldownSeconds: 0.5f,
                dedupeByKey: true);
        }

        private void OnMenu()
        {
            // If app controller exists, go back to menu
            if (GameAppController.Instance != null)
                GameAppController.Instance.GoToMainMenu();
            else
                _s?.NotificationService?.Push(
                    key: "ui.menu.missing",
                    title: "Menu",
                    body: "GameAppController missing",
                    severity: NotificationSeverity.Warning,
                    payload: new NotificationPayload(default, default, ""),
                    cooldownSeconds: 1f,
                    dedupeByKey: true);
        }

        private void OnVolumeChanged(ChangeEvent<float> ev)
        {
            AppSettings.MasterVolume = Mathf.Clamp01(ev.newValue);
            // Optional: route to AudioService if exists
            // (_s?.AudioService as ???)?.SetMasterVolume(AppSettings.MasterVolume);
        }

        private void OnDefaultSpeedChanged(ChangeEvent<int> ev)
        {
            AppSettings.DefaultSpeed = Mathf.Clamp(ev.newValue, 1, 3);

            // Apply if build phase
            var clock = _s?.RunClock;
            if (clock != null && clock.CurrentPhase == Phase.Build && clock.TimeScale > 0.01f)
                clock.SetTimeScale(AppSettings.DefaultSpeed);
        }
    }
}
