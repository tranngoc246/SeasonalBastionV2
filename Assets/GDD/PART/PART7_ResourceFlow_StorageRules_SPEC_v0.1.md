# PART 7 — RESOURCE FLOW PRIMITIVES + STORAGE SELECTION RULES (HQ / WAREHOUSE / FORGE / ARMORY) — SPEC v0.1

> Mục tiêu: chuẩn hoá “dòng chảy tài nguyên” để JobSystem/Economy/Ammo pipeline triển khai sạch, không spaghetti:
- Primitive ops: `Take`, `Put`, `Reserve`, `Transfer`, `HasEnough`
- Quy tắc “ai chứa gì” (Warehouse không chứa Ammo, HQ chứa cơ bản, Armory chứa Ammo)
- Chọn kho gần nhất (nearest storage) deterministic
- Local storage tại công trình tài nguyên + vận chuyển về kho
- Rule builder/smith/armory-runner lấy nguyên liệu theo ưu tiên
- Hook notification keys (Part 4)

Phần này vẫn **không** triển khai job scheduling/AI; chỉ cung cấp API & rules.

---

## 1) Canonical rules (LOCKED v0.1)

### 1.1 Storage roles
- **HQ**: chứa *basic resources* (Wood/Stone/Iron/Food), **không chứa Ammo** (tuỳ bạn; nếu HQ cần ammo về sau hãy mở khóa)
- **Warehouse**: chứa *basic resources*, **không chứa Ammo** (LOCKED)
- **Resource buildings** (Farmhouse/LumberCamp/...): có **local storage** cho resource của nó (cap nhỏ)
- **Forge**: local storage input (basic), output (Ammo) có thể:
  - Option A: output Ammo trong local storage của Forge (recommended)
- **Armory**: chứa **Ammo** (cap lớn), là source để resupply towers
- **Towers**: có ammo internal (`TowerState.AmmoCurrent`), không dùng Storage.

### 1.2 Who can haul what (roles)
- `Worker`: harvest resource -> tăng vào local storage (không auto tăng)
- `TransporterBasic` (Warehouse NPC): chỉ haul **basic** từ resource building -> Warehouse/HQ
- `Smith` (Forge NPC): lấy **basic inputs** từ kho gần nhất -> Forge, craft ammo
- `ArmoryRunner` (Armory NPC): lấy ammo từ Forge -> Armory; và resupply towers thiếu ammo
- `HQGeneralist`: Build/Repair/HaulBasic (không harvest). HaulBasic chỉ basic.

---

## 2) Resource math & result codes

### 2.1 Transfer result
```csharp
public enum TransferResult
{
    Success,
    SourceEmpty,
    DestinationFull,
    NotEnoughAmount,
    InvalidResourceType,
    RuleViolation
}
```

### 2.2 Selection result (storage finding)
```csharp
public enum StorageSelectFail
{
    None,
    NoCandidate,
    NoPath,
    RuleViolation
}

public readonly struct StorageSelectResult
{
    public readonly bool Ok;
    public readonly StorageSelectFail Fail;
    public readonly BuildingId Building;
    public readonly CellPos Cell;

    public StorageSelectResult(bool ok, StorageSelectFail fail, BuildingId b, CellPos cell)
    { Ok = ok; Fail = fail; Building = b; Cell = cell; }

    public static StorageSelectResult Success(BuildingId b, CellPos cell) => new(true, StorageSelectFail.None, b, cell);
    public static StorageSelectResult FailRes(StorageSelectFail f) => new(false, f, default, default);
}
```

---

## 3) Storage access abstraction

### 3.1 IStorageAccessor
Mục tiêu: code transfer không cần biết building type.

```csharp
public interface IStorageAccessor
{
    bool CanStore(BuildingState b, ResourceType type); // role rules (warehouse ammo forbidden)
    int GetAmount(ref BuildingState b, ResourceType type);
    int GetCap(ref BuildingState b, ResourceType type);
    int GetSpace(ref BuildingState b, ResourceType type);

    int TryAdd(ref BuildingState b, ResourceType type, int amount);
    int TryTake(ref BuildingState b, ResourceType type, int amount);
}
```

