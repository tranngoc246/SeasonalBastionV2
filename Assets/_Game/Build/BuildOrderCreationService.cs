using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class BuildOrderCreationService
    {
        private readonly GameServices _s;
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
            GameServices s,
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
            _s = s;
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

            if (_s.UnlockService != null && !_s.UnlockService.IsUnlocked(buildingDefId))
            {
                _s.NotificationService?.Push(
                    key: $"LockedBuild_{buildingDefId}",
                    title: "Locked",
                    body: $"Not unlocked yet: {buildingDefId}",
                    severity: NotificationSeverity.Warning,
                    payload: new NotificationPayload(default, default, buildingDefId),
                    cooldownSeconds: 0.25f,
                    dedupeByKey: true
                );
                return 0;
            }

            var placement = _s.PlacementService;
            var vr = placement.ValidateBuilding(buildingDefId, anchor, rotation);
            if (!vr.Ok)
            {
                _s.NotificationService?.Push(
                    key: "CantPlace",
                    title: "Can't place",
                    body: vr.FailReason switch
                    {
                        PlacementFailReason.Overlap => "Overlaps road/building.",
                        PlacementFailReason.BlockedBySite => "Blocked by site.",
                        PlacementFailReason.NoRoadConnection => "No road connection.",
                        PlacementFailReason.OutOfBounds => "Out of bounds.",
                        PlacementFailReason.InvalidRotation => "Invalid rotation.",
                        _ => "Invalid placement."
                    },
                    severity: NotificationSeverity.Warning,
                    payload: new NotificationPayload(default, default, "placement"),
                    cooldownSeconds: 0.35f,
                    dedupeByKey: true
                );

                return 0;
            }

            BuildingDef def = _s.DataRegistry.GetBuilding(buildingDefId);

            if (def.BuildCostsL1 != null && def.BuildCostsL1.Length > 0 && _s.StorageService != null)
            {
                for (int i = 0; i < def.BuildCostsL1.Length; i++)
                {
                    var c = def.BuildCostsL1[i];
                    if (c == null || c.Amount <= 0) continue;

                    int total = _s.StorageService.GetTotal(c.Resource);
                    if (total < c.Amount)
                    {
                        _s.NotificationService?.Push(
                            key: $"NoRes_{buildingDefId}_{c.Resource}",
                            title: "Not enough resources",
                            body: $"Need {c.Amount} {c.Resource} (have {total})",
                            severity: NotificationSeverity.Warning,
                            payload: new NotificationPayload(default, default, buildingDefId),
                            cooldownSeconds: 0.25f,
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
            var buildingId = _s.WorldState.Buildings.Create(bst);
            bst.Id = buildingId;
            _s.WorldState.Buildings.Set(buildingId, bst);

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

            var siteId = _s.WorldState.Sites.Create(site);
            site.Id = siteId;
            _s.WorldState.Sites.Set(siteId, site);

            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    _s.GridMap.SetSite(new CellPos(anchor.X + dx, anchor.Y + dy), siteId);

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

            _s.NotificationService?.Push(
                key: $"BuildStart_{buildingId.Value}",
                title: "Construction",
                body: $"Started: {buildingDefId}",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(buildingId, default, buildingDefId),
                cooldownSeconds: 0.25f,
                dedupeByKey: true
            );

            return orderId;
        }

        public int CreateUpgradeOrder(BuildingId building)
        {
            _ensureBusSubscribed?.Invoke();

            if (building.Value == 0) return 0;
            if (_s.WorldState == null || _s.WorldState.Buildings == null) return 0;
            if (!_s.WorldState.Buildings.Exists(building)) return 0;

            var bs = _s.WorldState.Buildings.Get(building);
            if (!bs.IsConstructed)
            {
                _s.NotificationService?.Push(
                    key: $"UpgradeNotConstructed_{building.Value}",
                    title: "Construction",
                    body: "Can't upgrade while under construction. Finish building first.",
                    severity: NotificationSeverity.Warning,
                    payload: new NotificationPayload(building, default, bs.DefId),
                    cooldownSeconds: 0.25f,
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

                _s.NotificationService?.Push(
                    key: $"UpgradeAlready_{building.Value}",
                    title: "Construction",
                    body: "Công trình đang nâng cấp rồi.",
                    severity: NotificationSeverity.Info,
                    payload: new NotificationPayload(building, default, "upgrade"),
                    cooldownSeconds: 0.25f,
                    dedupeByKey: true
                );

                return id;
            }

            var dr = _s.DataRegistry as IDataRegistry;
            if (dr == null)
            {
                _s.NotificationService?.Push(
                    key: $"UpgradeNoGraph_{building.Value}",
                    title: "Construction",
                    body: "Upgrade graph not loaded.",
                    severity: NotificationSeverity.Warning,
                    payload: new NotificationPayload(building, default, "upgrade"),
                    cooldownSeconds: 0.25f,
                    dedupeByKey: true
                );
                return 0;
            }

            var edges = dr.GetUpgradeEdgesFrom(bs.DefId);
            if (edges == null || edges.Count == 0)
            {
                _s.NotificationService?.Push(
                    key: $"UpgradeNoEdge_{building.Value}",
                    title: "Construction",
                    body: $"No upgrade available for: {bs.DefId}",
                    severity: NotificationSeverity.Info,
                    payload: new NotificationPayload(building, default, bs.DefId),
                    cooldownSeconds: 0.25f,
                    dedupeByKey: true
                );
                return 0;
            }

            var edge = edges[0];

            if (!string.IsNullOrWhiteSpace(edge.RequiresUnlocked) && _s.UnlockService != null && !_s.UnlockService.IsUnlocked(edge.RequiresUnlocked))
            {
                _s.NotificationService?.Push(
                    key: $"UpgradeLocked_{building.Value}_{edge.RequiresUnlocked}",
                    title: "Locked",
                    body: $"Not unlocked yet: {edge.RequiresUnlocked}",
                    severity: NotificationSeverity.Warning,
                    payload: new NotificationPayload(building, default, edge.RequiresUnlocked),
                    cooldownSeconds: 0.25f,
                    dedupeByKey: true
                );
                return 0;
            }

            if (!_s.DataRegistry.TryGetBuilding(edge.To, out var toDef) || toDef == null)
            {
                _s.NotificationService?.Push(
                    key: $"UpgradeMissingDef_{building.Value}",
                    title: "Construction",
                    body: $"Upgrade target def missing: {edge.To}",
                    severity: NotificationSeverity.Error,
                    payload: new NotificationPayload(building, default, edge.To),
                    cooldownSeconds: 0.25f,
                    dedupeByKey: true
                );
                return 0;
            }

            var fromDef = _s.DataRegistry.GetBuilding(bs.DefId);
            if (fromDef != null && toDef != null)
            {
                if (Math.Max(1, fromDef.SizeX) != Math.Max(1, toDef.SizeX) || Math.Max(1, fromDef.SizeY) != Math.Max(1, toDef.SizeY))
                {
                    _s.NotificationService?.Push(
                        key: $"UpgradeFootprintMismatch_{building.Value}",
                        title: "Construction",
                        body: "Upgrade footprint mismatch (not supported).",
                        severity: NotificationSeverity.Warning,
                        payload: new NotificationPayload(building, default, edge.To),
                        cooldownSeconds: 0.25f,
                        dedupeByKey: true
                    );
                    return 0;
                }
            }

            if (edge.Cost != null && edge.Cost.Length > 0 && _s.StorageService != null)
            {
                for (int i = 0; i < edge.Cost.Length; i++)
                {
                    var c = edge.Cost[i];
                    if (c == null || c.Amount <= 0) continue;

                    int total = _s.StorageService.GetTotal(c.Resource);
                    if (total < c.Amount)
                    {
                        _s.NotificationService?.Push(
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

            var siteId = _s.WorldState.Sites.Create(site);
            site.Id = siteId;
            _s.WorldState.Sites.Set(siteId, site);

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

            _s.NotificationService?.Push(
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
            if (_s.WorldState == null || _s.WorldState.Buildings == null) return 0;
            if (!_s.WorldState.Buildings.Exists(building)) return 0;

            var bs = _s.WorldState.Buildings.Get(building);
            if (!bs.IsConstructed) return 0;

            if (bs.MaxHP <= 0)
            {
                int mhp = 100;
                if (_s.DataRegistry.TryGetBuilding(bs.DefId, out var repairDef) && repairDef != null)
                    mhp = Math.Max(1, repairDef.MaxHp);
                bs.MaxHP = mhp;
                if (bs.HP <= 0) bs.HP = bs.MaxHP;
                _s.WorldState.Buildings.Set(building, bs);
            }

            if (bs.HP >= bs.MaxHP) return 0;

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

            _s.NotificationService?.Push(
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
            if (buildingId.Value == 0 || _s.WorldState?.Sites == null)
                return;

            var stale = new List<SiteId>();
            foreach (var siteId in _s.WorldState.Sites.Ids)
            {
                if (!_s.WorldState.Sites.Exists(siteId)) continue;
                var site = _s.WorldState.Sites.Get(siteId);
                if (site.TargetBuilding.Value == buildingId.Value)
                    stale.Add(siteId);
            }

            for (int i = 0; i < stale.Count; i++)
            {
                var siteId = stale[i];
                if (!_s.WorldState.Sites.Exists(siteId)) continue;
                var site = _s.WorldState.Sites.Get(siteId);
                var def = SafeGetBuildingDef(site.BuildingDefId);
                int w = Math.Max(1, def?.SizeX ?? 1);
                int h = Math.Max(1, def?.SizeY ?? 1);
                for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    _s.GridMap?.ClearSite(new CellPos(site.Anchor.X + dx, site.Anchor.Y + dy));

                _s.WorldState.Sites.Destroy(siteId);
                _s.EventBus?.Publish(new WorldStateChangedEvent("BuildSite", siteId.Value));
            }
        }

        private BuildingDef SafeGetBuildingDef(string defId)
        {
            if (_s?.DataRegistry == null || string.IsNullOrWhiteSpace(defId))
                return null;

            try { return _s.DataRegistry.GetBuilding(defId); }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[BuildOrderCreationService] Failed to resolve BuildingDef '{defId}' while cleaning orphan build sites. Using 1x1 fallback footprint. {ex}");
                return null;
            }
        }
    }
}
