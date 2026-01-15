# PART 9 — BUILD PIPELINE (ORDERS → SITE → DELIVERY TRACKING → COMPLETE) — SPEC v0.1

> Mục tiêu: biến “player actions” (Build/Upgrade/Repair/Demolish/Move) thành pipeline runtime ổn định:
- Player tạo **Order** (queue)
- System tạo **BuildSite** (placeholder runtime)
- Builder/HQGeneralist chạy jobs: **Fetch → Deliver → Work → Complete**
- Theo dõi “delivered materials” để resume sau pause/save
- Integration: Placement validation (Part 5), Resource flow (Part 7), Job model (Part 8), Notifications (Part 4)

Phần này quyết định “xây dựng có thật sự fun & deterministic”.

---

## 1) Definitions

### 1.1 BuildAction
```csharp
public enum BuildAction
{
    PlaceNew,   // build a new building
    Upgrade,
    Repair,
    Demolish,
    Move
}
```

### 1.2 BuildOrder (player intent)
- Order là “ý định”, có thể cancel/reorder, có priority.

```csharp
public struct BuildOrder
{
    public int OrderId;
    public BuildAction Action;

    public string DefId;            // PlaceNew (building def), Upgrade (new tier def) optional
    public BuildingId TargetBuilding;// Upgrade/Repair/Demolish/Move

    // PlaceNew / Move destination
    public CellPos Anchor;
    public EntryPoint Entry;

    public int Priority;            // lower = higher priority (0 is top)
    public bool IsPlayerPinned;     // player pinned as must-do
}
```

---

## 2) BuildSite (runtime placeholder)

### 2.1 Why
Bạn cần object runtime để:
- Track delivered materials
- Show “under construction” visuals
- Allow save/load resume
- Provide a single target for builder jobs

### 2.2 BuildSiteState
```csharp
public struct BuildSiteState
{
    public int SiteId;                 // unique int
    public BuildAction Action;

    public string TargetDefId;         // for PlaceNew or Upgrade target def
    public BuildingId TargetBuilding;  // for existing building actions

    public CellPos Anchor;
    public EntryPoint Entry;
    public CellPos EntryCell;

    public int HPAtStart;              // for repair track
    public int TierAtStart;

    public CostDef CostTotal;          // required materials (multi-resource)
    public CostDef CostDelivered;      // delivered so far

    public float WorkSecondsTotal;     // build time
    public float WorkSecondsDone;      // progress

    public bool IsCommitted;           // indicates building placed/upgrade applied
}
```

### 2.3 CostDef (multi-resource bundle)
Bạn đã dùng `CostDef` ở Part 8 recipe hook. Chuẩn hoá dạng:

```csharp
[System.Serializable]
public struct CostDef
{
    public int Wood;
    public int Stone;
    public int Iron;
    public int Food;
    public int Ammo; // usually 0 for build

    public bool IsZero => Wood==0 && Stone==0 && Iron==0 && Food==0 && Ammo==0;
}
```

Helper ops:
- `Remaining = Total - Delivered`
- `AddDelivered(type, amount)`
- `IsSatisfied(Delivered>=Total)`

---

## 3) BuildOrderService (player queue)

### 3.1 Responsibilities
- Add/Remove/Reorder orders
- Convert order → BuildSite (spawn site state) if valid
- Prevent duplicates (e.g., upgrade same building twice)

### 3.2 API
```csharp
public sealed class BuildOrderService
{
    private int _nextOrderId = 1;
    private readonly System.Collections.Generic.List<BuildOrder> _orders = new(64);

    public IReadOnlyList<BuildOrder> Orders => _orders;

    public int Enqueue(in BuildOrder order)
    {
        var o = order;
        o.OrderId = _nextOrderId++;
        _orders.Add(o);
        SortByPriority();
        return o.OrderId;
    }

    public bool Cancel(int orderId) { /* remove; return true if removed */ return true; }
    public void SetPriority(int orderId, int priority) { /* update */ SortByPriority(); }
    private void SortByPriority()
        => _orders.Sort((a,b) => a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority) : a.OrderId.CompareTo(b.OrderId));
}
```

---

