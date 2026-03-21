# SeasonalBastionV2 — Refactor File Split Plan

Mục tiêu: tách các "god files" thành các cụm file nhỏ hơn, trách nhiệm rõ hơn, nhưng **giữ nguyên public API hiện có** trong giai đoạn đầu để tránh vỡ gameplay.

---

# 1) Cây file/folder mới đề xuất

```txt
SeasonalBastionV2/
├─ Assets/
│  └─ _Game/
│     ├─ Build/
│     │  ├─ Game.Build.asmdef
│     │  ├─ BuildOrderService.cs                  // facade + public API hiện có
│     │  ├─ BuildOrderCreationService.cs         // create place/upgrade/repair orders
│     │  ├─ BuildOrderTickProcessor.cs           // tick active orders, sync progress, trigger complete
│     │  ├─ BuildJobPlanner.cs                   // ensure/cancel build & repair jobs
│     │  ├─ BuildOrderCompletionService.cs       // complete place/upgrade orders
│     │  ├─ BuildOrderCancellationService.cs     // cancel/rollback/refund/cleanup
│     │  ├─ BuildOrderReloadService.cs           // rebuild active orders after save load
│     │  ├─ BuildOrderWorkplaceResolver.cs       // resolve workplace for builder jobs
│     │  ├─ BuildOrderCostTracker.cs             // cost helpers, delivered mirror, refunds
│     │  └─ BuildOrderEventBridge.cs             // event bus bridge for auto-road, etc.
│     │
│     ├─ Core/
│     │  ├─ App/
│     │  ├─ Balance/
│     │  ├─ Boot/
│     │  │  ├─ Game.Boot.asmdef
│     │  │  ├─ BootComposition.cs
│     │  │  ├─ GameAppController.cs
│     │  │  ├─ GameBootstrap.cs
│     │  │  ├─ GameServicesFactory.cs
│     │  │  ├─ ViewServicesProvider_Bootstrap.cs
│     │  │  ├─ WaveCalendarResolver.cs
│     │  │  ├─ Data/
│     │  │  │  ├─ DataRegistry.cs               // facade + public query API
│     │  │  │  ├─ DataRegistryLoader.cs         // orchestration: load all from catalog
│     │  │  │  ├─ DataParseHelpers.cs           // ToCaps, ToCosts, ParseSeason, ParseResourceType...
│     │  │  │  ├─ BuildingWorkRoleResolver.cs   // parse/derive work role flags
│     │  │  │  ├─ TowerDefValidator.cs          // tower-specific validation rules
│     │  │  │  ├─ BuildingDefsLoader.cs         // Buildings.json loader
│     │  │  │  ├─ NpcDefsLoader.cs              // Npcs.json loader
│     │  │  │  ├─ TowerDefsLoader.cs            // Towers.json loader
│     │  │  │  ├─ EnemyDefsLoader.cs            // Enemies.json loader
│     │  │  │  ├─ RecipeDefsLoader.cs           // Recipes.json loader
│     │  │  │  ├─ WaveDefsLoader.cs             // Waves.json loader
│     │  │  │  ├─ RewardDefsLoader.cs           // Rewards.json loader
│     │  │  │  ├─ BalanceConfigLoader.cs        // Balance json loader
│     │  │  │  └─ BuildablesGraphLoader.cs      // nodes/upgrades graph loader
│     │  │
│     │  ├─ Contracts/
│     │  ├─ Events/
│     │  ├─ Input/
│     │  ├─ Loop/
│     │  ├─ RunStart/
│     │  │  ├─ StartMapConfigDto.cs
│     │  │  ├─ RunStartRuntime.cs
│     │  │  ├─ RunStartValidator.cs
│     │  │  ├─ RunStartFacade.cs                // public TryApply entrypoint
│     │  │  ├─ RunStartInputParser.cs           // markdown/json input parsing
│     │  │  ├─ RunStartConfigValidator.cs       // schema/header/config validation
│     │  │  ├─ RunStartRuntimeCacheBuilder.cs   // fill RunStartRuntime metadata
│     │  │  ├─ RunStartWorldBuilder.cs          // apply roads/buildings/grid occupancy
│     │  │  ├─ RunStartTowerInitializer.cs      // create/init tower state during run start
│     │  │  ├─ RunStartNpcSpawner.cs            // spawn NPCs and workplace links
│     │  │  ├─ RunStartZoneInitializer.cs       // zones from config or fallback
│     │  │  ├─ RunStartStorageInitializer.cs    // initial HQ storage
│     │  │  ├─ RunStartPlacementHelper.cs       // anchor resolution, dir parsing, road promotion
│     │  │  ├─ RunStartHqResolver.cs            // HQ target/lane target helpers
│     │  │  └─ RunStartBuildContext.cs          // context object: defId→BuildingId, created ids...
│     │  │
│     │  ├─ Runtime/
│     │  ├─ Services/
│     │  └─ Utils/
│     │
│     ├─ Jobs/
│     │  ├─ Game.Jobs.asmdef
│     │  ├─ JobScheduler.cs                     // facade + public API hiện có
│     │  ├─ JobSchedulerCache.cs               // sorted ids, workplace/npc caches
│     │  ├─ JobEnqueueService.cs               // enqueue harvest/haul jobs if needed
│     │  ├─ JobAssignmentService.cs            // assign jobs to idle NPCs
│     │  ├─ JobExecutionService.cs             // tick active jobs via executors
│     │  ├─ JobNotificationPolicy.cs           // no-job notification throttle/policy
│     │  ├─ JobWorkplacePolicy.cs              // allowed roles, job filtering
│     │  ├─ ResourceLogisticsPolicy.cs         // producer/capacity/resource rules
│     │  └─ JobStateCleanupService.cs          // cleanup NPC/job/claim state
│     │
│     └─ ... các module còn lại giữ nguyên trước
│
└─ docs/
   └─ architecture/
      └─ refactor-file-split-plan.md
```

