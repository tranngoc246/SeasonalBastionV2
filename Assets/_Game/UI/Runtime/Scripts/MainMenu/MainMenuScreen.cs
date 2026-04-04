using System;
using System.IO;
using SeasonalBastion.Contracts;
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
        private VisualElement _saveSlotsList;

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
            _saveSlotsList = _root.Q<VisualElement>("SaveSlotsList");
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
            var slots = ReadSaveSlots();
            bool hasSave = slots != null && slots.Count > 0;

            if (_btnContinue != null)
                _btnContinue.SetEnabled(hasSave);

            if (_lblSaveHint != null)
            {
                _lblSaveHint.text = hasSave
                    ? "Continue selects the latest valid save. Choose any listed save below in the future UI flow."
                    : "No valid save found yet. Continue is disabled until you save a run.";
            }

            RenderSaveSlots(slots);

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

        private System.Collections.Generic.IReadOnlyList<SaveSlotInfo> ReadSaveSlots()
        {
            try
            {
                var services = FindObjectOfType<GameBootstrap>()?.Services;
                if (services?.SaveService != null)
                    return services.SaveService.ListRunSaves();
            }
            catch { }

            var result = new System.Collections.Generic.List<SaveSlotInfo>();
            for (int i = 1; i <= 3; i++)
            {
                string p = Path.Combine(Application.persistentDataPath, $"save_{i}.json");
                if (File.Exists(p)) result.Add(new SaveSlotInfo { Slot = i, DisplayName = $"Save Slot {i}", FileName = Path.GetFileName(p), IsValid = true });
            }
            string ap = Path.Combine(Application.persistentDataPath, "save_autosave.json");
            if (File.Exists(ap)) result.Add(new SaveSlotInfo { Slot = 0, DisplayName = "Autosave", FileName = Path.GetFileName(ap), IsAutosave = true, IsValid = true });
            string lp = Path.Combine(Application.persistentDataPath, "run_save.json");
            if (File.Exists(lp)) result.Add(new SaveSlotInfo { Slot = 0, DisplayName = "Legacy Continue", FileName = Path.GetFileName(lp), IsLegacy = true, IsValid = true });
            return result;
        }

        private void RenderSaveSlots(System.Collections.Generic.IReadOnlyList<SaveSlotInfo> slots)
        {
            if (_saveSlotsList == null) return;
            _saveSlotsList.Clear();
            if (slots == null) return;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var item = new VisualElement();
                item.AddToClassList("save-slot-item");

                var title = new Label(slot.DisplayName ?? slot.FileName ?? "Save");
                title.AddToClassList("save-slot-title");
                item.Add(title);

                string ts = slot.TimestampUtc;
                if (DateTime.TryParse(slot.TimestampUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                    ts = dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

                var meta = new Label(slot.IsValid
                    ? $"Y{slot.YearIndex} • {slot.Season} D{slot.DayIndex} • Wave {slot.WaveIndex} • {ts}"
                    : $"Invalid save • {slot.Error}");
                meta.AddToClassList("save-slot-meta");
                item.Add(meta);

                _saveSlotsList.Add(item);
            }
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
