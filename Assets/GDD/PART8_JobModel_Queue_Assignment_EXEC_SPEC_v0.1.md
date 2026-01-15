# PART 8 — JOB MODEL + JOB QUEUE + ASSIGNMENT RULES (WORKER / TRANSPORTER / SMITH / ARMORY RUNNER / BUILDER) — SPEC v0.1

> Mục tiêu: tạo “xương sống gameplay” để AI/NPC có thể tự vận hành:
- Job archetypes rõ ràng, data-driven vừa đủ
- Job lifecycle chuẩn: Create → Claim → Execute → Complete/Fail → Cleanup
- Hạn chế race condition: claim-based (exclusive)
- Tách JobData (immutable) vs JobRuntime (mutable progress)
- Dispatch theo Role + Workplace assignments (NPC assigned vào building mới có job tương ứng)
- Integrate primitives Part 7 (resource flow) + notifications Part 4
- Không over-engineer: v0.1 hướng đến ship, sau này mở rộng.

Phần này **không** viết pathfinding cụ thể hay animation; chỉ model + scheduler + tick contract.

---

## 1) Definitions

### 1.1 JobArchetype (từ Part 0/Deliverable)
Dùng lại enum đã có:
- Leisure, Inspect
- HarvestFood/Wood/Stone/Iron
- HaulBasic, CraftAmmo, HaulAmmo, ResupplyTower
- Build, Upgrade, Repair, Demolish

### 1.2 JobId
```csharp
public readonly struct JobId
{
    public readonly int Value;
    public JobId(int v) { Value = v; }
    public bool IsValid => Value != 0;
}
```

### 1.3 Job status
```csharp
public enum JobStatus
{
    Created,
    Claimed,
    InProgress,
    Completed,
    Failed,
    Cancelled
}
```

### 1.4 Fail reasons (for analytics + notify)
```csharp
public enum JobFailReason
{
    None,
    NoPath,
    SourceEmpty,
    DestFull,
    ResourceInsufficient,
    TargetDestroyed,
    RuleViolation,
    Timeout
}
```

---

## 2) Job data split (immutable vs runtime)

### 2.1 JobData (immutable core)
JobData là struct, không thay đổi sau khi tạo.

```csharp
public struct JobData
{
    public JobId Id;
    public JobArchetype Type;

    // Ownership
    public BuildingId OwnerWorkplace; // building that "provides" this job (HQ, Warehouse, Forge, Armory, BuilderHut, etc.)

    // Targets
    public BuildingId SourceBuilding;  // optional
    public BuildingId DestBuilding;    // optional
    public TowerId TargetTower;        // for resupply
    public CellPos TargetCell;         // fallback for pathing

    // Resource
    public ResourceType ResourceType;
    public int Amount;

    // Timing (work duration)
    public float WorkSeconds;          // time spent working at site (harvest/craft/build)
}
```

### 2.2 JobRuntime (mutable progress + claim)
```csharp
public struct JobRuntime
{
    public JobStatus Status;

    public NpcId ClaimedBy;
    public float Progress01;

    public float CreatedAt;   // unscaled time
    public float StartedAt;   // when InProgress

    public JobFailReason FailReason;

    // Claim tokens (exclusive locks)
    public ClaimToken SourceClaim;
    public ClaimToken DestClaim;
    public ClaimToken TowerClaim;

    public void Reset()
    {
        Status = JobStatus.Created;
        ClaimedBy = default;
        Progress01 = 0f;
        FailReason = JobFailReason.None;
        SourceClaim = default;
        DestClaim = default;
        TowerClaim = default;
    }
}
```

---

## 3) Claim system (prevent contention)

### 3.1 Claim targets
- Source building storage (exclusive per resource type? v0.1: per building)
- Dest building storage (per building)
- Tower resupply (per tower)

