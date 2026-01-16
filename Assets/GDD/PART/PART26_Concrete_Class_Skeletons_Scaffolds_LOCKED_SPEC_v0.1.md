# PART 26 — CONCRETE CLASS SKELETONS (FOLDER + FILE SCAFFOLDS) — LOCKED SPEC v0.1

> Mục tiêu: bạn copy/paste/khởi tạo file C# nhanh, đúng kiến trúc, đúng contract Part 25.  
Phạm vi: **skeleton** (khung class + fields + method signatures + TODO), không triển khai logic đầy đủ.

Nguyên tắc:
- Mỗi service **1 class concrete** + 1 interface (đã có ở Part 25)
- Không circular dependencies: inject qua `GameServices` hoặc constructor injection.
- Tick order tập trung trong `GameLoop` (1 MonoBehaviour).

---

## 1) Folder structure (copy vào Assets)

```
Assets/Game/
  Core/
    Contracts/                 // PART25 interfaces & DTOs
    Boot/
      GameBootstrap.cs
      GameServices.cs
    Loop/
      GameLoop.cs
      TickOrder.cs
    Events/
      EventBus.cs
      GameEvents.cs
    Utils/
      TimeUtil.cs
      DeterminismUtil.cs
      Log.cs
  Defs/
    Buildings/
    Enemies/
    Waves/
    Recipes/
    Rewards/
    Audio/
    FX/
  World/
    State/
      WorldState.cs
      Stores/
        EntityStore.cs
        BuildingStore.cs
        NpcStore.cs
        TowerStore.cs
        EnemyStore.cs
        BuildSiteStore.cs
      States/
        BuildingState.cs
        NpcState.cs
        TowerState.cs
        EnemyState.cs
        BuildSiteState.cs
        RunModifiers.cs
    Ops/
      WorldOps.cs
    Index/
      WorldIndexService.cs
  Grid/
    GridMap.cs
    PlacementService.cs
  Economy/
    StorageService.cs
    ResourceFlowService.cs
  Jobs/
    ClaimService.cs
    JobBoard.cs
    JobScheduler.cs
    Executors/
      JobExecutorRegistry.cs
      HarvestExecutor.cs
      HaulBasicExecutor.cs
      BuildDeliverExecutor.cs
      BuildWorkExecutor.cs
      CraftAmmoExecutor.cs
      ResupplyTowerExecutor.cs
  Build/
    BuildOrderService.cs
  Combat/
    CombatService.cs
    WaveDirector.cs
    TowerCombatSystem.cs
    EnemySystem.cs
  Rewards/
    RewardService.cs
    RunOutcomeService.cs
  Save/
    SaveService.cs
    SaveMigrator.cs
  UI/
    (Part 13 screens/binders)
  Debug/
    (Part 15 dev tools)
```

> Gợi ý asmdef: `Game.Core`, `Game.World`, `Game.Grid`, `Game.Economy`, `Game.Jobs`, `Game.Build`, `Game.Combat`, `Game.UI`, `Game.Save`.

---

## 2) Boot & Service container

### 2.1 `GameBootstrap.cs` (MonoBehaviour entry)
```csharp
using UnityEngine;

public sealed class GameBootstrap : MonoBehaviour
{
    [SerializeField] private DefsCatalog _defsCatalog; // ScriptableObject listing all defs roots (optional)
    [SerializeField] private bool _autoStartRun = true;

    private GameServices _services;
    private GameLoop _loop;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        _services = GameServicesFactory.Create(_defsCatalog);
        _loop = new GameLoop(_services);

        if (_autoStartRun)
            _loop.StartNewRun(seed: 12345); // TODO: seed source UI
    }

    private void Update()
    {
        _loop.Tick(Time.deltaTime);
    }

    private void OnDestroy()
    {
        _loop.Dispose();
    }
}
```

### 2.2 `GameServicesFactory.cs` (compose graph)
```csharp
public static class GameServicesFactory
{
    public static GameServices Create(DefsCatalog catalog)
    {
        var services = new GameServices();

        // Core
        services.Events = new EventBus();
        services.Data = new DataRegistry(catalog);
        services.Validator = new DataValidator(); // optional
        services.RunClock = new RunClockService(services.Events);
        services.Notifications = new NotificationService(services.Events);

        // World
        services.World = new WorldState();
        services.WorldOps = new WorldOps(services.World, services.Events);
        services.Index = new WorldIndexService(services.World, services.Data);

        // Grid
        services.Grid = new GridMap(width: 64, height: 64);
        services.Placement = new PlacementService(services.Grid, services.World, services.Data, services.Index, services.Events);

        // Economy
        services.Storage = new StorageService(services.World, services.Data, services.Events);
        services.ResourceFlow = new ResourceFlowService(services.World, services.Index, services.Storage);

        // Jobs
        services.Claims = new ClaimService();
        services.Jobs = new JobBoard();
        services.Executors = new JobExecutorRegistry(services);
        services.Scheduler = new JobScheduler(services.World, services.Jobs, services.Claims, services.Executors, services.Events);

        // Build
        services.BuildOrders = new BuildOrderService(services);

        // Ammo & Combat
        services.Ammo = new AmmoService(services);
        services.Combat = new CombatService(services);

        // Rewards & Outcome
        services.Rewards = new RewardService(services);
        services.RunOutcome = new RunOutcomeService(services.Events);

        // Save
        services.Save = new SaveService(new SaveMigrator(), services.Data);

        // Optional Audio/FX
        // services.Audio = ...
        // services.FX = ...

        return services;
    }
}
```

