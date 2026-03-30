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
        [TextArea(2, 5)]
        [SerializeField]
        private string _dragonBackdropText =
            "SEASONAL BASTION STANDS. AUTUMN WINDS RISE. WINTER CLOSES IN. "
            + "THE BASTION ENDURES. TOWERS HOLD. THE FIELD PARTS AROUND THE DRAGON.";

        private UIDocument _doc;
        private Button _btnNewRun;
        private Button _btnContinue;
        private Button _btnQuit;
        private Label _lblSaveHint;

        private VisualElement _root;
        private VisualElement _menuCard;
        private VisualElement _dragonEffectHost;
        private DragonTextLayer _dragonLayer;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            InputSystemOnlyGuard.EnsureEventSystem_NewInputOnly();
        }

        private void OnEnable()
        {
            if (_doc == null)
                _doc = GetComponent<UIDocument>();

            _root = _doc != null ? _doc.rootVisualElement : null;
            if (_root == null)
                return;

            _btnNewRun = _root.Q<Button>("BtnNewRun");
            _btnContinue = _root.Q<Button>("BtnContinue");
            _btnQuit = _root.Q<Button>("BtnQuit");
            _lblSaveHint = _root.Q<Label>("LblSaveHint");
            _menuCard = _root.Q<VisualElement>("MenuCard");
            _dragonEffectHost = _root.Q<VisualElement>("DragonEffectHost");

            EnsureDragonLayer();

            if (_btnNewRun != null) _btnNewRun.clicked += OnNewRunClicked;
            if (_btnContinue != null) _btnContinue.clicked += OnContinueClicked;
            if (_btnQuit != null) _btnQuit.clicked += OnQuitClicked;

            RefreshState();
        }

        private void Update()
        {
            _dragonLayer?.Tick(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            if (_btnNewRun != null) _btnNewRun.clicked -= OnNewRunClicked;
            if (_btnContinue != null) _btnContinue.clicked -= OnContinueClicked;
            if (_btnQuit != null) _btnQuit.clicked -= OnQuitClicked;
        }

        private void EnsureDragonLayer()
        {
            if (_dragonEffectHost == null)
                return;

            if (_dragonLayer == null)
            {
                _dragonLayer = new DragonTextLayer();
                _dragonLayer.StretchToParentSize();
                _dragonEffectHost.Add(_dragonLayer);
            }

            _dragonLayer.SetText(_dragonBackdropText);
            _dragonLayer.SetExclusionElement(_menuCard);
        }

        private void RefreshState()
        {
            bool hasSave = File.Exists(Path.Combine(Application.persistentDataPath, "run_save.json"));

            if (_btnContinue != null)
                _btnContinue.SetEnabled(hasSave);

            if (_lblSaveHint != null)
            {
                _lblSaveHint.text = hasSave
                    ? "Continue is available. A run_save.json was found."
                    : "No run save found yet. Continue is disabled until you save a run.";
            }

            Debug.Log($"[MainMenu] Bound UI. hasSave={hasSave}, newRunBtn={_btnNewRun != null}, continueBtn={_btnContinue != null}, quitBtn={_btnQuit != null}, dragonLayer={_dragonLayer != null}");
        }

        private void OnNewRunClicked()
        {
            GameAppController app = GameAppController.Instance;
            if (app == null)
            {
                Debug.LogError("[MainMenu] GameAppController missing.");
                return;
            }

            app.RequestNewGame(_seedOverride, _wipeExistingSaveOnNewRun);
        }

        private void OnContinueClicked()
        {
            GameAppController app = GameAppController.Instance;
            if (app == null)
            {
                Debug.LogError("[MainMenu] GameAppController missing.");
                return;
            }

            app.RequestContinue();
        }

        private void OnQuitClicked()
        {
            GameAppController app = GameAppController.Instance;
            if (app != null)
                app.Quit();
            else
                Application.Quit();
        }
    }
}