### 3.2 ClaimToken + ClaimService
```csharp
public readonly struct ClaimToken
{
    public readonly int Key;     // unique key representing claimed object
    public readonly int OwnerId; // npcId.Value
    public ClaimToken(int key, int ownerId) { Key = key; OwnerId = ownerId; }
    public bool IsValid => Key != 0;
}

public sealed class ClaimService
{
    private readonly System.Collections.Generic.Dictionary<int, int> _claims = new(512); // key -> npcId

    public bool TryClaim(int key, NpcId npc, out ClaimToken token)
    {
        token = default;
        if (_claims.TryGetValue(key, out var owner) && owner != 0) return false;
        _claims[key] = npc.Value;
        token = new ClaimToken(key, npc.Value);
        return true;
    }

    public void Release(in ClaimToken token)
    {
        if (!token.IsValid) return;
        if (_claims.TryGetValue(token.Key, out var owner) && owner == token.OwnerId)
            _claims.Remove(token.Key);
    }

    // key helpers
    public static int KeyForBuilding(BuildingId b) => 1000000 + b.Value; // stable mapping
    public static int KeyForTower(TowerId t) => 2000000 + t.Value;
}
```

> v0.1: claim per building/tower is enough. Later you can claim per resource lane.

---

## 4) JobStore (queues + lookup)

### 4.1 Requirements
- O(1) add/remove
- Enumerate jobs for scheduling
- Keep per-workplace queues (since NPC assigned to building only do those jobs)

### 4.2 Structures (simple, no allocations per tick)
- Global `EntityStore<JobData>` + `EntityStore<JobRuntime>` keyed by jobId.Value
- Per workplace: `Dictionary<int, SimpleJobQueue>` or `List<JobId>` in WorkplaceState (but workplace is struct).
- v0.1 simplest: `JobBoard` maintains:
  - `Dictionary<int, JobRingBuffer>`: workplaceId -> ring buffer of job ids

### 4.3 Ring buffer
```csharp
public sealed class JobRingBuffer
{
    private JobId[] _buf;
    private int _head, _tail, _count;

    public int Count => _count;

    public JobRingBuffer(int cap = 64)
    {
        _buf = new JobId[cap];
    }

    public void Enqueue(JobId id)
    {
        if (_count == _buf.Length) Grow();
        _buf[_tail] = id;
        _tail = (_tail + 1) % _buf.Length;
        _count++;
    }

    public bool TryDequeue(out JobId id)
    {
        if (_count == 0) { id = default; return false; }
        id = _buf[_head];
        _head = (_head + 1) % _buf.Length;
        _count--;
        return true;
    }

    public JobId PeekAt(int index) // 0..count-1
    {
        var i = (_head + index) % _buf.Length;
        return _buf[i];
    }

    private void Grow()
    {
        var n = new JobId[_buf.Length * 2];
        for (int i = 0; i < _count; i++) n[i] = PeekAt(i);
        _buf = n;
        _head = 0; _tail = _count;
    }
}
```

### 4.4 JobBoard
```csharp
public sealed class JobBoard
{
    private readonly IdGenerator _jobIds = new();

    private readonly EntityStore<JobData> _data = new(1024);
    private readonly EntityStore<JobRuntime> _rt = new(1024);

    private readonly System.Collections.Generic.Dictionary<int, JobRingBuffer> _byWorkplace = new(128);

    public JobId CreateJob(in JobData jd)
    {
        var id = new JobId(_jobIds.Next());
        var data = jd; data.Id = id;

        _data.Create(id.Value, data);
        _rt.Create(id.Value, new JobRuntime { Status = JobStatus.Created, CreatedAt = UnityEngine.Time.unscaledTime });

        var wpKey = data.OwnerWorkplace.Value;
        if (!_byWorkplace.TryGetValue(wpKey, out var q))
            _byWorkplace[wpKey] = q = new JobRingBuffer(64);

        q.Enqueue(id);
        return id;
    }

    public bool IsAlive(JobId id) => _data.IsAlive(id.Value);

    public ref JobData GetData(JobId id) => ref _data.Get(id.Value);
    public ref JobRuntime GetRuntime(JobId id) => ref _rt.Get(id.Value);

    public JobRingBuffer GetQueueFor(BuildingId workplace)
        => _byWorkplace.TryGetValue(workplace.Value, out var q) ? q : null;

    public void DestroyJob(JobId id)
    {
        if (!IsAlive(id)) return;
        _data.Destroy(id.Value);
        _rt.Destroy(id.Value);
        // NOTE: queue contains stale id; scheduler must skip dead jobs (v0.1).
    }
}
```