> **Chú ý**: `GameServices` ở Part 25 có thể cần thêm fields `Events`, `Validator`, `WorldOps`, `Executors`. Bạn giữ nhất quán theo project.

---

## 3) GameLoop & Tick order

### 3.1 `TickOrder.cs` (LOCKED)
```csharp
public static class TickOrder
{
    // Order matters for determinism & correctness.
    public static void TickAll(GameServices s, float dt)
    {
        s.RunClock.Tick(dt);

        // Build/Economy first (produces/requests)
        s.BuildOrders.Tick(dt);
        s.Ammo.Tick(dt);

        // Jobs scheduling/execution
        s.Scheduler.Tick(dt);

        // Combat last (consumes ammo, deals damage)
        if (s.RunClock.CurrentPhase == Phase.Defend)
            s.Combat.Tick(dt);

        // Outcome check (HQ dead etc.)
        s.RunOutcome.Tick(dt);
    }
}
```

### 3.2 `GameLoop.cs`
```csharp
public sealed class GameLoop : System.IDisposable
{
    private readonly GameServices _s;

    public GameLoop(GameServices services) { _s = services; }

    public void StartNewRun(int seed)
    {
        // TODO: reset world, grid, indices, run modifiers
        _s.RunOutcome.Reset();
        _s.RunClock.Start(seed);
        _s.Index.RebuildAll();
    }

    public void Tick(float dt)
    {
        TickOrder.TickAll(_s, dt);
    }

    public void Dispose() { /* cleanup if needed */ }
}
```

---

## 4) Core: EventBus, Log, Utilities

### 4.1 `EventBus.cs`
```csharp
using System;
using System.Collections.Generic;

public sealed class EventBus : IEventBus
{
    private readonly Dictionary<Type, Delegate> _map = new();

    public void Publish<T>(T evt) where T : struct
    {
        if (_map.TryGetValue(typeof(T), out var del))
            ((Action<T>)del)?.Invoke(evt);
    }

    public void Subscribe<T>(Action<T> handler) where T : struct
    {
        _map[typeof(T)] = (Action<T>)_map.GetValueOrDefault(typeof(T)) + handler;
    }

    public void Unsubscribe<T>(Action<T> handler) where T : struct
    {
        _map[typeof(T)] = (Action<T>)_map.GetValueOrDefault(typeof(T)) - handler;
    }
}
```

### 4.2 `Log.cs` (channels)
```csharp
public static class Log
{
    public static bool Jobs = true;
    public static bool Economy = true;
    public static bool Combat = true;

    public static void J(string msg) { if (Jobs) UnityEngine.Debug.Log("[JOBS] " + msg); }
    public static void E(string msg) { if (Economy) UnityEngine.Debug.Log("[ECO] " + msg); }
    public static void C(string msg) { if (Combat) UnityEngine.Debug.Log("[COMBAT] " + msg); }
}
```

---

## 5) World: States + Stores

### 5.1 `EntityStore.cs` (generic)
```csharp
using System.Collections.Generic;

public abstract class EntityStore<TId, TState> : IEntityStore<TId, TState>
{
    protected readonly Dictionary<int, TState> _map = new();
    protected int _nextId = 1;

    public abstract int ToInt(TId id);
    public abstract TId FromInt(int v);

    public bool Exists(TId id) => _map.ContainsKey(ToInt(id));
    public TState Get(TId id) => _map[ToInt(id)];

    public void Set(TId id, TState state) => _map[ToInt(id)] = state;

    public TId Create(TState state)
    {
        var id = FromInt(_nextId++);
        _map[ToInt(id)] = state;
        return id;
    }

    public void Destroy(TId id) => _map.Remove(ToInt(id));

    public int Count => _map.Count;

    public IEnumerable<TId> Ids
    {
        get { foreach (var k in _map.Keys) yield return FromInt(k); }
    }
}
```

### 5.2 Store implementations (example)
```csharp
public sealed class BuildingStore : EntityStore<BuildingId, BuildingState>, IBuildingStore
{
    public override int ToInt(BuildingId id) => id.Value;
    public override BuildingId FromInt(int v) => new BuildingId(v);
}
```

