# PART 25 — TECHNICAL INTERFACES PACK (SERVICES + EVENTS + DTOS) — LOCKED SPEC v0.1

> Mục tiêu: gom **mọi interface + event + DTO** vào 1 chỗ để code giữa các Part không lệch nhau.
- Không bàn UI chi tiết ở đây, chỉ contract.
- Tất cả service phải **single source of truth**.
- Các hệ thống khác chỉ được gọi qua interface (hoặc concrete service nhưng theo đúng method list).

---

## 0) Conventions

### 0.1 Id types (strongly typed)
```csharp
public readonly struct BuildingId { public readonly int Value; public BuildingId(int v)=>Value=v; }
public readonly struct NpcId      { public readonly int Value; public NpcId(int v)=>Value=v; }
public readonly struct TowerId    { public readonly int Value; public TowerId(int v)=>Value=v; }
public readonly struct EnemyId    { public readonly int Value; public EnemyId(int v)=>Value=v; }
public readonly struct SiteId     { public readonly int Value; public SiteId(int v)=>Value=v; }
public readonly struct JobId      { public readonly int Value; public JobId(int v)=>Value=v; }
```

### 0.2 Cell types
```csharp
public readonly struct CellPos
{
    public readonly int X, Y;
    public CellPos(int x,int y){X=x;Y=y;}
}
public enum Dir4 { N,E,S,W }
```

### 0.3 Resource types
```csharp
public enum ResourceType
{
    Wood,
    Food,
    Stone,
    Iron,
    Ammo
}
```

### 0.4 Service access pattern
- Prefer one `GameServices` container that is created once at boot.
```csharp
public sealed class GameServices
{
    public IDataRegistry Data;
    public IRunClock RunClock;
    public INotificationService Notifications;
    public IGridMap Grid;
    public IWorldState World;
    public IWorldIndex Index;
    public IPlacementService Placement;
    public IStorageService Storage;
    public IResourceFlowService ResourceFlow;
    public IClaimService Claims;
    public IJobBoard Jobs;
    public IJobScheduler Scheduler;
    public IBuildOrderService BuildOrders;
    public IAmmoService Ammo;
    public ICombatService Combat;
    public IRewardService Rewards;
    public IRunOutcomeService RunOutcome;
    public ISaveService Save;
    public IAudioService Audio; // optional
    public IFXService FX;       // optional
}
```

---

## 1) DATA REGISTRY + VALIDATION

### 1.1 IDataRegistry
```csharp
public interface IDataRegistry
{
    T GetDef<T>(string id) where T : UnityEngine.Object;
    bool TryGetDef<T>(string id, out T def) where T : UnityEngine.Object;

    // common typed accessors:
    BuildingDef GetBuilding(string id);
    EnemyDef GetEnemy(string id);
    WaveDef GetWave(string id);
    RewardDef GetReward(string id);
    RecipeDef GetRecipe(string id);
}
```

### 1.2 IDataValidator
```csharp
public interface IDataValidator
{
    // Returns true if ok; errors list filled.
    bool ValidateAll(IDataRegistry reg, System.Collections.Generic.List<string> errors);
}
```

---

## 2) RUN CLOCK + SPEED CONTROLS

### 2еminder: Defend default 1x; allow 2x/3x if enabled.

### 2.1 Season/Phase enums
```csharp
public enum Season { Spring, Summer, Autumn, Winter }
public enum Phase  { Build, Defend }
```

### 2.2 IRunClock
```csharp
public interface IRunClock
{
    Season CurrentSeason { get; }
    int DayIndex { get; }             // 1-based or 0-based but must be consistent
    Phase CurrentPhase { get; }

    float TimeScale { get; }          // current applied
    bool DefendSpeedUnlocked { get; } // dev/user setting

    void SetTimeScale(float scale);   // 0,1,2,3 (or 0.5)
    void ForceSeasonDay(Season s, int dayIndex);

    // Time events
    event System.Action<Season,int> OnSeasonDayChanged;
    event System.Action<Phase> OnPhaseChanged;
    event System.Action OnDayEnded;
}
```