> v0.1 simplification: không remove khỏi ring buffer khi destroy (đỡ phức). Scheduler skip dead.

---

## 5) Workplace-based job provisioning rules

### 5.1 Rule
NPC chỉ được nhận job từ workplace mình được assign (hoặc HQ generalist rule):
- NPC assigned to `BuilderHut` => Build/Upgrade/Repair/Demolish jobs
- NPC assigned to `Warehouse` => HaulBasic jobs
- NPC assigned to `Forge` => CraftAmmo jobs
- NPC assigned to `Armory` => HaulAmmo + ResupplyTower jobs
- NPC assigned to `HQ` => Build/Repair/HaulBasic (LOCKED: không harvest)

### 5.2 Unassigned NPC
- Chỉ có Leisure/Inspect jobs (from global Leisure system) cho đến khi player assign.

---

## 6) Job creation (who creates jobs?)

### 6.1 Producer job creators
Mỗi workplace có “JobProvider” riêng. v0.1 pattern:
- JobProvider tick theo cadence (1–2s) để tạo jobs khi cần.

**A) Harvest providers (resource buildings)**
- Nếu local storage có space và có worker assigned:
  - Create Harvest job (amount chunk) cho workplace = resource building
- WorkSeconds / Amount dựa vào def tier (later use balance table)

**B) HaulBasic provider (Warehouse/HQ)**
- If there exists producer with local amount > 0 and warehouse has space:
  - Create HaulBasic job
  - OwnerWorkplace = Warehouse (so only its assigned transporter picks)
  - SourceBuilding = producer
  - DestBuilding = nearest warehouse (or HQ) — pick at claim time to be robust

**C) Forge provider**
- If forge output ammo storage has space:
  - If forge has enough inputs locally? (optional) else create “Fetch inputs” job?  
  v0.1 simplification: Smith job includes fetching inputs from storage:
  - Create CraftAmmo job with input type(s) embedded? (we need multi-input)
  - Since JobData has 1 resource only, we define “RecipeId” in job (see 6.3)

**D) Armory provider**
- If armory ammo < target buffer OR towers need ammo:
  - Create HaulAmmo job (Forge -> Armory)
- If any tower needs ammo:
  - Create ResupplyTower job (Armory -> Tower)

**E) Builder provider (HQ/BuilderHut)**
- When player queues build/upgrade/repair:
  - Create Build job with required cost (multi-resource)
  - Builder must fetch materials using Part 7 rules

### 6.2 Multi-input support (Recipes)
Ammo crafting needs multiple inputs (e.g. Iron + Wood).  
v0.1: add `RecipeId` field for Craft jobs; builder jobs also use `CostDef`.

Add to JobData:
```csharp
public string RecipeId;   // for CraftAmmo
public CostDef Cost;      // for Build/Upgrade/Repair/Ammo (optional)
```

> Nếu bạn không muốn enlarge JobData nhiều: chỉ add fields used by relevant jobs.

### 6.3 RecipeDef (data)
```csharp
[CreateAssetMenu(menuName="Game/Defs/RecipeDef")]
public sealed class RecipeDef : ScriptableObject
{
    public string Id; // recipe.ammo.arrow
    public CostDef Inputs;   // use CostDef as multi-resource bundle
    public ResourceType OutputType; // Ammo
    public int OutputAmount; // e.g. 10
    public float WorkSeconds; // craft time
}
```

