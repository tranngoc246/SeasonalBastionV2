using SeasonalBastion.Contracts;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SeasonalBastion
{
    public sealed class SeasonSummaryOverlay : MonoBehaviour
    {
        private GameBootstrap _boot;
        private GameServices _s;

        private Canvas _canvas;
        private GameObject _root;
        private TextMeshProUGUI _header;
        private TextMeshProUGUI _lines;
        private Button _dismissBtn;

        private bool _isOpen;
        private float _prevTimeScale = 1f;

        // anti-spam: show once per (year+season)
        private int _lastShownSeasonKey = -1;

        private void Awake()
        {
            _boot = GetComponent<GameBootstrap>();
            if (_boot == null) _boot = FindObjectOfType<GameBootstrap>();
            _s = _boot != null ? _boot.Services : null;

            EnsureEventSystem();
            BuildUI();

            if (_s?.EventBus != null)
            {
                _s.EventBus.Subscribe<EndSeasonRewardRequested>(OnEndSeasonRewardRequested);
                _s.EventBus.Subscribe<DayStartedEvent>(OnDayStarted); // để clear state khi new run / season change
            }

            HideImmediate();
        }

        private void OnDestroy()
        {
            if (_s?.EventBus != null)
            {
                _s.EventBus.Unsubscribe<EndSeasonRewardRequested>(OnEndSeasonRewardRequested);
                _s.EventBus.Unsubscribe<DayStartedEvent>(OnDayStarted);
            }
        }

        private void OnDayStarted(DayStartedEvent ev)
        {
            // Nếu new run quay về Spring Day1 Year1, hoặc season mới bắt đầu,
            // overlay không tự hiện; chỉ reset guard theo season key mới (để show được ở cuối season).
            int key = (ev.YearIndex * 10) + (int)ev.Season;
            if (ev.DayIndex == 1 && key != _lastShownSeasonKey)
            {
                // không làm gì thêm; guard sẽ set lại khi show
            }

            // Nếu đang mở mà start new run/force day, đóng luôn để tránh kẹt pause
            if (_isOpen && ev.DayIndex == 1)
                Dismiss();
        }

        private void OnEndSeasonRewardRequested(EndSeasonRewardRequested ev)
        {
            if (_s == null || _s.SeasonMetrics == null) return;

            int seasonKey = (ev.YearIndex * 10) + (int)ev.Season;

            if (_isOpen) return;
            if (_lastShownSeasonKey == seasonKey) return; // không spam

            _lastShownSeasonKey = seasonKey;

            var snap = _s.SeasonMetrics.GetSnapshot();
            Show(snap);
        }

        private void Show(in SeasonMetricsSnapshot snap)
        {
            _isOpen = true;

            // pause game
            if (_s?.RunClock != null)
            {
                _prevTimeScale = _s.RunClock.TimeScale;
                _s.RunClock.SetTimeScale(0f);
            }

            _header.text = $"SEASON SUMMARY — {snap.Season} (Year {snap.YearIndex})";
            _lines.text = BuildLines(snap);

            _root.SetActive(true);
        }

        private void Dismiss()
        {
            if (!_isOpen) return;
            _isOpen = false;

            _root.SetActive(false);

            // resume
            if (_s?.RunClock != null)
                _s.RunClock.SetTimeScale(_prevTimeScale <= 0f ? 1f : _prevTimeScale);
        }

        private void HideImmediate()
        {
            _isOpen = false;
            if (_root != null) _root.SetActive(false);
        }

        private void BuildUI()
        {
            // Canvas
            var go = new GameObject("SeasonSummaryCanvas");
            DontDestroyOnLoad(go);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<GraphicRaycaster>();

            // Root overlay
            _root = new GameObject("SeasonSummaryRoot");
            _root.transform.SetParent(go.transform, false);

            var bg = _root.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.65f);

            var rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_root.transform, false);

            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(720f, 360f);
            prt.anchoredPosition = Vector2.zero;

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 24, 24);
            vlg.spacing = 12f;
            vlg.childAlignment = TextAnchor.UpperLeft;

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Header TMP
            _header = CreateTMP(panel.transform, "Header", 28, FontStyles.Bold);
            _header.text = "SEASON SUMMARY";
            _header.alignment = TextAlignmentOptions.Left;

            // Lines TMP
            _lines = CreateTMP(panel.transform, "Lines", 22, FontStyles.Normal);
            _lines.text = "";
            _lines.alignment = TextAlignmentOptions.Left;
            _lines.enableWordWrapping = true;

            // Dismiss button
            _dismissBtn = CreateButton(panel.transform, "DismissButton", "Dismiss");
            _dismissBtn.onClick.AddListener(Dismiss);
        }

        private static TextMeshProUGUI CreateTMP(Transform parent, string name, int fontSize, FontStyles style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = Color.white;

            // Use TMP default font if available
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;

            var rt = tmp.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 0f);

            return tmp;
        }

        private static Button CreateButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.22f, 0.22f, 0.22f, 1f);

            var btn = go.AddComponent<Button>();

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(160f, 44f);

            var txt = CreateTMP(go.transform, "Label", 22, FontStyles.Bold);
            txt.text = label;
            txt.alignment = TextAlignmentOptions.Center;

            var trt = txt.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            return btn;
        }

        private static string BuildLines(in SeasonMetricsSnapshot snap)
        {
            int g = Sum(snap.GainedByRes);
            int s = Sum(snap.SpentByRes);

            string gained = FormatRes(snap.GainedByRes);
            string spent = FormatRes(snap.SpentByRes);

            // 4–6 dòng
            return
                $"• Resources gained: +{g}  ({gained})\n" +
                $"• Resources spent:  -{s}  ({spent})\n" +
                $"• Enemies killed:   {snap.EnemiesKilled}\n" +
                $"• Buildings built:  {snap.BuildingsBuilt}\n" +
                $"• Ammo used:        {snap.AmmoUsed}\n";
        }

        private static int Sum(int[] a)
        {
            if (a == null) return 0;
            int t = 0;
            for (int i = 0; i < a.Length; i++) t += a[i];
            return t;
        }

        private static string FormatRes(int[] a)
        {
            if (a == null || a.Length < 5) return "—";
            // theo enum: Wood, Food, Stone, Iron, Ammo
            return $"W:{a[0]} F:{a[1]} S:{a[2]} I:{a[3]} A:{a[4]}";
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var es = new GameObject("EventSystem");
            DontDestroyOnLoad(es);

            es.AddComponent<EventSystem>();

            // Prefer new Input System UI module if present, else fallback
            var t = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (t != null) es.AddComponent(t);
            else es.AddComponent<StandaloneInputModule>();
        }
    }
}
