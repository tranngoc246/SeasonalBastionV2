using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;

namespace SeasonalBastion
{
    public sealed class BuildOrderService : IBuildOrderService, ITickable
    {
        private readonly GameServices _services;
        private readonly List<int> _active = new();
        private readonly Dictionary<int, BuildOrder> _orders = new();
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

        private int _nextOrderId = 1;

        public event Action<int> OnOrderCompleted;

        public BuildOrderService(GameServices services)
        {
            _services = services;
            _eventBridge = new BuildOrderEventBridge(services.EventBus, _autoRoadByOrder);
            _buildJobOrchestrator = services.BuildJobOrchestrator ?? new BuildJobPlanner(services.WorldState, services.JobBoard, services.Pathfinder, services.DataRegistry, services.GridMap, _deliverJobsBySite, _workJobBySite);
            if (_services.BuildJobOrchestrator == null)
                _services.BuildJobOrchestrator = _buildJobOrchestrator;

            _costTracker = new BuildOrderCostTracker();
            _timePolicy = new BuildOrderTimePolicy(services.Balance);

            _cancellationService = new BuildOrderCancellationService(
                services.WorldState,
                services.GridMap,
                services.WorldIndex,
                services.StorageService,
                services.DataRegistry,
                services.EventBus,
                services.NotificationService,
                services.JobBoard,
                destroyPlaceholderOnCancel: true,
                _autoRoadByOrder,
                _repairJobByOrder,
                CancelTrackedJobsForSite);

            _reloadService = new BuildOrderReloadService(
                services.WorldState,
                services.NotificationService,
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
                services.WorldState,
                services.GridMap,
                services.DataRegistry,
                services.WorldIndex,
                services.EventBus,
                services.NotificationService,
                services.SaveService,
                services.RunClock,
                CancelTrackedJobsForSite,
                RemoveAutoRoadByOrder);

            _creationService = new BuildOrderCreationService(
                services.DataRegistry,
                services.WorldState,
                services.GridMap,
                services.EventBus,
                services.NotificationService,
                services.StorageService,
                services.UnlockService,
                services.PlacementService,
                services.Pathfinder,
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
                services.WorldState,
                services.DataRegistry,
                services.NotificationService,
                services.JobBoard,
                _repairJobByOrder,
                _cancellationService.CancelRepairJob);

            _tickProcessor = new BuildOrderTickProcessor(
                services.WorldState,
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
            if (!_orders.TryGetValue(orderId, out var order) || order.Completed)
                return;

            _cancellationService.Cancel(ref order);
            _orders.Remove(orderId);
            _active.Remove(orderId);
        }

        public bool CancelBySite(SiteId siteId)
            => TryCancelMatchingOrder(siteId, default, matchBySite: true);

        public bool CancelByBuilding(BuildingId buildingId)
            => TryCancelMatchingOrder(default, buildingId, matchBySite: false);

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

        private bool TryCancelMatchingOrder(SiteId siteId, BuildingId buildingId, bool matchBySite)
        {
            int targetValue = matchBySite ? siteId.Value : buildingId.Value;
            if (targetValue == 0) return false;

            for (int i = 0; i < _active.Count; i++)
            {
                int id = _active[i];
                if (!_orders.TryGetValue(id, out var order) || order.Completed)
                    continue;

                int candidate = matchBySite ? order.Site.Value : order.TargetBuilding.Value;
                if (candidate != targetValue)
                    continue;

                Cancel(id);
                return true;
            }

            return false;
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
            => _autoRoadByOrder.Remove(orderId);

        private void RaiseOrderCompleted(int orderId)
            => OnOrderCompleted?.Invoke(orderId);

        private BuildingId ResolveBuildWorkplace()
        {
            if (_services.BuildWorkplaceResolver != null)
                return _services.BuildWorkplaceResolver.ResolveBuildWorkplace();

            if (_services.Balance != null)
                return _services.Balance.ResolveBuilderWorkplace();

            return default;
        }

        private void EnsureBuildJobsForSite(SiteId siteId, BuildSiteState site, BuildingId workplace)
            => _buildJobOrchestrator.EnsureBuildJobsForSite(siteId, site, workplace);

        private void CancelTrackedJobsForSite(SiteId siteId)
            => _buildJobOrchestrator.CancelTrackedJobsForSite(siteId);

        private void CompletePlaceOrder(ref BuildOrder order)
            => _completionService.CompletePlace(ref order);

        private void CompleteUpgradeOrder(ref BuildOrder order)
            => _completionService.CompleteUpgrade(ref order);
    }
}
