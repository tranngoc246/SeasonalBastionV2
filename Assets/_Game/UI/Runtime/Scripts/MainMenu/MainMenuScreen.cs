using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    /// <summary>
    /// Main menu controller for MainMenu scene.
    /// Requires: UIDocument with MainMenu.uxml.
    /// </summary>
    public sealed class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;

        private Button _btnNew;
        private Button _btnContinue;
        private Button _btnSettings;
        private Button _btnQuit;

        private Label _lblHint;

        private VisualElement _settingsOverlay;
        private Button _btnSettingsClose;
        private Slider _sldVolume;
        private SliderInt _sldDefaultSpeed;

        private void Awake()
        {
            InputSystemOnlyGuard.EnsureEventSystem_NewInputOnly();

            if (_document == null)
                _document = GetComponent<UIDocument>();

            if (_document == null || _document.rootVisualElement == null)
            {
                Debug.LogError("[MainMenu] UIDocument missing.");
                enabled = false;
                return;
            }

            var root = _document.rootVisualElement;

            _btnNew = root.Q<Button>("BtnNewGame");
            _btnContinue = root.Q<Button>("BtnContinue");
            _btnSettings = root.Q<Button>("BtnSettings");
            _btnQuit = root.Q<Button>("BtnQuit");
            _lblHint = root.Q<Label>("LblHint");

            _settingsOverlay = root.Q<VisualElement>("SettingsOverlay");
            _btnSettingsClose = root.Q<Button>("BtnSettingsClose");
            _sldVolume = root.Q<Slider>("SldVolume");
            _sldDefaultSpeed = root.Q<SliderInt>("SldDefaultSpeed");

            Wire();
            RefreshContinueState();
            HideSettings();
        }

        private void Wire()
        {
            if (_btnNew != null) _btnNew.clicked += OnNewGame;
            if (_btnContinue != null) _btnContinue.clicked += OnContinue;
            if (_btnSettings != null) _btnSettings.clicked += ShowSettings;
            if (_btnQuit != null) _btnQuit.clicked += OnQuit;

            if (_btnSettingsClose != null) _btnSettingsClose.clicked += HideSettings;

            if (_sldVolume != null)
            {
                _sldVolume.value = AppSettings.MasterVolume;
                _sldVolume.RegisterValueChangedCallback(ev => AppSettings.MasterVolume = ev.newValue);
            }

            if (_sldDefaultSpeed != null)
            {
                _sldDefaultSpeed.value = AppSettings.DefaultSpeed;
                _sldDefaultSpeed.RegisterValueChangedCallback(ev => AppSettings.DefaultSpeed = ev.newValue);
            }
        }

        private void RefreshContinueState()
        {
            bool has = HasRunSaveFile();

            if (_btnContinue != null) _btnContinue.SetEnabled(has);
            if (_lblHint != null)
            {
                _lblHint.text = has
                    ? "Continue sẽ load run_save.json (nếu có)."
                    : "Chưa có save. Hãy bấm New Game để bắt đầu.";
            }
        }

        private void OnNewGame()
        {
            int seed = MakeSeed();
            GameAppController.Instance.RequestNewGame(seed, wipeExistingSave: true);
        }

        private void OnContinue()
        {
            if (!HasRunSaveFile())
            {
                RefreshContinueState();
                return;
            }

            GameAppController.Instance.RequestContinue();
        }

        private void OnQuit()
        {
            GameAppController.Instance.Quit();
        }

        private void ShowSettings()
        {
            if (_settingsOverlay != null) _settingsOverlay.style.display = DisplayStyle.Flex;
        }

        private void HideSettings()
        {
            if (_settingsOverlay != null) _settingsOverlay.style.display = DisplayStyle.None;
        }

        private static bool HasRunSaveFile()
        {
            var p = Path.Combine(Application.persistentDataPath, "run_save.json");
            return File.Exists(p);
        }

        private static int MakeSeed()
        {
            unchecked
            {
                long t = DateTime.UtcNow.Ticks;
                return (int)(t ^ (t >> 32));
            }
        }
    }
}