---

# 2) Mỗi cụm file dùng để làm gì

## 2.1 Build subsystem

### `BuildOrderService.cs`
- Giữ nguyên public API hiện có:
  - `CreatePlaceOrder`
  - `CreateUpgradeOrder`
  - `CreateRepairOrder`
  - `Cancel`
  - `Tick`
  - `TryGet`
  - `ClearAll`
  - `RebuildActivePlaceOrdersFromSitesAfterLoad`
- Chỉ làm **facade/coordinator**
- Không nên chứa logic dài nữa

### `BuildOrderCreationService.cs`
- Tạo order mới
- Validate unlock/resources/placement/upgrade target
- Tạo placeholder building + build site
- Push notification bắt đầu

### `BuildOrderTickProcessor.cs`
- Tick tất cả order active mỗi frame
- Sync order progress từ site/building state
- Check điều kiện complete
- Gọi completion service khi xong

### `BuildJobPlanner.cs`
- Ensure/cancel build jobs và repair jobs
- Nối giữa build system và job system
- Theo dõi `_deliverJobsBySite`, `_workJobBySite`, `_repairJobByOrder`

### `BuildOrderCompletionService.cs`
- Finalize khi place/upgrade xong
- Clear site
- Set building occupancy
- Create/update tower state nếu cần
- Fire event + notification complete

### `BuildOrderCancellationService.cs`
- Cancel order
- Rollback site/grid/placeholder building
- Refund delivered resources
- Cleanup tracked jobs

### `BuildOrderReloadService.cs`
- Dựng lại active orders sau khi load save
- Match site ↔ placeholder building

### `BuildOrderWorkplaceResolver.cs`
- Resolve workplace của builder jobs
- Ví dụ: prefer HQ rồi fallback theo work role

### `BuildOrderCostTracker.cs`
- Helper về cost/progress:
  - clone cost
  - delivered mirror
  - refund policy
  - nearest storage logic

### `BuildOrderEventBridge.cs`
- Subscribe/unsubscribe event bus
- Xử lý auto-road events hoặc event liên quan build

---

## 2.2 RunStart subsystem

### `RunStartFacade.cs`
- Entry point public duy nhất cho run start
- Orchestrate parse → validate → apply → post-validate

### `RunStartInputParser.cs`
- Parse input text
- Extract JSON từ markdown nếu cần
- `JsonUtility.FromJson(...)`

### `RunStartConfigValidator.cs`
- Validate `schemaVersion`, `coordSystem`, `lockedInvariants`, map size...
- Chỉ check config input, chưa apply runtime

### `RunStartRuntimeCacheBuilder.cs`
- Ghi metadata vào `RunStartRuntime`
- BuildableRect / SpawnGates / Zones / Lanes / LockedInvariants