### 3.2 Default implementation
```csharp
public sealed class DefaultStorageAccessor : IStorageAccessor
{
    public bool CanStore(BuildingState b, ResourceType t)
    {
        // Warehouse rule
        if ((b.Flags & BuildingFlags.Warehouse) != 0 && t == ResourceType.Ammo)
            return false;

        // HQ rule (v0.1)
        if ((b.Flags & BuildingFlags.HQ) != 0 && t == ResourceType.Ammo)
            return false;

        // Otherwise: allowed if cap > 0
        return b.Storage.GetCap(t) > 0;
    }

    public int GetAmount(ref BuildingState b, ResourceType t) => b.Storage.Get(t);
    public int GetCap(ref BuildingState b, ResourceType t) => b.Storage.GetCap(t);
    public int GetSpace(ref BuildingState b, ResourceType t) => b.Storage.Space(t);

    public int TryAdd(ref BuildingState b, ResourceType t, int amount)
        => CanStore(b, t) ? b.Storage.TryAdd(t, amount) : 0;

    public int TryTake(ref BuildingState b, ResourceType t, int amount)
        => b.Storage.TryTake(t, amount);
}
```

---

## 4) ResourceFlow primitives (core service)

### 4.1 Service responsibilities
- `HasEnough` (across storages)
- `TryTakeFromAny` (priority order)
- `TryPutToPreferred` (choose dest)
- `TransferBetweenBuildings`
- Provide deterministic selection order (distance + tie-break by id)

### 4.2 Class definition
```csharp
public sealed class ResourceFlowService
{
    private readonly WorldState _world;
    private readonly IDataRegistry _data;
    private readonly GridMap _grid;
    private readonly IStorageAccessor _storage;

    public ResourceFlowService(WorldState world, IDataRegistry data, GridMap grid, IStorageAccessor storage)
    { _world = world; _data = data; _grid = grid; _storage = storage; }

    // check total amount available in candidate storages
    public int TotalAvailable(ResourceType type, StorageQuery query)
    {
        var total = 0;
        foreach (var bId in query.Candidates)
        {
            if (!_world.Buildings.IsAlive(bId.Value)) continue;
            ref var b = ref _world.Buildings.Get(bId.Value);
            if (!_storage.CanStore(b, type)) continue; // canStore also implies it has that type cap
            total += _storage.GetAmount(ref b, type);
        }
        return total;
    }

    public bool HasEnough(ResourceType type, int needed, StorageQuery query)
        => TotalAvailable(type, query) >= needed;

    public TransferResult Transfer(BuildingId from, BuildingId to, ResourceType type, int amount, out int moved)
    {
        moved = 0;
        if (type == ResourceType.None) return TransferResult.InvalidResourceType;

        if (!_world.Buildings.IsAlive(from.Value) || !_world.Buildings.IsAlive(to.Value))
            return TransferResult.RuleViolation;

        ref var src = ref _world.Buildings.Get(from.Value);
        ref var dst = ref _world.Buildings.Get(to.Value);

        if (!_storage.CanStore(dst, type))
            return TransferResult.RuleViolation;

        var take = _storage.TryTake(ref src, type, amount);
        if (take <= 0) return TransferResult.SourceEmpty;

        var add = _storage.TryAdd(ref dst, type, take);
        if (add <= 0)
        {
            // rollback
            _storage.TryAdd(ref src, type, take);
            return TransferResult.DestinationFull;
        }

        // if partial add, rollback remainder
        if (add < take)
        {
            _storage.TryAdd(ref src, type, take - add);
        }

        moved = add;
        return TransferResult.Success;
    }
}
```

---

## 5) StorageQuery & candidate selection

### 5.1 StorageQuery
Represents “where to look” & priority.

```csharp
public readonly struct StorageQuery
{
    public readonly System.ReadOnlySpan<BuildingId> Candidates; // v0.1 you can implement as array/list

    public StorageQuery(System.ReadOnlySpan<BuildingId> candidates)
    { Candidates = candidates; }
}
```

> In C# non-Burst, `ReadOnlySpan` needs careful lifetime; v0.1 simplest: use `BuildingId[]`.

### 5.2 Candidate sets (precomputed lists)
Bạn nên precompute indices để nhanh:
- `AllBasicStorages`: HQ + Warehouses
- `AllAmmoStorages`: Armories
- `AllForgeBuildings`
- `AllResourceBuildingsByType`: wood producers, food producers...

