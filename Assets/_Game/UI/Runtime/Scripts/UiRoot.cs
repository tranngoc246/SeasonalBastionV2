using System;
using System.Collections;
using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    /// <summary>
    /// UI composition root for runtime HUD (UI Toolkit).
    /// - Supports additive scenes (UI scene loads before GameBootstrap).
    /// - Supports inactive GameBootstrap (FindObjectsOfTypeAll).
    /// - Allows explicit reference in Inspector (recommended).
    /// </summary>
    public sealed class UiRoot : MonoBehaviour
    {
        [Header("Wiring (recommended)")]
        [SerializeField] private GameBootstrap _bootstrap; // Drag in inspector if possible

        [Header("UIDocuments")]
        [SerializeField] private UIDocument _hudDocument;

        [Header("Templates")]
        [SerializeField] private VisualTreeAsset _notificationItemTemplate;

        [Header("Binding")]
        [SerializeField] private float _bindRetrySeconds = 2f;

        private GameServices _s;

        private HudPresenter _hudPresenter;
        private NotificationStackPresenter _notiPresenter;

        private Coroutine _bindCo;

        private void Awake()
        {
            EnsureEventSystem();

            if (_hudDocument == null)
                _hudDocument = GetComponent<UIDocument>();

            // SceneLoaded hook để hỗ trợ UI scene load trước, bootstrap load sau
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnEnable()
        {
            // Try bind immediately
            TryBindOrRetry();
        }

        private void OnDisable()
        {
            if (_bindCo != null)
            {
                StopCoroutine(_bindCo);
                _bindCo = null;
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            UnbindInternal();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Nếu lúc trước chưa bind được, thử lại khi scene mới load
            if (_s == null)
                TryBindOrRetry();
        }

        private void TryBindOrRetry()
        {
            if (_s != null) return;

            if (TryBind())
                return;

            if (_bindCo == null && _bindRetrySeconds > 0f)
                _bindCo = StartCoroutine(BindRetryCo());
        }

        private IEnumerator BindRetryCo()
        {
            float t = 0f;
            while (_s == null && t < _bindRetrySeconds)
            {
                // đợi 1 frame để GameBootstrap.Awake chạy nếu nó ở cùng scene nhưng chạy sau
                yield return null;
                t += Time.unscaledDeltaTime;

                if (TryBind())
                    break;
            }

            _bindCo = null;

            if (_s == null)
            {
                Debug.LogError("[UiRoot] Still cannot find GameBootstrap/GameServices after retry. " +
                               "Ensure a GameBootstrap exists AND is active (or assign it in UiRoot inspector).");
            }
        }

        private bool TryBind()
        {
            if (_hudDocument == null || _hudDocument.rootVisualElement == null)
            {
                Debug.LogError("[UiRoot] UIDocument/rootVisualElement missing.");
                return false;
            }

            var boot = ResolveBootstrap();
            if (boot == null)
                return false;

            var services = boot.Services;
            if (services == null)
            {
                // Có thể boot tồn tại nhưng chưa Awake/khởi tạo services xong.
                return false;
            }

            _s = services;

            // Create presenters once
            var root = _hudDocument.rootVisualElement;

            _hudPresenter = new HudPresenter(root, _s);
            _notiPresenter = new NotificationStackPresenter(root, _s, _notificationItemTemplate);

            _hudPresenter.Bind();
            _notiPresenter.Bind();

            Debug.Log("[UiRoot] Bound to GameServices successfully.");
            return true;
        }

        private void UnbindInternal()
        {
            _hudPresenter?.Unbind();
            _notiPresenter?.Unbind();

            _hudPresenter = null;
            _notiPresenter = null;
            _s = null;
        }

        private GameBootstrap ResolveBootstrap()
        {
            if (_bootstrap != null)
                return _bootstrap;

            // 1) Fast path: active objects
#if UNITY_2023_1_OR_NEWER
            _bootstrap = FindAnyObjectByType<GameBootstrap>();
#else
            _bootstrap = FindObjectOfType<GameBootstrap>();
#endif
            if (_bootstrap != null)
                return _bootstrap;

            // 2) Include inactive objects (khi GameBootstrap bị disable)
            var all = Resources.FindObjectsOfTypeAll<GameBootstrap>();
            if (all != null && all.Length > 0)
            {
                _bootstrap = all[0];
                return _bootstrap;
            }

            return null;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var es = new GameObject("EventSystem");
            DontDestroyOnLoad(es);

            es.AddComponent<EventSystem>();

            // Prefer new Input System UI module if present, else fallback.
            var t = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (t != null) es.AddComponent(t);
            else es.AddComponent<StandaloneInputModule>();
        }
    }
}