### `RunStartWorldBuilder.cs`
- Apply roads và buildings vào world/grid
- Tạo mapping `defId -> BuildingId`
- Occupy footprint
- Promote entry roads

### `RunStartTowerInitializer.cs`
- Tất cả logic tower riêng trong run start
- Tạo `TowerState`, standalone tower marker, init ammo/hp

### `RunStartNpcSpawner.cs`
- Spawn NPCs từ config
- Resolve workplace từ building map

### `RunStartZoneInitializer.cs`
- Tạo zones từ config hoặc fallback legacy

### `RunStartStorageInitializer.cs`
- Gán tài nguyên khởi đầu vào HQ/storage

### `RunStartPlacementHelper.cs`
- Placement helpers:
  - parse dir
  - resolve defId
  - pick valid anchor
  - compute entry cell
  - promote road

### `RunStartHqResolver.cs`
- Resolve HQ center / adjacent target cells cho lanes

### `RunStartBuildContext.cs`
- Context object truyền giữa các bước apply
- Ví dụ:
  - `Dictionary<string, BuildingId> DefIdToBuildingId`
  - `BuildingId HqId`
  - list created buildings/towers nếu cần

---

## 2.3 Job subsystem

### `JobScheduler.cs`
- Facade điều phối tick job system
- Giữ public API hiện có (`Tick`, `TryAssign`)

### `JobSchedulerCache.cs`
- Build/rebuild cache:
  - sorted NPC ids
  - sorted building ids
  - workplace has NPC
  - workplace NPC count

### `JobEnqueueService.cs`
- Enqueue `Harvest`, `HaulBasic`, ... nếu cần
- Tránh duplicate jobs
- Gate theo slot/capacity/resource availability

### `JobAssignmentService.cs`
- Assign job cho NPC idle
- Claim job từ board
- Update `NpcState`

### `JobExecutionService.cs`
- Tick các current jobs bằng executor registry
- Update board
- Cleanup terminal jobs

### `JobNotificationPolicy.cs`
- Throttle/push notification kiểu “NPC không có việc”

### `JobWorkplacePolicy.cs`
- Rule về workplace roles:
  - allowed roles
  - `IsJobAllowed(...)`
  - `HasWorkRole(...)`

### `ResourceLogisticsPolicy.cs`
- Rule về producer / storage caps / resource flows
- Xác định resource type của workplace, destination capacity, producer lookup...

### `JobStateCleanupService.cs`
- Cleanup current job / release claims / periodic invalid owner cleanup

---

## 2.4 Data loading subsystem

### `DataRegistry.cs`
- Facade + public query API
- Giữ dictionaries/runtime defs
- `GetBuilding`, `GetTower`, `GetNpc`, ...

### `DataRegistryLoader.cs`
- Orchestrator load từ `DefsCatalog`
- Gọi các typed loader theo thứ tự

### `DataParseHelpers.cs`
- Helper parse dùng chung:
  - `ParseSeason`
  - `ParseResourceType`
  - `ToCaps`
  - `ToCosts`
  - `ToUnlock`
  - `ToWaveEntries`

### `BuildingWorkRoleResolver.cs`
- Parse/derive `WorkRoleFlags` từ building json

### `TowerDefValidator.cs`
- Validate tower-specific invariants
- Ví dụ ammo/rof/threshold rules

### `BuildingDefsLoader.cs`
- Parse/load `Buildings.json`
- Map JSON -> `BuildingDef`

### `NpcDefsLoader.cs`
- Parse/load `Npcs.json`

### `TowerDefsLoader.cs`
- Parse/load `Towers.json`

### `EnemyDefsLoader.cs`
- Parse/load `Enemies.json`

### `RecipeDefsLoader.cs`
- Parse/load `Recipes.json`

### `WaveDefsLoader.cs`
- Parse/load `Waves.json`

### `RewardDefsLoader.cs`
- Parse/load `Rewards.json`

### `BalanceConfigLoader.cs`
- Parse/load balance config

### `BuildablesGraphLoader.cs`
- Parse/load build nodes + upgrade edges graph

---

# 3) Mapping file cũ -> file mới

## `Assets/_Game/Core/RunStart/RunStartApplier.cs`

### Chuyển sang:
- `RunStartFacade.cs`
  - `TryApply(...)`