---

## 3) NOTIFICATIONS (UI STACK + SPAM CONTROL)

### 3.1 Notification types
```csharp
public enum NotificationSeverity { Info, Warning, Error }

public readonly struct NotificationId
{
    public readonly int Value;
    public NotificationId(int v){Value=v;}
}

public readonly struct NotificationPayload
{
    public readonly BuildingId Building;
    public readonly TowerId Tower;
    public readonly string Extra;
    public NotificationPayload(BuildingId b, TowerId t, string extra)
    { Building=b; Tower=t; Extra=extra; }
}
```

### 3.2 INotificationService
```csharp
public interface INotificationService
{
    int MaxVisible { get; } // 3
    NotificationId Push(string key, string title, string body,
                        NotificationSeverity severity,
                        NotificationPayload payload,
                        float cooldownSeconds = 3f,
                        bool dedupeByKey = true);

    void Dismiss(NotificationId id);
    void ClearAll();

    event System.Action NotificationsChanged;
    System.Collections.Generic.IReadOnlyList<NotificationViewModel> GetVisible();
}
```

### 3.3 NotificationViewModel DTO
```csharp
public sealed class NotificationViewModel
{
    public NotificationId Id;
    public string Key;
    public string Title;
    public string Body;
    public NotificationSeverity Severity;
    public NotificationPayload Payload;
    public float CreatedAt;
}
```

---

## 4) GRID MAP + OCCUPANCY + ROAD

### 4.1 Cell occupancy
```csharp
public enum CellOccupancyKind { Empty, Road, Building, Site }

public readonly struct CellOccupancy
{
    public readonly CellOccupancyKind Kind;
    public readonly BuildingId Building;
    public readonly SiteId Site;
    public CellOccupancy(CellOccupancyKind k, BuildingId b, SiteId s)
    { Kind=k; Building=b; Site=s; }
}
```

### 4.2 IGridMap
```csharp
public interface IGridMap
{
    int Width { get; }
    int Height { get; }

    bool IsInside(CellPos c);
    CellOccupancy Get(CellPos c);

    bool IsRoad(CellPos c);
    bool IsBlocked(CellPos c);

    // Mutations should be controlled by services (Placement/BuildSite),
    // but GridMap provides low-level apply methods.
    void SetRoad(CellPos c, bool isRoad);
    void SetBuilding(CellPos c, BuildingId id);
    void ClearBuilding(CellPos c);

    void SetSite(CellPos c, SiteId id);
    void ClearSite(CellPos c);
}
```

---

## 5) WORLD STATE + STORES + OPS

### 5.1 IWorldState (read/write)
```csharp
public interface IWorldState
{
    // Stores
    IBuildingStore Buildings { get; }
    INpcStore Npcs { get; }
    ITowerStore Towers { get; }
    IEnemyStore Enemies { get; }
    IBuildSiteStore Sites { get; }

    // Global modifiers
    ref RunModifiers RunMods { get; }  // from Part 12
}
```

### 5.2 Stores (minimal contract)
```csharp
public interface IEntityStore<TId,TState>
{
    bool Exists(TId id);
    TState Get(TId id);
    void Set(TId id, TState state);      // overwrite
    TId Create(TState state);
    void Destroy(TId id);

    int Count { get; }
    System.Collections.Generic.IEnumerable<TId> Ids { get; }
}

public interface IBuildingStore : IEntityStore<BuildingId, BuildingState> {}
public interface INpcStore      : IEntityStore<NpcId, NpcState> {}
public interface ITowerStore    : IEntityStore<TowerId, TowerState> {}
public interface IEnemyStore    : IEntityStore<EnemyId, EnemyState> {}
public interface IBuildSiteStore: IEntityStore<SiteId, BuildSiteState> {}
```

