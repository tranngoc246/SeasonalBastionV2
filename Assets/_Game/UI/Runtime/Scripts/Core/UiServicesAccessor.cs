using SeasonalBastion.Contracts;

namespace SeasonalBastion.UI
{
    internal sealed class UiServicesAccessor
    {
        private readonly GameServices _services;

        public UiServicesAccessor(object services)
        {
            _services = services as GameServices;
        }

        public GameServices GameServices => _services;
        public IEventBus EventBus => _services?.EventBus;
    }
}
