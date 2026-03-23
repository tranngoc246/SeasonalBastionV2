using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class BuildOrderTickProcessor
    {
        private readonly GameServices _s;
        private readonly Dictionary<int, BuildOrder> _orders;
        private readonly List<int> _active;
        private readonly Func<BuildingId> _resolveBuildWorkplace;
        private readonly Action<SiteId, BuildSiteState, BuildingDef, BuildingId> _ensureBuildJobsForSite;
        private readonly Action<SiteId> _cancelTrackedJobsForSite;
        private readonly Action<int, ref BuildOrder, BuildingId> _tickRepairOrder;
        private readonly Action<ref BuildOrder> _completePlaceOrder;
        private readonly Action<ref BuildOrder> _completeUpgradeOrder;
        private readonly Action<int> _onOrderCompleted;

        public BuildOrderTickProcessor(
            GameServices s,
            Dictionary<int, BuildOrder> orders,
            List<int> active,
            Func<BuildingId> resolveBuildWorkplace,
            Action<SiteId, BuildSiteState, BuildingDef, BuildingId> ensureBuildJobsForSite,
            Action<SiteId> cancelTrackedJobsForSite,
            TickRepairOrderDelegate tickRepairOrder,
            CompleteOrderDelegate completePlaceOrder,
            CompleteOrderDelegate completeUpgradeOrder,
            Action<int> onOrderCompleted)
        {
            _s = s;
            _orders = orders;
            _active = active;
            _resolveBuildWorkplace = resolveBuildWorkplace;
            _ensureBuildJobsForSite = ensureBuildJobsForSite;
            _cancelTrackedJobsForSite = cancelTrackedJobsForSite;
            _tickRepairOrder = tickRepairOrder;
            _completePlaceOrder = completePlaceOrder;
            _completeUpgradeOrder = completeUpgradeOrder;
            _onOrderCompleted = onOrderCompleted;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            var workplace = _s.Balance != null ? _s.Balance.ResolveBuilderWorkplace() : _resolveBuildWorkplace();
            if (workplace.Value == 0)
                return;

            for (int i = 0; i < _active.Count; i++)
            {
                int id = _active[i];
                if (!_orders.TryGetValue(id, out var o)) continue;
                if (o.Completed) continue;

                if (o.Kind == BuildOrderKind.Repair)
                {
                    _tickRepairOrder?.Invoke(id, ref o, workplace);
                    _orders[id] = o;
                    continue;
                }

                if (o.Kind != BuildOrderKind.PlaceNew && o.Kind != BuildOrderKind.Upgrade)
                    continue;

                if (!_s.WorldState.Sites.Exists(o.Site))
                {
                    _cancelTrackedJobsForSite?.Invoke(o.Site);
                    o.Completed = true;
                    _orders[id] = o;
                    continue;
                }

                var site = _s.WorldState.Sites.Get(o.Site);
                var def = _s.DataRegistry.GetBuilding(o.BuildingDefId);
                _ensureBuildJobsForSite?.Invoke(o.Site, site, def, workplace);

                o.WorkSecondsDone = site.WorkSecondsDone;
                _orders[id] = o;

                if (IsReadyToWork(site) && site.WorkSecondsDone + 1e-4f >= site.WorkSecondsTotal)
                {
                    _cancelTrackedJobsForSite?.Invoke(o.Site);

                    if (o.Kind == BuildOrderKind.PlaceNew) _completePlaceOrder?.Invoke(ref o);
                    else if (o.Kind == BuildOrderKind.Upgrade) _completeUpgradeOrder?.Invoke(ref o);
                    _orders[id] = o;
                    _onOrderCompleted?.Invoke(id);
                }
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                int id = _active[i];
                if (_orders.TryGetValue(id, out var o) && o.Completed)
                    _active.RemoveAt(i);
            }
        }

        private static bool IsReadyToWork(in BuildSiteState site)
        {
            return site.RemainingCosts == null || site.RemainingCosts.Count == 0;
        }
    }
}
 }
        }

        private static bool IsReadyToWork(in BuildSiteState site)
        {
            return site.RemainingCosts == null || site.RemainingCosts.Count == 0;
        }
    }
}