### 5.3 WorldOps (place/destroy)
```csharp
public interface IWorldOps
{
    BuildingId CreateBuilding(string buildingDefId, CellPos anchor, Dir4 rotation);
    void DestroyBuilding(BuildingId id);

    NpcId CreateNpc(string npcDefId, CellPos spawn);
    void DestroyNpc(NpcId id);

    EnemyId CreateEnemy(string enemyDefId, CellPos spawn, int lane);
    void DestroyEnemy(EnemyId id);

    SiteId CreateBuildSite(string buildingDefId, CellPos anchor, Dir4 rotation);
    void DestroyBuildSite(SiteId id);
}
```

---

## 6) WORLD INDEX (derived lists)

### 6.1 IWorldIndex
```csharp
public interface IWorldIndex
{
    // storages
    System.Collections.Generic.IReadOnlyList<BuildingId> Warehouses { get; }
    System.Collections.Generic.IReadOnlyList<BuildingId> Producers  { get; }
    System.Collections.Generic.IReadOnlyList<BuildingId> Forges     { get; }
    System.Collections.Generic.IReadOnlyList<BuildingId> Armories   { get; }
    System.Collections.Generic.IReadOnlyList<TowerId>    Towers     { get; }

    // rebuild
    void RebuildAll();
    void OnBuildingCreated(BuildingId id);
    void OnBuildingDestroyed(BuildingId id);
}
```

---

## 7) PLACEMENT (ROAD ENTRY + DRIVEWAY)

### 7.1 Placement results
```csharp
public enum PlacementFailReason
{
    None,
    OutOfBounds,
    Overlap,
    NoRoadConnection,        // entry too far (driveway len=1)
    InvalidRotation,
    BlockedBySite,
    Unknown
}

public readonly struct PlacementResult
{
    public readonly bool Ok;
    public readonly PlacementFailReason Reason;
    public readonly CellPos SuggestedRoadCell; // driveway conversion target (if any)
    public PlacementResult(bool ok, PlacementFailReason r, CellPos drivewayTarget)
    { Ok=ok; Reason=r; SuggestedRoadCell=drivewayTarget; }
}
```

### 7.2 IPlacementService
```csharp
public interface IPlacementService
{
    PlacementResult ValidateBuilding(string buildingDefId, CellPos anchor, Dir4 rotation);
    BuildingId CommitBuilding(string buildingDefId, CellPos anchor, Dir4 rotation);

    // Road placement
    bool CanPlaceRoad(CellPos c);
    void PlaceRoad(CellPos c);
}
```

---

## 8) STORAGE + RESOURCE FLOW

### 8.1 Storage state DTO
```csharp
public readonly struct StorageSnapshot
{
    public readonly int Wood, Food, Stone, Iron, Ammo;
    public readonly int CapWood, CapFood, CapStone, CapIron, CapAmmo;
    public StorageSnapshot(int w,int f,int s,int i,int a,int cw,int cf,int cs,int ci,int ca)
    { Wood=w;Food=f;Stone=s;Iron=i;Ammo=a;CapWood=cw;CapFood=cf;CapStone=cs;CapIron=ci;CapAmmo=ca; }
}
```

### 8.2 IStorageService
```csharp
public interface IStorageService
{
    StorageSnapshot GetStorage(BuildingId building);

    bool CanStore(BuildingId building, ResourceType type);
    int GetAmount(BuildingId building, ResourceType type);
    int GetCap(BuildingId building, ResourceType type);

    int Add(BuildingId building, ResourceType type, int amount);     // returns actually added
    int Remove(BuildingId building, ResourceType type, int amount);  // returns actually removed

    int GetTotal(ResourceType type); // across allowed storages (ammo: only armory/forge local if you count)
}
```

### 8.3 Resource transfer selection
```csharp
public readonly struct StoragePick
{
    public readonly BuildingId Building;
    public readonly int Distance; // Manhattan
    public StoragePick(BuildingId b,int d){Building=b;Distance=d;}
}
```