Validator (Part 0) sẽ đảm bảo Forge/Armory chain exists.

---

## 7) Scheduler (job assignment to NPC)

### 7.1 Constraints
- Deterministic tie-break (so save/rewind stable)
- No heavy allocation
- Runs at fixed cadence (e.g. 0.5–1.0s)

### 7.2 Role eligibility
```csharp
public static class RoleEligibility
{
    public static bool CanDo(NpcRole role, JobArchetype job)
    {
        return role switch
        {
            NpcRole.Worker => job is JobArchetype.HarvestFood or JobArchetype.HarvestWood or JobArchetype.HarvestStone or JobArchetype.HarvestIron,
            NpcRole.TransporterBasic => job == JobArchetype.HaulBasic,
            NpcRole.Smith => job == JobArchetype.CraftAmmo,
            NpcRole.ArmoryRunner => job is JobArchetype.HaulAmmo or JobArchetype.ResupplyTower,
            NpcRole.Builder => job is JobArchetype.Build or JobArchetype.Upgrade or JobArchetype.Repair or JobArchetype.Demolish,
            NpcRole.HQGeneralist => job is JobArchetype.Build or JobArchetype.Repair or JobArchetype.HaulBasic,
            _ => job is JobArchetype.Leisure or JobArchetype.Inspect
        };
    }
}
```

### 7.3 Scheduler algorithm (v0.1)
For each NPC idle:
1) Determine workplace:
   - If assigned workplace valid -> use it
   - Else -> only Leisure/Inspect pool
2) Fetch queue for workplace from JobBoard
3) Iterate jobs in queue (bounded scan, e.g. max 12)
4) Pick first job that:
   - alive
   - status Created
   - eligible by role
   - claimable (source/dest/tower claims)
   - has feasible resource preconditions (optional, fast checks)
5) Claim → set runtime ClaimedBy → assign npc.CurrentJobId

Deterministic tie-break:
- Iterate NPCs by id ascending
- Iterate jobs by queue order (older first)
- Claim service ensures uniqueness

### 7.4 Scheduler skeleton
```csharp
public sealed class JobScheduler
{
    private readonly WorldState _world;
    private readonly JobBoard _jobs;
    private readonly ClaimService _claims;
    private readonly IDataRegistry _data;
    private readonly WorldIndexService _index; // from Part 7

    public JobScheduler(WorldState w, JobBoard jobs, ClaimService claims, IDataRegistry data, WorldIndexService index)
    { _world = w; _jobs = jobs; _claims = claims; _data = data; _index = index; }

    public void TickAssign()
    {
        var e = _world.Npcs.GetEnumerator();
        while (e.MoveNext())
        {
            var npcId = new NpcId(e.CurrentIdValue);
            ref var npc = ref _world.Npcs.Get(npcId.Value);
            if (npc.Status != NpcStatus.Idle) continue;
            if (npc.CurrentJobId != 0) continue;

            TryAssignOne(npcId, ref npc);
        }
    }

    private void TryAssignOne(NpcId npcId, ref NpcState npc)
    {
        // unassigned -> leisure/inspect handled by separate system (or global workplace id=HQ)
        if (!npc.AssignedWorkplace.IsValid)
            return;

        var q = _jobs.GetQueueFor(npc.AssignedWorkplace);
        if (q == null || q.Count == 0) return;

        const int SCAN_LIMIT = 12;
        var scan = System.Math.Min(SCAN_LIMIT, q.Count);

        for (int i = 0; i < scan; i++)
        {
            var jobId = q.PeekAt(i);
            if (!_jobs.IsAlive(jobId)) continue;

            ref var jd = ref _jobs.GetData(jobId);
            ref var rt = ref _jobs.GetRuntime(jobId);

            if (rt.Status != JobStatus.Created) continue;
            if (!RoleEligibility.CanDo(npc.Role, jd.Type)) continue;

            if (!TryClaimJob(jobId, npcId, ref jd, ref rt)) continue;

            rt.Status = JobStatus.Claimed;
            rt.ClaimedBy = npcId;

            npc.CurrentJobId = jobId.Value;
            npc.Status = NpcStatus.Moving; // start moving to job target
            return;
        }
    }

    private bool TryClaimJob(JobId jobId, NpcId npcId, ref JobData jd, ref JobRuntime rt)
    {
        // claim source building if exists
        if (jd.SourceBuilding.IsValid)
        {
            if (!_claims.TryClaim(ClaimService.KeyForBuilding(jd.SourceBuilding), npcId, out rt.SourceClaim))
                return false;
        }

        if (jd.DestBuilding.IsValid)
        {
            if (!_claims.TryClaim(ClaimService.KeyForBuilding(jd.DestBuilding), npcId, out rt.DestClaim))
            {
                _claims.Release(rt.SourceClaim);
                rt.SourceClaim = default;
                return false;
            }
        }

        if (jd.TargetTower.IsValid)
        {
            if (!_claims.TryClaim(ClaimService.KeyForTower(jd.TargetTower), npcId, out rt.TowerClaim))
            {
                _claims.Release(rt.SourceClaim);
                _claims.Release(rt.DestClaim);
                rt.SourceClaim = default;
                rt.DestClaim = default;
                return false;
            }
        }

        return true;
    }
}
```