### 5.3 `WorldState.cs`
```csharp
public sealed class WorldState : IWorldState
{
    public IBuildingStore Buildings { get; } = new BuildingStore();
    public INpcStore Npcs { get; } = new NpcStore();
    public ITowerStore Towers { get; } = new TowerStore();
    public IEnemyStore Enemies { get; } = new EnemyStore();
    public IBuildSiteStore Sites { get; } = new BuildSiteStore();

    private RunModifiers _mods;
    public ref RunModifiers RunMods => ref _mods;
}
```

### 5.4 `WorldOps.cs`
```csharp
public sealed class WorldOps : IWorldOps
{
    private readonly IWorldState _w;
    private readonly IEventBus _bus;

    public WorldOps(IWorldState w, IEventBus bus) { _w = w; _bus = bus; }

    public BuildingId CreateBuilding(string buildingDefId, CellPos anchor, Dir4 rotation)
    {
        // TODO: create state from def
        var st = new BuildingState { DefId = buildingDefId, Anchor = anchor, Rotation = rotation };
        var id = _w.Buildings.Create(st);

        _bus.Publish(new BuildingPlacedEvent(buildingDefId, id));
        return id;
    }

    public void DestroyBuilding(BuildingId id)
    {
        _w.Buildings.Destroy(id);
        // TODO: publish destroyed event
    }

    public NpcId CreateNpc(string npcDefId, CellPos spawn)
    {
        var st = new NpcState { DefId = npcDefId, Cell = spawn };
        return _w.Npcs.Create(st);
    }

    public void DestroyNpc(NpcId id) => _w.Npcs.Destroy(id);

    public EnemyId CreateEnemy(string enemyDefId, CellPos spawn, int lane)
    {
        var st = new EnemyState { DefId = enemyDefId, Cell = spawn, Lane = lane };
        return _w.Enemies.Create(st);
    }

    public void DestroyEnemy(EnemyId id) => _w.Enemies.Destroy(id);

    public SiteId CreateBuildSite(string buildingDefId, CellPos anchor, Dir4 rotation)
    {
        var st = new BuildSiteState { BuildingDefId = buildingDefId, Anchor = anchor, Rotation = rotation };
        return _w.Sites.Create(st);
    }

    public void DestroyBuildSite(SiteId id) => _w.Sites.Destroy(id);
}
```

---

## 6) Grid: GridMap + PlacementService

### 6.1 `GridMap.cs`
```csharp
public sealed class GridMap : IGridMap
{
    private readonly int _w, _h;
    private readonly CellOccupancy[] _cells;

    public GridMap(int width, int height)
    {
        _w = width; _h = height;
        _cells = new CellOccupancy[_w * _h];
        // default Empty
    }

    public int Width => _w;
    public int Height => _h;

    public bool IsInside(CellPos c) => c.X >= 0 && c.Y >= 0 && c.X < _w && c.Y < _h;

    private int Idx(CellPos c) => c.Y * _w + c.X;

    public CellOccupancy Get(CellPos c) => _cells[Idx(c)];

    public bool IsRoad(CellPos c) => Get(c).Kind == CellOccupancyKind.Road;

    public bool IsBlocked(CellPos c)
    {
        var o = Get(c).Kind;
        return o == CellOccupancyKind.Building || o == CellOccupancyKind.Site;
    }

    public void SetRoad(CellPos c, bool isRoad)
    {
        _cells[Idx(c)] = isRoad
            ? new CellOccupancy(CellOccupancyKind.Road, default, default)
            : new CellOccupancy(CellOccupancyKind.Empty, default, default);
    }

    public void SetBuilding(CellPos c, BuildingId id) =>
        _cells[Idx(c)] = new CellOccupancy(CellOccupancyKind.Building, id, default);

    public void ClearBuilding(CellPos c) =>
        _cells[Idx(c)] = new CellOccupancy(CellOccupancyKind.Empty, default, default);

    public void SetSite(CellPos c, SiteId id) =>
        _cells[Idx(c)] = new CellOccupancy(CellOccupancyKind.Site, default, id);

    public void ClearSite(CellPos c) =>
        _cells[Idx(c)] = new CellOccupancy(CellOccupancyKind.Empty, default, default);
}
```

