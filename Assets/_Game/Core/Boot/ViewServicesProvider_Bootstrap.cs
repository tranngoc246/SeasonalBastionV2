using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    /// <summary>
    /// Lives in Boot/runtime assembly -> can access GameBootstrap + GameServices.
    /// Exposes Contracts-only IViewServices to View2D.
    /// </summary>
    public sealed class ViewServicesProvider_Bootstrap : MonoBehaviour, IViewServicesProvider, IViewServices
    {
        [SerializeField] private GameBootstrap _bootstrap;

        private GameServices S => _bootstrap != null ? _bootstrap.Services : null;

        private void Awake()
        {
            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
        }

        // Provider
        public IViewServices GetViewServices() => this;

        // IViewServices (Contracts only)
        public IEventBus EventBus => S?.EventBus;
        public IGridMap GridMap => S?.GridMap;
        public IWorldState WorldState => S?.WorldState;
        public IWorldIndex WorldIndex => S?.WorldIndex;
        public IRunClock RunClock => S?.RunClock;
        public IDataRegistry DataRegistry => S?.DataRegistry;
    }
}