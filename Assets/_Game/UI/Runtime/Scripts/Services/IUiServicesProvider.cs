using UnityEngine;

namespace SeasonalBastion.UI.Services
{
    /// <summary>
    /// Adapter để inject GameServices của project thật vào UI.
    /// Bạn có thể tạo 1 MonoBehaviour khác implements interface này.
    /// </summary>
    public interface IUiServicesProvider
    {
        object GetServices();
    }

    /// <summary>
    /// Helper: kéo thả component bất kỳ vào inspector, nếu component implements IUiServicesProvider thì lấy services.
    /// </summary>
    public static class UiServicesProviderUtil
    {
        public static object TryGetServicesFrom(MonoBehaviour mb)
        {
            if (mb == null) return null;
            if (mb is IUiServicesProvider p) return p.GetServices();
            return null;
        }
    }
}