## 4) BuildSiteStore (runtime entities)

### 4.1 Store
Dùng EntityStore tương tự Part 6, hoặc đơn giản list.

```csharp
public sealed class BuildSiteStore
{
    private int _nextSiteId = 1;
    private readonly EntityStore<BuildSiteState> _sites = new(256);

    public int Create(in BuildSiteState s)
    {
        var id = _nextSiteId++;
        var ss = s; ss.SiteId = id;
        _sites.Create(id, ss);
        return id;
    }

    public bool IsAlive(int id) => _sites.IsAlive(id);
    public ref BuildSiteState Get(int id) => ref _sites.Get(id);
    public void Destroy(int id) => _sites.Destroy(id);
}
```

---

## 5) BuildSite creation rules (validation & setup)

### 5.1 PlaceNew
Steps:
1) Validate placement (Part 5) → get EntryCell + driveway plan
2) Create BuildSiteState with:
   - Action=PlaceNew
   - TargetDefId = building def id
   - Anchor/Entry/EntryCell
   - CostTotal from BuildingDef.BuildCost
   - WorkSecondsTotal from BuildingDef.BuildSeconds
   - Delivered = 0
3) Reserve footprint? (optional)
   - v0.1 recommended: mark “occupied by site” in occupancy layer using buildingId=negative?  
     Simpler: set occupancy to a special value to block other placements.
   - Or create a “SiteOccupancyLayer”
4) Notify if invalid:
   - `build.blocked.no_road` / `build.blocked.missing_resource` (only when confirm fail)

### 5.2 Upgrade
- Require existing building alive
- CostTotal from next tier def or upgrade table
- WorkSeconds from upgrade table
- Target location same as building
- BuildSiteState.TargetBuilding = building id

### 5.3 Repair
- Require building damaged
- CostTotal from repair cost rule (e.g. % of build cost)
- WorkSeconds from repair seconds table
- Delivered track
- On complete: HP restored

### 5.4 Demolish
- CostTotal = 0
- WorkSeconds = demolish time
- On complete: remove building, clear occupancy; optional refund resources (later)

### 5.5 Move
- Two-phase:
  1) Validate destination placement ignoring self occupancy
  2) CostTotal could be 0 or small “move fee”
  3) On complete: clear old occupancy, set new, apply driveway plan

---

## 6) Material delivery model (the key)

### 6.1 Why track delivery
- Builders can deliver partial
- Multiple builders can contribute (future)
- Save/load must resume

### 6.2 Remaining cost
`Remaining = CostTotal - CostDelivered`
- If remaining is satisfied (<=0 all types) => site ready to “Work”.

### 6.3 Delivery rules
- Builder carries 1 resource type at a time (v0.1)
- Deliver chunk size = min(remaining[type], builder carry amount)
- Delivered increments; carry decremented

---

## 7) Job generation for BuildSites (BuildJobProvider)

### 7.1 Provider responsibilities
- For each active build site:
  - If not materials satisfied: create “DeliverMaterial” jobs
  - Else: create “WorkOnSite” job
- OwnerWorkplace:
  - BuilderHut (if exists) else HQ
- Jobs should be throttled (avoid infinite create duplicates)

### 7.2 Job types needed
v0.1 introduces 2 internal archetypes (or reuse Build/Repair/Upgrade with step semantics):
- `DeliverMaterial` (subtype of Build)
- `WorkOnSite` (subtype of Build)

Simplest: keep JobArchetype = Build/Upgrade/Repair/Demolish/Move and store SiteId in job.

Add to JobData:
```csharp
public int SiteId; // link to BuildSiteState
public ResourceType DeliverType; // when delivery job
public int DeliverAmount; // requested
public bool IsDeliveryJob;
```

### 7.3 Throttle strategy
Per site keep “job tokens”:
- `bool HasActiveDeliveryJobForWood` etc.
- `bool HasActiveWorkJob`

Store in BuildSiteState:
```csharp
public byte ActiveDeliveryMask; // bit per resource type
public bool HasActiveWorkJob;
```

---

## 8) Builder execution flow (uses Part 7 + Part 8 executor)

