using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class BuildOrderCancellationService
    {
        private readonly IWorldState _worldState;
        private readonly IGridMap _gridMap;
        private readonly IWorldIndex _worldIndex;
        private readonly IStorageService _storageService;
        private readonly IDataRegistry _dataRegistry;
        private readonly IEventBus _eventBus;
        private readonly INotificationService _notificationService;
        private readonly IJobBoard _jobBoard;
        private readonly bool _destroyPlaceholderOnCancel;
        private readonly Dictionary<int, CellPos> _autoRoadByOrder;
        private readonly Dictionary<int, JobId> _repairJobByOrder;
        private readonly List<BuildingId> _buildingIdsBuf = new(128);
        private readonly Action<SiteId> _cancelTrackedJobsForSite;

        public BuildOrderCancellationService(
            IWorldState worldState,
            IGridMap gridMap,
            IWorldIndex worldIndex,
            IStorageService storageService,
            IDataRegistry dataRegistry,
            IEventBus eventBus,
            INotificationService notificationService,
            IJobBoard jobBoard,
            bool destroyPlaceholderOnCancel,
            Dictionary<int, CellPos> autoRoadByOrder,
            Dictionary<int, JobId> repairJobByOrder,
            Action<SiteId> cancelTrackedJobsForSite)
        {
            _worldState = worldState;
            _gridMap = gridMap;
            _worldIndex = worldIndex;
            _storageService = storageService;
            _dataRegistry = dataRegistry;
            _eventBus = eventBus;
            _notificationService = notificationService;
            _jobBoard = jobBoard;
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
            if (_jobBoard == null)
            {
                _repairJobByOrder.Remove(orderId);
                return;
            }

            if (_repairJobByOrder.TryGetValue(orderId, out var jid))
            {
                _jobBoard.Cancel(jid);
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
            CleanupOrphanSiteForBuilding(o.TargetBuilding);

            _notificationService?.Push(
                key: $"BuildCancel_{o.TargetBuilding.Value}",
                title: "Đã hủy xây dựng",
                body: "Lệnh xây công trình đã được hủy.",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(o.TargetBuilding, default, o.BuildingDefId),
                cooldownSeconds: 0.75f,
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

            CleanupOrphanSiteForBuilding(o.TargetBuilding);

            _notificationService?.Push(
                key: $"UpgradeCancel_{o.TargetBuilding.Value}",
                title: "Đã hủy nâng cấp",
                body: "Lệnh nâng cấp công trình đã được hủy.",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(o.TargetBuilding, default, o.BuildingDefId),
                cooldownSeconds: 0.75f,
                dedupeByKey: true);
        }

        private void CancelRepair(ref BuildOrder o)
        {
            CancelRepairJob(o.OrderId);

            _notificationService?.Push(
                key: $"RepairCancel_{o.TargetBuilding.Value}",
                title: "Đã hủy sửa chữa",
                body: "Lệnh sửa chữa công trình đã được hủy.",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(o.TargetBuilding, default, o.BuildingDefId),
                cooldownSeconds: 0.75f,
                dedupeByKey: true);
        }

        private void RemovePlaceholder(BuildingId buildingId)
        {
            if (!_destroyPlaceholderOnCancel) return;
            if (buildingId.Value == 0) return;
            if (_worldState?.Buildings == null) return;
            if (!_worldState.Buildings.Exists(buildingId)) return;

            var building = _worldState.Buildings.Get(buildingId);
            if (building.IsConstructed)
                return;

            var def = SafeGetBuildingDef(building.DefId);
            int w = Math.Max(1, def?.SizeX ?? 1);
            int h = Math.Max(1, def?.SizeY ?? 1);
            for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
                _gridMap?.ClearBuilding(new CellPos(building.Anchor.X + dx, building.Anchor.Y + dy));

            _worldState.Buildings.Destroy(buildingId);
            try { _worldIndex?.OnBuildingDestroyed(buildingId); }
            catch (Exception ex) { UnityEngine.Debug.LogError($"[BuildOrderCancellationService] Failed to update WorldIndex after removing placeholder building {buildingId.Value}: {ex}"); }
            _eventBus?.Publish(new WorldStateChangedEvent("Building", buildingId.Value));
            _eventBus?.Publish(new RoadsDirtyEvent());
        }

        private void CleanupBuildSite(SiteId siteId, in BuildSiteState site)
        {
            var def = SafeGetBuildingDef(site.BuildingDefId);
            int w = Math.Max(1, def?.SizeX ?? 1);
            int h = Math.Max(1, def?.SizeY ?? 1);

            for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
                _gridMap?.ClearSite(new CellPos(site.Anchor.X + dx, site.Anchor.Y + dy));

            if (_worldState.Sites.Exists(siteId))
                _worldState.Sites.Destroy(siteId);

            _eventBus?.Publish(new WorldStateChangedEvent("BuildSite", siteId.Value));
        }

        private void CleanupOrphanSiteForBuilding(BuildingId buildingId)
        {
            if (buildingId.Value == 0 || _worldState?.Sites == null)
                return;

            var stale = new List<SiteId>();
            foreach (var siteId in _worldState.Sites.Ids)
            {
                if (!_worldState.Sites.Exists(siteId)) continue;
                var site = _worldState.Sites.Get(siteId);
                if (site.TargetBuilding.Value == buildingId.Value)
                    stale.Add(siteId);
            }

            for (int i = 0; i < stale.Count; i++)
            {
                var siteId = stale[i];
                if (!_worldState.Sites.Exists(siteId)) continue;
                var site = _worldState.Sites.Get(siteId);
                CleanupBuildSite(siteId, site);
            }
        }

        private void TryRollbackAutoRoad(int orderId, in BuildOrder o)
        {
            if (_gridMap == null) return;
            if (!_autoRoadByOrder.TryGetValue(orderId, out var c)) return;
            if (!_gridMap.IsInside(c)) return;

            var occ = _gridMap.Get(c);
            if (occ.Kind == CellOccupancyKind.Site || occ.Kind == CellOccupancyKind.Building)
                return;

            if (_gridMap.IsRoad(c))
            {
                _gridMap.SetRoad(c, false);
                _eventBus?.Publish(new RoadsDirtyEvent());
            }
        }

        private void RefundDeliveredToNearestStorage(in BuildSiteState st)
        {
            if (_worldState == null || _storageService == null || _worldIndex == null) return;
            if (st.DeliveredSoFar == null || st.DeliveredSoFar.Count == 0) return;

            var whs = _worldIndex.Warehouses;
            if (whs == null || whs.Count == 0) return;

            _buildingIdsBuf.Clear();
            for (int i = 0; i < whs.Count; i++)
            {
                var bid = whs[i];
                if (bid.Value == 0) continue;
                if (!_worldState.Buildings.Exists(bid)) continue;

                var bs = _worldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;
                _buildingIdsBuf.Add(bid);
            }

            if (_buildingIdsBuf.Count == 0) return;

            var from = st.Anchor;
            _buildingIdsBuf.Sort((a, b) =>
            {
                var aa = _worldState.Buildings.Get(a).Anchor;
                var bb = _worldState.Buildings.Get(b).Anchor;
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
                    if (!_storageService.CanStore(dst, rt)) continue;
                    int added = _storageService.Add(dst, rt, left);
                    left -= added;
                }
            }
        }

        private bool TryGetSite(SiteId siteId, out BuildSiteState site)
        {
            site = default;
            if (siteId.Value == 0 || _worldState?.Sites == null || !_worldState.Sites.Exists(siteId))
                return false;

            site = _worldState.Sites.Get(siteId);
            return true;
        }

        private BuildingDef SafeGetBuildingDef(string defId)
        {
            if (_dataRegistry == null || string.IsNullOrWhiteSpace(defId))
                return null;

            try { return _dataRegistry.GetBuilding(defId); }
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
