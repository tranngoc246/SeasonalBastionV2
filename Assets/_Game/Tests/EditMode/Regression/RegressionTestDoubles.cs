using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;

namespace SeasonalBastion.Tests.EditMode
{
    internal static class RegressionTestServiceFactory
    {
        public static GameServices MakeServices(
            IEventBus bus,
            IDataRegistry data,
            INotificationService noti,
            IRunClock clock,
            IRunOutcomeService outcome,
            IWorldState world = null,
            IGridMap grid = null,
            IPlacementService placement = null)
        {
            return new GameServices
            {
                EventBus = bus,
                DataRegistry = data,
                NotificationService = noti,
                RunClock = clock,
                RunOutcomeService = outcome,
                WorldState = world,
                GridMap = grid,
                PlacementService = placement
            };
        }
    }

    internal sealed class TestEventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _subs = new();

        public void Publish<T>(T evt) where T : struct
        {
            if (_subs.TryGetValue(typeof(T), out var list))
            {
                for (int i = 0; i < list.Count; i++)
                    ((Action<T>)list[i]).Invoke(evt);
            }
        }

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (!_subs.TryGetValue(typeof(T), out var list))
            {
                list = new List<Delegate>();
                _subs.Add(typeof(T), list);
            }
            list.Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (_subs.TryGetValue(typeof(T), out var list))
                list.Remove(handler);
        }
    }

    internal sealed class TestDataRegistry : IDataRegistry
    {
        private readonly Dictionary<string, BuildingDef> _b = new(StringComparer.Ordinal);
        private readonly Dictionary<string, WaveDef> _waves = new(StringComparer.Ordinal);
        private readonly Dictionary<string, BuildableNodeDef> _nodes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, UpgradeEdgeDef> _edgesById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<UpgradeEdgeDef>> _edgesFrom = new(StringComparer.Ordinal);

        private readonly Dictionary<string, TowerDef> _towersById = new(StringComparer.Ordinal);

        public void Add(BuildingDef def) => _b[def.DefId] = def;
        public void AddWave(WaveDef def) => _waves[def.DefId] = def;
        public void AddTower(TowerDef def)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.DefId)) return;
            _towersById[def.DefId] = def;
        }

        public void AddNode(BuildableNodeDef node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Id)) return;
            _nodes[node.Id] = node;
        }

        public void AddUpgradeEdge(UpgradeEdgeDef edge)
        {
            if (edge == null || string.IsNullOrWhiteSpace(edge.Id)) return;
            _edgesById[edge.Id] = edge;
            if (!_edgesFrom.TryGetValue(edge.From ?? string.Empty, out var list))
            {
                list = new List<UpgradeEdgeDef>();
                _edgesFrom[edge.From ?? string.Empty] = list;
            }
            list.Add(edge);
        }

        public bool TryGetBuildableNode(string id, out BuildableNodeDef node) => _nodes.TryGetValue(id, out node);
        public IReadOnlyList<UpgradeEdgeDef> GetUpgradeEdgesFrom(string fromNodeId)
            => _edgesFrom.TryGetValue(fromNodeId ?? string.Empty, out var list) ? list : Array.Empty<UpgradeEdgeDef>();
        public bool TryGetUpgradeEdge(string edgeId, out UpgradeEdgeDef edge) => _edgesById.TryGetValue(edgeId, out edge);
        public bool IsPlaceableBuildable(string nodeId) => true;

        public BuildingDef GetBuilding(string id)
        {
            if (_b.TryGetValue(id, out var def)) return def;
            throw new KeyNotFoundException($"BuildingDef not found: {id}");
        }

        public bool TryGetBuilding(string id, out BuildingDef def) => _b.TryGetValue(id, out def);

        public EnemyDef GetEnemy(string id) => throw new NotSupportedException();
        public bool TryGetEnemy(string id, out EnemyDef def) { def = default; return false; }
        public WaveDef GetWave(string id)
        {
            if (_waves.TryGetValue(id, out var def)) return def;
            throw new NotSupportedException();
        }
        public bool TryGetWave(string id, out WaveDef def) => _waves.TryGetValue(id, out def);
        public RewardDef GetReward(string id) => throw new NotSupportedException();
        public bool TryGetReward(string id, out RewardDef def) { def = default; return false; }
        public RecipeDef GetRecipe(string id) => throw new NotSupportedException();
        public bool TryGetRecipe(string id, out RecipeDef def) { def = default; return false; }
        public NpcDef GetNpc(string id) => throw new NotSupportedException();
        public bool TryGetNpc(string id, out NpcDef def) { def = default; return false; }
        public TowerDef GetTower(string id)
        {
            if (_towersById.TryGetValue(id, out var def)) return def;
            throw new NotSupportedException();
        }
        public bool TryGetTower(string id, out TowerDef def) => _towersById.TryGetValue(id, out def);

        public T GetDef<T>(string id) where T : UnityEngine.Object => throw new NotSupportedException();
        public bool TryGetDef<T>(string id, out T def) where T : UnityEngine.Object { def = default; return false; }
    }

    internal sealed class FakeRunClock : IRunClock
    {
        public Season CurrentSeason { get; private set; } = Season.Spring;
        public int DayIndex { get; private set; } = 1;
        public Phase CurrentPhase { get; private set; } = Phase.Build;

        public float TimeScale { get; private set; } = 1f;
        public bool DefendSpeedUnlocked { get; set; } = false;

        public event Action<Season, int> OnSeasonDayChanged;
        public event Action<Phase> OnPhaseChanged;
        public event Action OnDayEnded;

        public void SetTimeScale(float scale) => TimeScale = scale;

        public void ForceSeasonDay(Season s, int dayIndex)
        {
            CurrentSeason = s;
            DayIndex = dayIndex;

            var newPhase = (s == Season.Autumn || s == Season.Winter) ? Phase.Defend : Phase.Build;
            if (CurrentPhase != newPhase)
            {
                CurrentPhase = newPhase;
                OnPhaseChanged?.Invoke(newPhase);
            }

            OnSeasonDayChanged?.Invoke(s, dayIndex);
        }

        public void RaiseDayEnded() => OnDayEnded?.Invoke();
        public void RaisePhaseChanged(Phase p) { CurrentPhase = p; OnPhaseChanged?.Invoke(p); }
    }

    internal sealed class FakeRunOutcomeService : IRunOutcomeService
    {
        public RunOutcome Outcome { get; private set; } = RunOutcome.Ongoing;
        public RunEndReason Reason { get; private set; } = RunEndReason.None;

        public int ResetCalled { get; private set; } = 0;

        public void ResetOutcome()
        {
            ResetCalled++;
            Outcome = RunOutcome.Ongoing;
            Reason = RunEndReason.None;
        }

        public void Defeat()
        {
            Outcome = RunOutcome.Defeat;
            Reason = RunEndReason.HqDestroyed;
            OnRunEnded?.Invoke(Outcome);
        }

        public void Victory()
        {
            Outcome = RunOutcome.Victory;
            Reason = RunEndReason.SurvivedWinterYear2;
            OnRunEnded?.Invoke(Outcome);
        }

        public void Abort()
        {
            Outcome = RunOutcome.Abort;
            Reason = RunEndReason.Aborted;
            OnRunEnded?.Invoke(Outcome);
        }

        public event Action<RunOutcome> OnRunEnded;
    }

    internal sealed class FakeWaveCalendarResolver : IWaveCalendarResolver
    {
        private readonly IReadOnlyList<WaveDef> _waves;

        public FakeWaveCalendarResolver(params WaveDef[] waves)
        {
            _waves = waves ?? Array.Empty<WaveDef>();
        }

        public IReadOnlyList<WaveDef> Resolve(int year, Season season, int day) => _waves;
    }

    internal sealed class FakeStorageService : IStorageService
    {
        private readonly Dictionary<(int building, ResourceType type), int> _amounts = new();
        private readonly Dictionary<(int building, ResourceType type), int> _caps = new();
        private readonly HashSet<(int building, ResourceType type)> _blocked = new();

        public void SetCap(BuildingId building, ResourceType type, int cap) => _caps[(building.Value, type)] = cap;
        public void SetAmount(BuildingId building, ResourceType type, int amount) => _amounts[(building.Value, type)] = Math.Max(0, amount);
        public void SetCanStore(BuildingId building, ResourceType type, bool canStore)
        {
            var key = (building.Value, type);
            if (canStore) _blocked.Remove(key);
            else _blocked.Add(key);
        }

        public StorageSnapshot GetStorage(BuildingId building) => default;
        public bool CanStore(BuildingId building, ResourceType type) => !_blocked.Contains((building.Value, type)) && GetCap(building, type) > 0;
        public int GetAmount(BuildingId building, ResourceType type) => _amounts.TryGetValue((building.Value, type), out var v) ? v : 0;

        // Default cap is intentionally permissive for tests that do not focus on storage caps.
        // Tests that care about caps should explicitly call SetCap(...).
        public int GetCap(BuildingId building, ResourceType type) => _caps.TryGetValue((building.Value, type), out var v) ? v : 999;
        public int Add(BuildingId building, ResourceType type, int amount)
        {
            if (amount <= 0) return 0;
            if (!CanStore(building, type)) return 0;
            int cur = GetAmount(building, type);
            int cap = GetCap(building, type);
            int free = Math.Max(0, cap - cur);
            int add = Math.Min(free, amount);
            _amounts[(building.Value, type)] = cur + add;
            return add;
        }
        public int Remove(BuildingId building, ResourceType type, int amount)
        {
            if (amount <= 0) return 0;
            int cur = GetAmount(building, type);
            int rem = Math.Min(cur, amount);
            _amounts[(building.Value, type)] = cur - rem;
            return rem;
        }
        public int GetTotal(ResourceType type)
        {
            int total = 0;
            foreach (var kv in _amounts)
                if (kv.Key.type == type) total += kv.Value;
            return total;
        }
    }

    internal sealed class FakePlacementService : IPlacementService
    {
        public PlacementResult NextResult = new PlacementResult(true, PlacementFailReason.None, default);
        public int ValidateCalls { get; private set; }

        public PlacementResult ValidateBuilding(string buildingDefId, CellPos anchor, Dir4 rotation)
        {
            ValidateCalls++;
            return NextResult;
        }

        public BuildingId CommitBuilding(string buildingDefId, CellPos anchor, Dir4 rotation) => default;
        public bool CanPlaceRoad(CellPos c) => false;
        public void PlaceRoad(CellPos c) { }
        public bool CanRemoveRoad(CellPos c) => false;
        public void RemoveRoad(CellPos c) { }
    }

    internal sealed class FakeUnlockService : IUnlockService
    {
        private readonly HashSet<string> _unlocked = new(StringComparer.Ordinal);

        public void Unlock(string id)
        {
            if (!string.IsNullOrWhiteSpace(id)) _unlocked.Add(id);
        }

        public bool IsUnlocked(string defId) => !string.IsNullOrWhiteSpace(defId) && _unlocked.Contains(defId);
    }

    internal sealed class FakeBuildOrderService : IBuildOrderService
    {
        public event Action<int> OnOrderCompleted;
        public int TickCalls { get; private set; }
        public int CreatePlaceOrder(string buildingDefId, CellPos anchor, Dir4 rotation) => 0;
        public int CreateUpgradeOrder(BuildingId building) => 0;
        public int CreateRepairOrder(BuildingId building) => 0;
        public bool TryGet(int orderId, out BuildOrder order) { order = default; return false; }
        public void Cancel(int orderId) { }
        public bool CancelBySite(SiteId siteId) => false;
        public bool CancelByBuilding(BuildingId buildingId) => false;
        public void ClearAll() { }
        public void Tick(float dt) { TickCalls++; }
    }

    internal sealed class FakeCombatService : ICombatService
    {
        public int TickCalls { get; private set; }
        public bool IsActive => false;
        public event Action<string> OnWaveStarted;
        public event Action<string> OnWaveEnded;
        public void OnDefendPhaseStarted() { }
        public void OnDefendPhaseEnded() { }
        public void Tick(float dt) => TickCalls++;
        public void SpawnWave(string waveDefId) { }
        public void KillAllEnemies() { }
        public void ForceResolveWave() { }
    }

    internal sealed class FakeResourceFlowService : IResourceFlowService, ITickable
    {
        public int TickCalls { get; private set; }
        public bool TryPickSource(CellPos from, ResourceType type, int minAmount, out StoragePick pick) { pick = default; return false; }
        public bool TryPickDest(CellPos from, ResourceType type, int minSpace, out StoragePick pick) { pick = default; return false; }
        public int Transfer(BuildingId src, BuildingId dst, ResourceType type, int amount) => 0;
        public void Tick(float dt) => TickCalls++;
    }

    internal sealed class FakeAmmoTickService : IAmmoService
    {
        public int TickCalls { get; private set; }
        public int PendingRequests => 0;
        public void NotifyTowerAmmoChanged(TowerId tower, int current, int max) { }
        public void EnqueueRequest(AmmoRequest req) { }
        public bool TryDequeueNext(out AmmoRequest req) { req = default; return false; }
        public bool TryStartCraft(BuildingId forge) => false;
        public void Tick(float dt) => TickCalls++;
    }

    internal sealed class FakeJobSchedulerTickService : IJobScheduler
    {
        public int TickCalls { get; private set; }
        public int AssignedThisTick => 0;
        public void Tick(float dt) => TickCalls++;
        public bool TryAssign(NpcId npc, out Job assigned) { assigned = default; return false; }
    }

    internal sealed class FakeUnlockTickService : IUnlockService, ITickable
    {
        public int TickCalls { get; private set; }
        public bool IsUnlocked(string defId) => false;
        public void Tick(float dt) => TickCalls++;
    }

    internal sealed class FakeProducerLoopTickService : ITickable
    {
        public int TickCalls { get; private set; }
        public void Tick(float dt) => TickCalls++;
    }

    internal sealed class DelegatingPlacementService : IPlacementService
    {
        private readonly Func<string, CellPos, Dir4, PlacementResult> _validate;

        public DelegatingPlacementService(Func<string, CellPos, Dir4, PlacementResult> validate)
        {
            _validate = validate;
        }

        public PlacementResult ValidateBuilding(string buildingDefId, CellPos anchor, Dir4 rotation)
            => _validate != null ? _validate(buildingDefId, anchor, rotation) : new PlacementResult(true, PlacementFailReason.None, anchor);

        public BuildingId CommitBuilding(string buildingDefId, CellPos anchor, Dir4 rotation) => default;
        public bool CanPlaceRoad(CellPos c) => false;
        public void PlaceRoad(CellPos c) { }
        public bool CanRemoveRoad(CellPos c) => false;
        public void RemoveRoad(CellPos c) { }
    }

    internal sealed class FakeJobExecutor : IJobExecutor
    {
        private readonly Func<NpcId, NpcState, Job, float, JobStatus?> _statusSelector;

        public FakeJobExecutor(Func<NpcId, NpcState, Job, float, JobStatus?> statusSelector)
        {
            _statusSelector = statusSelector;
        }

        public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
        {
            var next = _statusSelector?.Invoke(npc, npcState, job, dt);
            if (next.HasValue)
                job.Status = next.Value;
            return true;
        }
    }

    internal readonly struct HarvestTargetSelectorCall
    {
        public readonly ResourceType ResourceType;
        public readonly CellPos Origin;
        public readonly int WorkplaceId;
        public readonly int Slot;

        public HarvestTargetSelectorCall(ResourceType resourceType, CellPos origin, int workplaceId, int slot)
        {
            ResourceType = resourceType;
            Origin = origin;
            WorkplaceId = workplaceId;
            Slot = slot;
        }
    }

    internal sealed class FakeHarvestTargetSelector : IHarvestTargetSelector
    {
        private readonly Queue<CellPos> _cells = new();
        private readonly List<HarvestTargetSelectorCall> _calls = new();
        public bool AlwaysFail { get; set; }
        public int Calls { get; private set; }
        public IReadOnlyList<HarvestTargetSelectorCall> CallTrace => _calls;

        public FakeHarvestTargetSelector(CellPos fixedCell)
        {
            _cells.Enqueue(fixedCell);
        }

        public FakeHarvestTargetSelector(params CellPos[] cells)
        {
            if (cells != null)
            {
                for (int i = 0; i < cells.Length; i++)
                    _cells.Enqueue(cells[i]);
            }
        }

        public bool TryPickBestHarvestTarget(GameServices services, IWorldState world, ResourceType resourceType, CellPos origin, int workplaceId, int slot, out CellPos zoneCell)
        {
            Calls++;
            _calls.Add(new HarvestTargetSelectorCall(resourceType, origin, workplaceId, slot));

            if (AlwaysFail)
            {
                zoneCell = default;
                return false;
            }

            if (_cells.Count > 0)
            {
                zoneCell = _cells.Count == 1 ? _cells.Peek() : _cells.Dequeue();
                return true;
            }

            zoneCell = default;
            return false;
        }
    }
}
