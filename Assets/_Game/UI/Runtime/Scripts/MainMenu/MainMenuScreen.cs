using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] private int _seedOverride;
        [SerializeField] private bool _wipeExistingSaveOnNewRun = true;

        private UIDocument _doc;
        private Button _btnNewRun;
        private Button _btnContinue;
        private Button _btnQuit;
        private Label _lblSaveHint;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            InputSystemOnlyGuard.EnsureEventSystem_NewInputOnly();
        }

        private void OnEnable()
        {
            if (_doc == null)
                _doc = GetComponent<UIDocument>();

            var root = _doc != null ? _doc.rootVisualElement : null;
            if (root == null)
                return;

            _btnNewRun = root.Q<Button>("BtnNewRun");
            _btnContinue = root.Q<Button>("BtnContinue");
            _btnQuit = root.Q<Button>("BtnQuit");
            _lblSaveHint = root.Q<Label>("LblSaveHint");

            if (_btnNewRun != null) _btnNewRun.clicked += OnNewRunClicked;
            if (_btnContinue != null) _btnContinue.clicked += OnContinueClicked;
            if (_btnQuit != null) _btnQuit.clicked += OnQuitClicked;

            RefreshState();
        }

        private void OnDisable()
        {
            if (_btnNewRun != null) _btnNewRun.clicked -= OnNewRunClicked;
            if (_btnContinue != null) _btnContinue.clicked -= OnContinueClicked;
            if (_btnQuit != null) _btnQuit.clicked -= OnQuitClicked;
        }

        private void RefreshState()
        {
            bool hasSave = File.Exists(Path.Combine(Application.persistentDataPath, "run_save.json"));

            if (_btnContinue != null)
                _btnContinue.SetEnabled(hasSave);

            if (_lblSaveHint != null)
                _lblSaveHint.text = hasSave
                    ? "Continue is available. A run_save.json was found."
                    : "No run save found yet. Start a New Run first.";

            Debug.Log($"[MainMenu] Bound UI. hasSave={hasSave}, newRunBtn={_btnNewRun != null}, continueBtn={_btnContinue != null}, quitBtn={_btnQuit != null}");
        }

        private void OnNewRunClicked()
        {
            var app = GameAppController.Instance;
            if (app == null)
            {
                Debug.LogError("[MainMenu] GameAppController missing.");
                return;
            }

            app.RequestNewGame(_seedOverride, _wipeExistingSaveOnNewRun);
        }

        private void OnContinueClicked()
        {
            var app = GameAppController.Instance;
            if (app == null)
            {
                Debug.LogError("[MainMenu] GameAppController missing.");
                return;
            }

            app.RequestContinue();
        }

        private void OnQuitClicked()
        {
            var app = GameAppController.Instance;
            if (app != null) app.Quit();
            else Application.Quit();
        }
    }
}
