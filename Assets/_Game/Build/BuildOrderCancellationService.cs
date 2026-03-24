using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class BuildOrderCancellationService
    {
        private readonly GameServices _s;
        private readonly bool _destroyPlaceholderOnCancel;
        private readonly Dictionary<int, CellPos> _autoRoadByOrder;
        private readonly Dictionary<int, JobId> _repairJobByOrder;
        private readonly List<BuildingId> _buildingIdsBuf = new(128);
        private readonly Action<SiteId> _cancelTrackedJobsForSite;

        public BuildOrderCancellationService(
            GameServices s,
            bool destroyPlaceholderOnCancel,
            Dictionary<int, CellPos> autoRoadByOrder,
            Dictionary<int, JobId> repairJobByOrder,
            Action<SiteId> cancelTrackedJobsForSite)
        {
            _s = s;
            _destroyPlaceholderOnCancel = destroyPlaceholderOnCancel;
            _autoRoadByOrder = autoRoadByOrder;
            _repairJobByOrder = repairJobByOrder;
            _cancelTrackedJobsForSite = cancelTrackedJobsForSite;
        }

        public void Cancel(ref BuildOrder o)
        {
            if (o.Completed) return;

            if (o.Kind == BuildOrderKind.PlaceNew)
            {
                _cancelTrackedJobsForSite?.Invoke(o.Site);
                TryRollbackAutoRoad(o.OrderId, o);
                _autoRoadByOrder.Remove(o.OrderId);

                if (_s.WorldState.Sites.Exists(o.Site))
                {
                    var stRefund = _s.WorldState.Sites.Get(o.Site);
                    RefundDeliveredToNearestStorage(stRefund);
                }

                if (_s.WorldState.Sites.Exists(o.Site))
                {
                    var def = _s.DataRegistry.GetBuilding(o.BuildingDefId);
                    int w = Math.Max(1, def.SizeX);
                    int h = Math.Max(1, def.SizeY);

                    var st = _s.WorldState.Sites.Get(o.Site);
                    for (int dy = 0; dy < h; dy++)
                        for (int dx = 0; dx < w; dx++)
                            _s.GridMap.ClearSite(new CellPos(st.Anchor.X + dx, st.Anchor.Y + dy));

                    _s.WorldState.Sites.Destroy(o.Site);
                }

                if (_destroyPlaceholderOnCancel && _s.WorldState.Buildings.Exists(o.TargetBuilding))
                    _s.WorldState.Buildings.Destroy(o.TargetBuilding);

                _s.NotificationService?.Push(
                    key: $"BuildCancel_{o.TargetBuilding.Value}",
                    title: "Construction",
                    body: $"Cancelled: {o.BuildingDefId}",
                    severity: NotificationSeverity.Info,
                    payload: new NotificationPayload(o.TargetBuilding, default, o.BuildingDefId),
                    cooldownSeconds: 0.25f,
                    dedupeByKey: true);
            }
            else if (o.Kind == BuildOrderKind.Repair)
            {
                CancelRepairJob(o.OrderId);

                _s.NotificationService?.Push(
                    key: $"RepairCancel_{o.TargetBuilding.Value}",
                    title: "Construction",
                    body: $"Repair cancelled: {o.BuildingDefId}",
                    severity: NotificationSeverity.Info,
                    payload: new NotificationPayload(o.TargetBuilding, default, o.BuildingDefId),
                    cooldownSeconds: 0.25f,
                    dedupeByKey: true);
            }
        }

        public void CancelRepairJob(int orderId)
        {
            if (_repairJobByOrder.TryGetValue(orderId, out var jid))
            {
                _s.JobBoard.Cancel(jid);
                _repairJobByOrder.Remove(orderId);
            }
        }

        private void TryRollbackAutoRoad(int orderId, in BuildOrder o)
        {
            if (_s.GridMap == null) return;
            if (!_autoRoadByOrder.TryGetValue(orderId, out var c)) return;
            if (!_s.GridMap.IsInside(c)) return;

            var occ = _s.GridMap.Get(c);
            if (occ.Kind == CellOccupancyKind.Site || occ.Kind == CellOccupancyKind.Building)
                return;

            if (_s.GridMap.IsRoad(c))
            {
                _s.GridMap.SetRoad(c, false);
                _s.EventBus?.Publish(new RoadsDirtyEvent());
            }
        }

        private void RefundDeliveredToNearestStorage(in BuildSiteState st)
        {
            if (_s.WorldState == null || _s.StorageService == null || _s.WorldIndex == null) return;
            if (st.DeliveredSoFar == null || st.DeliveredSoFar.Count == 0) return;

            var whs = _s.WorldIndex.Warehouses;
            if (whs == null || whs.Count == 0) return;

            _buildingIdsBuf.Clear();
            for (int i = 0; i < whs.Count; i++)
            {
                var bid = whs[i];
                if (bid.Value == 0) continue;
                if (!_s.WorldState.Buildings.Exists(bid)) continue;

                var bs = _s.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;
                _buildingIdsBuf.Add(bid);
            }

            if (_buildingIdsBuf.Count == 0) return;

            var from = st.Anchor;
            _buildingIdsBuf.Sort((a, b) =>
            {
                var aa = _s.WorldState.Buildings.Get(a).Anchor;
                var bb = _s.WorldState.Buildings.Get(b).Anchor;
                int da = Manhattan(from, aa);
                int db = Manhattan(from, bb);
                if (da != db) return da.CompareTo(db);
                return a.Value.CompareTo(b.Value);
            });

            for (int i = 0; i < st.DeliveredSoFar.Count; i++)
            {
                var c = st.DeliveredSoFar[i];
                if (c.Amount <= 0) continue;

                int left = c.Amount;
                var rt = c.Resource;

                for (int k = 0; k < _buildingIdsBuf.Count && left > 0; k++)
                {
                    var dst = _buildingIdsBuf[k];
                    if (!_s.StorageService.CanStore(dst, rt)) continue;
                    int added = _s.StorageService.Add(dst, rt, left);
                    left -= added;
                }
            }
        }

        private static int Manhattan(CellPos a, CellPos b)
        {
            int dx = a.X - b.X; if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
            return dx + dy;
        }
    }
}