---

## 8) Job execution (tick contract)

### 8.1 State machine per NPC
NPC executes job as a sequence of steps:
1) Move to source/target cell
2) Work for `WorkSeconds` (harvest/craft/build)
3) Transfer resources using Part 7 (take/put)
4) Mark job completed; release claims; clear npc job

v0.1: represent steps in `NpcState` with a small enum.

```csharp
public enum JobStep
{
    None,
    GoToSource,
    TakeFromSource,
    GoToDest,
    PutToDest,
    GoToTarget,     // tower/build site
    WorkAtTarget,
    Complete
}
```

Add to `NpcState`:
- `public JobStep JobStep;`
- `public float StepTimer;`

### 8.2 Executor service
```csharp
public sealed class JobExecutor
{
    private readonly WorldState _world;
    private readonly JobBoard _jobs;
    private readonly ClaimService _claims;
    private readonly ResourceFlowService _flow;
    private readonly INotificationService _notifs;

    public void Tick(float dt)
    {
        // iterate NPCs with current job
        // state machine by job type
    }
}
```

### 8.3 Execution templates (per archetype)

#### A) Harvest* (Worker on resource building)
- Target: producer building (owner workplace)
- Steps:
  1) GoToTarget (producer entry cell or resource zone)
  2) WorkAtTarget for harvest seconds
  3) Add resource to producer local storage (`TryAdd`), if full -> fail with notification `storage.local_full.<id>`
- Completion: job done.

#### B) HaulBasic (TransporterBasic)
- Source: producer
- Dest: Warehouse/HQ (selected at claim time or just-in-time)
- Steps:
  1) GoToSource
  2) TakeFromSource: take up to npc.Carry.Cap of resource type from producer local storage
  3) GoToDest
  4) PutToDest: add to dest storage; if full -> fail (put back? optional)
- Fail handling:
  - source empty -> fail `SourceEmpty` (job can be cancelled)
  - dest full -> notify `storage.warehouse_full`

#### C) CraftAmmo (Smith)
- Steps:
  1) Fetch inputs (multi-resource) from storages into Forge local storage or directly “consume”
     - v0.1 recommended: Smith carries inputs one-by-one (or treat as instant if you don't model carry multiple types)
  2) WorkAtTarget in Forge for recipe seconds
  3) Put ammo to Forge local storage (type Ammo)
- Fail:
  - insufficient inputs -> `resource.insufficient.iron` etc + notify `ammo.forge_no_input` (or `resource.insufficient.<type>`)

