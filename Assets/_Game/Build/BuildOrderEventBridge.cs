using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class BuildOrderEventBridge
    {
        private readonly GameServices _s;
        private readonly Dictionary<int, CellPos> _autoRoadByOrder;
        private bool _busSubscribed;

        public BuildOrderEventBridge(GameServices s, Dictionary<int, CellPos> autoRoadByOrder)
        {
            _s = s;
            _autoRoadByOrder = autoRoadByOrder;
        }

        public void EnsureSubscribed()
        {
            if (_busSubscribed) return;
            var bus = _s.EventBus;
            if (bus == null) return;

            bus.Subscribe<BuildOrderAutoRoadCreatedEvent>(OnAutoRoadCreated);
            _busSubscribed = true;
        }

        public void Unsubscribe()
        {
            if (!_busSubscribed || _s.EventBus == null) return;
            _s.EventBus.Unsubscribe<BuildOrderAutoRoadCreatedEvent>(OnAutoRoadCreated);
            _busSubscribed = false;
        }

        private void OnAutoRoadCreated(BuildOrderAutoRoadCreatedEvent e)
        {
            if (e.OrderId <= 0) return;
            _autoRoadByOrder[e.OrderId] = e.RoadCell;
        }
    }
}