Khuyến nghị: dùng `WorldIndexService` (Part 7.7).

---

## 6) Deterministic nearest storage selection

### 6.1 Distance metric
- Use Manhattan distance: `abs(dx)+abs(dy)` between NPC cell and building entry cell.
- Tie-break: smaller `BuildingId.Value` wins.

```csharp
public static class StorageSelector
{
    public static StorageSelectResult FindNearest(
        WorldState world,
        IStorageAccessor storage,
        ResourceType type,
        CellPos fromCell,
        System.Collections.Generic.IEnumerable<BuildingId> candidates,
        bool requireHasAmount,
        bool requireHasSpace)
    {
        var bestId = default(BuildingId);
        var bestCell = default(CellPos);
        var bestDist = int.MaxValue;
        var found = false;

        foreach (var id in candidates)
        {
            if (!world.Buildings.IsAlive(id.Value)) continue;
            ref var b = ref world.Buildings.Get(id.Value);

            if (!storage.CanStore(b, type)) continue;

            if (requireHasAmount && storage.GetAmount(ref b, type) <= 0) continue;
            if (requireHasSpace && storage.GetSpace(ref b, type) <= 0) continue;

            // use EntryCell for distance (more meaningful for path)
            var target = b.EntryCell;
            var dist = System.Math.Abs(target.X - fromCell.X) + System.Math.Abs(target.Y - fromCell.Y);

            if (!found || dist < bestDist || (dist == bestDist && id.Value < bestId.Value))
            {
                found = true;
                bestDist = dist;
                bestId = id;
                bestCell = target;
            }
        }

        return found ? StorageSelectResult.Success(bestId, bestCell)
                     : StorageSelectResult.FailRes(StorageSelectFail.NoCandidate);
    }
}
```

---

## 7) WorldIndexService (precomputed indices)

### 7.1 Why
Khi chạy game, bạn cần nhanh:
- tìm warehouse gần nhất
- tìm armory gần nhất
- list resource buildings by resource type

### 7.2 Index data
```csharp
public sealed class WorldIndexService
{
    private readonly WorldState _world;

    public readonly System.Collections.Generic.List<BuildingId> Warehouses = new(16);
    public readonly System.Collections.Generic.List<BuildingId> Armories = new(8);
    public readonly System.Collections.Generic.List<BuildingId> Forges = new(8);

    public readonly System.Collections.Generic.List<BuildingId> WoodProducers = new(32);
    public readonly System.Collections.Generic.List<BuildingId> FoodProducers = new(32);
    public readonly System.Collections.Generic.List<BuildingId> StoneProducers = new(32);
    public readonly System.Collections.Generic.List<BuildingId> IronProducers = new(32);

    public WorldIndexService(WorldState world) { _world = world; }

    public void RebuildAll(IDataRegistry data)
    {
        Warehouses.Clear(); Armories.Clear(); Forges.Clear();
        WoodProducers.Clear(); FoodProducers.Clear(); StoneProducers.Clear(); IronProducers.Clear();

        var e = _world.Buildings.GetEnumerator();
        while (e.MoveNext())
        {
            var idVal = e.CurrentIdValue;
            var id = new BuildingId(idVal);
            var b = e.Current;

            if ((b.Flags & BuildingFlags.Warehouse) != 0) Warehouses.Add(id);
            if ((b.Flags & BuildingFlags.Armory) != 0) Armories.Add(id);
            if ((b.Flags & BuildingFlags.Forge) != 0) Forges.Add(id);

            // Producers: detect by def.Category + tags or by def id naming
            // v0.1 recommendation: add fields to BuildingDef:
            // - ProducesResourceType (optional)
            // For now assume: you add `public ResourceType Produces;` in BuildingDef (future tweak)
        }
    }
}
```

### 7.3 Rebuild triggers
- On building placed/destroyed: update incremental (fast)
- v0.1 simplest: rebuild on `BuildingPlacedEvent`/`BuildingDestroyedEvent`

---

## 8) Priority rules for “take from where?” (important)

