# PART 6 — ENTITY STORES + RUNTIME STATE (BUILDINGS / NPC / TOWERS / ENEMIES) — SPEC v0.1

> Mục tiêu: tạo “runtime model” ổn định để về sau làm JobSystem/Economy/Combat mà không phải làm lại:
- Split rõ: **Def (data)** vs **State (runtime)**
- EntityId + Stores (create/get/destroy) không allocation
- Building/NPC/Tower/Enemy states đủ cho v0.1 (premium roguelite run)
- Chuẩn hoá “inventory/storage” và “carry”
- Hook cho save/load + migration

Phần này KHÔNG làm AI/job/combat logic. Chỉ data structures + stores + core ops.

---

## 1) EntityId & Id Providers

### 1.1 EntityId structs (typed IDs)
Khuyến nghị: typed id để tránh nhầm BuildingId với NPCId.

```csharp
public readonly struct BuildingId
{
    public readonly int Value;
    public BuildingId(int v) { Value = v; }
    public bool IsValid => Value != 0;
}

public readonly struct NpcId
{
    public readonly int Value;
    public NpcId(int v) { Value = v; }
    public bool IsValid => Value != 0;
}

public readonly struct TowerId
{
    public readonly int Value;
    public TowerId(int v) { Value = v; }
    public bool IsValid => Value != 0;
}

public readonly struct EnemyId
{
    public readonly int Value;
    public EnemyId(int v) { Value = v; }
    public bool IsValid => Value != 0;
}
```

### 1.2 Id generation (monotonic)
```csharp
public sealed class IdGenerator
{
    private int _next = 1;
    public int Next() => _next++;
    public void Reset(int start = 1) { _next = start; }
}
```

---

## 2) Entity Stores (no allocation patterns)

### 2.1 Store goals
- Create returns id
- Get by id returns `ref` (struct storage) để mutate không copy
- Destroy marks slot free
- Enumerate alive items without allocations

### 2.2 Implementation option (v0.1): dense arrays + free list
```csharp
public sealed class EntityStore<T> where T : struct
{
    private T[] _items;
    private bool[] _alive;
    private int _countAlive;

    public int Capacity => _items.Length;
    public int AliveCount => _countAlive;

    public EntityStore(int capacity)
    {
        _items = new T[capacity];
        _alive = new bool[capacity];
    }

    // id.Value maps to index (id=1 -> idx=1). idx=0 reserved as invalid.
    public ref T Create(int idValue, in T initial)
    {
        EnsureCapacity(idValue + 1);
        _items[idValue] = initial;
        if (!_alive[idValue]) { _alive[idValue] = true; _countAlive++; }
        return ref _items[idValue];
    }

    public bool IsAlive(int idValue)
        => idValue > 0 && idValue < _alive.Length && _alive[idValue];

    public ref T Get(int idValue)
    {
        if (!IsAlive(idValue)) throw new System.Exception($"Entity not alive: {idValue}");
        return ref _items[idValue];
    }

    public bool TryGet(int idValue, out T value)
    {
        if (!IsAlive(idValue)) { value = default; return false; }
        value = _items[idValue]; return true;
    }

    public void Destroy(int idValue)
    {
        if (!IsAlive(idValue)) return;
        _alive[idValue] = false;
        _items[idValue] = default;
        _countAlive--;
    }

    public Enumerator GetEnumerator() => new Enumerator(_items, _alive);

    public struct Enumerator
    {
        private readonly T[] _items;
        private readonly bool[] _alive;
        private int _i;

        public Enumerator(T[] items, bool[] alive)
        { _items = items; _alive = alive; _i = 0; }

        public bool MoveNext()
        {
            for (_i++; _i < _alive.Length; _i++)
                if (_alive[_i]) return true;
            return false;
        }

        public ref readonly T Current => ref _items[_i];
        public int CurrentIdValue => _i;
    }

    private void EnsureCapacity(int needed)
    {
        if (needed <= _items.Length) return;
        var newSize = System.Math.Max(needed, _items.Length * 2);
        System.Array.Resize(ref _items, newSize);
        System.Array.Resize(ref _alive, newSize);
    }
}
```

> NOTE: Nếu bạn cần mutate trong loop, bạn có thể dùng `ref var item = ref store.Get(id)`.

---

## 3) Resource containers (Storage & Carry)

### 3.1 ResourceStack (single type)
```csharp
public struct ResourceStack
{
    public ResourceType Type;
    public int Amount;
    public int Cap;

    public void Clear() { Type = ResourceType.None; Amount = 0; }
    public int Space => Cap - Amount;
}
```

### 3.2 Storage (multi-type)
v0.1: fixed array by enum index (fast, no dict).