### 6.2 `PlacementService.cs` (skeleton)
```csharp
public sealed class PlacementService : IPlacementService
{
    private readonly IGridMap _grid;
    private readonly IWorldState _world;
    private readonly IDataRegistry _data;
    private readonly IWorldIndex _index;
    private readonly IEventBus _bus;

    public PlacementService(IGridMap grid, IWorldState world, IDataRegistry data, IWorldIndex index, IEventBus bus)
    { _grid = grid; _world = world; _data = data; _index = index; _bus = bus; }

    public bool CanPlaceRoad(CellPos c)
    {
        if (!_grid.IsInside(c)) return false;
        if (_grid.IsBlocked(c)) return false;
        // TODO: enforce orthogonal placement is a tool-level rule
        return true;
    }

    public void PlaceRoad(CellPos c)
    {
        if (!CanPlaceRoad(c)) return;
        _grid.SetRoad(c, true);
        _bus.Publish(new RoadPlacedEvent(c));
    }

    public PlacementResult ValidateBuilding(string buildingDefId, CellPos anchor, Dir4 rotation)
    {
        // TODO:
        // - bounds check footprint
        // - overlap check
        // - entry/road (driveway len=1)
        // - site blocking
        return new PlacementResult(true, PlacementFailReason.None, default);
    }

    public BuildingId CommitBuilding(string buildingDefId, CellPos anchor, Dir4 rotation)
    {
        // TODO: call Validate + apply occupancy + create building in world
        // NOTE: driveway conversion must be deterministic here
        throw new System.NotImplementedException();
    }
}
```

---

## 7) Economy: StorageService + ResourceFlowService

### 7.1 `StorageService.cs` (rules: warehouse no ammo, HQ no ammo)
```csharp
public sealed class StorageService : IStorageService
{
    private readonly IWorldState _w;
    private readonly IDataRegistry _data;
    private readonly IEventBus _bus;

    public StorageService(IWorldState w, IDataRegistry data, IEventBus bus)
    { _w = w; _data = data; _bus = bus; }

    public StorageSnapshot GetStorage(BuildingId building) { throw new System.NotImplementedException(); }

    public bool CanStore(BuildingId building, ResourceType type)
    {
        // TODO:
        // - warehouse/hq forbid ammo
        // - check def flags + caps
        return true;
    }

    public int GetAmount(BuildingId building, ResourceType type) { throw new System.NotImplementedException(); }
    public int GetCap(BuildingId building, ResourceType type) { throw new System.NotImplementedException(); }

    public int Add(BuildingId building, ResourceType type, int amount)
    {
        // TODO: clamp to cap, return added, publish ResourceDeliveredEvent if dest is warehouse/hq
        throw new System.NotImplementedException();
    }

    public int Remove(BuildingId building, ResourceType type, int amount) { throw new System.NotImplementedException(); }

    public int GetTotal(ResourceType type) { throw new System.NotImplementedException(); }
}
```

### 7.2 `ResourceFlowService.cs` (selection)
```csharp
public sealed class ResourceFlowService : IResourceFlowService
{
    private readonly IWorldState _w;
    private readonly IWorldIndex _index;
    private readonly IStorageService _storage;

    public ResourceFlowService(IWorldState w, IWorldIndex index, IStorageService storage)
    { _w = w; _index = index; _storage = storage; }

    public bool TryPickSource(CellPos from, ResourceType type, int minAmount, out StoragePick pick)
    {
        // TODO: deterministic nearest; tie-break by id
        pick = default;
        return false;
    }

    public bool TryPickDest(CellPos from, ResourceType type, int minSpace, out StoragePick pick)
    {
        pick = default;
        return false;
    }

    public int Transfer(BuildingId src, BuildingId dst, ResourceType type, int amount)
    {
        // TODO: remove then add; ensure atomic semantics or revert if add fails
        throw new System.NotImplementedException();
    }
}
```

---

## 8) Jobs: Claims + Board + Scheduler + Executors

### 8.1 `ClaimService.cs`
```csharp
public sealed class ClaimService : IClaimService
{
    private readonly System.Collections.Generic.Dictionary<ClaimKey, NpcId> _map = new();

    public bool TryAcquire(ClaimKey key, NpcId owner)
    {
        if (_map.TryGetValue(key, out var o)) return o.Value == owner.Value;
        _map[key] = owner;
        return true;
    }

    public bool IsOwnedBy(ClaimKey key, NpcId owner) =>
        _map.TryGetValue(key, out var o) && o.Value == owner.Value;

    public void Release(ClaimKey key, NpcId owner)
    {
        if (IsOwnedBy(key, owner)) _map.Remove(key);
    }

    public void ReleaseAll(NpcId owner)
    {
        // TODO: iterate safely (copy keys) remove owned
        throw new System.NotImplementedException();
    }

    public int ActiveClaimsCount => _map.Count;
}
```

