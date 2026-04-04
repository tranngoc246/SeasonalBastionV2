using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class RewardSelectionOverlay : MonoBehaviour
    {
        private GameBootstrap _boot;
        private GameServices _s;

        private Canvas _canvas;
        private GameObject _root;
        private TextMeshProUGUI _header;
        private TextMeshProUGUI _subtext;
        private Button[] _choiceButtons;
        private TextMeshProUGUI[] _choiceLabels;

        private bool _isOpen;
        private float _prevTimeScale = 1f;

        private void Awake()
        {
            _boot = GetComponent<GameBootstrap>();
            if (_boot == null) _boot = FindObjectOfType<GameBootstrap>();
            _s = _boot != null ? _boot.Services : null;

            EnsureEventSystem();
            BuildUI();
            BindRewardService();
            HideImmediate();
        }

        private void OnDestroy()
        {
            UnbindRewardService();
        }

        private void BindRewardService()
        {
            if (_s?.RewardService == null) return;

            _s.RewardService.OnSelectionStarted += OnSelectionStarted;
            _s.RewardService.OnSelectionEnded += OnSelectionEnded;
            _s.RewardService.OnRewardChosen += OnRewardChosen;
        }

        private void UnbindRewardService()
        {
            if (_s?.RewardService == null) return;

            _s.RewardService.OnSelectionStarted -= OnSelectionStarted;
            _s.RewardService.OnSelectionEnded -= OnSelectionEnded;
            _s.RewardService.OnRewardChosen -= OnRewardChosen;
        }

        private void OnSelectionStarted()
        {
            if (_s?.RewardService == null)
                return;

            Show(_s.RewardService.CurrentOffer);
        }

        private void OnSelectionEnded()
        {
            Hide();
        }

        private void OnRewardChosen(string rewardId)
        {
            _s?.NotificationService?.Push(
                key: $"reward_picked_{rewardId}",
                title: "Reward chosen",
                body: GetRewardTitle(rewardId),
                severity: NotificationSeverity.Info,
                payload: default,
                cooldownSeconds: 0.25f,
                dedupeByKey: true);
        }

        private void Show(RewardOffer offer)
        {
            _isOpen = true;

            if (_s?.RunClock != null)
            {
                _prevTimeScale = _s.RunClock.TimeScale;
                _s.RunClock.SetTimeScale(0f);
            }

            _header.text = "CHOOSE A REWARD";
            _subtext.text = "Pick 1 of 3. Effect applies immediately.";

            SetChoice(0, offer.A);
            SetChoice(1, offer.B);
            SetChoice(2, offer.C);

            _root.SetActive(true);
        }

        private void Hide()
        {
            if (!_isOpen) return;
            _isOpen = false;

            _root.SetActive(false);

            if (_s?.RunClock != null)
                _s.RunClock.SetTimeScale(_prevTimeScale <= 0f ? 1f : _prevTimeScale);
        }

        private void HideImmediate()
        {
            _isOpen = false;
            if (_root != null) _root.SetActive(false);
        }

        private void SetChoice(int index, string rewardId)
        {
            if (_choiceButtons == null || _choiceLabels == null) return;
            if (index < 0 || index >= _choiceButtons.Length) return;

            _choiceLabels[index].text = BuildRewardLine(rewardId);
            _choiceButtons[index].onClick.RemoveAllListeners();
            int chosenIndex = index;
            _choiceButtons[index].onClick.AddListener(() => _s?.RewardService?.Choose(chosenIndex));
        }

        private string BuildRewardLine(string rewardId)
        {
            return $"{GetRewardTitle(rewardId)}\n<size=18><color=#CFCFCF>{GetRewardDescription(rewardId)}</color></size>";
        }

        private static string GetRewardTitle(string rewardId)
        {
            return rewardId switch
            {
                "Reward_BuildSpeed" => "+Build speed",
                "Reward_AmmoCapacity" => "+Ammo capacity",
                "Reward_TowerReload" => "+Tower reload speed",
                "Reward_NpcMoveSpeed" => "+NPC move speed",
                _ => rewardId ?? "Unknown reward",
            };
        }

        private static string GetRewardDescription(string rewardId)
        {
            return rewardId switch
            {
                "Reward_BuildSpeed" => "Builders work 15% faster this run.",
                "Reward_AmmoCapacity" => "All towers gain +5 ammo capacity.",
                "Reward_TowerReload" => "All towers reload/fire 12% faster.",
                "Reward_NpcMoveSpeed" => "NPCs move 10% faster this run.",
                _ => "Applies immediately.",
            };
        }

        private void BuildUI()
        {
            var go = new GameObject("RewardSelectionCanvas");
            DontDestroyOnLoad(go);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<GraphicRaycaster>();

            _root = new GameObject("RewardSelectionRoot");
            _root.transform.SetParent(go.transform, false);

            var bg = _root.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.72f);

            var rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var panel = new GameObject("Panel");
            panel.transform.SetParent(_root.transform, false);

            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.10f, 0.10f, 0.10f, 0.98f);

            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(860f, 420f);
            prt.anchoredPosition = Vector2.zero;

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 24, 24);
            vlg.spacing = 14f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            _header = CreateTMP(panel.transform, "Header", 30, FontStyles.Bold, TextAlignmentOptions.Center);
            _subtext = CreateTMP(panel.transform, "Subtext", 20, FontStyles.Normal, TextAlignmentOptions.Center);

            _choiceButtons = new Button[3];
            _choiceLabels = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                (_choiceButtons[i], _choiceLabels[i]) = CreateChoiceButton(panel.transform, $"Choice{i}");
            }
        }

        private static TextMeshProUGUI CreateTMP(Transform parent, string name, int fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.enableWordWrapping = true;

            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = fontSize <= 20 ? 34f : 46f;

            return tmp;
        }

        private static (Button button, TextMeshProUGUI label) CreateChoiceButton(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.18f, 0.18f, 1f);

            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            colors.highlightedColor = new Color(0.28f, 0.28f, 0.28f, 1f);
            colors.pressedColor = new Color(0.36f, 0.36f, 0.36f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 96f);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 96f;

            var label = CreateTMP(go.transform, "Label", 24, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(18f, 8f);
            lrt.offsetMax = new Vector2(-18f, -8f);

            return (button, label);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var es = new GameObject("EventSystem");
            DontDestroyOnLoad(es);

            es.AddComponent<EventSystem>();

            var t = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (t != null) es.AddComponent(t);
            else es.AddComponent<StandaloneInputModule>();
        }
    }
}
