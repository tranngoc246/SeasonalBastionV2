using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;

namespace SeasonalBastion
{
    public sealed class BuildOrderService : IBuildOrderService, ITickable
    {
        private readonly GameServices _s;
        private int _nextOrderId = 1;

        private readonly List<int> _active = new();
        private readonly Dictionary<int, BuildOrder> _orders = new();

        private readonly bool _destroyPlaceholderOnCancel = true;

        private readonly Dictionary<int, List<JobId>> _deliverJobsBySite = new();
        private readonly Dictionary<int, JobId> _workJobBySite = new();
        private readonly Dictionary<int, JobId> _repairJobByOrder = new();
        private readonly Dictionary<int, CellPos> _autoRoadByOrder = new();

        private readonly BuildOrderReloadService _reloadService;
        private readonly BuildOrderCompletionService _completionService;
        private readonly BuildOrderCreationService _creationService;
        private readonly BuildOrderTickProcessor _tickProcessor;
        private readonly BuildOrderEventBridge _eventBridge;
        private readonly IBuildJobOrchestrator _buildJobOrchestrator;
        private readonly BuildOrderCancellationService _cancellationService;
        private readonly BuildOrderCostTracker _costTracker;

        public event Action<int> OnOrderCompleted;

        public BuildOrderService(GameServices s)
        {
            _s = s;
            _eventBridge = new BuildOrderEventBridge(s, _autoRoadByOrder);
            _buildJobOrchestrator = s.BuildJobOrchestrator ?? new BuildJobPlanner(s, _deliverJobsBySite, _workJobBySite);
            if (_s.BuildJobOrchestrator == null)
                _s.BuildJobOrchestrator = _buildJobOrchestrator;
            _costTracker = new BuildOrderCostTracker();
            _cancellationService = new BuildOrderCancellationService(
                s,
                _destroyPlaceholderOnCancel,
                _autoRoadByOrder,
                _repairJobByOrder,
                CancelTrackedJobsForSite);
            _reloadService = new BuildOrderReloadService(
                s,
                _orders,
                _active,
                _deliverJobsBySite,
                _workJobBySite,
                _autoRoadByOrder,
                _repairJobByOrder,
                _eventBridge.EnsureSubscribed,
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
                _eventBridge.EnsureSubscribed,
                AllocateOrderId,
                ComputeWorkSecondsTotal,
                ComputeWorkSecondsTotalFromChunks,
                ComputeRepairSeconds,
                _costTracker.CloneCostsOrEmpty,
                _costTracker.BuildDeliveredMirror);
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
            => _creationService.CreatePlaceOrder(buildingDefId, anchor, rotation);

        public int CreateUpgradeOrder(BuildingId building)
            => _creationService.CreateUpgradeOrder(building);

        public int CreateRepairOrder(BuildingId building)
            => _creationService.CreateRepairOrder(building);

        public bool TryGet(int orderId, out BuildOrder order) => _orders.TryGetValue(orderId, out order);

        public void Cancel(int orderId)
        {
            if (!_orders.TryGetValue(orderId, out var o)) return;
            if (o.Completed) return;

            _cancellationService.Cancel(ref o);
            _orders.Remove(orderId);
            _active.Remove(orderId);
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

        public void Tick(float dt)
        {
            _eventBridge.EnsureSubscribed();
            _tickProcessor.Tick(dt);
        }

        public void ClearAll()
        {
            _eventBridge.Unsubscribe();
            ResetRuntimeTracking();
        }

        public int RebuildActivePlaceOrdersFromSitesAfterLoad()
            => _reloadService.RebuildActivePlaceOrdersFromSitesAfterLoad();

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

        private BuildingId ResolveBuildWorkplace()
        {
            if (_s.BuildWorkplaceResolver != null)
                return _s.BuildWorkplaceResolver.ResolveBuildWorkplace();

            if (_s.Balance != null)
                return _s.Balance.ResolveBuilderWorkplace();

            return default;
        }

        private void EnsureBuildJobsForSite(SiteId siteId, BuildSiteState site, BuildingId workplace)
            => _buildJobOrchestrator.EnsureBuildJobsForSite(siteId, site, workplace);

        private void CancelTrackedJobsForSite(SiteId siteId)
            => _buildJobOrchestrator.CancelTrackedJobsForSite(siteId);

        private void TickRepairOrder(int orderId, ref BuildOrder o, BuildingId workplace)
        {
            if (_s.JobBoard == null) return;

            if (!_s.WorldState.Buildings.Exists(o.TargetBuilding))
            {
                _cancellationService.CancelRepairJob(orderId);
                o.Completed = true;
                return;
            }

            var bs = _s.WorldState.Buildings.Get(o.TargetBuilding);
            if (!bs.IsConstructed)
            {
                _cancellationService.CancelRepairJob(orderId);
                o.Completed = true;
                return;
            }

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
                _cancellationService.CancelRepairJob(orderId);
                o.Completed = true;
                _s.NotificationService?.Push(
                    key: $"RepairDone_{o.TargetBuilding.Value}",
                    title: "Construction",
                    body: $"Repair completed: {bs.DefId}",
                    severity: NotificationSeverity.Info,
                    payload: new NotificationPayload(o.TargetBuilding, default, bs.DefId),
                    cooldownSeconds: 0.25f,
                    dedupeByKey: true);
                return;
            }

            if (_repairJobByOrder.TryGetValue(orderId, out var jid))
            {
                if (!_s.JobBoard.TryGet(jid, out var j) || IsTerminal(j.Status))
                {
                    _repairJobByOrder.Remove(orderId);
                }
                else
                {
                    // Retarget recoverable queued repair jobs when builder availability changes
                    // (BuilderHut preferred, HQ fallback if BuilderHut has no idle worker).
                    if (j.Status == JobStatus.Created && j.Workplace.Value != workplace.Value)
                    {
                        j.Workplace = workplace;
                        _s.JobBoard.Update(j);
                    }
                    return;
                }
            }

            var job = new Job
            {
                Archetype = JobArchetype.RepairWork,
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

            var newId = _s.JobBoard.Enqueue(job);
            _repairJobByOrder[orderId] = newId;
        }

        private void CompletePlaceOrder(ref BuildOrder o)
            => _completionService.CompletePlace(ref o);

        private void CompleteUpgradeOrder(ref BuildOrder o)
            => _completionService.CompleteUpgrade(ref o);

        private static bool IsTerminal(JobStatus s)
            => s == JobStatus.Completed || s == JobStatus.Failed || s == JobStatus.Cancelled;
    }
}
