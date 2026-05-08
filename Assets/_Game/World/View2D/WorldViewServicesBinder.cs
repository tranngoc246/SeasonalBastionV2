using SeasonalBastion.Contracts;
using SeasonalBastion.UI.Services;
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

            var all = Object.FindObjectsOfType<MonoBehaviour>();
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

            object services = UiServicesProviderUtil.TryGetServicesFrom(source);
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
