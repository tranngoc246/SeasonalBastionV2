using System;
using System.Collections;
using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    public sealed class UiRoot : MonoBehaviour
    {
        [Header("Wiring (recommended)")]
        [SerializeField] private GameBootstrap _bootstrap;

        [Header("UIDocuments")]
        [SerializeField] private UIDocument _hudDocument;
        [SerializeField] private UIDocument _panelsDocument;
        [SerializeField] private UIDocument _modalsDocument;

        [Header("Templates")]
        [SerializeField] private VisualTreeAsset _notificationItemTemplate;

        [Header("Binding")]
        [SerializeField] private float _bindRetrySeconds = 2f;

        private GameServices _s;

        private HudPresenter _hudPresenter;
        private NotificationStackPresenter _notiPresenter;
        private ResourceBarPresenter _resPresenter;

        private WorldSelectionController _selection;
        private InspectPanelPresenter _inspectPresenter;

        private ModalsPresenter _modalsPresenter;

        private Coroutine _bindCo;
        private bool _bound;

        private void Awake()
        {
            InputSystemOnlyGuard.EnsureEventSystem_NewInputOnly();

            if (_hudDocument == null)
                _hudDocument = GetComponent<UIDocument>();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnEnable()
        {
            TryBindOrRetry();
        }

        private void OnDisable()
        {
            StopRetry();
            UnbindInternal();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            StopRetry();
            UnbindInternal();
        }

        private void Update()
        {
            _inspectPresenter?.Tick(Time.unscaledDeltaTime);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_bound)
                TryBindOrRetry();
        }

        private void StopRetry()
        {
            if (_bindCo != null)
            {
                StopCoroutine(_bindCo);
                _bindCo = null;
            }
        }

        private void TryBindOrRetry()
        {
            if (_bound) return;

            if (TryBind())
            {
                StopRetry();
                return;
            }

            if (_bindCo == null && _bindRetrySeconds > 0f)
                _bindCo = StartCoroutine(BindRetryCo());
        }

        private IEnumerator BindRetryCo()
        {
            float t = 0f;

            while (!_bound && t < _bindRetrySeconds)
            {
                yield return null;

                if (_bound) break;

                t += Time.unscaledDeltaTime;

                if (TryBind())
                    break;
            }

            _bindCo = null;

            if (!_bound)
            {
                Debug.LogError(
                    "[UiRoot] Still cannot bind UI after retry. " +
                    "Ensure GameBootstrap is active and UIDocuments are assigned/active."
                );
            }
        }

        private bool TryBind()
        {
            if (_bound) return true;

            var boot = ResolveBootstrap();
            if (boot == null) return false;

            var services = boot.Services;
            if (services == null) return false;

            // Validate documents BEFORE committing _s/_bound
            if (_hudDocument == null) return false;
            var hudRoot = _hudDocument.rootVisualElement;
            if (hudRoot == null) return false;

            if (_panelsDocument == null) return false;
            var panelsRoot = _panelsDocument.rootVisualElement;
            if (panelsRoot == null) return false;

            if (_modalsDocument == null) return false;
            var modalsRoot = _modalsDocument.rootVisualElement;
            if (modalsRoot == null) return false;

            _s = services;

            PreparePanelsLayer(panelsRoot);
            PrepareModalsLayer(modalsRoot);

            _hudPresenter = new HudPresenter(hudRoot, _s);
            _notiPresenter = new NotificationStackPresenter(hudRoot, _s, _notificationItemTemplate);
            _resPresenter = new ResourceBarPresenter(hudRoot, _s, 0.33f);

            _hudPresenter.Bind();
            _notiPresenter.Bind();
            _resPresenter.Bind();

            EnsureSelectionController();
            _selection.Bind(_s);

            _inspectPresenter = new InspectPanelPresenter(panelsRoot, _s, _selection, 0.33f);
            _inspectPresenter.Bind();

            // Modals: Pause/Settings + RunEnd
            _modalsPresenter = new ModalsPresenter(hudRoot, modalsRoot, _s);
            _modalsPresenter.Bind();

            _bound = true;
            Debug.Log("[UiRoot] Bound to GameServices successfully.");
            return true;
        }

        private void PreparePanelsLayer(VisualElement panelsRoot)
        {
            panelsRoot.pickingMode = PickingMode.Ignore;
            panelsRoot.style.display = DisplayStyle.None;
        }

        private void PrepareModalsLayer(VisualElement modalsRoot)
        {
            // Hidden by default. When opened, presenter will set picking + display.
            modalsRoot.pickingMode = PickingMode.Ignore;
            modalsRoot.style.display = DisplayStyle.None;
        }

        private void EnsureSelectionController()
        {
            if (_selection != null) return;

            _selection = gameObject.GetComponent<WorldSelectionController>();
            if (_selection == null)
                _selection = gameObject.AddComponent<WorldSelectionController>();
        }

        private void UnbindInternal()
        {
            _hudPresenter?.Unbind();
            _notiPresenter?.Unbind();
            _resPresenter?.Unbind();
            _inspectPresenter?.Unbind();
            _modalsPresenter?.Unbind();

            _hudPresenter = null;
            _notiPresenter = null;
            _resPresenter = null;
            _inspectPresenter = null;
            _modalsPresenter = null;
            _selection = null;

            _s = null;
            _bound = false;
        }

        private GameBootstrap ResolveBootstrap()
        {
            if (_bootstrap != null)
                return _bootstrap;

#if UNITY_2023_1_OR_NEWER
            _bootstrap = FindAnyObjectByType<GameBootstrap>();
#else
            _bootstrap = FindObjectOfType<GameBootstrap>();
#endif
            if (_bootstrap != null)
                return _bootstrap;

            var all = Resources.FindObjectsOfTypeAll<GameBootstrap>();
            if (all != null && all.Length > 0)
            {
                _bootstrap = all[0];
                return _bootstrap;
            }

            return null;
        }
    }
}