- `RunStartInputParser.cs`
  - `ExtractJsonIfMarkdown(...)`
- `RunStartConfigValidator.cs`
  - `ValidateStartMapHeader(...)`
- `RunStartRuntimeCacheBuilder.cs`
  - phần cache `RunStartRuntime`
- `RunStartWorldBuilder.cs`
  - roads + buildings + occupancy + mapping
- `RunStartTowerInitializer.cs`
  - `IsArrowTowerLike(...)`
  - `TryCreateArrowTowerState(...)`
  - `TryCreateArrowTowerStandalone(...)`
  - `TryPickValidTowerCell(...)`
- `RunStartNpcSpawner.cs`
  - phần spawn NPC
- `RunStartZoneInitializer.cs`
  - `EnsureZonesFromConfigOrFallback(...)`
  - `TryMapZoneTypeToResource(...)`
  - `AddRectZone(...)`
- `RunStartStorageInitializer.cs`
  - `ApplyStartingStorage(...)`
- `RunStartPlacementHelper.cs`
  - `ResolveBuildingDefIdOrNull(...)`
  - `TryPickValidAnchor(...)`
  - `HasBuildingDef(...)`
  - `ParseDir4(...)`
  - `PromoteRunStartEntryRoads(...)`
  - `PromoteRoadIfPossible(...)`
  - `ComputeEntryOutsideFootprint(...)`
- `RunStartHqResolver.cs`
  - `TryResolveHQTargetCell(...)`
  - `TryResolveHQTargetCellAdjacent(...)`
  - `IsGoodTargetCell(...)`
- `RunStartBuildContext.cs`
  - context object để chia sẻ state apply

---

## `Assets/_Game/Build/BuildOrderService.cs`

### Chuyển sang:
- `BuildOrderService.cs`
  - public facade methods
- `BuildOrderCreationService.cs`
  - `CreatePlaceOrder(...)`
  - `CreateUpgradeOrder(...)`
  - `CreateRepairOrder(...)`
  - `ComputeWorkSecondsTotal(...)`
  - `ComputeWorkSecondsTotalFromChunks(...)`
  - `ComputeRepairSeconds(...)`
- `BuildOrderTickProcessor.cs`
  - `Tick(...)`
  - phần duyệt `_active`, sync progress, complete check
- `BuildJobPlanner.cs`
  - `EnsureBuildJobsForSite(...)`
  - `TickRepairOrder(...)`
  - `CancelTrackedJobsForSite(...)`
  - `CancelRepairJob(...)`
  - `CancelDeliveryJobs(...)`
  - `PruneTerminal(...)`
  - `IsTerminal(...)`
- `BuildOrderCompletionService.cs`
  - `CompletePlaceOrder(...)`
  - `CompleteUpgradeOrder(...)`
- `BuildOrderCancellationService.cs`
  - `Cancel(...)`
  - `TryRollbackAutoRoad(...)`
  - `CancelBySite(...)`
  - `CancelByBuilding(...)`
  - `RefundDeliveredToNearestStorage(...)`
  - `Manhattan(...)`
- `BuildOrderReloadService.cs`
  - `RebuildActivePlaceOrdersFromSitesAfterLoad()`
  - `Pack(...)`
- `BuildOrderWorkplaceResolver.cs`
  - `ResolveBuildWorkplace()`
- `BuildOrderCostTracker.cs`
  - `CloneCostsOrEmpty(...)`
  - `BuildDeliveredMirror(...)`
  - `IsReadyToWork(...)`
- `BuildOrderEventBridge.cs`
  - `EnsureBusSubscribed()`
  - `OnAutoRoadCreated(...)`

---

## `Assets/_Game/Jobs/JobScheduler.cs`

### Chuyển sang:
- `JobScheduler.cs`
  - public facade + top-level tick orchestration
- `JobSchedulerCache.cs`
  - `BuildSortedNpcIds()`
  - `BuildSortedBuildingIds()`
  - `BuildWorkplaceHasNpcSet()`
- `JobEnqueueService.cs`
  - `EnqueueHarvestJobsIfNeeded()`
  - `EnqueueHaulJobsIfNeeded()`
  - `TryEnsureHaulJob(...)`
  - `TryEnsureHaulJobToProducer(...)`
- `JobAssignmentService.cs`
  - `TryAssign(...)`
  - `TryAssignInternal(...)`
