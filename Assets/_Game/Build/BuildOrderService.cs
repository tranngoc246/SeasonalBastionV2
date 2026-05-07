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
        private readonly BuildOrderTimePolicy _timePolicy;
        private readonly BuildOrderRepairService _repairService;

        public event Action<int> OnOrderCompleted;

        public BuildOrderService(GameServices s)
        {
            _s = s;
            _eventBridge = new BuildOrderEventBridge(s.EventBus, _autoRoadByOrder);
            _buildJobOrchestrator = s.BuildJobOrchestrator ?? new BuildJobPlanner(s, _deliverJobsBySite, _workJobBySite);
            if (_s.BuildJobOrchestrator == null)
                _s.BuildJobOrchestrator = _buildJobOrchestrator;
            _costTracker = new BuildOrderCostTracker();
            _timePolicy = new BuildOrderTimePolicy(s.Balance);
            _cancellationService = new BuildOrderCancellationService(
                s.WorldState,
                s.GridMap,
                s.WorldIndex,
                s.StorageService,
                s.DataRegistry,
                s.EventBus,
                s.NotificationService,
                s.JobBoard,
                _destroyPlaceholderOnCancel,
                _autoRoadByOrder,
                _repairJobByOrder,
                CancelTrackedJobsForSite);
            _reloadService = new BuildOrderReloadService(
                s.WorldState,
                s.NotificationService,
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
                s.WorldState,
                s.GridMap,
                s.DataRegistry,
                s.WorldIndex,
                s.EventBus,
                s.NotificationService,
                s.SaveService,
                s.RunClock,
                CancelTrackedJobsForSite,
                RemoveAutoRoadByOrder);
            _creationService = new BuildOrderCreationService(
                s.DataRegistry,
                s.WorldState,
                s.GridMap,
                s.EventBus,
                s.NotificationService,
                s.StorageService,
                s.UnlockService,
                s.PlacementService,
                s.Pathfinder,
                _orders,
                _active,
                _eventBridge.EnsureSubscribed,
                AllocateOrderId,
                _timePolicy.ComputeWorkSecondsTotal,
                _timePolicy.ComputeWorkSecondsTotalFromChunks,
                _timePolicy.ComputeRepairSeconds,
                _costTracker.CloneCostsOrEmpty,
                _costTracker.BuildDeliveredMirror);
            _repairService = new BuildOrderRepairService(
                s.WorldState,
                s.DataRegistry,
                s.NotificationService,
                s.JobBoard,
                _repairJobByOrder,
                _cancellationService.CancelRepairJob);
            _tickProcessor = new BuildOrderTickProcessor(
                s,
                _orders,
                _active,
                ResolveBuildWorkplace,
                EnsureBuildJobsForSite,
                CancelTrackedJobsForSite,
                _repairService.TickRepairOrder,
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

        private void CompletePlaceOrder(ref BuildOrder o)
            => _completionService.CompletePlace(ref o);

        private void CompleteUpgradeOrder(ref BuildOrder o)
            => _completionService.CompleteUpgrade(ref o);
    }
}