### 8.4 IResourceFlowService
```csharp
public interface IResourceFlowService
{
    // pick nearest source with enough amount (or any amount)
    bool TryPickSource(CellPos from, ResourceType type, int minAmount, out StoragePick pick);

    // pick nearest destination with enough space
    bool TryPickDest(CellPos from, ResourceType type, int minSpace, out StoragePick pick);

    // atomic transfer (server-authoritative in sim)
    int Transfer(BuildingId src, BuildingId dst, ResourceType type, int amount);
}
```

---

## 9) CLAIMS (EXCLUSIVITY)

### 9.1 Claim keys
```csharp
public enum ClaimKind
{
    StorageSource,
    StorageDest,
    TowerResupply,
    BuildSite,
    ProducerNode
}

public readonly struct ClaimKey
{
    public readonly ClaimKind Kind;
    public readonly int A; // id value (building/tower/site)
    public readonly int B; // optional (resource type int)
    public ClaimKey(ClaimKind k,int a,int b){Kind=k;A=a;B=b;}
}
```

### 9.2 IClaimService
```csharp
public interface IClaimService
{
    bool TryAcquire(ClaimKey key, NpcId owner);
    bool IsOwnedBy(ClaimKey key, NpcId owner);
    void Release(ClaimKey key, NpcId owner);
    void ReleaseAll(NpcId owner);

    int ActiveClaimsCount { get; }
}
```

---

## 10) JOBS (BOARD + SCHEDULER + EXECUTION)

### 10.1 Job archetypes
```csharp
public enum JobArchetype
{
    Leisure,
    Inspect,

    Harvest,
    HaulBasic,

    BuildDeliver,
    BuildWork,

    CraftAmmo,
    HaulAmmoToArmory,
    ResupplyTower
}
public enum JobStatus { Created, Claimed, InProgress, Completed, Failed, Cancelled }
```

### 10.2 Job DTO
```csharp
public struct Job
{
    public JobId Id;
    public JobArchetype Archetype;
    public JobStatus Status;

    public NpcId ClaimedBy;

    public BuildingId Workplace;      // source workplace (HQ/warehouse/forge/armory/producer)
    public BuildingId SourceBuilding; // optional
    public BuildingId DestBuilding;   // optional
    public SiteId Site;               // optional
    public TowerId Tower;             // optional

    public ResourceType ResourceType; // optional
    public int Amount;                // optional

    public CellPos TargetCell;        // optional convenience
    public float CreatedAt;
}
```

### 10.3 IJobBoard
```csharp
public interface IJobBoard
{
    JobId Enqueue(Job job);
    bool TryPeekForWorkplace(BuildingId workplace, out Job job); // deterministic order
    bool TryClaim(JobId id, NpcId npc);

    bool TryGet(JobId id, out Job job);
    void Update(Job job);        // status transitions
    void Cancel(JobId id);

    int CountForWorkplace(BuildingId workplace);
}
```

### 10.4 Scheduler
```csharp
public interface IJobScheduler
{
    // called in tick
    void Tick(float dt);

    // manual attempt to assign
    bool TryAssign(NpcId npc, out Job assigned);

    // debug
    int AssignedThisTick { get; }
}
```

### 10.5 Executor interface
```csharp
public interface IJobExecutor
{
    // returns true if progressed; false if waiting/stuck
    bool Tick(NpcId npc, ref NpcState npcState, ref Job job, float dt);
}
```

---

## 11) BUILD PIPELINE (ORDERS + SITES + DELIVERY + COMMIT)

### 11.1 Build order DTOs
```csharp
public enum BuildOrderKind { PlaceNew, Upgrade, Repair }

public struct BuildOrder
{
    public int OrderId;
    public BuildOrderKind Kind;

    public string BuildingDefId;
    public BuildingId TargetBuilding; // for upgrade/repair
    public SiteId Site;               // for PlaceNew

    public CostDef RequiredCost;
    public CostProgress Delivered;
    public float WorkSecondsRequired;
    public float WorkSecondsDone;

    public bool Completed;
}
```

```csharp
public struct CostProgress
{
    public int Wood, Food, Stone, Iron, Ammo;
}
```