### 8.2 `JobBoard.cs`
```csharp
public sealed class JobBoard : IJobBoard
{
    private readonly System.Collections.Generic.Dictionary<int, Job> _jobs = new();
    private readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.Queue<int>> _queues = new();
    private int _nextId = 1;

    public JobId Enqueue(Job job)
    {
        job.Id = new JobId(_nextId++);
        _jobs[job.Id.Value] = job;

        var key = job.Workplace.Value;
        if (!_queues.TryGetValue(key, out var q)) _queues[key] = q = new System.Collections.Generic.Queue<int>();
        q.Enqueue(job.Id.Value);
        return job.Id;
    }

    public bool TryPeekForWorkplace(BuildingId workplace, out Job job)
    {
        job = default;
        if (!_queues.TryGetValue(workplace.Value, out var q) || q.Count == 0) return false;

        // TODO: skip cancelled/completed stale ids (while loop)
        var id = q.Peek();
        return _jobs.TryGetValue(id, out job);
    }

    public bool TryClaim(JobId id, NpcId npc)
    {
        if (!_jobs.TryGetValue(id.Value, out var j)) return false;
        if (j.Status != JobStatus.Created) return false;
        j.Status = JobStatus.Claimed;
        j.ClaimedBy = npc;
        _jobs[id.Value] = j;
        return true;
    }

    public bool TryGet(JobId id, out Job job) => _jobs.TryGetValue(id.Value, out job);

    public void Update(Job job) => _jobs[job.Id.Value] = job;

    public void Cancel(JobId id)
    {
        if (_jobs.TryGetValue(id.Value, out var j))
        {
            j.Status = JobStatus.Cancelled;
            _jobs[id.Value] = j;
        }
    }

    public int CountForWorkplace(BuildingId workplace) =>
        _queues.TryGetValue(workplace.Value, out var q) ? q.Count : 0;
}
```

### 8.3 `JobExecutorRegistry.cs`
```csharp
public sealed class JobExecutorRegistry
{
    private readonly System.Collections.Generic.Dictionary<JobArchetype, IJobExecutor> _map = new();

    public JobExecutorRegistry(GameServices s)
    {
        _map[JobArchetype.Harvest] = new HarvestExecutor(s);
        _map[JobArchetype.HaulBasic] = new HaulBasicExecutor(s);
        _map[JobArchetype.BuildDeliver] = new BuildDeliverExecutor(s);
        _map[JobArchetype.BuildWork] = new BuildWorkExecutor(s);
        _map[JobArchetype.CraftAmmo] = new CraftAmmoExecutor(s);
        _map[JobArchetype.ResupplyTower] = new ResupplyTowerExecutor(s);
        // Leisure/Inspect optional later
    }

    public IJobExecutor Get(JobArchetype a) => _map[a];
}
```

### 8.4 `JobScheduler.cs` (skeleton)
```csharp
public sealed class JobScheduler : IJobScheduler
{
    private readonly IWorldState _w;
    private readonly IJobBoard _board;
    private readonly IClaimService _claims;
    private readonly JobExecutorRegistry _exec;
    private readonly IEventBus _bus;

    public int AssignedThisTick { get; private set; }

    public JobScheduler(IWorldState w, IJobBoard board, IClaimService claims, JobExecutorRegistry exec, IEventBus bus)
    { _w = w; _board = board; _claims = claims; _exec = exec; _bus = bus; }

    public void Tick(float dt)
    {
        AssignedThisTick = 0;

        // 1) assign jobs to idle NPCs (by workplace)
        // 2) tick current jobs (executor)
        // 3) cleanup completed/failed, release claims
        // TODO: deterministic iteration order (npc id ascending)
    }

    public bool TryAssign(NpcId npc, out Job assigned)
    {
        assigned = default;
        // TODO: find workplace, peek job, claim, set NPC state
        return false;
    }
}
```

### 8.5 Example executor skeleton
```csharp
public sealed class HarvestExecutor : IJobExecutor
{
    private readonly GameServices _s;
    public HarvestExecutor(GameServices s){ _s = s; }

    public bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt)
    {
        // TODO:
        // - move to producer target
        // - work timer
        // - on complete: add to producer local storage
        // - mark job completed
        return false;
    }
}
```

---

## 9) Build pipeline: `BuildOrderService.cs` (skeleton)

```csharp
public sealed class BuildOrderService : IBuildOrderService
{
    private readonly GameServices _s;
    private int _nextOrderId = 1;

    public event System.Action<int> OnOrderCompleted;

    public BuildOrderService(GameServices s){ _s = s; }

    public int CreatePlaceOrder(string buildingDefId, CellPos anchor, Dir4 rotation)
    {
        // TODO:
        // - validate placement
        // - create build site
        // - create order referencing site
        return _nextOrderId++;
    }

    public int CreateUpgradeOrder(BuildingId building) { throw new System.NotImplementedException(); }
    public int CreateRepairOrder(BuildingId building) { throw new System.NotImplementedException(); }

    public bool TryGet(int orderId, out BuildOrder order) { order = default; return false; }

    public void Cancel(int orderId) { /* TODO */ }

    public void Tick(float dt)
    {
        // TODO:
        // - for each active order: generate delivery/work jobs
        // - detect completion -> commit building/upgrade/repair, fire event
    }
}
```

---

## 10) Ammo: `AmmoService.cs` (skeleton)

