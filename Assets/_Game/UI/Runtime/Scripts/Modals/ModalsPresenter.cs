using System;
using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    internal sealed class ModalsPresenter
    {
        private readonly GameServices _s;
        private readonly VisualElement _modalsRoot;

        // Root parts
        private readonly VisualElement _scrim;
        private readonly VisualElement _modalHost;

        // HUD hook
        private readonly Button _btnMenu;

        // Settings modal
        private readonly VisualElement _settingsModal;
        private readonly Label _settingsTitle;
        private readonly Label _settingsHint;
        private readonly Slider _sldVolume;
        private readonly SliderInt _sldDefaultSpeed;
        private readonly Button _btnResume;
        private readonly Button _btnSave;
        private readonly Button _btnBackToMenu;

        // RunEnd modal
        private readonly VisualElement _runEndModal;
        private readonly Label _runEndTitle;
        private readonly Label _runEndBody;
        private readonly Button _btnRunEndNew;
        private readonly Button _btnRunEndMenu;
        private readonly Button _btnRunEndQuit;

        private bool _isOpen;
        private float _prevTimeScale = 1f;

        // keep delegates to unhook cleanly
        private EventCallback<ChangeEvent<float>> _onVolumeChanged;
        private EventCallback<ChangeEvent<int>> _onDefaultSpeedChanged;
        private EventCallback<ClickEvent> _onScrimClicked;

        public ModalsPresenter(VisualElement hudRoot, VisualElement modalsRoot, GameServices s)
        {
            _s = s;
            _modalsRoot = modalsRoot;

            _btnMenu = hudRoot?.Q<Button>("BtnMenu");

            _scrim = _modalsRoot?.Q<VisualElement>("Scrim");
            _modalHost = _modalsRoot?.Q<VisualElement>("ModalHost");

            _settingsModal = _modalsRoot?.Q<VisualElement>("SettingsModal");
            _settingsTitle = _settingsModal?.Q<Label>("SettingsTitle");
            _settingsHint = _settingsModal?.Q<Label>("LblSettingsHint");
            _sldVolume = _settingsModal?.Q<Slider>("SldVolume");
            _sldDefaultSpeed = _settingsModal?.Q<SliderInt>("SldDefaultSpeed");
            _btnResume = _settingsModal?.Q<Button>("BtnResume");
            _btnSave = _settingsModal?.Q<Button>("BtnSave");
            _btnBackToMenu = _settingsModal?.Q<Button>("BtnBackToMenu");

            _runEndModal = _modalsRoot?.Q<VisualElement>("RunEndModal");
            _runEndTitle = _runEndModal?.Q<Label>("RunEndTitle");
            _runEndBody = _runEndModal?.Q<Label>("RunEndBody");
            _btnRunEndNew = _runEndModal?.Q<Button>("BtnRunEndNew");
            _btnRunEndMenu = _runEndModal?.Q<Button>("BtnRunEndMenu");
            _btnRunEndQuit = _runEndModal?.Q<Button>("BtnRunEndQuit");
        }

        public void Bind()
        {
            HideAllImmediate();

            // Scrim click-outside to close (optional)
            if (_scrim != null)
            {
                _scrim.pickingMode = PickingMode.Position;
                _onScrimClicked = _ => CloseAll();
                _scrim.RegisterCallback(_onScrimClicked);
            }

            if (_btnMenu != null) _btnMenu.clicked += ToggleSettings;

            if (_btnResume != null) _btnResume.clicked += CloseAll;
            if (_btnSave != null) _btnSave.clicked += SaveNow;
            if (_btnBackToMenu != null) _btnBackToMenu.clicked += BackToMenu;

            if (_btnRunEndNew != null) _btnRunEndNew.clicked += RunEnd_NewGame;
            if (_btnRunEndMenu != null) _btnRunEndMenu.clicked += BackToMenu;
            if (_btnRunEndQuit != null) _btnRunEndQuit.clicked += Quit;

            if (_sldVolume != null)
            {
                _sldVolume.value = AppSettings.MasterVolume;
                _onVolumeChanged = ev => AppSettings.MasterVolume = ev.newValue;
                _sldVolume.RegisterValueChangedCallback(_onVolumeChanged);
            }

            if (_sldDefaultSpeed != null)
            {
                _sldDefaultSpeed.value = AppSettings.DefaultSpeed;
                _onDefaultSpeedChanged = ev => AppSettings.DefaultSpeed = ev.newValue;
                _sldDefaultSpeed.RegisterValueChangedCallback(_onDefaultSpeedChanged);
            }

            if (_s?.EventBus != null)
            {
                _s.EventBus.Subscribe<RunEndedEvent>(OnRunEndedEvent);
            }
        }

        public void Unbind()
        {
            if (_btnMenu != null) _btnMenu.clicked -= ToggleSettings;

            if (_btnResume != null) _btnResume.clicked -= CloseAll;
            if (_btnSave != null) _btnSave.clicked -= SaveNow;
            if (_btnBackToMenu != null) _btnBackToMenu.clicked -= BackToMenu;

            if (_btnRunEndNew != null) _btnRunEndNew.clicked -= RunEnd_NewGame;
            if (_btnRunEndMenu != null) _btnRunEndMenu.clicked -= BackToMenu;
            if (_btnRunEndQuit != null) _btnRunEndQuit.clicked -= Quit;

            if (_sldVolume != null && _onVolumeChanged != null)
                _sldVolume.UnregisterValueChangedCallback(_onVolumeChanged);

            if (_sldDefaultSpeed != null && _onDefaultSpeedChanged != null)
                _sldDefaultSpeed.UnregisterValueChangedCallback(_onDefaultSpeedChanged);

            if (_scrim != null && _onScrimClicked != null)
                _scrim.UnregisterCallback(_onScrimClicked);

            if (_s?.EventBus != null)
            {
                _s.EventBus.Unsubscribe<RunEndedEvent>(OnRunEndedEvent);
            }

            CloseAll();
        }

        /// <summary>
        /// M3: allow toolbar button to toggle settings without duplicating logic.
        /// </summary>
        public void ToggleSettingsExternal()
        {
            ToggleSettings();
        }

        private void ToggleSettings()
        {
            if (_runEndModal != null && _runEndModal.style.display == DisplayStyle.Flex)
                return; // don't override run-end modal

            bool open = _settingsModal != null && _settingsModal.style.display != DisplayStyle.Flex;
            if (open) OpenSettings();
            else CloseAll();
        }

        private void OpenSettings()
        {
            OpenRoot();

            if (_settingsTitle != null) _settingsTitle.text = "PAUSE";
            if (_settingsHint != null) _settingsHint.text = "";

            if (_settingsModal != null) _settingsModal.style.display = DisplayStyle.Flex;
            if (_runEndModal != null) _runEndModal.style.display = DisplayStyle.None;
        }

        private void OnRunEndedEvent(RunEndedEvent ev)
        {
            OpenRunEnd(ev.Outcome);
        }

        private void OpenRunEnd(RunOutcome outcome)
        {
            OpenRoot();

            if (_runEndTitle != null)
                _runEndTitle.text = outcome == RunOutcome.Victory ? "VICTORY" : "DEFEAT";

            if (_runEndBody != null)
            {
                var rc = _s?.RunClock;
                string body = "Run ended.";
                if (rc is RunClockService rcs)
                {
                    body = $"Reached: Year {rcs.YearIndex} \x95 {rc.CurrentSeason} D{rc.DayIndex}";
                }
                _runEndBody.text = body;
            }

            if (_settingsModal != null) _settingsModal.style.display = DisplayStyle.None;
            if (_runEndModal != null) _runEndModal.style.display = DisplayStyle.Flex;
        }

        private void OpenRoot()
        {
            if (_modalsRoot == null) return;

            if (!_isOpen)
            {
                _isOpen = true;

                // pause game
                var rc = _s?.RunClock;
                if (rc != null)
                {
                    _prevTimeScale = rc.TimeScale <= 0.01f ? 1f : rc.TimeScale;
                    rc.SetTimeScale(0f);
                }
            }

            _modalsRoot.style.display = DisplayStyle.Flex;
            _modalsRoot.pickingMode = PickingMode.Position; // block click-through
        }

        private void CloseAll()
        {
            if (_modalsRoot == null) return;

            HideAllImmediate();

            if (_isOpen)
            {
                _isOpen = false;

                // resume
                var rc = _s?.RunClock;
                if (rc != null)
                {
                    float target = _prevTimeScale <= 0.01f ? 1f : _prevTimeScale;

                    // apply app default speed if we were in build and user changed it
                    if (rc.CurrentPhase == Phase.Build)
                        target = Mathf.Clamp(AppSettings.DefaultSpeed, 1, 3);

                    rc.SetTimeScale(target);
                }
            }

            _modalsRoot.style.display = DisplayStyle.None;
            _modalsRoot.pickingMode = PickingMode.Ignore;
        }

        private void HideAllImmediate()
        {
            if (_modalsRoot == null) return;

            if (_settingsModal != null) _settingsModal.style.display = DisplayStyle.None;
            if (_runEndModal != null) _runEndModal.style.display = DisplayStyle.None;

            _modalsRoot.pickingMode = PickingMode.Ignore;
            _modalsRoot.style.display = DisplayStyle.None;
        }

        private void SaveNow()
        {
            if (_s?.SaveService == null || _s.WorldState == null || _s.RunClock == null)
            {
                if (_settingsHint != null) _settingsHint.text = "SaveService missing.";
                return;
            }

            var r = _s.SaveService.SaveRun(_s.WorldState, _s.RunClock);
            if (r.Code == SaveResultCode.Ok)
            {
                if (_settingsHint != null) _settingsHint.text = "Saved.";
                _s.NotificationService?.Push("SAVE_OK", "SAVE", "Saved.", NotificationSeverity.Info, default, 1f, true);
            }
            else
            {
                if (_settingsHint != null) _settingsHint.text = "Save failed: " + r.Message;
                _s.NotificationService?.Push("SAVE_FAIL", "SAVE", r.Message, NotificationSeverity.Error, default, 1f, true);
            }
        }

        private void BackToMenu()
        {
            CloseAll();
            GameAppController.Instance.GoToMainMenu();
        }

        private void RunEnd_NewGame()
        {
            CloseAll();
            GameAppController.Instance.RequestNewGame(seed: 0, wipeExistingSave: true);
        }

        private void Quit()
        {
            GameAppController.Instance.Quit();
        }
    }
}