### 8.1 Builder/Smith taking inputs (basic)
Rule: when a consumer needs basic resource (wood/stone/iron/food):
1) Nearest **Warehouse**
2) If none / empty -> nearest **HQ**
3) If still not enough -> nearest **producer local storage** that has amount
4) If still not enough -> fail & notify `resource.insufficient.<type>`

### 8.2 TransporterBasic moving from producers -> warehouse/hq
Rule: When hauling basic produced resource:
- Source: nearest producer with local storage > 0
- Destination:
  1) nearest Warehouse that has space
  2) else HQ if has space
  3) else fail (notify `storage.warehouse_full` or `storage.local_full.<producer>`)

### 8.3 Smith (Forge) crafting ammo
- Input: basic resources (e.g. iron+wood) from priority rule 8.1
- Output:
  - Add Ammo to Forge local storage (cap set in Forge building def)
  - If Forge local ammo full -> notify `storage.local_full.<forgeId>`

### 8.4 ArmoryRunner
- If any tower needs ammo (<=25%):
  1) Ensure Armory has ammo; if not, pull from Forge local ammo
  2) Deliver ammo to that tower (tower internal ammo)
- If no tower needs ammo:
  - Pull ammo from Forge -> store in Armory (keep buffer)
- If Forge has no ammo => notify `ammo.forge_no_input` (or `ammo.armory_empty` depending stage)

> NOTE: actual delivery to tower is job logic, nhưng primitives cần hỗ trợ:
> - `TakeAmmoFromForgeToCarry`
> - `PutAmmoToArmory`
> - `PutAmmoToTower`

---

## 9) Tower ammo “request” model (hook for jobs)

### 9.1 Threshold rule
- When `TowerState.AmmoCurrent <= ThresholdAmmo` => tower emits `TowerAmmoRequestEvent`
- Duplicate suppression per tower with cooldown (notifications part 4 + request queue)

```csharp
public readonly struct TowerAmmoRequestEvent
{
    public readonly TowerId Tower;
    public readonly int AmmoNeeded;
    public TowerAmmoRequestEvent(TowerId t, int need) { Tower = t; AmmoNeeded = need; }
}
```

> ArmoryRunner job system sẽ subscribe và prioritize.

---

## 10) Notifications mapping (Part 4 keys)

### 10.1 Core notifications to trigger
- When consumer cannot find enough resources:
  - `resource.insufficient.<type>` (Warning)
- When no source exists:
  - `resource.no_source.<type>` (Warning)
- When local storage full:
  - `storage.local_full.<buildingId>` (Info/Warning)
- When warehouse/hq full:
  - `storage.warehouse_full` / `storage.hq_full` (Warning)
- When tower low ammo:
  - `ammo.tower_low.<towerId>` (Warning)
- When tower empty:
  - `ammo.tower_empty.<towerId>` (Error)
- When armory empty while tower needs ammo:
  - `ammo.armory_empty` (Warning/Error by design)

---

## 11) Minimal API surface for JobSystem (what Part 8 will use)

### 11.1 “Pick source/dest” functions
Provide these helper methods (thin wrappers):
- `FindNearestBasicStorageWithAmount(fromCell, type)`
- `FindNearestBasicStorageWithSpace(fromCell, type)`
- `FindNearestProducerWithAmount(fromCell, type)`
- `FindNearestArmoryWithSpace(fromCell)`
- `FindNearestForgeWithAmmo(fromCell)`

Each returns `StorageSelectResult`.

### 11.2 “Take/Put” functions
- `TryTake(BuildingId, type, amount, out taken)`
- `TryPut(BuildingId, type, amount, out added)`
- `TryTransfer(from, to, type, amount, out moved)`

---

## 12) QA Checklist (Part 7)
- [ ] Warehouse cannot store Ammo (enforced by CanStore)
- [ ] Nearest selection deterministic (distance + id tie-break)
- [ ] Priority rules implemented (warehouse -> hq -> producer)
- [ ] Transfer rollback works on partial add
- [ ] Indices rebuild correctly when buildings placed/destroyed
- [ ] Notification keys used consistently

---

## 13) Next Part (Part 8 đề xuất)
**JOB MODEL + JOB QUEUE (archetypes) + assignment rules** — dùng primitives Part 7 để tạo jobs sạch cho Worker/Transporter/Smith/ArmoryRunner/Builder.