```csharp
public sealed class AmmoService : IAmmoService
{
    private readonly GameServices _s;
    private readonly System.Collections.Generic.List<AmmoRequest> _urgent = new();
    private readonly System.Collections.Generic.List<AmmoRequest> _normal = new();

    public AmmoService(GameServices s){ _s = s; }

    public int PendingRequests => _urgent.Count + _normal.Count;

    public void NotifyTowerAmmoChanged(TowerId tower, int current, int max)
    {
        // TODO:
        // - if <=25% enqueue request (urgent if 0)
    }

    public void EnqueueRequest(AmmoRequest req)
    {
        if (req.Priority == AmmoRequestPriority.Urgent) _urgent.Add(req);
        else _normal.Add(req);
    }

    public bool TryDequeueNext(out AmmoRequest req)
    {
        // TODO: deterministic, urgent first, then oldest
        req = default;
        return false;
    }

    public bool TryStartCraft(BuildingId forge)
    {
        // TODO: verify inputs exist, create craft job
        return false;
    }

    public void Tick(float dt)
    {
        // TODO: could be empty if you rely on job providers in BuildOrder/Job system
    }
}
```

---

## 11) Combat: `CombatService.cs` + sub-systems

### 11.1 `WaveDirector.cs`
```csharp
public sealed class WaveDirector
{
    private readonly GameServices _s;
    public WaveDirector(GameServices s){ _s = s; }

    public void StartDayWaves(int dayIndex)
    {
        // TODO: decide which wave defs to run
    }

    public void Tick(float dt)
    {
        // TODO: spawn batches over time
    }
}
```

### 11.2 `CombatService.cs`
```csharp
public sealed class CombatService : ICombatService
{
    private readonly GameServices _s;
    private readonly WaveDirector _waves;

    public bool IsActive { get; private set; }

    public event System.Action<string> OnWaveStarted;
    public event System.Action<string> OnWaveEnded;

    public CombatService(GameServices s)
    {
        _s = s;
        _waves = new WaveDirector(s);
    }

    public void OnDefendPhaseStarted()
    {
        IsActive = true;
        _waves.StartDayWaves(_s.RunClock.DayIndex);
    }

    public void OnDefendPhaseEnded()
    {
        IsActive = false;
        // TODO: cleanup enemies?
    }

    public void Tick(float dt)
    {
        if (!IsActive) return;

        _waves.Tick(dt);
        // TODO: tick enemies movement/attack
        // TODO: tick towers targeting/firing (ammo consume)
        // TODO: check wave end
    }

    public void SpawnWave(string waveDefId)
    {
        // TODO: dev hook
    }

    public void KillAllEnemies()
    {
        // TODO: clear enemies store
    }
}
```

---

## 12) Rewards + Outcome: `RewardService.cs`, `RunOutcomeService.cs`

### 12.1 `RewardService.cs` (skeleton)
```csharp
public sealed class RewardService : IRewardService
{
    private readonly GameServices _s;

    public bool IsSelectionActive { get; private set; }
    public RewardOffer CurrentOffer { get; private set; }

    public event System.Action OnSelectionStarted;
    public event System.Action<string> OnRewardChosen;
    public event System.Action OnSelectionEnded;

    public RewardService(GameServices s){ _s = s; }

    public RewardOffer GenerateOffer(int dayIndex, int seed)
    {
        // TODO: deterministic pool pick
        return new RewardOffer("reward.x", "reward.y", "reward.z");
    }

    public void StartSelection(RewardOffer offer)
    {
        IsSelectionActive = true;
        CurrentOffer = offer;
        OnSelectionStarted?.Invoke();
    }

    public void Choose(int slotIndex)
    {
        // TODO: apply reward effect to RunMods or defs modifiers
        var chosen = slotIndex == 0 ? CurrentOffer.A : (slotIndex == 1 ? CurrentOffer.B : CurrentOffer.C);
        OnRewardChosen?.Invoke(chosen);

        IsSelectionActive = false;
        OnSelectionEnded?.Invoke();
    }
}
```

### 12.2 `RunOutcomeService.cs`
```csharp
public sealed class RunOutcomeService : IRunOutcomeService
{
    private readonly IEventBus _bus;
    public RunOutcome Outcome { get; private set; } = RunOutcome.Ongoing;

    public event System.Action<RunOutcome> OnRunEnded;

    public RunOutcomeService(IEventBus bus){ _bus = bus; }

    public void Reset() => Outcome = RunOutcome.Ongoing;

    public void Defeat()
    {
        if (Outcome != RunOutcome.Ongoing) return;
        Outcome = RunOutcome.Defeat;
        OnRunEnded?.Invoke(Outcome);
        _bus.Publish(new RunEndedEvent(Outcome));
    }

    public void Victory()
    {
        if (Outcome != RunOutcome.Ongoing) return;
        Outcome = RunOutcome.Victory;
        OnRunEnded?.Invoke(Outcome);
        _bus.Publish(new RunEndedEvent(Outcome));
    }

    public void Abort()
    {
        if (Outcome != RunOutcome.Ongoing) return;
        Outcome = RunOutcome.Abort;
        OnRunEnded?.Invoke(Outcome);
        _bus.Publish(new RunEndedEvent(Outcome));
    }

    public void Tick(float dt)
    {
        // TODO: if HQ hp <=0 -> Defeat
    }
}
```

