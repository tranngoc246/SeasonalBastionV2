using SeasonalBastion.UI.Services;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI
{
    /// <summary>
    /// Attach vào UIRoot prefab.
    /// - Reference 4 UIDocuments (HUD/Panels/Modals/Overlay)
    /// - Reference PanelSettings chung
    /// - Optional: ServicesProvider để inject GameServices
    /// </summary>
    public sealed class UiBootstrap : MonoBehaviour
    {
        [Header("Documents")]
        [SerializeField] private UIDocument _hud;
        [SerializeField] private UIDocument _panels;
        [SerializeField] private UIDocument _modals;
        [SerializeField] private UIDocument _overlay;

        [Header("Shared PanelSettings (required)")]
        [SerializeField] private PanelSettings _sharedPanelSettings;

        [Header("Optional: Services Provider (MonoBehaviour implementing IUiServicesProvider)")]
        [SerializeField] private MonoBehaviour _servicesProvider;

        [Header("Optional: Pause Controller Adapter (MonoBehaviour implementing IUiPauseController)")]
        [SerializeField] private MonoBehaviour _pauseController;

        private UiSystem _uiSystem;

        private void Awake()
        {
            ApplyPanelSettingsAndSorting();
        }

        private void Start()
        {
            // Create UiSystem runtime (or you can place UiSystem component manually and reference it)
            _uiSystem = GetComponent<UiSystem>();
            if (_uiSystem == null) _uiSystem = gameObject.AddComponent<UiSystem>();

            object services = UiServicesProviderUtil.TryGetServicesFrom(_servicesProvider);
            var pause = _pauseController as IUiPauseController;

            _uiSystem.Initialize(_hud, _panels, _modals, _overlay, services, pause);
        }

        private void ApplyPanelSettingsAndSorting()
        {
            if (_sharedPanelSettings == null) return;

            ApplyDoc(_hud, 0);
            ApplyDoc(_panels, 10);
            ApplyDoc(_modals, 20);
            ApplyDoc(_overlay, 30);
        }

        private void ApplyDoc(UIDocument doc, int sortingOrder)
        {
            if (doc == null) return;
            doc.panelSettings = _sharedPanelSettings;
            doc.sortingOrder = sortingOrder;
        }
    }
}