### 11.2 IBuildOrderService
```csharp
public interface IBuildOrderService
{
    int CreatePlaceOrder(string buildingDefId, CellPos anchor, Dir4 rotation);
    int CreateUpgradeOrder(BuildingId building);
    int CreateRepairOrder(BuildingId building);

    bool TryGet(int orderId, out BuildOrder order);
    void Cancel(int orderId);

    // tick drives generating jobs (deliver/work)
    void Tick(float dt);

    event System.Action<int> OnOrderCompleted;
}
```

### 11.3 BuildSite state
```csharp
public struct BuildSiteState
{
    public SiteId Id;
    public string BuildingDefId;
    public CellPos Anchor;
    public Dir4 Rotation;

    public CostDef RequiredCost;
    public CostProgress Delivered;

    public float WorkSecondsRequired;
    public float WorkSecondsDone;

    public bool Completed;
}
```

---

## 12) AMMO PIPELINE (FORGE + ARMORY + TOWER REQUESTS)

### 12.1 Request DTO
```csharp
public enum AmmoRequestPriority { Normal, Urgent }

public struct AmmoRequest
{
    public TowerId Tower;
    public int AmountNeeded;
    public AmmoRequestPriority Priority;
    public float CreatedAt;
}
```

### 12.2 IAmmoService
```csharp
public interface IAmmoService
{
    // Tower monitor
    void NotifyTowerAmmoChanged(TowerId tower, int current, int max);

    // Request queue (armory uses this)
    void EnqueueRequest(AmmoRequest req);
    bool TryDequeueNext(out AmmoRequest req); // urgent first

    // Crafting
    bool TryStartCraft(BuildingId forge);
    void Tick(float dt);

    // Debug
    int PendingRequests { get; }
}
```

---

## 13) COMBAT (WAVES + ENEMIES + TOWERS)

### 13.1 ICombatService
```csharp
public interface ICombatService
{
    bool IsActive { get; }
    void OnDefendPhaseStarted();
    void OnDefendPhaseEnded();

    void Tick(float dt);

    // Debug
    void SpawnWave(string waveDefId);
    void KillAllEnemies();

    event System.Action<string> OnWaveStarted;
    event System.Action<string> OnWaveEnded;
}
```

---

## 14) REWARDS + RUN OUTCOME

### 14.1 Reward offering
```csharp
public readonly struct RewardOffer
{
    public readonly string A, B, C; // reward ids
    public RewardOffer(string a,string b,string c){A=a;B=b;C=c;}
}
```

### 14.2 IRewardService
```csharp
public interface IRewardService
{
    bool IsSelectionActive { get; }
    RewardOffer CurrentOffer { get; }

    // called by RunClock end-of-defend-day
    RewardOffer GenerateOffer(int dayIndex, int seed);

    void StartSelection(RewardOffer offer);
    void Choose(int slotIndex); // 0..2

    event System.Action OnSelectionStarted;
    event System.Action<string> OnRewardChosen;
    event System.Action OnSelectionEnded;
}
```

### 14.3 Run outcome
```csharp
public enum RunOutcome { Ongoing, Victory, Defeat, Abort }

public interface IRunOutcomeService
{
    RunOutcome Outcome { get; }
    void Defeat();
    void Victory();
    void Abort();

    event System.Action<RunOutcome> OnRunEnded;
}
```

---

## 15) SAVE / LOAD

### 15.1 Save result
```csharp
public enum SaveResultCode { Ok, Failed, IncompatibleSchema, NotFound }

public readonly struct SaveResult
{
    public readonly SaveResultCode Code;
    public readonly string Message;
    public SaveResult(SaveResultCode c,string m){Code=c;Message=m;}
}
```

### 15.2 ISaveService
```csharp
public interface ISaveService
{
    int CurrentSchemaVersion { get; }

    SaveResult SaveRun(IWorldState world, IRunClock clock);
    SaveResult LoadRun(out RunSaveDTO dto);

    SaveResult SaveMeta(MetaSaveDTO dto);
    SaveResult LoadMeta(out MetaSaveDTO dto);

    bool HasRunSave();
    void DeleteRunSave();
}
```

