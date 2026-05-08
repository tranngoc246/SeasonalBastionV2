using System;
using System.Reflection;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion.View2D
{
    internal sealed class WorldViewServicesBinder
    {
        public bool TryBind(MonoBehaviour servicesSource, bool autoFindIfNull, out WorldViewServicesContext context, out MonoBehaviour resolvedSource)
        {
            context = default;
            resolvedSource = servicesSource;

            if (TryBuildContext(servicesSource, out context))
                return true;

            if (!autoFindIfNull)
                return false;

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

        private static bool TryBuildContext(MonoBehaviour source, out WorldViewServicesContext context)
        {
            context = default;
            if (source == null)
                return false;

            object services = TryExtractServicesFromMono(source);
            if (services is not GameServices gameServices)
                return false;

            context = new WorldViewServicesContext(
                gameServices.EventBus,
                gameServices.GridMap,
                gameServices.WorldState,
                gameServices.DataRegistry,
                gameServices.ResourcePatchService,
                gameServices.GetType().FullName ?? "GameServices");
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
                    Debug.LogWarning($"[WorldViewServicesBinder] Failed to read Services property from {type.Name}: {ex}");
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
                    Debug.LogWarning($"[WorldViewServicesBinder] Failed to invoke GetServices on {type.Name}: {ex}");
                }
            }

            return null;
        }
    }

    internal readonly struct WorldViewServicesContext
    {
        public readonly IEventBus EventBus;
        public readonly IGridMap GridMap;
        public readonly IWorldState WorldState;
        public readonly IDataRegistry DataRegistry;
        public readonly ResourcePatchService ResourcePatchService;
        public readonly string ServicesTypeName;

        public WorldViewServicesContext(
            IEventBus eventBus,
            IGridMap gridMap,
            IWorldState worldState,
            IDataRegistry dataRegistry,
            ResourcePatchService resourcePatchService,
            string servicesTypeName)
        {
            EventBus = eventBus;
            GridMap = gridMap;
            WorldState = worldState;
            DataRegistry = dataRegistry;
            ResourcePatchService = resourcePatchService;
            ServicesTypeName = servicesTypeName;
        }
    }
}