- `JobExecutionService.cs`
  - phần tick current jobs theo executor
- `JobNotificationPolicy.cs`
  - `NotifyNoJobs(...)`
- `JobWorkplacePolicy.cs`
  - `GetWorkplaceAllowedRoles(...)`
  - `HasWorkRole(...)`
  - `IsJobAllowed(...)`
- `ResourceLogisticsPolicy.cs`
  - `TryGetProducerFor(...)`
  - `AnyHarvestProducerHasAmount(...)`
  - `AnyPileHasAmount(...)`
  - `AnyHaulDestinationHasFree(...)`
  - `IsWarehouseWorkplace(...)`
  - `IsHarvestProducer(...)`
  - `HarvestResourceType(...)`
  - `HarvestLocalCap(...)`
  - `DestCap(...)`
  - `GetAmountFromBuilding(...)`
  - `NormalizeLevel(...)`
  - `EqualsIgnoreCase(...)`
- `JobStateCleanupService.cs`
  - `CleanupNpcJob(...)`
  - `IsTerminal(...)`
  - claim cleanup timer logic

---

## `Assets/_Game/Core/Boot/DataRegistry.cs`

### Chuyển sang:
- `DataRegistry.cs`
  - facade + query API + dictionaries
- `DataRegistryLoader.cs`
  - `LoadAllFromCatalog()`
- `BuildingDefsLoader.cs`
  - `LoadBuildings(...)`
- `NpcDefsLoader.cs`
  - `LoadNpcs(...)`
- `TowerDefsLoader.cs`
  - `LoadTowers(...)`
- `EnemyDefsLoader.cs`
  - `LoadEnemies(...)`
- `RecipeDefsLoader.cs`
  - `LoadRecipes(...)`
- `WaveDefsLoader.cs`
  - `LoadWaves(...)`
- `RewardDefsLoader.cs`
  - `LoadRewards(...)`
- `BalanceConfigLoader.cs`
  - `LoadBalance(...)`
- `BuildablesGraphLoader.cs`
  - `LoadBuildablesGraph(...)`
- `DataParseHelpers.cs`
  - `ToCaps(...)`
  - `ParseSeason(...)`
  - `ParseResourceType(...)`
  - `ToCosts(...)`
  - `ToUnlock(...)`
  - `ToWaveEntries(...)`
- `BuildingWorkRoleResolver.cs`
  - `ParseWorkRolesOrDerive(...)`
- `TowerDefValidator.cs`
  - `ValidateTowers()`

---

# 4) Thứ tự refactor khuyến nghị

## Phase 1 — dễ và an toàn nhất
1. `RunStartApplier` → tách trước
2. `DataRegistry` → tách typed loaders

## Phase 2 — gameplay runtime, rủi ro vừa
3. `JobScheduler` → tách cache / assignment / execution

## Phase 3 — rủi ro cao nhất
4. `BuildOrderService` → tách cuối

Lý do:
- `BuildOrderService` đang đụng grid + sites + buildings + jobs + notifications + save/load
- rất dễ regressions nếu tách quá sớm

---

# 5) Quy tắc triển khai để không vỡ project

## Rule A — Giữ nguyên public API lớp cũ
Ví dụ:
- `RunStartApplier.TryApply(...)`
- `BuildOrderService.CreatePlaceOrder(...)`
- `JobScheduler.Tick(...)`
- `DataRegistry.GetBuilding(...)`

Bên ngoài vẫn gọi như cũ.

## Rule B — Chỉ chuyển ruột method sang lớp mới
Lúc đầu có thể để lớp cũ kiểu:
```csharp
public bool TryStart(...)
{
    return _creationService.TryStart(...);
}
```

## Rule C — Mỗi commit chỉ tách 1 concern
Ví dụ với `JobScheduler`:
- commit 1: tách cache
- commit 2: tách assignment
- commit 3: tách execution
- commit 4: tách enqueue/policy

## Rule D — Chưa tối ưu behavior trong lúc tách
Mục tiêu đầu tiên là:
- file nhỏ hơn
- trách nhiệm rõ hơn
- gameplay không đổi

---

# 6) Bản rút gọn nếu muốn ít file hơn

Nếu bạn muốn ít file hơn để đỡ mệt, dùng cây rút gọn này:

