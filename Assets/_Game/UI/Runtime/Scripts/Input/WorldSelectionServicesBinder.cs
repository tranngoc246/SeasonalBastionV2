using SeasonalBastion.UI.Services;
using UnityEngine;

namespace SeasonalBastion.UI.Input
{
    internal sealed class WorldSelectionServicesBinder
    {
        public bool TryResolve(UiSystem uiSystem, MonoBehaviour servicesProvider, out GameServices services)
        {
            services = null;

            object servicesObj = uiSystem?.Ctx?.Services;
            if (servicesObj == null)
                servicesObj = UiServicesProviderUtil.TryGetServicesFrom(servicesProvider);

            services = servicesObj as GameServices;
            return services != null;
        }
    }
}
