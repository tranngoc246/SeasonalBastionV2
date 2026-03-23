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

        private readonly BuildOrderWorkplaceResolver _workplaceResolver;
        private readonly BuildOrderReloadService _reloadService;
        private readonly BuildOrderCompletionService _completionService;
        private readonly BuildOrderCreationService _creationService;
        private readonly BuildOrderTickProcessor _tickProcessor;

        public event Action<int> OnOrderCompleted;

        public BuildOrderService(GameServices s)
        {
            _s = s;
            _workplaceResolver = new BuildOrderWorkplaceResolver(s);
            _reloadService = new BuildOrderReloadService(
                s,
                _orders,
                _active,
                _deliverJobsBySite,
                _workJobBySite,
                _autoRoadByOrder,
                _repairJobByOrder,
                EnsureBusSubscribed,
                ResetRuntimeTracking,
                AllocateOrderId);
            _completionService = new BuildOrderCompletionService(
                s,
                CancelTrackedJobsForSite,
                RemoveAutoRoadByOrder);
            _creationService = new BuildOrderCreationService(
                s,
                _orders,
                _active,
                EnsureBusSubscribed,
                AllocateOrderId,
                ComputeWorkSecondsTotal,
                ComputeWorkSecondsTotalFromChunks,
                ComputeRepairSeconds,
                CloneCostsOrEmpty,
                BuildDeliveredMirror);
            _tickProcessor = new BuildOrderTickProcessor(
                s,
                _orders,
                _active,
                ResolveBuildWorkplace,
                EnsureBuildJobsForSite,
                CancelTrackedJobsForSite,
                TickRepairOrder,
                CompletePlaceOrder,
                CompleteUpgradeOrder,
                RaiseOrderCompleted);
        }

        public int CreatePlaceOrder(string buildingDefId, CellPos anchor, Dir4 rotation)
        {
            return _creationService.CreatePlaceOrder(buildingDefId, anchor, rotation);
        }

        public int CreateUpgradeOrder(BuildingId building)
        {
            return _creationService.CreateUpgradeOrder(building);
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
            return _creationService.CreateRepairOrder(building);
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

            var workplace = _s.Balance != null ? _s.Balance.ResolveBuilderWorkplace() : _workplaceResolver.ResolveBuildWorkplace();
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
                if (_s.DataRegistry.TryGetBuilding(bs.DefId, out var repairDef) && repairDef != null)
                    mhp = Math.Max(1, repairDef.MaxHp);
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
            _completionService.CompletePlace(ref o);
            return;
        }

        private void CompleteUpgradeOrder(ref BuildOrder o)
        {
            _completionService.CompleteUpgrade(ref o);
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

            ResetRuntimeTracking();
            _buildingIdsBuf.Clear();
        }

        private void ResetRuntimeTracking()
        {
            _nextOrderId = 1;
            _active.Clear();
            _orders.Clear();
            _deliverJobsBySite.Clear();
            _workJobBySite.Clear();
            _autoRoadByOrder.Clear();
            _repairJobByOrder.Clear();
        }

        private int AllocateOrderId() => _nextOrderId++;

        private void RemoveAutoRoadByOrder(int orderId)
        {
            _autoRoadByOrder.Remove(orderId);
        }

        private void RaiseOrderCompleted(int orderId)
        {
            OnOrderCompleted?.Invoke(orderId);
        }

        public int RebuildActivePlaceOrdersFromSitesAfterLoad()
        {
            return _reloadService.RebuildActivePlaceOrdersFromSitesAfterLoad();
        }

        private static bool IsReadyToWork(in BuildSiteState site)
        {
            return site.RemainingCosts == null || site.RemainingCosts.Count == 0;
        }

        private BuildingId ResolveBuildWorkplace()
        {
            return _workplaceResolver.ResolveBuildWorkplace();
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