```txt
Assets/_Game/Build/
├─ BuildOrderService.cs
├─ BuildOrderCreationService.cs
├─ BuildOrderTickProcessor.cs
├─ BuildOrderCompletionService.cs
└─ BuildOrderCancellationService.cs

Assets/_Game/Core/RunStart/
├─ RunStartFacade.cs
├─ RunStartParser.cs
├─ RunStartWorldBuilder.cs
└─ RunStartHelpers.cs

Assets/_Game/Jobs/
├─ JobScheduler.cs
├─ JobSchedulerCache.cs
├─ JobEnqueueService.cs
├─ JobAssignmentService.cs
└─ JobExecutionService.cs

Assets/_Game/Core/Boot/Data/
├─ DataRegistry.cs
├─ DataRegistryLoader.cs
├─ BuildingDefsLoader.cs
├─ OtherDefsLoader.cs
└─ DataParseHelpers.cs
```

Bản này vẫn đủ để hạ nhiệt các god file, nhưng số file không bùng nổ quá nhanh.

---

# 7) Checklist copy nhanh

## Bạn có thể copy cây này theo đúng thứ tự:
- Tạo `Assets/_Game/Core/RunStart/RunStartFacade.cs`
- Tạo `Assets/_Game/Core/RunStart/RunStartInputParser.cs`
- Tạo `Assets/_Game/Core/RunStart/RunStartConfigValidator.cs`
- Tạo `Assets/_Game/Core/RunStart/RunStartRuntimeCacheBuilder.cs`
- Tạo `Assets/_Game/Core/RunStart/RunStartWorldBuilder.cs`
- Tạo `Assets/_Game/Core/RunStart/RunStartTowerInitializer.cs`
- Tạo `Assets/_Game/Core/RunStart/RunStartNpcSpawner.cs`
- Tạo `Assets/_Game/Core/RunStart/RunStartZoneInitializer.cs`
- Tạo `Assets/_Game/Core/RunStart/RunStartStorageInitializer.cs`
- Tạo `Assets/_Game/Core/RunStart/RunStartPlacementHelper.cs`
- Tạo `Assets/_Game/Core/RunStart/RunStartHqResolver.cs`
- Tạo `Assets/_Game/Core/RunStart/RunStartBuildContext.cs`

- Tạo `Assets/_Game/Core/Boot/Data/`
- Tạo toàn bộ `*Loader.cs`, `DataParseHelpers.cs`, `BuildingWorkRoleResolver.cs`, `TowerDefValidator.cs`

- Tạo `Assets/_Game/Jobs/JobSchedulerCache.cs`
- Tạo `Assets/_Game/Jobs/JobEnqueueService.cs`
- Tạo `Assets/_Game/Jobs/JobAssignmentService.cs`
- Tạo `Assets/_Game/Jobs/JobExecutionService.cs`
- Tạo `Assets/_Game/Jobs/JobNotificationPolicy.cs`
- Tạo `Assets/_Game/Jobs/JobWorkplacePolicy.cs`
- Tạo `Assets/_Game/Jobs/ResourceLogisticsPolicy.cs`
- Tạo `Assets/_Game/Jobs/JobStateCleanupService.cs`

- Tạo `Assets/_Game/Build/BuildOrderCreationService.cs`
- Tạo `Assets/_Game/Build/BuildOrderTickProcessor.cs`
- Tạo `Assets/_Game/Build/BuildJobPlanner.cs`
- Tạo `Assets/_Game/Build/BuildOrderCompletionService.cs`
- Tạo `Assets/_Game/Build/BuildOrderCancellationService.cs`
- Tạo `Assets/_Game/Build/BuildOrderReloadService.cs`
- Tạo `Assets/_Game/Build/BuildOrderWorkplaceResolver.cs`
- Tạo `Assets/_Game/Build/BuildOrderCostTracker.cs`
- Tạo `Assets/_Game/Build/BuildOrderEventBridge.cs`

---

# 8) Kết luận

Blueprint này ưu tiên:
- dễ đọc code
- giảm file phình quá to
- tách đúng boundary nghiệp vụ
- vẫn giữ được API cũ để refactor dần

Nếu làm tiếp, bước hợp lý nhất là:
1. Tách `RunStartApplier`
2. Tách `DataRegistry`
3. Tách `JobScheduler`
4. Tách `BuildOrderService`

Khi bắt đầu code, hãy giữ mỗi bước nhỏ và commit riêng.
