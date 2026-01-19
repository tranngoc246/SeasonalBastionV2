using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;

namespace SeasonalBastion
{
    public sealed class BuildOrderService : IBuildOrderService, ITickable
    {
        private readonly GameServices _s;
        private int _nextOrderId = 1;

        // Deterministic: ids always increase; iterate in insertion order
        private readonly List<int> _active = new();
        private readonly Dictionary<int, BuildOrder> _orders = new();

        // Long-term safety: prevent ghost placeholder on cancel
        private readonly bool _destroyPlaceholderOnCancel = true;

        public event Action<int> OnOrderCompleted;

        public BuildOrderService(GameServices s) { _s = s; }

        public int CreatePlaceOrder(string buildingDefId, CellPos anchor, Dir4 rotation)
        {
            // 1) Validate via PlacementService (single source of truth)
            var placement = _s.PlacementService;
            var vr = placement.ValidateBuilding(buildingDefId, anchor, rotation);
            if (!vr.Ok) return 0;

            // 2) Read def for footprint + base level
            BuildingDef def = _s.DataRegistry.GetBuilding(buildingDefId);
            int w = Math.Max(1, def.SizeX);
            int h = Math.Max(1, def.SizeY);
            int level = Math.Max(1, def.BaseLevel);

            // 3) Create placeholder building (not constructed yet)
            var bst = new BuildingState
            {
                DefId = buildingDefId,
                Anchor = anchor,
                Rotation = rotation,
                Level = level,
                IsConstructed = false
            };
            var buildingId = _s.WorldState.Buildings.Create(bst);
            bst.Id = buildingId;
            _s.WorldState.Buildings.Set(buildingId, bst);

            // 4) Create build site state
            float workTotal = ComputeWorkSecondsTotal(def);
            var site = new BuildSiteState
            {
                BuildingDefId = buildingDefId,
                TargetLevel = level,
                Anchor = anchor,
                Rotation = rotation,
                IsActive = true,
                WorkSecondsDone = 0f,
                WorkSecondsTotal = Math.Max(0.1f, workTotal),
                RemainingCosts = null // Day 6: ignore delivery
            };
            var siteId = _s.WorldState.Sites.Create(site);
            site.Id = siteId;
            _s.WorldState.Sites.Set(siteId, site);

            // 5) Occupy footprint as Site
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    _s.GridMap.SetSite(new CellPos(anchor.X + dx, anchor.Y + dy), siteId);

            // 6) Create order
            int orderId = _nextOrderId++;
            var order = new BuildOrder
            {
                OrderId = orderId,
                Kind = BuildOrderKind.PlaceNew,
                BuildingDefId = buildingDefId,
                TargetBuilding = buildingId,
                Site = siteId,
                RequiredCost = null, // Day 6: no delivery
                Delivered = default,
                WorkSecondsRequired = site.WorkSecondsTotal,
                WorkSecondsDone = 0f,
                Completed = false
            };

            _orders[orderId] = order;
            _active.Add(orderId);

            // Notify: started construction (Site created)
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
            // VS#1/Day6 scope: not implemented yet -> return 0 instead of throwing
            _s.NotificationService?.Push(
                key: $"UpgradeNotImpl_{building.Value}",
                title: "Construction",
                body: "Upgrade is not available in VS#1.",
                severity: NotificationSeverity.Warning,
                payload: new NotificationPayload(building, default, "upgrade"),
                cooldownSeconds: 0.25f,
                dedupeByKey: true
            );
            return 0;
        }

        public int CreateRepairOrder(BuildingId building)
        {
            // VS#1/Day6 scope: not implemented yet -> return 0 instead of throwing
            _s.NotificationService?.Push(
                key: $"RepairNotImpl_{building.Value}",
                title: "Construction",
                body: "Repair is not available in VS#1.",
                severity: NotificationSeverity.Warning,
                payload: new NotificationPayload(building, default, "repair"),
                cooldownSeconds: 0.25f,
                dedupeByKey: true
            );
            return 0;
        }

        public bool TryGet(int orderId, out BuildOrder order) => _orders.TryGetValue(orderId, out order);

