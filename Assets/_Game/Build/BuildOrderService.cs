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

        // siteId.Value -> pending deliver job ids
        private readonly Dictionary<int, List<JobId>> _deliverJobsBySite = new();
        // siteId.Value -> active work job id
        private readonly Dictionary<int, JobId> _workJobBySite = new();

        // Day22: orderId -> active repair work job id
        private readonly Dictionary<int, JobId> _repairJobByOrder = new();

        // orderId -> driveway road cell auto-created by PlacementService (rollback on cancel)
        private readonly Dictionary<int, CellPos> _autoRoadByOrder = new();
        private bool _busSubscribed;

        // reuse buffers
        private readonly List<BuildingId> _buildingIdsBuf = new(128);

        public event Action<int> OnOrderCompleted;

        public BuildOrderService(GameServices s) { _s = s; }

        public int CreatePlaceOrder(string buildingDefId, CellPos anchor, Dir4 rotation)
        {
            EnsureBusSubscribed();

            // Unlock gating must be enforced at authoritative layer (not only UI/debug).
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

            // 1) Validate via PlacementService (single source of truth)
            var placement = _s.PlacementService;
            var vr = placement.ValidateBuilding(buildingDefId, anchor, rotation);
            if (!vr.Ok)
            {
                // Authoritative "Can't place" notification (commit-time only)
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
                    // Global event: no building/tower yet
                    payload: new NotificationPayload(default, default, "placement"),
                    cooldownSeconds: 0.35f,
                    dedupeByKey: true
                );

                return 0;
            }


            // 2) Read def for footprint + base level
            BuildingDef def = _s.DataRegistry.GetBuilding(buildingDefId);

            // Authoritative resource gating: block placing if total storage is insufficient
            if (def.BuildCostsL1 != null && def.BuildCostsL1.Length > 0 && _s.StorageService != null)
            {
                for (int i = 0; i < def.BuildCostsL1.Length; i++)
                {
                    var c = def.BuildCostsL1[i];
                    if (c == null) continue;
                    if (c.Amount <= 0) continue;

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

            // 3) Create placeholder building (not constructed yet)
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
                DeliveredSoFar = BuildDeliveredMirror(def.BuildCostsL1),
                RemainingCosts = CloneCostsOrEmpty(def.BuildCostsL1),
                Kind = 0,
                TargetBuilding = buildingId,
                FromDefId = "",
                EdgeId = ""
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
                RequiredCost = default,
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
            EnsureBusSubscribed();

            if (building.Value == 0) return 0;
            if (_s.WorldState == null || _s.WorldState.Buildings == null) return 0;
            if (!_s.WorldState.Buildings.Exists(building)) return 0;

            var bs = _s.WorldState.Buildings.Get(building);
            if (!bs.IsConstructed) return 0;

            // Prevent duplicate upgrade order on same building
            for (int i = 0; i < _active.Count; i++)
            {
                int id = _active[i];
                if (!_orders.TryGetValue(id, out var oo)) continue;
                if (oo.Completed) continue;
                if (oo.Kind != BuildOrderKind.Upgrade) continue;
                if (oo.TargetBuilding.Value == building.Value) return id;
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

            // deterministic pick: first edge (sorted in DataRegistry)
            var edge = edges[0];

            // Optional unlock gating
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

            // Validate target def exists + same footprint (in-place upgrade)
            BuildingDef toDef;
            try { toDef = _s.DataRegistry.GetBuilding(edge.To); }
            catch { return 0; }

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

            // Build site (NO GridMap.SetSite for upgrade)
            int targetLevel = 1;
            if (dr.TryGetBuildableNode(edge.To, out var node) && node != null) targetLevel = Math.Max(1, node.Level);
            else targetLevel = Math.Max(1, toDef.BaseLevel);

            float workTotal = ComputeWorkSecondsTotalFromChunks(edge.WorkChunks);

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
                DeliveredSoFar = BuildDeliveredMirror(edge.Cost),
                RemainingCosts = CloneCostsOrEmpty(edge.Cost)
            };

            var siteId = _s.WorldState.Sites.Create(site);
            site.Id = siteId;
            _s.WorldState.Sites.Set(siteId, site);

            int orderId = _nextOrderId++;
            var order = new BuildOrder
            {
                OrderId = orderId,
                Kind = BuildOrderKind.Upgrade,
                BuildingDefId = edge.To,     // target def id
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

        private float ComputeWorkSecondsTotalFromChunks(int chunks)
        {
            if (chunks <= 0) chunks = (_s.Balance != null ? _s.Balance.FallbackBuildChunksL1 : 2);

            float chunkSec = _s.Balance != null ? _s.Balance.BuildChunkSec : 6f;
            int builderTier = _s.Balance != null ? _s.Balance.GetBuilderTier() : 1;
            float mult = _s.Balance != null ? _s.Balance.GetBuildSpeedMult(builderTier) : 1f;

            float total = chunks * chunkSec * mult;
            if (total < 0.1f) total = 0.1f;
            return total;
        }

        public int CreateRepairOrder(BuildingId building)
        {
            EnsureBusSubscribed();

            if (building.Value == 0) return 0;
            if (_s.WorldState == null || _s.WorldState.Buildings == null) return 0;
            if (!_s.WorldState.Buildings.Exists(building)) return 0;

            var bs = _s.WorldState.Buildings.Get(building);
            if (!bs.IsConstructed) return 0;

            // fix-up max hp from def if missing
            if (bs.MaxHP <= 0)
            {
                int mhp = 100;
                try { mhp = Math.Max(1, _s.DataRegistry.GetBuilding(bs.DefId).MaxHp); } catch { }
                bs.MaxHP = mhp;
                if (bs.HP <= 0) bs.HP = bs.MaxHP;
                _s.WorldState.Buildings.Set(building, bs);
            }

            if (bs.HP >= bs.MaxHP) return 0;

            // prevent duplicate repair orders for same building
            for (int i = 0; i < _active.Count; i++)
            {
                int id = _active[i];
                if (!_orders.TryGetValue(id, out var oo)) continue;
                if (oo.Completed) continue;
                if (oo.Kind != BuildOrderKind.Repair) continue;
                if (oo.TargetBuilding.Value == building.Value) return id;
            }

            int orderId = _nextOrderId++;
            var order = new BuildOrder
            {
                OrderId = orderId,
                Kind = BuildOrderKind.Repair,
                BuildingDefId = bs.DefId,
                TargetBuilding = building,
                Site = default,
                RequiredCost = default,
                Delivered = default,

                // time-only minimal: work seconds proportional to missing hp
                WorkSecondsRequired = ComputeRepairSeconds(bs.HP, bs.MaxHP),
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

        private float ComputeRepairSeconds(int hp, int maxHp)
        {
            if (maxHp <= 0) return 0f;
            int missing = maxHp - hp;
            if (missing <= 0) return 0f;

            float chunkSec = _s.Balance != null ? _s.Balance.RepairChunkSec : 4f;
            float healPct = _s.Balance != null ? _s.Balance.RepairHealPct : 0.15f;

            int perChunk = Math.Max(1, (int)Math.Ceiling(maxHp * healPct));
            int chunks = (missing + perChunk - 1) / perChunk;

            int builderTier = _s.Balance != null ? _s.Balance.GetBuilderTier() : 1;
            float timeMult = _s.Balance != null ? _s.Balance.GetRepairTimeMult(builderTier) : 1f;

            float total = chunks * chunkSec * timeMult;
            return total < chunkSec ? chunkSec : total;
        }

        public bool TryGet(int orderId, out BuildOrder order) => _orders.TryGetValue(orderId, out order);

        public void Cancel(int orderId)
        {
            if (!_orders.TryGetValue(orderId, out var o)) return;
            if (o.Completed) return;

            if (o.Kind == BuildOrderKind.PlaceNew)
            {
                // Day19: cancel pending jobs for this site (avoid orphan jobs)
                CancelTrackedJobsForSite(o.Site);

                TryRollbackAutoRoad(orderId);
                _autoRoadByOrder.Remove(orderId);

                // Day21: refund resources already delivered to this site (best-effort, deterministic)
                if (_s.WorldState.Sites.Exists(o.Site))
                {
                    var stRefund = _s.WorldState.Sites.Get(o.Site);
                    RefundDeliveredToNearestStorage(stRefund);
                }

                // Day21: clear local tracking maps to avoid ghost tracking after cancel
                _deliverJobsBySite.Remove(o.Site.Value);
                _workJobBySite.Remove(o.Site.Value);

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
            else if (o.Kind == BuildOrderKind.Repair)
            {
                CancelRepairJob(orderId);

                _s.NotificationService?.Push(
                    key: $"RepairCancel_{o.TargetBuilding.Value}",
                    title: "Construction",
                    body: $"Repair cancelled: {o.BuildingDefId}",
                    severity: NotificationSeverity.Info,
                    payload: new NotificationPayload(o.TargetBuilding, default, o.BuildingDefId),
                    cooldownSeconds: 0.25f,
                    dedupeByKey: true
                );
            }

            _orders.Remove(orderId);
            _active.Remove(orderId);
        }

        private void TryRollbackAutoRoad(int orderId)
        {
            if (!_autoRoadByOrder.TryGetValue(orderId, out var c)) return;
            if (_s.GridMap == null) return;
            if (!_s.GridMap.IsInside(c)) return;

            var occ = _s.GridMap.Get(c);
            if (occ.Kind == CellOccupancyKind.Site || occ.Kind == CellOccupancyKind.Building)
                return;

            if (_s.GridMap.IsRoad(c))
                _s.GridMap.SetRoad(c, false);
        }

        // Day21: tiện ích cancel theo Site/Building để debug tool gọi (không cần biết orderId)
        public bool CancelBySite(SiteId siteId)
        {
            if (siteId.Value == 0) return false;
            for (int i = 0; i < _active.Count; i++)
            {
                int id = _active[i];
                if (!_orders.TryGetValue(id, out var o)) continue;
                if (o.Completed) continue;
                if (o.Site.Value != siteId.Value) continue;
                Cancel(id);
                return true;
            }
            return false;
        }

        public bool CancelByBuilding(BuildingId buildingId)
        {
            if (buildingId.Value == 0) return false;
            for (int i = 0; i < _active.Count; i++)
            {
                int id = _active[i];
                if (!_orders.TryGetValue(id, out var o)) continue;
                if (o.Completed) continue;
                if (o.TargetBuilding.Value != buildingId.Value) continue;
                Cancel(id);
                return true;
            }
            return false;
        }

        private void EnsureBusSubscribed()
        {
            if (_busSubscribed) return;
            var bus = _s.EventBus;
            if (bus == null) return;

            bus.Subscribe<BuildOrderAutoRoadCreatedEvent>(OnAutoRoadCreated);
            _busSubscribed = true;
        }

        private void OnAutoRoadCreated(BuildOrderAutoRoadCreatedEvent e)
        {
            if (e.OrderId <= 0) return;
            _autoRoadByOrder[e.OrderId] = e.RoadCell;
        }

        public void Tick(float dt)
        {
            EnsureBusSubscribed();

            if (dt <= 0f) return;

            var workplace = _s.Balance != null ? _s.Balance.ResolveBuilderWorkplace() : ResolveBuildWorkplace();
            if (workplace.Value == 0)
                return; // no build workplace => cannot create jobs deterministically

            // Iterate deterministic by insertion order (orderId always increasing)
            for (int i = 0; i < _active.Count; i++)
            {
                int id = _active[i];
                if (!_orders.TryGetValue(id, out var o)) continue;
                if (o.Completed) continue;

                if (o.Kind == BuildOrderKind.Repair)
                {
                    TickRepairOrder(id, ref o, workplace);
                    _orders[id] = o;
                    continue; // IMPORTANT: repair has no site
                }

                if (o.Kind != BuildOrderKind.PlaceNew && o.Kind != BuildOrderKind.Upgrade)
                    continue;

                // Site might have been destroyed (edge case)
                if (!_s.WorldState.Sites.Exists(o.Site))
                {
                    CancelTrackedJobsForSite(o.Site);
                    o.Completed = true;
                    _orders[id] = o;
                    continue;
                }

                var site = _s.WorldState.Sites.Get(o.Site);

                // Day19: Ensure delivery/work jobs exist before JobScheduler tick
                var def = _s.DataRegistry.GetBuilding(o.BuildingDefId);
                EnsureBuildJobsForSite(o.Site, site, def, workplace);

                // Sync order progress from site (executor will advance site.WorkSecondsDone)
                o.WorkSecondsDone = site.WorkSecondsDone;
                _orders[id] = o;

                // Complete when:
                // - remaining costs are done
                // - and work seconds done >= total
                if (IsReadyToWork(site) && site.WorkSecondsDone + 1e-4f >= site.WorkSecondsTotal)
                {
                    CancelTrackedJobsForSite(o.Site);

                    if (o.Kind == BuildOrderKind.PlaceNew) CompletePlaceOrder(ref o);
                    else if (o.Kind == BuildOrderKind.Upgrade) CompleteUpgradeOrder(ref o);
                    _orders[id] = o;
                    OnOrderCompleted?.Invoke(id);
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

        private void TickRepairOrder(int orderId, ref BuildOrder o, BuildingId workplace)
        {
            if (_s.JobBoard == null) return;

            if (!_s.WorldState.Buildings.Exists(o.TargetBuilding))
            {
                CancelRepairJob(orderId);
                o.Completed = true;
                return;
            }

            var bs = _s.WorldState.Buildings.Get(o.TargetBuilding);
            if (!bs.IsConstructed)
            {
                CancelRepairJob(orderId);
                o.Completed = true;
                return;
            }

            // fix-up hp
            if (bs.MaxHP <= 0)
            {
                int mhp = 100;
                try { mhp = Math.Max(1, _s.DataRegistry.GetBuilding(bs.DefId).MaxHp); } catch { }
                bs.MaxHP = mhp;
                if (bs.HP <= 0) bs.HP = bs.MaxHP;
                _s.WorldState.Buildings.Set(o.TargetBuilding, bs);
            }

            if (bs.HP >= bs.MaxHP)
            {
                CancelRepairJob(orderId);
                o.Completed = true;
                _s.NotificationService?.Push(
                    key: $"RepairDone_{o.TargetBuilding.Value}",
                    title: "Construction",
                    body: $"Repair completed: {bs.DefId}",
                    severity: NotificationSeverity.Info,
                    payload: new NotificationPayload(o.TargetBuilding, default, bs.DefId),
                    cooldownSeconds: 0.25f,
                    dedupeByKey: true
                );
                return;
            }

            // ensure 1 repair job exists
            if (_repairJobByOrder.TryGetValue(orderId, out var jid))
            {
                if (!_s.JobBoard.TryGet(jid, out var j) || IsTerminal(j.Status))
                    _repairJobByOrder.Remove(orderId);
            }

            if (!_repairJobByOrder.ContainsKey(orderId))
            {
                var j = new Job
                {
                    Archetype = JobArchetype.RepairWork, // cần enum + executor (file khác)
                    Status = JobStatus.Created,
                    Workplace = workplace,

                    SourceBuilding = default,
                    DestBuilding = o.TargetBuilding,
                    Site = default,
                    Tower = default,

                    ResourceType = 0,
                    Amount = 0,

                    TargetCell = bs.Anchor,
                    CreatedAt = 0
                };

                var newId = _s.JobBoard.Enqueue(j);
                _repairJobByOrder[orderId] = newId;
            }
        }

        private void CompletePlaceOrder(ref BuildOrder o)
        {
            if (o.Completed) return;

            // Day19: cancel any leftover jobs for this site (safety)
            CancelTrackedJobsForSite(o.Site);

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

            // Day22: safety - init hp if missing
            if (b.MaxHP <= 0)
            {
                int mhp = 100;
                try { mhp = Math.Max(1, _s.DataRegistry.GetBuilding(b.DefId).MaxHp); } catch { }
                b.MaxHP = mhp;
            }
            if (b.HP <= 0) b.HP = b.MaxHP;

            _s.WorldState.Buildings.Set(o.TargetBuilding, b);

            // 4) Set building occupancy
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    _s.GridMap.SetBuilding(new CellPos(b.Anchor.X + dx, b.Anchor.Y + dy), o.TargetBuilding);

            // 4.5) If this building is a Tower => create TowerState (combat/ammo pipeline)
            if (def != null && def.IsTower && _s.WorldState?.Towers != null)
            {
                // Tower fires from CENTER cell of footprint (3x3 => +1,+1)
                var towerCell = new CellPos(b.Anchor.X + (w / 2), b.Anchor.Y + (h / 2));

                // Avoid duplicate tower at same cell (safety)
                bool exists = false;
                foreach (var tid0 in _s.WorldState.Towers.Ids)
                {
                    var ts0 = _s.WorldState.Towers.Get(tid0);
                    if (ts0.Cell.X == towerCell.X && ts0.Cell.Y == towerCell.Y) { exists = true; break; }
                }

                if (!exists)
                {
                    int hpMax = Math.Max(1, def.MaxHp);
                    int ammoMax = 0;

                    // Prefer TowerDef if present (Towers.json)
                    try
                    {
                        var tdef = _s.DataRegistry.GetTower(b.DefId);
                        if (tdef != null)
                        {
                            hpMax = Math.Max(1, tdef.MaxHp);
                            ammoMax = Math.Max(0, tdef.AmmoMax);
                        }
                    }
                    catch { }

                    var ts = new TowerState
                    {
                        Cell = towerCell,
                        Hp = hpMax,
                        HpMax = hpMax,
                        Ammo = ammoMax,
                        AmmoCap = ammoMax,
                    };

                    var tid = _s.WorldState.Towers.Create(ts);
                    ts.Id = tid;
                    _s.WorldState.Towers.Set(tid, ts);

                    // Mirror ammo into building state for UI/debug (match RunStart behavior)
                    b.Ammo = ammoMax;
                    _s.WorldState.Buildings.Set(o.TargetBuilding, b);
                }
            }

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
            _autoRoadByOrder.Remove(o.OrderId);
        }

        private void CompleteUpgradeOrder(ref BuildOrder o)
        {
            if (o.Completed) return;

            CancelTrackedJobsForSite(o.Site);

            var site = _s.WorldState.Sites.Get(o.Site);

            // Destroy site (upgrade site không occupy grid)
            _s.WorldState.Sites.Destroy(o.Site);

            // Apply upgrade
            var b = _s.WorldState.Buildings.Get(o.TargetBuilding);

            string fromId = site.FromDefId;
            string toId = o.BuildingDefId;

            b.DefId = toId;
            b.Level = Math.Max(1, site.TargetLevel);
            b.IsConstructed = true;

            // HP reset to new max (simple rule)
            int mhp = 100;
            try { mhp = Math.Max(1, _s.DataRegistry.GetBuilding(toId).MaxHp); } catch { }
            b.MaxHP = mhp;
            b.HP = mhp;

            _s.WorldState.Buildings.Set(o.TargetBuilding, b);

            // Tower sync (if upgraded def is tower)
            try
            {
                var def = _s.DataRegistry.GetBuilding(toId);
                if (def != null && def.IsTower && _s.WorldState?.Towers != null)
                {
                    int w = Math.Max(1, def.SizeX);
                    int h = Math.Max(1, def.SizeY);
                    var towerCell = new CellPos(b.Anchor.X + (w / 2), b.Anchor.Y + (h / 2));

                    // find tower at cell
                    TowerId found = default;
                    foreach (var tid in _s.WorldState.Towers.Ids)
                    {
                        var ts0 = _s.WorldState.Towers.Get(tid);
                        if (ts0.Cell.X == towerCell.X && ts0.Cell.Y == towerCell.Y) { found = tid; break; }
                    }

                    int hpMax = Math.Max(1, def.MaxHp);
                    int ammoMax = 0;
                    try
                    {
                        var tdef = _s.DataRegistry.GetTower(toId);
                        if (tdef != null)
                        {
                            hpMax = Math.Max(1, tdef.MaxHp);
                            ammoMax = Math.Max(0, tdef.AmmoMax);
                        }
                    }
                    catch { }

                    if (found.Value != 0)
                    {
                        var ts = _s.WorldState.Towers.Get(found);
                        ts.HpMax = hpMax;
                        ts.Hp = hpMax;
                        ts.AmmoCap = ammoMax;
                        if (ts.Ammo > ts.AmmoCap) ts.Ammo = ts.AmmoCap;
                        _s.WorldState.Towers.Set(found, ts);

                        b.Ammo = ts.Ammo;
                        _s.WorldState.Buildings.Set(o.TargetBuilding, b);
                    }
                }
            }
            catch { }

            // WorldIndex refresh (tags may change)
            try { _s.WorldIndex?.OnBuildingDestroyed(o.TargetBuilding); } catch { }
            try { _s.WorldIndex?.OnBuildingCreated(o.TargetBuilding); } catch { }

            // Event
            _s.EventBus.Publish(new BuildingUpgradedEvent(fromId, toId, o.TargetBuilding));

            _s.NotificationService?.Push(
                key: $"UpgradeComplete_{o.TargetBuilding.Value}",
                title: "Construction",
                body: $"Upgraded: {fromId} -> {toId} (Lv {b.Level})",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(o.TargetBuilding, default, toId),
                cooldownSeconds: 0.25f,
                dedupeByKey: true
            );

            o.Completed = true;
        }

        private float ComputeWorkSecondsTotal(BuildingDef def)
        {
            int chunks = def.BuildChunksL1 > 0 ? def.BuildChunksL1 : (_s.Balance != null ? _s.Balance.FallbackBuildChunksL1 : 2);

            float chunkSec = _s.Balance != null ? _s.Balance.BuildChunkSec : 6f;
            int builderTier = _s.Balance != null ? _s.Balance.GetBuilderTier() : 1;
            float mult = _s.Balance != null ? _s.Balance.GetBuildSpeedMult(builderTier) : 1f;

            float total = chunks * chunkSec * mult;
            if (total < 0.1f) total = 0.1f;
            return total;
        }

        public void ClearAll()
        {
            // best-effort unsubscribe (avoid double handlers on domain reload / re-init)
            if (_busSubscribed && _s.EventBus != null)
            {
                _s.EventBus.Unsubscribe<BuildOrderAutoRoadCreatedEvent>(OnAutoRoadCreated);
                _busSubscribed = false;
            }

            _nextOrderId = 1;
            _active.Clear();
            _orders.Clear();
            _deliverJobsBySite.Clear();
            _workJobBySite.Clear();
            _buildingIdsBuf.Clear();
            _autoRoadByOrder.Clear();
            _repairJobByOrder.Clear();
        }

        public int RebuildActivePlaceOrdersFromSitesAfterLoad()
        {
            EnsureBusSubscribed();

            // Reset internal runtime tracking (jobs will be re-created by Tick)
            _nextOrderId = 1;
            _active.Clear();
            _orders.Clear();
            _deliverJobsBySite.Clear();
            _workJobBySite.Clear();
            _autoRoadByOrder.Clear();
            _repairJobByOrder.Clear();

            if (_s?.WorldState?.Sites == null || _s.WorldState.Buildings == null)
                return 0;

            // Index placeholders: (anchor + defId) -> buildingId (prefer smallest id)
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

            // Sites in deterministic order
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
                        // Best-effort warning (don’t create new placeholder silently)
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

                    int orderId = _nextOrderId++;

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

                    int orderId = _nextOrderId++;
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
                    _orders[orderId] = order; _active.Add(orderId);
                    created++;
                    continue;
                }                
            }

            return created;
        }

        // + ADD: deterministic cheap key (same spirit as SaveLoadApplier.Pack)
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

        private static bool IsReadyToWork(in BuildSiteState site)
        {
            return site.RemainingCosts == null || site.RemainingCosts.Count == 0;
        }

        private BuildingId ResolveBuildWorkplace()
        {
            _buildingIdsBuf.Clear();
            foreach (var id in _s.WorldState.Buildings.Ids) _buildingIdsBuf.Add(id);
            _buildingIdsBuf.Sort((a, b) => a.Value.CompareTo(b.Value));

            // Prefer HQ (constructed)
            for (int i = 0; i < _buildingIdsBuf.Count; i++)
            {
                var bid = _buildingIdsBuf[i];
                if (!_s.WorldState.Buildings.Exists(bid)) continue;

                var bs = _s.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                var def = _s.DataRegistry.GetBuilding(bs.DefId);
                if (def.IsHQ) return bid;
            }

            // Fallback: any workplace with Build role (if you have such defs)
            for (int i = 0; i < _buildingIdsBuf.Count; i++)
            {
                var bid = _buildingIdsBuf[i];
                if (!_s.WorldState.Buildings.Exists(bid)) continue;

                var bs = _s.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                var def = _s.DataRegistry.GetBuilding(bs.DefId);
                if ((def.WorkRoles & WorkRoleFlags.Build) != 0) return bid;
            }

            return default;
        }

        private void EnsureBuildJobsForSite(SiteId siteId, BuildSiteState site, BuildingDef def, BuildingId workplace)
        {
            if (_s.JobBoard == null) return;

            // prune stale tracked jobs
            if (_deliverJobsBySite.TryGetValue(siteId.Value, out var list))
                PruneTerminal(list);

            if (_workJobBySite.TryGetValue(siteId.Value, out var wid))
            {
                if (!_s.JobBoard.TryGet(wid, out var wj) || IsTerminal(wj.Status))
                    _workJobBySite.Remove(siteId.Value);
            }

            // New behavior: 1 builder job handles BOTH delivery + build.
            // So we never enqueue BuildDeliver jobs. Also cancel any legacy deliver jobs still tracked.
            CancelDeliveryJobs(siteId);

            // Ensure 1 BuildWork job always exists while site is active (even when not ready).
            if (!_workJobBySite.ContainsKey(siteId.Value))
            {
                var j = new Job
                {
                    Archetype = JobArchetype.BuildWork,
                    Status = JobStatus.Created,
                    Workplace = workplace,

                    SourceBuilding = default,
                    DestBuilding = default,
                    Site = siteId,
                    Tower = default,

                    // BuildWorkExecutor will reuse these fields for its internal delivery phases:
                    // ResourceType = current hauling resource
                    // Amount = carried amount
                    ResourceType = 0,
                    Amount = 0,

                    TargetCell = site.Anchor,
                    CreatedAt = 0
                };

                var newId = _s.JobBoard.Enqueue(j);
                _workJobBySite[siteId.Value] = newId;
            }
        }

        private void CancelTrackedJobsForSite(SiteId siteId)
        {
            CancelDeliveryJobs(siteId);

            if (_workJobBySite.TryGetValue(siteId.Value, out var wid))
            {
                _s.JobBoard.Cancel(wid);
                _workJobBySite.Remove(siteId.Value);
            }
        }

        private void CancelRepairJob(int orderId)
        {
            if (_repairJobByOrder.TryGetValue(orderId, out var jid))
            {
                _s.JobBoard.Cancel(jid);
                _repairJobByOrder.Remove(orderId);
            }
        }

        private void CancelDeliveryJobs(SiteId siteId)
        {
            if (_deliverJobsBySite.TryGetValue(siteId.Value, out var list))
            {
                for (int i = 0; i < list.Count; i++)
                    _s.JobBoard.Cancel(list[i]);
                list.Clear();
                _deliverJobsBySite.Remove(siteId.Value);
            }
        }

        private void PruneTerminal(List<JobId> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var id = list[i];
                if (!_s.JobBoard.TryGet(id, out var j) || IsTerminal(j.Status))
                    list.RemoveAt(i);
            }
        }

        private static bool IsTerminal(JobStatus s)
        {
            return s == JobStatus.Completed || s == JobStatus.Failed || s == JobStatus.Cancelled;
        }
        // -------------------------
        // Day21: Cancel refund policy
        // -------------------------
        private void RefundDeliveredToNearestStorage(in BuildSiteState st)
        {
            if (_s.WorldState == null || _s.StorageService == null || _s.WorldIndex == null) return;
            if (st.DeliveredSoFar == null || st.DeliveredSoFar.Count == 0) return;

            var whs = _s.WorldIndex.Warehouses;
            if (whs == null || whs.Count == 0) return;

            // Build candidate list once (deterministic)
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

            // Refund each delivered cost line to nearest storage/HQ (best-effort).
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

        // VS2 Day18: helpers for site cost tracking (delivery gate)
        private static List<CostDef> CloneCostsOrEmpty(CostDef[] arr)
        {
            if (arr == null || arr.Length == 0) return new List<CostDef>(0);

            var list = new List<CostDef>(arr.Length);
            for (int i = 0; i < arr.Length; i++)
            {
                var c = arr[i];
                if (c == null) continue;

                int amt = c.Amount;
                if (amt <= 0) continue;

                list.Add(new CostDef { Resource = c.Resource, Amount = amt });
            }
            return list;
        }

        private static List<CostDef> BuildDeliveredMirror(CostDef[] arr)
        {
            if (arr == null || arr.Length == 0) return new List<CostDef>(0);

            var list = new List<CostDef>(arr.Length);
            for (int i = 0; i < arr.Length; i++)
            {
                var c = arr[i];
                if (c == null) continue;

                // keep only positive cost lines
                if (c.Amount <= 0) continue;

                list.Add(new CostDef { Resource = c.Resource, Amount = 0 });
            }
            return list;
        }
    }
}