```csharp
public struct Storage
{
    // index by ResourceType int (assuming enum small)
    public int Wood, Stone, Iron, Food, Ammo;
    public int WoodCap, StoneCap, IronCap, FoodCap, AmmoCap;

    public int Get(ResourceType t) => t switch
    {
        ResourceType.Wood => Wood,
        ResourceType.Stone => Stone,
        ResourceType.Iron => Iron,
        ResourceType.Food => Food,
        ResourceType.Ammo => Ammo,
        _ => 0
    };

    public int GetCap(ResourceType t) => t switch
    {
        ResourceType.Wood => WoodCap,
        ResourceType.Stone => StoneCap,
        ResourceType.Iron => IronCap,
        ResourceType.Food => FoodCap,
        ResourceType.Ammo => AmmoCap,
        _ => 0
    };

    public int Space(ResourceType t) => GetCap(t) - Get(t);

    public bool CanAdd(ResourceType t, int amount) => Space(t) >= amount;

    public int TryAdd(ResourceType t, int amount)
    {
        if (amount <= 0) return 0;
        var add = System.Math.Min(amount, Space(t));
        if (add <= 0) return 0;

        switch (t)
        {
            case ResourceType.Wood: Wood += add; break;
            case ResourceType.Stone: Stone += add; break;
            case ResourceType.Iron: Iron += add; break;
            case ResourceType.Food: Food += add; break;
            case ResourceType.Ammo: Ammo += add; break;
        }
        return add;
    }

    public int TryTake(ResourceType t, int amount)
    {
        if (amount <= 0) return 0;
        var cur = Get(t);
        var take = System.Math.Min(amount, cur);
        if (take <= 0) return 0;

        switch (t)
        {
            case ResourceType.Wood: Wood -= take; break;
            case ResourceType.Stone: Stone -= take; break;
            case ResourceType.Iron: Iron -= take; break;
            case ResourceType.Food: Food -= take; break;
            case ResourceType.Ammo: Ammo -= take; break;
        }
        return take;
    }

    public void ApplyCaps(in StorageCapsDef caps)
    {
        WoodCap = caps.WoodCap;
        StoneCap = caps.StoneCap;
        IronCap = caps.IronCap;
        FoodCap = caps.FoodCap;
        AmmoCap = caps.AmmoCap;

        // clamp existing
        Wood = System.Math.Min(Wood, WoodCap);
        Stone = System.Math.Min(Stone, StoneCap);
        Iron = System.Math.Min(Iron, IronCap);
        Food = System.Math.Min(Food, FoodCap);
        Ammo = System.Math.Min(Ammo, AmmoCap);
    }
}
```

---

## 4) BuildingState (runtime)

### 4.1 Required fields (v0.1)
- identity: `BuildingId`, `DefId`
- placement: `Anchor`, `EntryPoint`, computed `EntryCell` (optional cached)
- hp: `HP`
- storage: `Storage`
- workplace: assigned npc list + slots
- tags runtime: `Flags` (IsHQ/IsWarehouse/IsForge/IsArmory)

### 4.2 Workplace runtime (v0.1)
Không dùng List (allocation). Dùng fixed slots array.

```csharp
public struct WorkplaceState
{
    public int Slots; // from WorkplaceDef
    public NpcId Slot0, Slot1, Slot2, Slot3; // v0.1 limit 4 (enough)
    public int AssignedCount;

    public bool TryAssign(NpcId npc)
    {
        if (AssignedCount >= Slots) return false;
        if (!Slot0.IsValid) { Slot0 = npc; AssignedCount++; return true; }
        if (!Slot1.IsValid) { Slot1 = npc; AssignedCount++; return true; }
        if (!Slot2.IsValid) { Slot2 = npc; AssignedCount++; return true; }
        if (!Slot3.IsValid) { Slot3 = npc; AssignedCount++; return true; }
        return false;
    }

    public bool Unassign(NpcId npc)
    {
        if (Slot0.Value == npc.Value) { Slot0 = default; AssignedCount--; return true; }
        if (Slot1.Value == npc.Value) { Slot1 = default; AssignedCount--; return true; }
        if (Slot2.Value == npc.Value) { Slot2 = default; AssignedCount--; return true; }
        if (Slot3.Value == npc.Value) { Slot3 = default; AssignedCount--; return true; }
        return false;
    }

    public bool Contains(NpcId npc)
        => Slot0.Value == npc.Value || Slot1.Value == npc.Value || Slot2.Value == npc.Value || Slot3.Value == npc.Value;
}
```

> Nếu về sau slots > 4, bạn refactor sang `FixedList` (Unity.Collections) hoặc custom array pool. v0.1 không cần over-engineer.