### 15.3 Save DTOs (minimal)
```csharp
public sealed class RunSaveDTO
{
    public int schemaVersion;
    public int seed;

    public string season;
    public int dayIndex;
    public float timeScale;

    public WorldDTO world;
    public BuildDTO build;
    public CombatDTO combat;
    public RewardsDTO rewards;
}

public sealed class MetaSaveDTO
{
    public int schemaVersion;
    public int currency;
    public System.Collections.Generic.List<string> unlockIds;
    public System.Collections.Generic.Dictionary<string,int> perkLevels;
}
```

> NOTE: derived indices and caches must be rebuilt on load, not serialized.

---

## 16) AUDIO / FX (OPTIONAL INTERFACES)

### 16.1 IAudioService
```csharp
public interface IAudioService
{
    void Play(AudioEventId id);
    void PlayAt(AudioEventId id, UnityEngine.Vector3 worldPos);
}
```

### 16.2 IFXService
```csharp
public interface IFXService
{
    void Play(FXEventId id, UnityEngine.Vector3 worldPos);
    void PlayOn(FXEventId id, UnityEngine.Transform target);
}
```

---

## 17) EVENT BUS (OPTIONAL BUT RECOMMENDED)

> Nếu bạn không muốn dùng EventBus, bạn vẫn phải emit các event bằng callbacks.
EventBus giúp tách coupling.

### 17.1 Basic EventBus
```csharp
public interface IEventBus
{
    void Publish<T>(T evt) where T : struct;
    void Subscribe<T>(System.Action<T> handler) where T : struct;
    void Unsubscribe<T>(System.Action<T> handler) where T : struct;
}
```

### 17.2 Common events structs
```csharp
public readonly struct BuildingPlacedEvent
{
    public readonly string DefId;
    public readonly BuildingId Building;
    public BuildingPlacedEvent(string d, BuildingId b){DefId=d;Building=b;}
}

public readonly struct RoadPlacedEvent
{
    public readonly CellPos Cell;
    public RoadPlacedEvent(CellPos c){Cell=c;}
}

public readonly struct NPCAssignedEvent
{
    public readonly NpcId Npc;
    public readonly BuildingId Workplace;
    public NPCAssignedEvent(NpcId n, BuildingId w){Npc=n;Workplace=w;}
}

public readonly struct ResourceDeliveredEvent
{
    public readonly ResourceType Type;
    public readonly int Amount;
    public readonly BuildingId Dest;
    public ResourceDeliveredEvent(ResourceType t,int a,BuildingId d){Type=t;Amount=a;Dest=d;}
}

public readonly struct WaveStartedEvent
{
    public readonly string WaveId;
    public WaveStartedEvent(string id){WaveId=id;}
}
public readonly struct WaveEndedEvent
{
    public readonly string WaveId;
    public WaveEndedEvent(string id){WaveId=id;}
}

public readonly struct RewardPickedEvent
{
    public readonly string RewardId;
    public RewardPickedEvent(string id){RewardId=id;}
}

public readonly struct RunEndedEvent
{
    public readonly RunOutcome Outcome;
    public RunEndedEvent(RunOutcome o){Outcome=o;}
}
```

---

## 18) “LOCKED” invariants (must not break)
- Warehouse: `CanStore(Ammo) == false`
- HQ workplace roles: only Build/Repair/HaulBasic (no Harvest, no Ammo jobs)
- Tower ammo request threshold: <= 25% triggers request; urgent if 0
- Notifications: max visible 3, newest on top
- Defend entering: default timescale = 1x

---

## 19) What to implement first using this pack
1) Create folder `Assets/Game/Core/Contracts/` and put these interfaces there.
2) Implement concrete services behind them (one by one).
3) Write 5–10 editmode tests validating invariants above.

---

## 20) Next Part (Part 26 đề xuất)
**Concrete class skeletons**: đưa ra file/class scaffolds (MonoBehaviour/Service) tương ứng với pack này để bạn copy vào project nhanh.

