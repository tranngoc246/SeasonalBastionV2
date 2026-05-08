using System;
using System.Reflection;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    internal sealed class PlacementServicesBinder
    {
        public bool TryBind(MonoBehaviour servicesSource, out PlacementServicesContext context, out MonoBehaviour resolvedSource)
        {
            context = default;
            resolvedSource = servicesSource;

            if (TryBuildContext(servicesSource, out context))
                return true;

            var all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude);
            for (int i = 0; i < all.Length; i++)
            {
                var mb = all[i];
                if (mb == null)
                    continue;

                if (!TryBuildContext(mb, out context))
                    continue;

                resolvedSource = mb;
                return true;
            }

            return false;
        }

        private static bool TryBuildContext(MonoBehaviour source, out PlacementServicesContext context)
        {
            context = default;
            if (source == null)
                return false;

            object services = TryExtractServicesFromMono(source);
            if (services is not GameServices gameServices)
                return false;

            if (gameServices.EventBus == null || gameServices.PlacementService == null || gameServices.GridMap == null)
                return false;

            context = new PlacementServicesContext(
                gameServices.EventBus,
                gameServices.PlacementService,
                gameServices.NotificationService,
                gameServices.GridMap,
                gameServices.DataRegistry,
                gameServices.RunClock);
            return true;
        }

        private static object TryExtractServicesFromMono(MonoBehaviour mb)
        {
            var type = mb.GetType();

            var prop = type.GetProperty("Services", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null)
            {
                try
                {
                    var value = prop.GetValue(mb);
                    if (value != null)
                        return value;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlacementServicesBinder] Failed to read Services property from {type.Name}: {ex}");
                }
            }

            var method = type.GetMethod("GetServices", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null && method.GetParameters().Length == 0)
            {
                try
                {
                    var value = method.Invoke(mb, null);
                    if (value != null)
                        return value;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlacementServicesBinder] Failed to invoke GetServices on {type.Name}: {ex}");
                }
            }

            return null;
        }
    }

    internal readonly struct PlacementServicesContext
    {
        public readonly IEventBus EventBus;
        public readonly IPlacementService PlacementService;
        public readonly INotificationService NotificationService;
        public readonly IGridMap GridMap;
        public readonly IDataRegistry DataRegistry;
        public readonly IRunClock RunClock;

        public PlacementServicesContext(
            IEventBus eventBus,
            IPlacementService placementService,
            INotificationService notificationService,
            IGridMap gridMap,
            IDataRegistry dataRegistry,
            IRunClock runClock)
        {
            EventBus = eventBus;
            PlacementService = placementService;
            NotificationService = notificationService;
            GridMap = gridMap;
            DataRegistry = dataRegistry;
            RunClock = runClock;
        }
    }
}