### 4.3 Building flags
```csharp
[System.Flags]
public enum BuildingFlags
{
    None = 0,
    HQ = 1<<0,
    Warehouse = 1<<1,
    Forge = 1<<2,
    Armory = 1<<3,
    ResourceProducer = 1<<4,
    Defense = 1<<5
}
```

### 4.4 BuildingState struct
```csharp
public struct BuildingState
{
    public BuildingId Id;
    public string DefId;
    public int Tier;

    public CellPos Anchor;
    public EntryPoint Entry;
    public CellPos EntryCell; // cached at placement time (optional)

    public int HP;
    public int MaxHP;

    public Storage Storage;
    public WorkplaceState Workplace;

    public BuildingFlags Flags;

    public bool IsDestroyed => HP <= 0;
}
```

### 4.5 Create building from def (factory)
```csharp
public static class BuildingFactory
{
    public static BuildingState Create(BuildingId id, BuildingDef def, CellPos anchor, EntryPoint entry, CellPos entryCell)
    {
        var s = new BuildingState();
        s.Id = id;
        s.DefId = def.Id;
        s.Tier = def.Tier;

        s.Anchor = anchor;
        s.Entry = entry;
        s.EntryCell = entryCell;

        s.MaxHP = def.MaxHP;
        s.HP = def.MaxHP;

        s.Storage = default;
        s.Storage.ApplyCaps(def.StorageCaps);

        s.Workplace = default;
        if (def.Workplace != null)
        {
            s.Workplace.Slots = def.Workplace.Slots;
        }

        s.Flags = BuildingFlags.None;
        if (def.IsHQ) s.Flags |= BuildingFlags.HQ;
        if (def.IsWarehouse) s.Flags |= BuildingFlags.Warehouse;
        if (def.IsForge) s.Flags |= BuildingFlags.Forge;
        if (def.IsArmory) s.Flags |= BuildingFlags.Armory;

        return s;
    }
}
```

---

## 5) NPCState (runtime)

### 5.1 Agent status & role
```csharp
public enum NpcRole
{
    Unassigned,
    HQGeneralist,     // HQ can Build/Repair/HaulBasic only
    Worker,           // harvest
    TransporterBasic, // warehouse hauling (no ammo)
    Smith,            // forge crafting
    ArmoryRunner,     // haul ammo + resupply towers
    Builder           // build/upgrade/repair/demolish (if you use Builder Hut)
}

public enum NpcStatus
{
    Idle,
    Moving,
    Working,
    Carrying,
    Waiting
}
```

### 5.2 Path state (minimal placeholder)
```csharp
public struct PathState
{
    public bool HasPath;
    public int PathVersion; // optional for invalidation
    public CellPos NextCell;
}
```

### 5.3 NPCState struct
```csharp
public struct NpcState
{
    public NpcId Id;

    public NpcRole Role;
    public NpcStatus Status;

    public CellPos Cell;
    public PathState Path;

    public BuildingId AssignedWorkplace;  // 0 if none

    public ResourceStack Carry;

    // current job link (just handle, job system later)
    public int CurrentJobId; // 0 if none
}
```

### 5.4 Spawn factory
```csharp
public static class NpcFactory
{
    public static NpcState Create(NpcId id, CellPos spawnCell, NpcRole role = NpcRole.Unassigned)
    {
        return new NpcState
        {
            Id = id,
            Role = role,
            Status = NpcStatus.Idle,
            Cell = spawnCell,
            Carry = new ResourceStack { Type = ResourceType.None, Amount = 0, Cap = 20 }, // tune later
            AssignedWorkplace = default,
            CurrentJobId = 0
        };
    }
}
```

---

## 6) TowerState (runtime)

### 6.1 Required fields
- identity: `TowerId`, `DefId`
- placement: `Cell`
- hp, ammo current

```csharp
public struct TowerState
{
    public TowerId Id;
    public string DefId;
    public int Tier;

    public CellPos Cell;

    public int HP;
    public int MaxHP;

    public int AmmoCurrent;
    public int AmmoMax;

    public bool NeedsAmmo; // derived or cached flag
}
```

### 6.2 Factory
```csharp
public static class TowerFactory
{
    public static TowerState Create(TowerId id, TowerDef def, CellPos cell)
    {
        return new TowerState
        {
            Id = id,
            DefId = def.Id,
            Tier = def.Tier,
            Cell = cell,
            MaxHP = def.MaxHP,
            HP = def.MaxHP,
            AmmoMax = def.Ammo.AmmoMax,
            AmmoCurrent = def.Ammo.AmmoMax, // start full for initial tower if desired
            NeedsAmmo = false
        };
    }
}
```

