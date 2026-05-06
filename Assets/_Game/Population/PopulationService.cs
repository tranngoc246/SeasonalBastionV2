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

        private readonly IEventBus _eventBus;
        private readonly IDataRegistry _dataRegistry;
        private readonly IRunClock _runClock;
        private readonly INotificationService _notificationService;
        private readonly IWorldState _worldState;
        private readonly IGridMap _gridMap;
        private readonly IStorageService _storageService;
        private readonly IRunOutcomeService _runOutcomeService;
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
            _runClock = runClock;
            _notificationService = notificationService;
            _worldState = worldState;
            _gridMap = gridMap;
            _storageService = storageService;
            _runOutcomeService = runOutcomeService;
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
            _state.PopulationCap = CountPopulationCap();
            _state.DailyFoodNeed = _state.PopulationCurrent * FoodPerNpcPerDay;
        }

        public void LoadState(float growthProgressDays, int starvationDays, bool starvedToday)
        {
            _loadingSnapshot = true;
            _state.GrowthProgressDays = growthProgressDays < 0f ? 0f : growthProgressDays;
            _state.StarvationDays = starvationDays < 0 ? 0 : starvationDays;
            _state.StarvedToday = starvedToday;
            RebuildDerivedState();
        }

        public void OnDayStarted()
        {
            RebuildDerivedState();

            int need = _state.DailyFoodNeed;
            int available = _storageService?.GetTotal(ResourceType.Food) ?? 0;
            int consumed = ConsumeFoodDeterministic(need);
            bool starved = consumed < need;

            _state.StarvedToday = starved;
            if (starved)
            {
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
            else
            {
                _state.StarvationDays = 0;
            }

            RebuildDerivedState();

            if (!CanGrowToday(available))
                return;

            _state.GrowthProgressDays += 1f;
            while (_state.GrowthProgressDays >= GrowthDaysPerNpc)
            {
                if (!TrySpawnNewVillager())
                    break;

                _state.GrowthProgressDays -= GrowthDaysPerNpc;
                RebuildDerivedState();

                _notificationService?.Push(
                    key: $"population.new.npc.{_state.PopulationCurrent}",
                    title: "Có NPC mới",
                    body: $"Dân số đã tăng lên {_state.PopulationCurrent}/{_state.PopulationCap}. Hãy giao việc cho NPC mới khi phù hợp.",
                    severity: NotificationSeverity.Info,
                    payload: default,
                    cooldownSeconds: 5f,
                    dedupeByKey: true);

                if (!CanGrowToday(_storageService?.GetTotal(ResourceType.Food) ?? 0))
                    break;
            }
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
            if (!ShouldRebuildPopulationForPlaced(ev.DefId))
                return;

            RebuildDerivedState();
        }

        private void OnBuildingUpgraded(BuildingUpgradedEvent ev)
        {
            if (!ShouldRebuildPopulationForUpgrade(ev.FromDefId, ev.ToDefId))
                return;

            RebuildDerivedState();
        }

        private bool ShouldRebuildPopulationForPlaced(string defId)
        {
            if (_dataRegistry == null || string.IsNullOrWhiteSpace(defId))
                return true;

            return _dataRegistry.TryGetBuilding(defId, out var def) && def != null && def.IsHouse;
        }

        private bool ShouldRebuildPopulationForUpgrade(string fromDefId, string toDefId)
        {
            if (_dataRegistry == null)
                return true;

            bool fromIsHouse = !string.IsNullOrWhiteSpace(fromDefId)
                && _dataRegistry.TryGetBuilding(fromDefId, out var fromDef)
                && fromDef != null
                && fromDef.IsHouse;

            bool toIsHouse = !string.IsNullOrWhiteSpace(toDefId)
                && _dataRegistry.TryGetBuilding(toDefId, out var toDef)
                && toDef != null
                && toDef.IsHouse;

            return fromIsHouse || toIsHouse;
        }

        private bool CanGrowToday(int availableFoodBeforeConsume)
        {
            if (_runClock != null && _runClock.CurrentPhase == Phase.Defend)
                return false;

            if (_state.StarvedToday)
                return false;

            if (_state.PopulationCurrent >= _state.PopulationCap)
                return false;

            int reserveRequired = _state.DailyFoodNeed * FoodReserveDaysRequiredForGrowth;
            if (availableFoodBeforeConsume < reserveRequired)
                return false;

            return true;
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
                var bid = ids[i];
                if (!_storageService.CanStore(bid, ResourceType.Food))
                    continue;

                int rem = _storageService.Remove(bid, ResourceType.Food, left);
                if (rem <= 0)
                    continue;

                consumed += rem;
                left -= rem;
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

        private int CountPopulationCap()
        {
            if (_worldState?.Buildings == null || _dataRegistry == null)
                return 0;

            int cap = 0;
            foreach (var id in _worldState.Buildings.Ids)
            {
                if (!_worldState.Buildings.Exists(id)) continue;
                var bs = _worldState.Buildings.Get(id);
                if (!bs.IsConstructed) continue;

                var def = _dataRegistry.GetBuilding(bs.DefId);
                if (def == null || !def.IsHouse) continue;

                int level = bs.Level;
                if (level < 1) level = 1;
                if (level > 3) level = 3;

                cap += level switch
                {
                    1 => 2,
                    2 => 4,
                    3 => 6,
                    _ => 2
                };
            }

            return cap;
        }

        private bool TrySpawnNewVillager()
        {
            if (_worldState?.Npcs == null)
                return false;

            var spawn = ResolveSpawnCellNearHq();
            string npcDefId = ResolveDefaultPopulationNpcDefId();
            var st = new NpcState
            {
                DefId = npcDefId,
                Cell = spawn,
                Workplace = default,
                CurrentJob = default,
                IsIdle = true
            };

            var id = _worldState.Npcs.Create(st);
            st.Id = id;
            _worldState.Npcs.Set(id, st);
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
            foreach (var bid in _worldState.Buildings.Ids)
            {
                if (!_worldState.Buildings.Exists(bid)) continue;
                var bs = _worldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;
                var def = _dataRegistry.GetBuilding(bs.DefId);
                if (def == null || !def.IsHQ) continue;

                if (best.Value == 0 || bid.Value < best.Value)
                    best = bid;
            }

            return best.Value != 0 ? best : null;
        }

        private CellPos FindHqApproachCell(BuildingId hq)
        {
            var bs = _worldState.Buildings.Get(hq);
            var def = _dataRegistry.GetBuilding(bs.DefId);
            if (def == null)
                return bs.Anchor;

            int x = bs.Anchor.X + Math.Max(0, def.SizeX / 2);
            int y = bs.Anchor.Y - 1;
            return new CellPos(x, y);
        }

        private string ResolveDefaultPopulationNpcDefId()
        {
            if (_dataRegistry == null)
                return "NPC_HQ_Worker";

            if (_dataRegistry.TryGetNpc("NPC_HQ_Worker", out var _))
                return "NPC_HQ_Worker";

            if (_dataRegistry.TryGetNpc("npc_villager_t1", out var _))
                return "npc_villager_t1";

            return "NPC_HQ_Worker";
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
        {
            if (!_gridMap.IsInside(cell)) return false;
            return _gridMap.Get(cell).Kind == CellOccupancyKind.Empty;
        }

        private CellPos? FindNearbyCell(CellPos desired, CellOccupancyKind wanted)
        {
            const int maxR = 8;

            bool IsMatch(CellPos c)
            {
                if (!_gridMap.IsInside(c)) return false;
                return _gridMap.Get(c).Kind == wanted;
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

