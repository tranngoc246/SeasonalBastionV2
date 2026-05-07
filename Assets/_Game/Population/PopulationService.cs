using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class PopulationService : IPopulationService
    {
        private const int FoodPerNpcPerDay = 5;
        private const float GrowthDaysPerNpc = 1f;
        private const int FoodReserveDaysRequiredForGrowth = 2;
        private const string DefaultPopulationNpcDefId = "NPC_HQ_Worker";
        private const string FallbackPopulationNpcDefId = "npc_villager_t1";

        private readonly IEventBus _eventBus;
        private readonly IDataRegistry _dataRegistry;
        private readonly INotificationService _notificationService;
        private readonly IWorldState _worldState;
        private readonly IGridMap _gridMap;
        private readonly IStorageService _storageService;
        private readonly IRunOutcomeService _runOutcomeService;
        private readonly PopulationHousingPolicy _housingPolicy;
        private readonly PopulationGrowthPolicy _growthPolicy;
        private PopulationState _state;
        private bool _ignoreNextDayStartedEvent;
        private bool _loadingSnapshot;

        public PopulationState State => _state;

        public PopulationService(
            IEventBus eventBus,
            IDataRegistry dataRegistry,
            IRunClock runClock,
            INotificationService notificationService,
            IWorldState worldState,
            IGridMap gridMap,
            IStorageService storageService,
            IRunOutcomeService runOutcomeService)
        {
            _eventBus = eventBus;
            _dataRegistry = dataRegistry;
            _notificationService = notificationService;
            _worldState = worldState;
            _gridMap = gridMap;
            _storageService = storageService;
            _runOutcomeService = runOutcomeService;
            _housingPolicy = new PopulationHousingPolicy(dataRegistry);
            _growthPolicy = new PopulationGrowthPolicy(runClock, FoodReserveDaysRequiredForGrowth);
            _eventBus?.Subscribe<DayStartedEvent>(OnDayStartedEvent);
            _eventBus?.Subscribe<BuildingPlacedEvent>(OnBuildingPlaced);
            _eventBus?.Subscribe<BuildingUpgradedEvent>(OnBuildingUpgraded);
            Reset();
        }

        public void Reset()
        {
            _ignoreNextDayStartedEvent = true;
            _loadingSnapshot = false;
            _state = default;
            RebuildDerivedState();
        }

        public void RebuildDerivedState()
        {
            _state.PopulationCurrent = CountPopulationCurrent();
            _state.PopulationCap = _housingPolicy.CountPopulationCap(_worldState);
            _state.DailyFoodNeed = _state.PopulationCurrent * FoodPerNpcPerDay;
        }

        public void LoadState(float growthProgressDays, int starvationDays, bool starvedToday)
        {
            _loadingSnapshot = true;
            _state.GrowthProgressDays = Math.Max(0f, growthProgressDays);
            _state.StarvationDays = Math.Max(0, starvationDays);
            _state.StarvedToday = starvedToday;
            RebuildDerivedState();
        }

        public void OnDayStarted()
        {
            RebuildDerivedState();

            int availableFood = _storageService?.GetTotal(ResourceType.Food) ?? 0;
            int need = _state.DailyFoodNeed;
            int consumed = ConsumeFoodDeterministic(need);
            UpdateStarvationState(need, consumed);

            RebuildDerivedState();
            if (!_growthPolicy.CanGrowToday(_state, availableFood))
                return;

            AdvanceGrowth();
        }

        private void OnDayStartedEvent(DayStartedEvent ev)
        {
            if (_loadingSnapshot)
            {
                _loadingSnapshot = false;
                return;
            }

            if (_ignoreNextDayStartedEvent)
            {
                _ignoreNextDayStartedEvent = false;
                return;
            }

            if (_runOutcomeService != null && _runOutcomeService.Outcome != RunOutcome.Ongoing)
                return;

            OnDayStarted();
        }

        private void OnBuildingPlaced(BuildingPlacedEvent ev)
        {
            if (_housingPolicy.ShouldRebuildForPlaced(ev.DefId))
                RebuildDerivedState();
        }

        private void OnBuildingUpgraded(BuildingUpgradedEvent ev)
        {
            if (_housingPolicy.ShouldRebuildForUpgrade(ev.FromDefId, ev.ToDefId))
                RebuildDerivedState();
        }

        private void UpdateStarvationState(int need, int consumed)
        {
            bool starved = consumed < need;
            _state.StarvedToday = starved;
            if (!starved)
            {
                _state.StarvationDays = 0;
                return;
            }

            _state.StarvationDays++;
            _notificationService?.Push(
                key: "population.food.shortage",
                title: "Thiếu lương thực",
                body: $"Hôm nay cần {need} Food nhưng chỉ tiêu thụ được {consumed}. Dân số sẽ chưa thể tăng.",
                severity: NotificationSeverity.Warning,
                payload: default,
                cooldownSeconds: 10f,
                dedupeByKey: true);
        }

        private void AdvanceGrowth()
        {
            _state.GrowthProgressDays += 1f;
            while (_state.GrowthProgressDays >= GrowthDaysPerNpc)
            {
                if (!TrySpawnNewVillager())
                    break;

                _state.GrowthProgressDays -= GrowthDaysPerNpc;
                RebuildDerivedState();
                NotifyPopulationGrowth();

                int availableFood = _storageService?.GetTotal(ResourceType.Food) ?? 0;
                if (!_growthPolicy.CanGrowToday(_state, availableFood))
                    break;
            }
        }

        private void NotifyPopulationGrowth()
        {
            _notificationService?.Push(
                key: $"population.new.npc.{_state.PopulationCurrent}",
                title: "Có NPC mới",
                body: $"Dân số đã tăng lên {_state.PopulationCurrent}/{_state.PopulationCap}. Hãy giao việc cho NPC mới khi phù hợp.",
                severity: NotificationSeverity.Info,
                payload: default,
                cooldownSeconds: 5f,
                dedupeByKey: true);
        }

        private int ConsumeFoodDeterministic(int need)
        {
            if (need <= 0 || _storageService == null || _worldState?.Buildings == null)
                return 0;

            int left = need;
            int consumed = 0;

            var ids = new List<BuildingId>();
            foreach (var id in _worldState.Buildings.Ids)
                ids.Add(id);
            ids.Sort((a, b) => a.Value.CompareTo(b.Value));

            for (int i = 0; i < ids.Count && left > 0; i++)
            {
                var buildingId = ids[i];
                if (!_storageService.CanStore(buildingId, ResourceType.Food))
                    continue;

                int removed = _storageService.Remove(buildingId, ResourceType.Food, left);
                if (removed <= 0)
                    continue;

                consumed += removed;
                left -= removed;
            }

            return consumed;
        }

        private int CountPopulationCurrent()
        {
            if (_worldState?.Npcs == null)
                return 0;

            int count = 0;
            foreach (var id in _worldState.Npcs.Ids)
            {
                if (_worldState.Npcs.Exists(id))
                    count++;
            }
            return count;
        }

        private bool TrySpawnNewVillager()
        {
            if (_worldState?.Npcs == null)
                return false;

            var spawn = ResolveSpawnCellNearHq();
            string npcDefId = ResolveDefaultPopulationNpcDefId();
            var state = new NpcState
            {
                DefId = npcDefId,
                Cell = spawn,
                Workplace = default,
                CurrentJob = default,
                IsIdle = true
            };

            var id = _worldState.Npcs.Create(state);
            state.Id = id;
            _worldState.Npcs.Set(id, state);
            return true;
        }

        private CellPos ResolveSpawnCellNearHq()
        {
            var hq = FindPrimaryHq();
            var desired = hq.HasValue ? FindHqApproachCell(hq.Value) : new CellPos(0, 0);
            return ResolveSpawnCell(desired);
        }

        private BuildingId? FindPrimaryHq()
        {
            if (_worldState?.Buildings == null || _dataRegistry == null)
                return null;

            BuildingId best = default;
            foreach (var buildingId in _worldState.Buildings.Ids)
            {
                if (!_worldState.Buildings.Exists(buildingId))
                    continue;

                var building = _worldState.Buildings.Get(buildingId);
                if (!building.IsConstructed)
                    continue;

                var def = _dataRegistry.GetBuilding(building.DefId);
                if (def == null || !def.IsHQ)
                    continue;

                if (best.Value == 0 || buildingId.Value < best.Value)
                    best = buildingId;
            }

            return best.Value != 0 ? best : null;
        }

        private CellPos FindHqApproachCell(BuildingId hq)
        {
            var building = _worldState.Buildings.Get(hq);
            var def = _dataRegistry.GetBuilding(building.DefId);
            if (def == null)
                return building.Anchor;

            int x = building.Anchor.X + Math.Max(0, def.SizeX / 2);
            int y = building.Anchor.Y - 1;
            return new CellPos(x, y);
        }

        private string ResolveDefaultPopulationNpcDefId()
        {
            if (_dataRegistry == null)
                return DefaultPopulationNpcDefId;

            if (_dataRegistry.TryGetNpc(DefaultPopulationNpcDefId, out var _))
                return DefaultPopulationNpcDefId;

            if (_dataRegistry.TryGetNpc(FallbackPopulationNpcDefId, out var _))
                return FallbackPopulationNpcDefId;

            return DefaultPopulationNpcDefId;
        }

        private CellPos ResolveSpawnCell(CellPos desired)
        {
            if (_gridMap == null)
                return desired;

            if (IsPreferredSpawnCell(desired))
                return desired;

            var empty = FindNearbyCell(desired, CellOccupancyKind.Empty);
            if (empty.HasValue)
                return empty.Value;

            var road = FindNearbyCell(desired, CellOccupancyKind.Road);
            if (road.HasValue)
                return road.Value;

            if (_gridMap.IsInside(desired))
                return desired;

            int x = Math.Clamp(desired.X, 0, _gridMap.Width - 1);
            int y = Math.Clamp(desired.Y, 0, _gridMap.Height - 1);
            return new CellPos(x, y);
        }

        private bool IsPreferredSpawnCell(CellPos cell)
            => _gridMap.IsInside(cell) && _gridMap.Get(cell).Kind == CellOccupancyKind.Empty;

        private CellPos? FindNearbyCell(CellPos desired, CellOccupancyKind wanted)
        {
            const int maxR = 8;

            bool IsMatch(CellPos cell)
            {
                if (!_gridMap.IsInside(cell))
                    return false;
                return _gridMap.Get(cell).Kind == wanted;
            }

            for (int r = 1; r <= maxR; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    int ax = r - Math.Abs(dy);
                    int dx1 = -ax;
                    int dx2 = ax;

                    var c1 = new CellPos(desired.X + dx1, desired.Y + dy);
                    if (IsMatch(c1)) return c1;

                    if (dx2 != dx1)
                    {
                        var c2 = new CellPos(desired.X + dx2, desired.Y + dy);
                        if (IsMatch(c2)) return c2;
                    }
                }
            }

            return null;
        }
    }
}
