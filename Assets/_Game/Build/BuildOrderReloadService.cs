using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class BuildOrderReloadService
    {
        private readonly GameServices _s;
        private readonly Dictionary<int, BuildOrder> _orders;
        private readonly List<int> _active;
        private readonly Dictionary<int, List<JobId>> _deliverJobsBySite;
        private readonly Dictionary<int, JobId> _workJobBySite;
        private readonly Dictionary<int, CellPos> _autoRoadByOrder;
        private readonly Dictionary<int, JobId> _repairJobByOrder;
        private readonly Action _ensureBusSubscribed;
        private readonly Action _resetRuntimeTracking;
        private readonly Func<int> _allocateOrderId;

        public BuildOrderReloadService(
            GameServices s,
            Dictionary<int, BuildOrder> orders,
            List<int> active,
            Dictionary<int, List<JobId>> deliverJobsBySite,
            Dictionary<int, JobId> workJobBySite,
            Dictionary<int, CellPos> autoRoadByOrder,
            Dictionary<int, JobId> repairJobByOrder,
            Action ensureBusSubscribed,
            Action resetRuntimeTracking,
            Func<int> allocateOrderId)
        {
            _s = s;
            _orders = orders;
            _active = active;
            _deliverJobsBySite = deliverJobsBySite;
            _workJobBySite = workJobBySite;
            _autoRoadByOrder = autoRoadByOrder;
            _repairJobByOrder = repairJobByOrder;
            _ensureBusSubscribed = ensureBusSubscribed;
            _resetRuntimeTracking = resetRuntimeTracking;
            _allocateOrderId = allocateOrderId;
        }

        public int RebuildActivePlaceOrdersFromSitesAfterLoad()
        {
            _ensureBusSubscribed?.Invoke();
            _resetRuntimeTracking?.Invoke();

            if (_s?.WorldState?.Sites == null || _s.WorldState.Buildings == null)
                return 0;

            var placeholderByKey = new Dictionary<long, BuildingId>(128);

            foreach (var bid in _s.WorldState.Buildings.Ids)
            {
                if (!_s.WorldState.Buildings.Exists(bid)) continue;
                var b = _s.WorldState.Buildings.Get(bid);
                if (b.IsConstructed) continue;

                long k = Pack(b.Anchor.X, b.Anchor.Y, b.DefId);
                if (!placeholderByKey.TryGetValue(k, out var old) || bid.Value < old.Value)
                    placeholderByKey[k] = bid;
            }

            var siteIds = new List<SiteId>(128);
            foreach (var sid in _s.WorldState.Sites.Ids) siteIds.Add(sid);
            siteIds.Sort((a, b) => a.Value.CompareTo(b.Value));

            int created = 0;

            for (int i = 0; i < siteIds.Count; i++)
            {
                var sid = siteIds[i];
                if (!_s.WorldState.Sites.Exists(sid)) continue;

                var site = _s.WorldState.Sites.Get(sid);
                if (!site.IsActive) continue;

                if (site.Kind == 0)
                {
                    long key = Pack(site.Anchor.X, site.Anchor.Y, site.BuildingDefId);

                    if (!placeholderByKey.TryGetValue(key, out var buildingId) || buildingId.Value == 0 || !_s.WorldState.Buildings.Exists(buildingId))
                    {
                        _s.NotificationService?.Push(
                            key: $"LoadMissingPlaceholder_{sid.Value}",
                            title: "Load Warning",
                            body: $"Missing placeholder for site #{sid.Value}: {site.BuildingDefId} @ ({site.Anchor.X},{site.Anchor.Y})",
                            severity: NotificationSeverity.Warning,
                            payload: new NotificationPayload(default, default, "load"),
                            cooldownSeconds: 0f,
                            dedupeByKey: true
                        );
                        continue;
                    }

                    int orderId = _allocateOrderId();
                    var order = new BuildOrder
                    {
                        OrderId = orderId,
                        Kind = BuildOrderKind.PlaceNew,
                        BuildingDefId = site.BuildingDefId,
                        TargetBuilding = buildingId,
                        Site = sid,
                        RequiredCost = default,
                        Delivered = default,
                        WorkSecondsRequired = site.WorkSecondsTotal,
                        WorkSecondsDone = site.WorkSecondsDone,
                        Completed = false
                    };

                    _orders[orderId] = order;
                    _active.Add(orderId);
                    created++;
                }
                else
                {
                    var buildingId = site.TargetBuilding;
                    if (!_s.WorldState.Buildings.Exists(buildingId)) continue;

                    int orderId = _allocateOrderId();
                    var order = new BuildOrder
                    {
                        OrderId = orderId,
                        Kind = BuildOrderKind.Upgrade,
                        BuildingDefId = site.BuildingDefId,
                        TargetBuilding = buildingId,
                        Site = sid,
                        WorkSecondsRequired = site.WorkSecondsTotal,
                        WorkSecondsDone = site.WorkSecondsDone,
                        Completed = false
                    };

                    _orders[orderId] = order;
                    _active.Add(orderId);
                    created++;
                    continue;
                }
            }

            return created;
        }

        private static long Pack(int x, int y, string defId)
        {
            unchecked
            {
                long h = 1469598103934665603L;
                h = (h ^ x) * 1099511628211L;
                h = (h ^ y) * 1099511628211L;
                if (!string.IsNullOrEmpty(defId))
                {
                    for (int i = 0; i < defId.Length; i++)
                        h = (h ^ defId[i]) * 1099511628211L;
                }
                return h;
            }
        }
    }
}