        public void Cancel(int orderId)
        {
            if (!_orders.TryGetValue(orderId, out var o)) return;
            if (o.Completed) return;

            if (o.Kind == BuildOrderKind.PlaceNew)
            {
                // 1) Clear site occupancy + destroy site
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

                // 2) Optional: destroy placeholder building to avoid ghost state
                if (_destroyPlaceholderOnCancel && _s.WorldState.Buildings.Exists(o.TargetBuilding))
                {
                    _s.WorldState.Buildings.Destroy(o.TargetBuilding);
                }

                _s.NotificationService?.Push(
                    key: $"BuildCancel_{o.TargetBuilding.Value}",
                    title: "Construction",
                    body: $"Cancelled: {o.BuildingDefId}",
                    severity: NotificationSeverity.Info,
                    payload: new NotificationPayload(o.TargetBuilding, default, o.BuildingDefId),
                    cooldownSeconds: 0.25f,
                    dedupeByKey: true
                );
            }

            _orders.Remove(orderId);
            _active.Remove(orderId);
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            // Iterate deterministic by insertion order (orderId always increasing)
            for (int i = 0; i < _active.Count; i++)
            {
                int id = _active[i];
                if (!_orders.TryGetValue(id, out var o)) continue;
                if (o.Completed) continue;

                if (o.Kind != BuildOrderKind.PlaceNew) continue;

                // Site might have been destroyed (edge case)
                if (!_s.WorldState.Sites.Exists(o.Site))
                {
                    o.Completed = true;
                    _orders[id] = o;
                    continue;
                }

                // Progress work
                o.WorkSecondsDone += dt;

                var site = _s.WorldState.Sites.Get(o.Site);
                site.WorkSecondsDone = o.WorkSecondsDone;
                _s.WorldState.Sites.Set(o.Site, site);

                // Complete?
                if (o.WorkSecondsDone + 1e-4f >= o.WorkSecondsRequired)
                {
                    CompletePlaceOrder(ref o);
                    _orders[id] = o;

                    OnOrderCompleted?.Invoke(id);
                }
                else
                {
                    _orders[id] = o;
                }
            }

            // Cleanup completed orders (keep deterministic)
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                int id = _active[i];
                if (_orders.TryGetValue(id, out var o) && o.Completed)
                    _active.RemoveAt(i);
            }
        }

        private void CompletePlaceOrder(ref BuildOrder o)
        {
            if (o.Completed) return;

            // read site + def
            var site = _s.WorldState.Sites.Get(o.Site);
            var def = _s.DataRegistry.GetBuilding(o.BuildingDefId);

            int w = Math.Max(1, def.SizeX);
            int h = Math.Max(1, def.SizeY);

            // 1) Clear site occupancy
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    _s.GridMap.ClearSite(new CellPos(site.Anchor.X + dx, site.Anchor.Y + dy));

            // 2) Destroy site
            _s.WorldState.Sites.Destroy(o.Site);

            // 3) Finalize building placeholder
            var b = _s.WorldState.Buildings.Get(o.TargetBuilding);
            b.IsConstructed = true;
            _s.WorldState.Buildings.Set(o.TargetBuilding, b);

            // 4) Set building occupancy
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    _s.GridMap.SetBuilding(new CellPos(b.Anchor.X + dx, b.Anchor.Y + dy), o.TargetBuilding);

            // 5) WorldIndex hook (if available)
            try { _s.WorldIndex?.OnBuildingCreated(o.TargetBuilding); } catch { }

            // 6) Publish placed event on completion
            _s.EventBus.Publish(new BuildingPlacedEvent(o.BuildingDefId, o.TargetBuilding));

            _s.NotificationService?.Push(
                key: $"BuildComplete_{o.TargetBuilding.Value}",
                title: "Construction",
                body: $"Completed: {o.BuildingDefId} (Lv {b.Level}) @ ({b.Anchor.X},{b.Anchor.Y})",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(o.TargetBuilding, default, o.BuildingDefId),
                cooldownSeconds: 0.25f,
                dedupeByKey: true
            );

            o.Completed = true;
        }

        private static float ComputeWorkSecondsTotal(BuildingDef def)
        {
            // VS#1 simple deterministic formula:
            // base + area factor
            int w = Math.Max(1, def.SizeX);
            int h = Math.Max(1, def.SizeY);
            int area = w * h;

            return 1.5f + area * 0.35f;
        }
    }
}