---

## 13) Save: `SaveService.cs` + `SaveMigrator.cs`

### 13.1 `SaveMigrator.cs` (step migrator skeleton)
```csharp
public sealed class SaveMigrator
{
    public int CurrentSchemaVersion => 1;

    public bool TryMigrate(RunSaveDTO dto, out RunSaveDTO migrated)
    {
        // TODO: while dto.schemaVersion < Current: apply steps
        migrated = dto;
        return true;
    }

    public bool TryMigrate(MetaSaveDTO dto, out MetaSaveDTO migrated)
    {
        migrated = dto;
        return true;
    }
}
```

### 13.2 `SaveService.cs` (file IO skeleton)
```csharp
using System.IO;
using UnityEngine;

public sealed class SaveService : ISaveService
{
    private readonly SaveMigrator _migrator;
    private readonly IDataRegistry _data;

    public int CurrentSchemaVersion => _migrator.CurrentSchemaVersion;

    private string RunPath => Path.Combine(Application.persistentDataPath, "run_save.json");
    private string MetaPath => Path.Combine(Application.persistentDataPath, "meta_save.json");

    public SaveService(SaveMigrator migrator, IDataRegistry data)
    { _migrator = migrator; _data = data; }

    public bool HasRunSave() => File.Exists(RunPath);

    public void DeleteRunSave()
    {
        if (File.Exists(RunPath)) File.Delete(RunPath);
    }

    public SaveResult SaveRun(IWorldState world, IRunClock clock)
    {
        // TODO: build DTO from world+clock (keep minimal)
        return new SaveResult(SaveResultCode.Ok, "TODO");
    }

    public SaveResult LoadRun(out RunSaveDTO dto)
    {
        dto = null;
        if (!File.Exists(RunPath)) return new SaveResult(SaveResultCode.NotFound, "No run save");

        // TODO: read json, migrate, return
        return new SaveResult(SaveResultCode.Ok, "TODO");
    }

    public SaveResult SaveMeta(MetaSaveDTO dto)
    {
        // TODO: write
        return new SaveResult(SaveResultCode.Ok, "TODO");
    }

    public SaveResult LoadMeta(out MetaSaveDTO dto)
    {
        dto = null;
        // TODO: read+ migrate
        return new SaveResult(SaveResultCode.Ok, "TODO");
    }
}
```

---

## 14) RunClock: `RunClockService.cs` (skeleton)

```csharp
public sealed class RunClockService : IRunClock
{
    private readonly IEventBus _bus;

    public Season CurrentSeason { get; private set; } = Season.Spring;
    public int DayIndex { get; private set; } = 1;
    public Phase CurrentPhase { get; private set; } = Phase.Build;

    public float TimeScale { get; private set; } = 1f;
    public bool DefendSpeedUnlocked { get; private set; } = false;

    public event System.Action<Season,int> OnSeasonDayChanged;
    public event System.Action<Phase> OnPhaseChanged;
    public event System.Action OnDayEnded;

    private float _dayTimer;

    public RunClockService(IEventBus bus){ _bus = bus; }

    public void Start(int seed)
    {
        // TODO reset; store seed if needed
        CurrentSeason = Season.Spring;
        DayIndex = 1;
        CurrentPhase = Phase.Build;
        TimeScale = 1f;
        _dayTimer = 0;
        OnSeasonDayChanged?.Invoke(CurrentSeason, DayIndex);
        OnPhaseChanged?.Invoke(CurrentPhase);
    }

    public void Tick(float dt)
    {
        var scaled = dt * TimeScale;
        _dayTimer += scaled;

        // TODO: use Deliverable B pacing numbers
        // if day ends: advance day, raise OnDayEnded
    }

    public void SetTimeScale(float scale) => TimeScale = scale;

    public void ForceSeasonDay(Season s, int dayIndex)
    {
        CurrentSeason = s;
        DayIndex = dayIndex;
        OnSeasonDayChanged?.Invoke(CurrentSeason, DayIndex);
    }
}
```

---

## 15) Notifications: `NotificationService.cs` (skeleton)