---

## 7) EnemyState (runtime)

```csharp
public enum EnemyStatus { Spawning, Moving, Attacking, Dead }

public struct EnemyState
{
    public EnemyId Id;
    public string DefId;

    public CellPos Cell;
    public EnemyStatus Status;

    public int HP;
    public float MoveProgress; // for smooth stepping if needed

    public int CurrentTargetBuildingId; // 0=HQ, else building id (optional)
}
```

Factory uses EnemyDef stats.

---

## 8) WorldState container (single source runtime)

### 8.1 WorldState responsibilities
- Holds stores + id generators
- Provides create/destroy helpers
- This is what SaveService serializes

```csharp
public sealed class WorldState
{
    public readonly IdGenerator BuildingIds = new();
    public readonly IdGenerator NpcIds = new();
    public readonly IdGenerator TowerIds = new();
    public readonly IdGenerator EnemyIds = new();

    public readonly EntityStore<BuildingState> Buildings = new(256);
    public readonly EntityStore<NpcState> Npcs = new(256);
    public readonly EntityStore<TowerState> Towers = new(128);
    public readonly EntityStore<EnemyState> Enemies = new(512);

    // indexes (optional v0.1)
    public BuildingId HQ; // cache HQ building id
}
```

---

## 9) WorldOps (create/destroy + grid integration hooks)

### 9.1 Create building
- Validate placement (Part 5)
- Commit occupancy/driveway (Part 5)
- Create building state in store
- Update HQ cache if needed

```csharp
public sealed class WorldOps
{
    private readonly WorldState _world;
    private readonly IDataRegistry _data;
    private readonly GridMap _grid;
    private readonly BuildingPlacementService _placement;

    public WorldOps(WorldState world, IDataRegistry data, GridMap grid)
    {
        _world = world; _data = data; _grid = grid;
        _placement = new BuildingPlacementService(grid);
    }

    public bool TryPlaceBuilding(string buildingDefId, CellPos anchor, EntryPoint entry, out BuildingId buildingId, out PlacementResult result)
    {
        buildingId = default;

        var def = _data.GetBuilding(buildingDefId);
        result = _placement.Validate(def, anchor, entry);
        if (!result.Ok) return false;

        var id = new BuildingId(_world.BuildingIds.Next());

        // commit grid occupancy & driveway
        _placement.CommitPlaceBuilding(id.Value, def, anchor, entry);

        // create state
        var state = BuildingFactory.Create(id, def, anchor, entry, result.EntryCell);
        _world.Buildings.Create(id.Value, state);

        if (def.IsHQ) _world.HQ = id;

        buildingId = id;
        return true;
    }
}
```

> NOTE: `CommitPlaceBuilding` trong Part 5 đang là method trong placement service. Bạn nên expose nó public.

### 9.2 Destroy building
- Clear occupancy cells
- Remove state
- Handle unassign NPC (future JobSystem)
- Notify systems via EventBus (optional hook)

```csharp
public void DestroyBuilding(BuildingId id)
{
    if (!_world.Buildings.IsAlive(id.Value)) return;

    ref var b = ref _world.Buildings.Get(id.Value);
    var def = _data.GetBuilding(b.DefId);

    FootprintUtil.ForEachCell(b.Anchor, def.Footprint.Width, def.Footprint.Height, cell =>
        _grid.SetOccupant(cell, 0));

    _world.Buildings.Destroy(id.Value);
}
```

---

## 10) Save/Load considerations (v0.1)
- Save `WorldState` gồm:
  - next id counters
  - list alive entities & states
  - grid road + occupancy (occupancy có thể rebuild từ buildings, road phải save)
- Occupancy nên rebuild:
  - clear grid occupancy
  - for each building alive -> set occupant in footprint
  - apply driveway already stored as road; road layer loaded from save

### 10.1 Serialize format
- JSON/binary tuỳ bạn; v0.1 đề xuất JSON + gzip cho nhanh dev
- `DefId` stored as string stable ids

---

## 11) QA Checklist (Part 6)
- [ ] Can create/destroy buildings without memory leak
- [ ] Stores enumerate alive items correctly
- [ ] Storage caps apply from def
- [ ] HQ cache correct
- [ ] NPC spawn works, carry initialized
- [ ] Tower state has ammo fields ready for pipeline
- [ ] Save can reconstruct occupancy deterministically

---

## 12) Next Part (Part 7 đề xuất)
**Resource Flow primitives**: “take from storage / add to storage / choose nearest storage” + “HQ/warehouse/armory rules”, để JobSystem không bị spaghetti.

