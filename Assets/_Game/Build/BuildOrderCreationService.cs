using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class BuildOrderCreationService
    {
        private readonly IDataRegistry _dataRegistry;
        private readonly IWorldState _worldState;
        private readonly IGridMap _gridMap;
        private readonly IEventBus _eventBus;
        private readonly INotificationService _notificationService;
        private readonly IStorageService _storageService;
        private readonly IUnlockService _unlockService;
        private readonly IPlacementService _placementService;
        private readonly IPathfinderRuntime _pathfinder;
        private readonly Dictionary<int, BuildOrder> _orders;
        private readonly List<int> _active;
        private readonly Action _ensureBusSubscribed;
        private readonly Func<int> _allocateOrderId;
        private readonly Func<BuildingDef, float> _computeWorkSecondsTotal;
        private readonly Func<int, float> _computeWorkSecondsTotalFromChunks;
        private readonly Func<int, int, float> _computeRepairSeconds;
        private readonly Func<CostDef[], List<CostDef>> _cloneCostsOrEmpty;
        private readonly Func<CostDef[], List<CostDef>> _buildDeliveredMirror;

        public BuildOrderCreationService(
            IDataRegistry dataRegistry,
            IWorldState worldState,
            IGridMap gridMap,
            IEventBus eventBus,
            INotificationService notificationService,
            IStorageService storageService,
            IUnlockService unlockService,
            IPlacementService placementService,
            IPathfinderRuntime pathfinder,
            Dictionary<int, BuildOrder> orders,
            List<int> active,
            Action ensureBusSubscribed,
            Func<int> allocateOrderId,
            Func<BuildingDef, float> computeWorkSecondsTotal,
            Func<int, float> computeWorkSecondsTotalFromChunks,
            Func<int, int, float> computeRepairSeconds,
            Func<CostDef[], List<CostDef>> cloneCostsOrEmpty,
            Func<CostDef[], List<CostDef>> buildDeliveredMirror)
        {
            _dataRegistry = dataRegistry;
            _worldState = worldState;
            _gridMap = gridMap;
            _eventBus = eventBus;
            _notificationService = notificationService;
            _storageService = storageService;
            _unlockService = unlockService;
            _placementService = placementService;
            _pathfinder = pathfinder;
            _orders = orders;
            _active = active;
            _ensureBusSubscribed = ensureBusSubscribed;
            _allocateOrderId = allocateOrderId;
            _computeWorkSecondsTotal = computeWorkSecondsTotal;
            _computeWorkSecondsTotalFromChunks = computeWorkSecondsTotalFromChunks;
            _computeRepairSeconds = computeRepairSeconds;
            _cloneCostsOrEmpty = cloneCostsOrEmpty;
            _buildDeliveredMirror = buildDeliveredMirror;
        }

        public int CreatePlaceOrder(string buildingDefId, CellPos anchor, Dir4 rotation)
        {
            _ensureBusSubscribed?.Invoke();

            if (_unlockService != null && !_unlockService.IsUnlocked(buildingDefId))
            {
                _notificationService?.Push(
                    key: $"LockedBuild_{buildingDefId}",
                    title: "Chưa mở khóa",
                    body: "Công trình này chưa thể xây ở thời điểm hiện tại.",
                    severity: NotificationSeverity.Warning,
                    payload: new NotificationPayload(default, default, buildingDefId),
                    cooldownSeconds: 2f,
                    dedupeByKey: true
                );
                return 0;
            }

            var placement = _placementService;
            var vr = placement.ValidateBuilding(buildingDefId, anchor, rotation);
            if (!vr.Ok)
            {
                _notificationService?.Push(
                    key: "CantPlace",
                    title: "Không thể đặt công trình",
                    body: vr.FailReason switch
                    {
                        PlacementFailReason.Overlap => "Vị trí này đang chồng lên đường hoặc công trình khác.",
                        PlacementFailReason.BlockedBySite => "Vị trí này đang bị một site xây dựng chiếm chỗ.",
                        PlacementFailReason.NoRoadConnection => "Công trình cần kết nối với đường.",
                        PlacementFailReason.OutOfBounds => "Vị trí này nằm ngoài bản đồ.",
                        PlacementFailReason.InvalidRotation => "Hướng đặt công trình không hợp lệ.",
                        _ => "Vị trí đặt công trình không hợp lệ."
                    },
                    severity: NotificationSeverity.Warning,
                    payload: new NotificationPayload(default, default, "placement"),
                    cooldownSeconds: 1.5f,
                    dedupeByKey: true
                );

                return 0;
            }

            BuildingDef def = _dataRegistry.GetBuilding(buildingDefId);

            if (def.BuildCostsL1 != null && def.BuildCostsL1.Length > 0 && _storageService != null)
            {
                for (int i = 0; i < def.BuildCostsL1.Length; i++)
                {
                    var c = def.BuildCostsL1[i];
                    if (c == null || c.Amount <= 0) continue;

                    int total = _storageService.GetTotal(c.Resource);
                    if (total < c.Amount)
                    {
                        _notificationService?.Push(
                            key: $"NoRes_{buildingDefId}_{c.Resource}",
                            title: "Thiếu tài nguyên",
                            body: $"Cần {c.Amount} {c.Resource}, hiện chỉ có {total}.",
                            severity: NotificationSeverity.Warning,
                            payload: new NotificationPayload(default, default, buildingDefId),
                            cooldownSeconds: 2f,
                            dedupeByKey: true
                        );
                        return 0;
                    }
                }
            }

            int w = Math.Max(1, def.SizeX);
            int h = Math.Max(1, def.SizeY);
            int level = Math.Max(1, def.BaseLevel);

            var bst = new BuildingState
            {
                DefId = buildingDefId,
                Anchor = anchor,
                Rotation = rotation,
                Level = level,
                IsConstructed = false,
                MaxHP = Math.Max(1, def.MaxHp),
                HP = Math.Max(1, def.MaxHp),
            };
            var buildingId = _worldState.Buildings.Create(bst);
            bst.Id = buildingId;
            _worldState.Buildings.Set(buildingId, bst);

            float workTotal = _computeWorkSecondsTotal(def);
            var site = new BuildSiteState
            {
                BuildingDefId = buildingDefId,
                TargetLevel = level,
                Anchor = anchor,
                Rotation = rotation,
                IsActive = true,
                WorkSecondsDone = 0f,
                WorkSecondsTotal = Math.Max(0.1f, workTotal),
                DeliveredSoFar = _buildDeliveredMirror(def.BuildCostsL1),
                RemainingCosts = _cloneCostsOrEmpty(def.BuildCostsL1),
                Kind = 0,
                TargetBuilding = buildingId,
                FromDefId = "",
                EdgeId = ""
            };

            CleanupOrphanSiteForBuilding(buildingId);

            var siteId = _worldState.Sites.Create(site);
            site.Id = siteId;
            _worldState.Sites.Set(siteId, site);

            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    _gridMap.SetSite(new CellPos(anchor.X + dx, anchor.Y + dy), siteId);

            int orderId = _allocateOrderId();
            var order = new BuildOrder
            {
                OrderId = orderId,
                Kind = BuildOrderKind.PlaceNew,
                BuildingDefId = buildingDefId,
                TargetBuilding = buildingId,
                Site = siteId,
                RequiredCost = default,
                Delivered = default,
                WorkSecondsRequired = site.WorkSecondsTotal,
                WorkSecondsDone = 0f,
                Completed = false
            };

            _orders[orderId] = order;
            _active.Add(orderId);

            _notificationService?.Push(
                key: $"BuildStart_{buildingId.Value}",
                title: "Khởi công",
                body: "Đã tạo site xây dựng mới.",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(buildingId, default, buildingDefId),
                cooldownSeconds: 0.75f,
                dedupeByKey: true
            );

            return orderId;
        }

        public int CreateUpgradeOrder(BuildingId building)
        {
            _ensureBusSubscribed?.Invoke();

            if (building.Value == 0) return 0;
            if (_worldState == null || _worldState.Buildings == null) return 0;
            if (!_worldState.Buildings.Exists(building)) return 0;

            var bs = _worldState.Buildings.Get(building);
            if (!bs.IsConstructed)
            {
                _notificationService?.Push(
                    key: $"UpgradeNotConstructed_{building.Value}",
                    title: "Không thể nâng cấp",
                    body: "Hãy hoàn thành công trình hiện tại trước khi nâng cấp.",
                    severity: NotificationSeverity.Warning,
                    payload: new NotificationPayload(building, default, bs.DefId),
                    cooldownSeconds: 1.5f,
                    dedupeByKey: true
                );
                return 0;
            }

            for (int i = 0; i < _active.Count; i++)
            {
                int id = _active[i];
                if (!_orders.TryGetValue(id, out var oo)) continue;
                if (oo.Completed) continue;
                if (oo.Kind != BuildOrderKind.Upgrade) continue;
                if (oo.TargetBuilding.Value != building.Value) continue;

                _notificationService?.Push(
                    key: $"UpgradeAlready_{building.Value}",
                    title: "Đang nâng cấp",
                    body: "Công trình này đã có lệnh nâng cấp rồi.",
                    severity: NotificationSeverity.Info,
                    payload: new NotificationPayload(building, default, "upgrade"),
                    cooldownSeconds: 1.5f,
                    dedupeByKey: true
                );

                return id;
            }

            var dr = _dataRegistry as IDataRegistry;
            if (dr == null)
            {
                _notificationService?.Push(
                    key: $"UpgradeNoGraph_{building.Value}",
                    title: "Không thể nâng cấp",
                    body: "Dữ liệu nâng cấp chưa được nạp đúng.",
                    severity: NotificationSeverity.Warning,
                    payload: new NotificationPayload(building, default, "upgrade"),
                    cooldownSeconds: 2f,
                    dedupeByKey: true
                );
                return 0;
            }

            var edges = dr.GetUpgradeEdgesFrom(bs.DefId);
            if (edges == null || edges.Count == 0)
            {
                _notificationService?.Push(
                    key: $"UpgradeNoEdge_{building.Value}",
                    title: "Không có nâng cấp",
                    body: "Công trình này hiện chưa có cấp nâng cấp tiếp theo.",
                    severity: NotificationSeverity.Info,
                    payload: new NotificationPayload(building, default, bs.DefId),
                    cooldownSeconds: 2f,
                    dedupeByKey: true
                );
                return 0;
            }

            var edge = edges[0];

            if (!string.IsNullOrWhiteSpace(edge.RequiresUnlocked) && _unlockService != null && !_unlockService.IsUnlocked(edge.RequiresUnlocked))
            {
                _notificationService?.Push(
                    key: $"UpgradeLocked_{building.Value}_{edge.RequiresUnlocked}",
                    title: "Chưa mở khóa",
                    body: "Nâng cấp này chưa khả dụng ở thời điểm hiện tại.",
                    severity: NotificationSeverity.Warning,
                    payload: new NotificationPayload(building, default, edge.RequiresUnlocked),
                    cooldownSeconds: 2f,
                    dedupeByKey: true
                );
                return 0;
            }

            if (!_dataRegistry.TryGetBuilding(edge.To, out var toDef) || toDef == null)
            {
                _notificationService?.Push(
                    key: $"UpgradeMissingDef_{building.Value}",
                    title: "Không thể nâng cấp",
                    body: "Không tìm thấy dữ liệu của cấp nâng cấp tiếp theo.",
                    severity: NotificationSeverity.Error,
                    payload: new NotificationPayload(building, default, edge.To),
                    cooldownSeconds: 2f,
                    dedupeByKey: true
                );
                return 0;
            }

            if (!JobReachabilityHelper.IsBuildingEntryReachable(_dataRegistry, _gridMap, _pathfinder, bs, bs.Anchor))
            {
                _notificationService?.Push(
                    key: $"UpgradeUnreachable_{building.Value}",
                    title: "Không thể nâng cấp",
                    body: "Công trình này hiện không có lối tiếp cận hợp lệ cho thợ xây.",
                    severity: NotificationSeverity.Warning,
                    payload: new NotificationPayload(building, default, edge.To),
                    cooldownSeconds: 2f,
                    dedupeByKey: true
                );
                return 0;
            }

            var fromDef = _dataRegistry.GetBuilding(bs.DefId);
            if (fromDef != null && toDef != null)
            {
                if (Math.Max(1, fromDef.SizeX) != Math.Max(1, toDef.SizeX) || Math.Max(1, fromDef.SizeY) != Math.Max(1, toDef.SizeY))
                {
                    _notificationService?.Push(
                        key: $"UpgradeFootprintMismatch_{building.Value}",
                        title: "Không thể nâng cấp",
                        body: "Cấp nâng cấp này đổi footprint công trình nên hiện chưa được hỗ trợ.",
                        severity: NotificationSeverity.Warning,
                        payload: new NotificationPayload(building, default, edge.To),
                        cooldownSeconds: 2f,
                        dedupeByKey: true
                    );
                    return 0;
                }
            }

            if (edge.Cost != null && edge.Cost.Length > 0 && _storageService != null)
            {
                for (int i = 0; i < edge.Cost.Length; i++)
                {
                    var c = edge.Cost[i];
                    if (c == null || c.Amount <= 0) continue;

                    int total = _storageService.GetTotal(c.Resource);
                    if (total < c.Amount)
                    {
                        _notificationService?.Push(
                            key: $"NoRes_Upgrade_{building.Value}_{c.Resource}",
                            title: "Not enough resources",
                            body: $"Need {c.Amount} {c.Resource} (have {total})",
                            severity: NotificationSeverity.Warning,
                            payload: new NotificationPayload(building, default, edge.To),
                            cooldownSeconds: 0.25f,
                            dedupeByKey: true
                        );
                        return 0;
                    }
                }
            }

            int targetLevel = 1;
            if (dr.TryGetBuildableNode(edge.To, out var node) && node != null) targetLevel = Math.Max(1, node.Level);
            else targetLevel = Math.Max(1, toDef.BaseLevel);

            float workTotal = _computeWorkSecondsTotalFromChunks(edge.WorkChunks);

            var site = new BuildSiteState
            {
                Kind = 1,
                TargetBuilding = building,
                FromDefId = bs.DefId,
                EdgeId = edge.Id,
                BuildingDefId = edge.To,
                TargetLevel = targetLevel,
                Anchor = bs.Anchor,
                Rotation = bs.Rotation,
                IsActive = true,
                WorkSecondsDone = 0f,
                WorkSecondsTotal = Math.Max(0.1f, workTotal),
                DeliveredSoFar = _buildDeliveredMirror(edge.Cost),
                RemainingCosts = _cloneCostsOrEmpty(edge.Cost)
            };

            CleanupOrphanSiteForBuilding(building);

            var siteId = _worldState.Sites.Create(site);
            site.Id = siteId;
            _worldState.Sites.Set(siteId, site);

            int orderId = _allocateOrderId();
            var order = new BuildOrder
            {
                OrderId = orderId,
                Kind = BuildOrderKind.Upgrade,
                BuildingDefId = edge.To,
                TargetBuilding = building,
                Site = siteId,
                RequiredCost = default,
                Delivered = default,
                WorkSecondsRequired = site.WorkSecondsTotal,
                WorkSecondsDone = 0f,
                Completed = false
            };

            _orders[orderId] = order;
            _active.Add(orderId);

            _notificationService?.Push(
                key: $"UpgradeStart_{building.Value}",
                title: "Construction",
                body: $"Upgrade started: {bs.DefId} -> {edge.To}",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(building, default, edge.To),
                cooldownSeconds: 0.25f,
                dedupeByKey: true
            );

            return orderId;
        }

        public int CreateRepairOrder(BuildingId building)
        {
            _ensureBusSubscribed?.Invoke();

            if (building.Value == 0) return 0;
            if (_worldState == null || _worldState.Buildings == null) return 0;
            if (!_worldState.Buildings.Exists(building)) return 0;

            var bs = _worldState.Buildings.Get(building);
            if (!bs.IsConstructed) return 0;

            if (bs.MaxHP <= 0)
            {
                int mhp = 100;
                if (_dataRegistry.TryGetBuilding(bs.DefId, out var repairDef) && repairDef != null)
                    mhp = Math.Max(1, repairDef.MaxHp);
                bs.MaxHP = mhp;
                if (bs.HP <= 0) bs.HP = bs.MaxHP;
                _worldState.Buildings.Set(building, bs);
            }

            if (bs.HP >= bs.MaxHP) return 0;

            if (!JobReachabilityHelper.IsBuildingEntryReachable(_dataRegistry, _gridMap, _pathfinder, bs, bs.Anchor))
            {
                _notificationService?.Push(
                    key: $"RepairUnreachable_{building.Value}",
                    title: "Không thể sửa chữa",
                    body: "Công trình này hiện không có lối tiếp cận hợp lệ cho thợ xây.",
                    severity: NotificationSeverity.Warning,
                    payload: new NotificationPayload(building, default, bs.DefId),
                    cooldownSeconds: 2f,
                    dedupeByKey: true
                );
                return 0;
            }

            for (int i = 0; i < _active.Count; i++)
            {
                int id = _active[i];
                if (!_orders.TryGetValue(id, out var oo)) continue;
                if (oo.Completed) continue;
                if (oo.Kind != BuildOrderKind.Repair) continue;
                if (oo.TargetBuilding.Value == building.Value) return id;
            }

            int orderId = _allocateOrderId();
            var order = new BuildOrder
            {
                OrderId = orderId,
                Kind = BuildOrderKind.Repair,
                BuildingDefId = bs.DefId,
                TargetBuilding = building,
                Site = default,
                RequiredCost = default,
                Delivered = default,
                WorkSecondsRequired = _computeRepairSeconds(bs.HP, bs.MaxHP),
                WorkSecondsDone = 0f,
                Completed = false
            };

            _orders[orderId] = order;
            _active.Add(orderId);

            _notificationService?.Push(
                key: $"RepairStart_{building.Value}",
                title: "Construction",
                body: $"Repair started: {bs.DefId} ({bs.HP}/{bs.MaxHP})",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(building, default, bs.DefId),
                cooldownSeconds: 0.25f,
                dedupeByKey: true
            );

            return orderId;
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
                var def = SafeGetBuildingDef(site.BuildingDefId);
                int w = Math.Max(1, def?.SizeX ?? 1);
                int h = Math.Max(1, def?.SizeY ?? 1);
                for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    _gridMap?.ClearSite(new CellPos(site.Anchor.X + dx, site.Anchor.Y + dy));

                _worldState.Sites.Destroy(siteId);
                _eventBus?.Publish(new WorldStateChangedEvent("BuildSite", siteId.Value));
            }
        }

        private BuildingDef SafeGetBuildingDef(string defId)
        {
            if (_dataRegistry == null || string.IsNullOrWhiteSpace(defId))
                return null;

            try { return _dataRegistry.GetBuilding(defId); }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[BuildOrderCreationService] Failed to resolve BuildingDef '{defId}' while cleaning orphan build sites. Using 1x1 fallback footprint. {ex}");
                return null;
            }
        }
    }
}
