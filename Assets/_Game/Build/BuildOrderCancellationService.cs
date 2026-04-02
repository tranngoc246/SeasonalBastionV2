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

            switch (o.Kind)
            {
                case BuildOrderKind.PlaceNew:
                    CancelPlace(ref o);
                    break;
                case BuildOrderKind.Upgrade:
                    CancelUpgrade(ref o);
                    break;
                case BuildOrderKind.Repair:
                    CancelRepair(ref o);
                    break;
            }

            o.Completed = true;
        }

        public void CancelRepairJob(int orderId)
        {
            if (_repairJobByOrder.TryGetValue(orderId, out var jid))
            {
                _s.JobBoard.Cancel(jid);
                _repairJobByOrder.Remove(orderId);
            }
        }

        private void CancelPlace(ref BuildOrder o)
        {
            _cancelTrackedJobsForSite?.Invoke(o.Site);
            TryRollbackAutoRoad(o.OrderId, o);
            _autoRoadByOrder.Remove(o.OrderId);

            if (TryGetSite(o.Site, out var site))
            {
                RefundDeliveredToNearestStorage(site);
                CleanupBuildSite(o.Site, site);
            }

            RemovePlaceholder(o.TargetBuilding);

            _s.NotificationService?.Push(
                key: $"BuildCancel_{o.TargetBuilding.Value}",
                title: "Construction",
                body: $"Cancelled: {o.BuildingDefId}",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(o.TargetBuilding, default, o.BuildingDefId),
                cooldownSeconds: 0.25f,
                dedupeByKey: true);
        }

        private void CancelUpgrade(ref BuildOrder o)
        {
            _cancelTrackedJobsForSite?.Invoke(o.Site);

            if (TryGetSite(o.Site, out var site))
            {
                RefundDeliveredToNearestStorage(site);
                CleanupBuildSite(o.Site, site);
            }

            _s.NotificationService?.Push(
                key: $"UpgradeCancel_{o.TargetBuilding.Value}",
                title: "Construction",
                body: $"Upgrade cancelled: {o.BuildingDefId}",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(o.TargetBuilding, default, o.BuildingDefId),
                cooldownSeconds: 0.25f,
                dedupeByKey: true);
        }

        private void CancelRepair(ref BuildOrder o)
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

        private void RemovePlaceholder(BuildingId buildingId)
        {
            if (!_destroyPlaceholderOnCancel) return;
            if (buildingId.Value == 0) return;
            if (_s.WorldState?.Buildings == null) return;
            if (!_s.WorldState.Buildings.Exists(buildingId)) return;

            var building = _s.WorldState.Buildings.Get(buildingId);
            if (building.IsConstructed)
                return;

            var def = SafeGetBuildingDef(building.DefId);
            int w = Math.Max(1, def?.SizeX ?? 1);
            int h = Math.Max(1, def?.SizeY ?? 1);
            for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
                _s.GridMap?.ClearBuilding(new CellPos(building.Anchor.X + dx, building.Anchor.Y + dy));

            _s.WorldState.Buildings.Destroy(buildingId);
            try { _s.WorldIndex?.OnBuildingDestroyed(buildingId); } catch { }
            _s.EventBus?.Publish(new WorldStateChangedEvent("Building", buildingId.Value));
            _s.EventBus?.Publish(new RoadsDirtyEvent());
        }

        private void CleanupBuildSite(SiteId siteId, in BuildSiteState site)
        {
            var def = SafeGetBuildingDef(site.BuildingDefId);
            int w = Math.Max(1, def?.SizeX ?? 1);
            int h = Math.Max(1, def?.SizeY ?? 1);

            for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
                _s.GridMap?.ClearSite(new CellPos(site.Anchor.X + dx, site.Anchor.Y + dy));

            if (_s.WorldState.Sites.Exists(siteId))
                _s.WorldState.Sites.Destroy(siteId);

            _s.EventBus?.Publish(new WorldStateChangedEvent("BuildSite", siteId.Value));
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

        private bool TryGetSite(SiteId siteId, out BuildSiteState site)
        {
            site = default;
            if (siteId.Value == 0 || _s.WorldState?.Sites == null || !_s.WorldState.Sites.Exists(siteId))
                return false;

            site = _s.WorldState.Sites.Get(siteId);
            return true;
        }

        private BuildingDef SafeGetBuildingDef(string defId)
        {
            if (_s?.DataRegistry == null || string.IsNullOrWhiteSpace(defId))
                return null;

            try { return _s.DataRegistry.GetBuilding(defId); }
            catch { return null; }
        }

        private static int Manhattan(CellPos a, CellPos b)
        {
            int dx = a.X - b.X; if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
            return dx + dy;
        }
    }
}
