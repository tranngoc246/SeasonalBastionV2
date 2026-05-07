using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class BuildOrderEventBridge
    {
        private readonly IEventBus _eventBus;
        private readonly Dictionary<int, CellPos> _autoRoadByOrder;
        private bool _busSubscribed;

        public BuildOrderEventBridge(IEventBus eventBus, Dictionary<int, CellPos> autoRoadByOrder)
        {
            _eventBus = eventBus;
            _autoRoadByOrder = autoRoadByOrder;
        }

        public void EnsureSubscribed()
        {
            if (_busSubscribed || _eventBus == null) return;

            _eventBus.Subscribe<BuildOrderAutoRoadCreatedEvent>(OnAutoRoadCreated);
            _busSubscribed = true;
        }

        public void Unsubscribe()
        {
            if (!_busSubscribed || _eventBus == null) return;
            _eventBus.Unsubscribe<BuildOrderAutoRoadCreatedEvent>(OnAutoRoadCreated);
            _busSubscribed = false;
        }

        private void OnAutoRoadCreated(BuildOrderAutoRoadCreatedEvent e)
        {
            if (e.OrderId <= 0) return;
            _autoRoadByOrder[e.OrderId] = e.RoadCell;
        }
    }
}