### 8.1 Delivery job execution
Steps:
1) Determine needed type/amount from JobData (DeliverType/DeliverAmount)
2) Select source using priority rule (Part 7):
   - Warehouse -> HQ -> producer
3) GoToSource; Take into carry
4) GoToSite; Deliver:
   - Update `BuildSiteState.CostDelivered += delivered`
5) If delivered completes type requirement, clear mask bit; job completes.

Failure:
- No source / insufficient -> job fails, notify `resource.insufficient.<type>`
- Path fail -> `path.blocked` (cooldown)

### 8.2 Work job execution
Precondition: materials satisfied.
Steps:
1) GoToSite
2) Work for `WorkSecondsRemaining` (tick StepTimer)
3) On complete:
   - Apply action result (commit)

---

## 9) Commit rules per action

### 9.1 PlaceNew commit
When work completed:
- Create building in WorldState + Grid occupancy using Part 6/5:
  - Option A: we already occupied footprint with “site occupancy”. Convert to real occupancy by buildingId.
  - Apply driveway conversion if needed (already planned).
- Destroy build site state
- Notify: optional “build complete” info (not required)

### 9.2 Upgrade commit
- Increase tier / swap def id
- Update storage caps/workplace slots if tier changes
- Destroy site state

### 9.3 Repair commit
- Set HP = MaxHP
- Destroy site state

### 9.4 Demolish commit
- Remove building state + clear grid occupancy
- Destroy site state

### 9.5 Move commit
- Clear old occupancy
- Set new occupancy
- Update building anchor/entry/entryCell
- Apply driveway conversion at new position
- Destroy site state

---

## 10) Concurrency rules (multi-builders safe)

v0.1 allows 1 builder or multiple. To stay safe:
- Claim system (Part 8) should claim:
  - SiteId claim (exclusive) for Work job
  - Delivery jobs can be per resource type claim:
    - Claim key: `KeyForSite(siteId, resourceType)`
- BuildSite mutations must be atomic (single thread in main loop is fine).

Claim key helper:
```csharp
public static int KeyForSite(int siteId) => 3000000 + siteId;
public static int KeyForSiteResource(int siteId, ResourceType t) => 4000000 + (siteId * 32) + (int)t;
```

---

## 11) UI hooks (player guidance)

### 11.1 Site visual states
- “Needs materials” show icon
- “Working” show progress bar
- “Blocked” show reason (if last fail reason cached)

Add to BuildSiteState:
```csharp
public JobFailReason LastFail;
public float LastFailAt;
```

### 11.2 Notifications
- On confirm build when invalid:
  - `build.blocked.no_road`
- When delivery cannot proceed:
  - `resource.insufficient.<type>`
- When site stuck long:
  - `workplace.none` or `build.blocked.missing_resource` (throttled)

---

## 12) Save/Load
Save includes:
- BuildOrder queue
- BuildSiteStore states
- Any active jobs referencing SiteId
On load:
- Rebuild indices (Part 7)
- Ensure placement occupancy for sites is restored (site blocks area)

---

## 13) Minimal starting run setup (fits your start proposal)
Player starts with:
- HQ (1 NPC HQGeneralist)
- 2 Houses (capacity 4)
- Farmhouse + zone (1 Worker assigned)
- LumberCamp (1 Worker assigned)
- 1 Arrow Tower (ammo full)
Auto-fill only at run start (locked)

Build pipeline must support:
- Place Warehouse (to reduce local full)
- Place Forge + Armory later (unlock-driven)

---

## 14) QA Checklist (Part 9)
- [ ] PlaceNew: validate + create site + delivery + complete creates building
- [ ] Materials delivered tracked correctly and persists
- [ ] Builder uses priority source order and respects Warehouse no-ammo rule
- [ ] Work job cannot start until materials satisfied
- [ ] Site occupancy blocks overlapping placement
- [ ] Cancel order destroys site + releases claims safely
- [ ] Move respects road/driveway rules

---

## 15) Next Part (Part 10 đề xuất)
**Economy loop end-to-end** (Harvest → Local → Haul → Consume → Ammo pipeline) + “day/season pacing hooks” để run hoàn chỉnh.