```csharp
public sealed class NotificationService : INotificationService
{
    public int MaxVisible => 3;

    private readonly IEventBus _bus;
    private readonly System.Collections.Generic.List<NotificationViewModel> _visible = new();
    private readonly System.Collections.Generic.Dictionary<string, float> _cooldowns = new();
    private int _nextId = 1;

    public event System.Action NotificationsChanged;

    public NotificationService(IEventBus bus){ _bus = bus; }

    public NotificationId Push(string key, string title, string body, NotificationSeverity severity,
                               NotificationPayload payload, float cooldownSeconds = 3f, bool dedupeByKey = true)
    {
        // TODO: cooldown + dedupe
        var id = new NotificationId(_nextId++);
        var vm = new NotificationViewModel{
            Id = id, Key = key, Title = title, Body = body, Severity = severity, Payload = payload,
            CreatedAt = UnityEngine.Time.realtimeSinceStartup
        };

        // newest first
        _visible.Insert(0, vm);
        if (_visible.Count > MaxVisible) _visible.RemoveAt(_visible.Count - 1);

        NotificationsChanged?.Invoke();
        return id;
    }

    public void Dismiss(NotificationId id)
    {
        _visible.RemoveAll(x => x.Id.Value == id.Value);
        NotificationsChanged?.Invoke();
    }

    public void ClearAll()
    {
        _visible.Clear();
        NotificationsChanged?.Invoke();
    }

    public System.Collections.Generic.IReadOnlyList<NotificationViewModel> GetVisible() => _visible;
}
```

---

## 16) WorldIndex: `WorldIndexService.cs` (skeleton)

```csharp
public sealed class WorldIndexService : IWorldIndex
{
    private readonly IWorldState _w;
    private readonly IDataRegistry _data;

    private readonly System.Collections.Generic.List<BuildingId> _warehouses = new();
    private readonly System.Collections.Generic.List<BuildingId> _producers  = new();
    private readonly System.Collections.Generic.List<BuildingId> _forges     = new();
    private readonly System.Collections.Generic.List<BuildingId> _armories   = new();
    private readonly System.Collections.Generic.List<TowerId> _towers        = new();

    public System.Collections.Generic.IReadOnlyList<BuildingId> Warehouses => _warehouses;
    public System.Collections.Generic.IReadOnlyList<BuildingId> Producers  => _producers;
    public System.Collections.Generic.IReadOnlyList<BuildingId> Forges     => _forges;
    public System.Collections.Generic.IReadOnlyList<BuildingId> Armories   => _armories;
    public System.Collections.Generic.IReadOnlyList<TowerId>    Towers     => _towers;

    public WorldIndexService(IWorldState w, IDataRegistry data){ _w = w; _data = data; }

    public void RebuildAll()
    {
        _warehouses.Clear(); _producers.Clear(); _forges.Clear(); _armories.Clear(); _towers.Clear();

        foreach (var bid in _w.Buildings.Ids)
        {
            // TODO: read BuildingDef flags
        }
        foreach (var tid in _w.Towers.Ids)
        {
            _towers.Add(tid);
        }
    }

    public void OnBuildingCreated(BuildingId id) { /* TODO incremental */ }
    public void OnBuildingDestroyed(BuildingId id) { /* TODO incremental */ }
}
```

---

## 17) Minimal “state” structs (placeholders)
Create in `Assets/Game/World/State/States/`:

```csharp
public struct BuildingState
{
    public string DefId;
    public CellPos Anchor;
    public Dir4 Rotation;

    public int HP;
    public int Tier;

    // storage amounts (keep simple for v0.1)
    public int Wood, Food, Stone, Iron, Ammo;

    // workplace assignment
    public int AssignedNpcCount; // or list elsewhere
}

public struct NpcState
{
    public string DefId;
    public CellPos Cell;

    public BuildingId Workplace; // 0 if unassigned
    public JobId CurrentJob;

    public ResourceType CarryType;
    public int CarryAmount;

    public float WorkTimer;
}

public struct TowerState
{
    public string DefId;
    public CellPos Cell;

    public int HP;
    public int Ammo;
    public int AmmoMax;

    public float FireCooldown;
}

public struct EnemyState
{
    public string DefId;
    public CellPos Cell;

    public int HP;
    public int Lane;
    public float AttackCooldown;
}

public struct RunModifiers
{
    public float TowerDamageMul;
    public float HarvestSpeedMul;
    public float MoveSpeedMul;
    // TODO extend as reward effects
}
```

---

## 18) “Build now” checklist (sau Part 26)
1) Tạo các file skeleton theo thứ tự Phase 1–2 trong Part 24.
2) Cho chạy scene với `GameBootstrap` + `GameLoop`.
3) Dùng DevPanel (Part 15) hoặc temporary debug keys để:
   - place road
   - place HQ
4) Sau đó implement dần:
   - StorageService -> ResourceFlow -> JobScheduler -> Harvest/Haul executors
   - BuildOrder pipeline
   - Ammo + Combat + Rewards

---

## 19) Next Part (Part 27 đề xuất)
**Concrete “Vertical Slice #1” implementation plan**: viết task list chi tiết + code order cho Sprint 1–2 (boot → placement → resource flow → jobs), kèm acceptance tests từng ngày.