> NOTE: v0.1 simplification for carry:
> - Smith carry only 1 type at a time: loop inputs sequentially.
> - This is fine for ship; not over-engineer.

#### D) HaulAmmo (ArmoryRunner: Forge -> Armory)
- Source: Forge (Ammo local)
- Dest: Armory (Ammo storage)
- Steps identical to HaulBasic but resource type Ammo.
- Fail:
  - Armory full -> notify `storage.local_full.<armoryId>`
  - Forge empty -> just complete/skip

#### E) ResupplyTower (ArmoryRunner: Armory -> Tower)
- Source: Armory ammo storage
- Dest: Tower internal ammo
- Steps:
  1) GoToArmory
  2) Take ammo into carry
  3) GoToTower
  4) Put into tower: `TowerState.AmmoCurrent = min(max, +carry)`
- Fail:
  - armory empty -> notify `ammo.armory_empty`
  - tower destroyed -> fail TargetDestroyed

#### F) Build/Upgrade/Repair (Builder / HQGeneralist / BuilderHut)
- Target: build site buildingId (or ghost plan)
- Requires `CostDef` multi-resources:
  - builder fetches materials from Warehouse/HQ/Producer (Part 7 priority)
- Steps (concept):
  1) For each needed resource type:
     - GoToSource (nearest with amount)
     - Take to carry
     - GoToTarget
     - Deliver (track delivered amount)
  2) WorkAtTarget for build seconds
  3) On complete: create building / apply upgrade / repair HP.

> Build/upgrade/repair orchestration is big; v0.1 spec defines the model; Part 9 can detail build pipeline.

---

## 9) Job cancellation & cleanup

### 9.1 When to cancel
- Target destroyed / removed
- Claim invalidated (source/dest no longer exists)
- NPC reassigned by player (manual cancel)

### 9.2 Cleanup must always happen
- Release all claim tokens
- Clear npc.CurrentJobId, step state
- Optionally return carried resources to nearest storage (best effort), or drop (not recommended)

### 9.3 Cleanup helper
```csharp
public static class JobCleanup
{
    public static void ReleaseClaims(ClaimService claims, ref JobRuntime rt)
    {
        claims.Release(rt.SourceClaim);
        claims.Release(rt.DestClaim);
        claims.Release(rt.TowerClaim);

        rt.SourceClaim = default;
        rt.DestClaim = default;
        rt.TowerClaim = default;
    }
}
```

---

## 10) Notifications (Part 4 integration)
- On job fail due to resource:
  - `resource.insufficient.<type>` (Warning)
- On dest full:
  - `storage.warehouse_full` or `storage.local_full.<id>`
- On tower empty:
  - `ammo.tower_empty.<towerId>` (Error)
- On no job available for assigned NPC:
  - `workplace.none` (Info, cooldown high)

---

## 11) Minimum viable “run works” job set (v0.1)
Để chạy được loop:
- HarvestWood, HarvestFood (2 producers)
- HaulBasic (move to HQ/warehouse)
- CraftAmmo (forge)
- HaulAmmo (forge -> armory)
- ResupplyTower (armory -> tower)
- Build/Repair basic (HQ generalist)

Leisure/Inspect chỉ phụ để giảm dead time.

---

## 12) QA Checklist (Part 8)
- [ ] JobBoard create/queue by workplace OK
- [ ] Scheduler assigns only eligible jobs
- [ ] Claims prevent 2 NPC doing same transfer/tower
- [ ] Cleanup always releases claims
- [ ] No per-frame allocations in scheduler
- [ ] Notifications not spam (use keys + cooldown)
- [ ] Unassigned NPC doesn't steal workplace jobs

---

## 13) Next Part (Part 9 đề xuất)
**BUILD PIPELINE (player orders → build site → material delivery tracking → completion)** + “Move/Demolish/Repair” specifics.

