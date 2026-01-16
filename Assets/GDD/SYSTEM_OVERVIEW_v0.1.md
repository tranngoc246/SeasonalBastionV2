# SYSTEM OVERVIEW v0.1 — Seasonal Bastion (_Game)

> Tài liệu này được sinh tự động từ nội dung folder `_Game` bạn gửi. Mục tiêu: giúp bạn (và dev mới) hiểu nhanh từng module, từng file và điểm vào hệ thống.

## 1) Kiến trúc tổng quan
### 1.1 Two-layer (Contracts / Runtime)
- **Contracts**: `Core/Contracts/` (asmdef `Game.Contracts`), namespace **`SeasonalBastion.Contracts`** — interface/enums/DTO/struct state, không chứa logic runtime.
- **Runtime**: các folder còn lại (asmdef `Game.Core`, `Game.World`, ...), namespace **`SeasonalBastion`** — implement services, stores, systems, UI presenter, boot graph.

### 1.2 Bản đồ asmdef (assemblies)
| Assembly | Path | References (raw) |
|---|---|---|
| Game.Boot | Core/Boot/Game.Boot.asmdef | Game.Contracts, Game.Core, Game.Defs, Game.World, Game.Grid, Game.Economy, Game.Jobs, Game.Build, Game.Combat, Game.Rewards, Game.Save |
| Game.Build | Build/Game.Build.asmdef | Game.Contracts, Game.Core, Game.World, Game.Grid, Game.Economy, Game.Jobs |
| Game.Combat | Combat/Game.Combat.asmdef | Game.Contracts, Game.Core, Game.World, Game.Grid, Game.Economy, Game.Jobs |
| Game.Contracts | Core/Contracts/Game.Contracts.asmdef | - |
| Game.Core | Core/Game.Core.asmdef | Game.Contracts |
| Game.Debug | Debug/Game.Debug.asmdef | Game.Core, Game.World, Game.Grid, Game.Economy, Game.Jobs, Game.Combat, Game.Rewards, Game.Save |
| Game.Defs | Defs/Game.Defs.asmdef | Game.Contracts |
| Game.Economy | Economy/Game.Economy.asmdef | Game.Contracts, Game.Core, Game.World |
| Game.Grid | Grid/Game.Grid.asmdef | Game.Contracts, Game.Core, Game.World |
| Game.Jobs | Jobs/Game.Jobs.asmdef | Game.Contracts, Game.Core, Game.World, Game.Economy |
| Game.Rewards | Rewards/Game.Rewards.asmdef | Game.Contracts, Game.Core, Game.World, Game.Combat |
| Game.Save | Save/Game.Save.asmdef | Game.Contracts, Game.Core, Game.World, Game.Grid, Game.Economy, Game.Jobs, Game.Build, Game.Combat, Game.Rewards |
| Game.UI | UI/Game.UI.asmdef | Game.Contracts, Game.Core, Game.World, Game.Grid, Game.Economy, Game.Jobs, Game.Combat, Game.Rewards |
| Game.World | World/Game.World.asmdef | Game.Contracts, Game.Core |

## 2) Boot flow & điểm vào hệ thống
### 2.1 Các file entry/boot chính (tìm được trong repo)
- `Core/Boot/GameBootstrap.cs`
- `Core/Boot/GameServicesFactory.cs`
- `Core/Loop/GameLoop.cs`
- `Core/Loop/TickOrder.cs`

### 2.2 Trình tự khởi tạo (theo code hiện có)
1) `GameBootstrap` (MonoBehaviour) gọi `GameServicesFactory` để dựng `GameServices` (service container).
2) Tạo `GameLoop` và bắt đầu run (StartNewRun / init clock/state).
3) Mỗi frame: `GameLoop.Tick(dt)` → `TickOrder.TickAll(services, dt)` tick các service nào implement `ITickable`.

## 3) Tổng quan theo folder (chi tiết từng file)
### 3.1) `Core/`
Nền tảng runtime: service container, boot graph, game loop/tick, event bus, notification (logic), util chung.

| File | Types (namespace) | Assembly | Trách nhiệm | Public API (gợi ý) | Called by (tìm nhanh) |
|---|---|---|---|---|---|
| `Core/Boot/DataRegistry.cs` | class DataRegistry (SeasonalBastion) | Game.Boot | PATCH v0.1.1 — DataRegistry implements Part25 IDataRegistry | GetBuilding: BuildingDef, GetEnemy: EnemyDef, GetWave: WaveDef, GetReward: RewardDef, GetRecipe: RecipeDef, ClearAll: void | Core/Boot/GameServicesFactory.cs, Core/GameServices.cs, Defs/DefsCatalog.cs |
| `Core/Boot/DataValidator.cs` | class DataValidator (SeasonalBastion) | Game.Boot | PATCH v0.1.1 — DataValidator implements Part25 IDataValidator | ValidateAll: bool | Core/Boot/GameServicesFactory.cs, Core/GameServices.cs |
| `Core/Boot/GameBootstrap.cs` | class GameBootstrap (SeasonalBastion) | Game.Boot | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | - | Chưa thấy nơi gọi trực tiếp |
| `Core/Boot/GameServices.cs` | class BootComposition (SeasonalBastion) | Game.Boot | PATCH v0.1.3 — Boot composition helper (NOT the GameServices container) | - | Chưa thấy nơi gọi trực tiếp |
| `Core/Boot/GameServicesFactory.cs` | class GameServicesFactory (SeasonalBastion) | Game.Boot | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | GameServicesFactory: class, Create: GameServices | Core/Boot/GameBootstrap.cs |
| `Core/Contracts/Ammo/AmmoTypes.cs` | enum AmmoRequestPriority; struct AmmoRequest (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | AmmoRequestPriority: enum, AmmoRequest: struct | Economy/AmmoService.cs, Core/Contracts/Ammo/IAmmoService.cs |
| `Core/Contracts/Ammo/IAmmoService.cs` | interface IAmmoService (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IAmmoService: interface | Core/GameServices.cs, Economy/AmmoService.cs |
| `Core/Contracts/Audio/AudioEventId.cs` | - | Game.Contracts | PATCH v0.1.1 — Missing contract types to unblock compilation (Unity 2022.3) | Equals: bool | Chưa thấy nơi gọi trực tiếp |
| `Core/Contracts/Audio/IAudioService.cs` | interface IAudioService (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IAudioService: interface | Core/GameServices.cs |
| `Core/Contracts/Build/BuildTypes.cs` | enum BuildOrderKind; struct BuildOrder; struct CostProgress (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | BuildOrderKind: enum, BuildOrder: struct, CostProgress: struct | Build/BuildOrderService.cs, Core/Contracts/Build/IBuildOrderService.cs, Economy/AmmoService.cs |
| `Core/Contracts/Build/IBuildOrderService.cs` | interface IBuildOrderService (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IBuildOrderService: interface | Build/BuildOrderService.cs, Core/GameServices.cs |
| `Core/Contracts/Claims/ClaimTypes.cs` | enum ClaimKind (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | ClaimKind: enum | Chưa thấy nơi gọi trực tiếp |
| `Core/Contracts/Claims/IClaimService.cs` | interface IClaimService (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IClaimService: interface | Core/GameServices.cs, Jobs/ClaimService.cs, Jobs/JobScheduler.cs |
| `Core/Contracts/Combat/ICombatService.cs` | interface ICombatService (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | ICombatService: interface | Combat/CombatService.cs, Core/GameServices.cs |
| `Core/Contracts/Common/CellTypes.cs` | enum Dir4 (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | Dir4: enum | Build/BuildOrderService.cs, Core/Contracts/Build/IBuildOrderService.cs, Core/Contracts/Placement/IPlacementService.cs, Core/Contracts/World/IWorldOps.cs |
| `Core/Contracts/Common/IdTypes.cs` | - | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | - | Chưa thấy nơi gọi trực tiếp |
| `Core/Contracts/Common/ResourceTypes.cs` | enum ResourceType (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | ResourceType: enum | Core/Contracts/Data/DefDTOs_Missing.cs, Core/Contracts/Economy/IResourceFlowService.cs, Core/Contracts/Economy/IStorageService.cs, Core/Contracts/Events/CommonEvents.cs |
| `Core/Contracts/Common/RunEnums.cs` | enum Season; enum Phase; enum RunOutcome (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | Season: enum, Phase: enum, RunOutcome: enum | Core/Contracts/Run/IRunClock.cs, Core/Loop/GameLoop.cs, Core/Loop/RunClockService.cs |
| `Core/Contracts/Core/GameServices.cs` | class ContractsVersionMarker (SeasonalBastion.Contracts) | Game.Contracts | PATCH v0.1.3 — Contracts should not define the runtime service container. | - | Chưa thấy nơi gọi trực tiếp |
| `Core/Contracts/Data/DefDTOs_Missing.cs` | class BuildingDef; class EnemyDef; class WaveDef; class RewardDef; class RecipeDef; class CostDef (SeasonalBastion.Contracts) | Game.Contracts | PATCH v0.1.1 — Missing contract types to unblock compilation (Unity 2022.3) | - | Core/Boot/DataRegistry.cs, Core/Contracts/Data/IDataRegistry.cs, World/Index/WorldIndexService.cs |
| `Core/Contracts/Data/IDataRegistry.cs` | interface IDataRegistry (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IDataRegistry: interface | Core/Boot/DataRegistry.cs, Core/Boot/DataValidator.cs, Core/Contracts/Data/DefDTOs_Missing.cs, Core/Contracts/Data/IDataValidator.cs |
| `Core/Contracts/Data/IDataValidator.cs` | interface IDataValidator (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IDataValidator: interface | Core/Boot/DataValidator.cs, Core/GameServices.cs |
| `Core/Contracts/Economy/IResourceFlowService.cs` | interface IResourceFlowService (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IResourceFlowService: interface | Core/GameServices.cs, Economy/ResourceFlowService.cs |
| `Core/Contracts/Economy/IStorageService.cs` | interface IStorageService (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IStorageService: interface | Core/GameServices.cs, Economy/ResourceFlowService.cs, Economy/StorageService.cs |
| `Core/Contracts/Economy/StorageDTOs.cs` | - | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | - | Chưa thấy nơi gọi trực tiếp |
| `Core/Contracts/Events/CommonEvents.cs` | - | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | - | Chưa thấy nơi gọi trực tiếp |
| `Core/Contracts/Events/IEventBus.cs` | interface IEventBus (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IEventBus: interface | Core/Events/EventBus.cs, Core/GameServices.cs, Core/Loop/NotificationService.cs, Core/Loop/RunClockService.cs |
| `Core/Contracts/FX/FXEventId.cs` | - | Game.Contracts | PATCH v0.1.1 — Missing contract types to unblock compilation (Unity 2022.3) | Equals: bool | Chưa thấy nơi gọi trực tiếp |
| `Core/Contracts/FX/IFXService.cs` | interface IFXService (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IFXService: interface | Core/GameServices.cs |
| `Core/Contracts/Grid/GridTypes.cs` | enum CellOccupancyKind (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | CellOccupancyKind: enum | Grid/GridMap.cs |
| `Core/Contracts/Grid/IGridMap.cs` | interface IGridMap (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IGridMap: interface | Core/GameServices.cs, Grid/GridMap.cs, Grid/PlacementService.cs |
| `Core/Contracts/Jobs/IJobBoard.cs` | interface IJobBoard (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IJobBoard: interface | Core/GameServices.cs, Jobs/JobBoard.cs, Jobs/JobScheduler.cs |
| `Core/Contracts/Jobs/IJobExecutor.cs` | interface IJobExecutor (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IJobExecutor: interface | Jobs/Executors/BuildDeliverExecutor.cs, Jobs/Executors/BuildWorkExecutor.cs, Jobs/Executors/CraftAmmoExecutor.cs, Jobs/Executors/HarvestExecutor.cs |
| `Core/Contracts/Jobs/IJobScheduler.cs` | interface IJobScheduler (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IJobScheduler: interface | Core/GameServices.cs, Jobs/JobScheduler.cs |
| `Core/Contracts/Jobs/JobTypes.cs` | enum JobArchetype; enum JobStatus; struct Job (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | JobArchetype: enum, JobStatus: enum, Job: struct | Jobs/Executors/JobExecutorRegistry.cs, Jobs/JobBoard.cs |
| `Core/Contracts/Notifications/INotificationService.cs` | interface INotificationService (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | INotificationService: interface | Core/GameServices.cs, Core/Loop/NotificationService.cs |
| `Core/Contracts/Notifications/NotificationTypes.cs` | enum NotificationSeverity (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | NotificationSeverity: enum | Core/Contracts/Notifications/INotificationService.cs, Core/Contracts/Notifications/NotificationViewModel.cs, Core/Loop/NotificationService.cs |
| `Core/Contracts/Notifications/NotificationViewModel.cs` | class NotificationViewModel (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | - | Core/Contracts/Notifications/INotificationService.cs, Core/Loop/NotificationService.cs |
| `Core/Contracts/Placement/IPlacementService.cs` | interface IPlacementService (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IPlacementService: interface | Core/GameServices.cs, Grid/PlacementService.cs |
| `Core/Contracts/Placement/PlacementTypes.cs` | enum PlacementFailReason (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | PlacementFailReason: enum | Grid/PlacementService.cs |
| `Core/Contracts/Rewards/IRewardService.cs` | interface IRewardService (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IRewardService: interface | Core/GameServices.cs, Rewards/RewardService.cs |
| `Core/Contracts/Rewards/IRunOutcomeService.cs` | interface IRunOutcomeService (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IRunOutcomeService: interface | Core/GameServices.cs, Rewards/RunOutcomeService.cs |
| `Core/Contracts/Rewards/RewardTypes.cs` | - | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | - | Chưa thấy nơi gọi trực tiếp |
| `Core/Contracts/Run/IRunClock.cs` | interface IRunClock (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IRunClock: interface | Core/Contracts/Save/ISaveService.cs, Core/GameServices.cs, Core/Loop/RunClockService.cs, Save/SaveService.cs |
| `Core/Contracts/Save/ISaveService.cs` | interface ISaveService (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | ISaveService: interface | Core/GameServices.cs, Save/SaveService.cs |
| `Core/Contracts/Save/SaveDTOs.cs` | class RunSaveDTO; class MetaSaveDTO (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | - | Core/Contracts/Save/ISaveService.cs, Save/SaveMigrator.cs, Save/SaveService.cs |
| `Core/Contracts/Save/SaveDTOs_Missing.cs` | class WorldDTO; class BuildDTO; class CombatDTO; class RewardsDTO (SeasonalBastion.Contracts) | Game.Contracts | PATCH v0.1.1 — Missing contract types to unblock compilation (Unity 2022.3) | - | Core/Contracts/Save/SaveDTOs.cs |
| `Core/Contracts/Save/SaveTypes.cs` | enum SaveResultCode (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | SaveResultCode: enum | Save/SaveService.cs |
| `Core/Contracts/World/IWorldIndex.cs` | interface IWorldIndex (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IWorldIndex: interface | Core/GameServices.cs, Economy/ResourceFlowService.cs, Grid/PlacementService.cs, World/Index/WorldIndexService.cs |
| `Core/Contracts/World/IWorldOps.cs` | interface IWorldOps (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IWorldOps: interface | Core/GameServices.cs, World/Ops/WorldOps.cs |
| `Core/Contracts/World/IWorldState.cs` | interface IWorldState (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | IWorldState: interface | Core/Contracts/Save/ISaveService.cs, Core/GameServices.cs, Economy/ResourceFlowService.cs, Economy/StorageService.cs |
| `Core/Contracts/World/States/BuildSiteState.cs` | struct BuildSiteState (SeasonalBastion.Contracts) | Game.Contracts | PATCH v0.1.4 — BuildSiteState fields aligned with WorldOps usage | BuildSiteState: struct | Core/Contracts/Save/SaveDTOs_Missing.cs, Core/Contracts/World/Stores/IBuildSiteStore.cs, World/Ops/WorldOps.cs, World/State/Stores/BuildSiteStore.cs |
| `Core/Contracts/World/States/BuildingState.cs` | struct BuildingState (SeasonalBastion.Contracts) | Game.Contracts | PATCH v0.1.2 — Contracts canonical BuildingState | BuildingState: struct | Core/Contracts/Save/SaveDTOs_Missing.cs, Core/Contracts/World/Stores/IBuildingStore.cs, World/Ops/WorldOps.cs, World/State/Stores/BuildingStore.cs |
| `Core/Contracts/World/States/EnemyState.cs` | struct EnemyState (SeasonalBastion.Contracts) | Game.Contracts | PATCH v0.1.3 — Contracts canonical EnemyState with DefId and Lane fields | EnemyState: struct | Core/Contracts/Save/SaveDTOs_Missing.cs, Core/Contracts/World/Stores/IEnemyStore.cs, World/Ops/WorldOps.cs, World/State/Stores/EnemyStore.cs |
| `Core/Contracts/World/States/NpcState.cs` | struct NpcState (SeasonalBastion.Contracts) | Game.Contracts | PATCH v0.1.3 — Contracts canonical NpcState with DefId field | NpcState: struct | Core/Contracts/Jobs/IJobExecutor.cs, Core/Contracts/Save/SaveDTOs_Missing.cs, Core/Contracts/World/Stores/INpcStore.cs, Jobs/Executors/BuildDeliverExecutor.cs |
| `Core/Contracts/World/States/RunModifiers.cs` | struct RunModifiers (SeasonalBastion.Contracts) | Game.Contracts | PATCH v0.1.2 — Contracts canonical RunModifiers | RunModifiers: struct | Core/Contracts/World/IWorldState.cs, World/State/WorldState.cs |
| `Core/Contracts/World/States/RuntimeStates_Missing.cs` | - | Game.Contracts | PATCH v0.1.4 — Removed duplicate contract state structs to avoid type collisions. | - | Chưa thấy nơi gọi trực tiếp |
| `Core/Contracts/World/States/TowerState.cs` | struct TowerState (SeasonalBastion.Contracts) | Game.Contracts | PATCH v0.1.2 — Contracts canonical TowerState | TowerState: struct | Core/Contracts/Save/SaveDTOs_Missing.cs, Core/Contracts/World/Stores/ITowerStore.cs, World/State/Stores/TowerStore.cs |
| `Core/Contracts/World/Stores/IBuildSiteStore.cs` | interface IBuildSiteStore (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | - | Core/Contracts/World/IWorldState.cs, World/State/Stores/BuildSiteStore.cs, World/State/WorldState.cs |
| `Core/Contracts/World/Stores/IBuildingStore.cs` | interface IBuildingStore (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | - | Core/Contracts/World/IWorldState.cs, World/State/Stores/BuildingStore.cs, World/State/WorldState.cs |
| `Core/Contracts/World/Stores/IEnemyStore.cs` | interface IEnemyStore (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | - | Core/Contracts/World/IWorldState.cs, World/State/Stores/EnemyStore.cs, World/State/WorldState.cs |
| `Core/Contracts/World/Stores/IEntityStore.cs` | interface IEntityStore (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | - | Core/Contracts/World/Stores/IBuildSiteStore.cs, Core/Contracts/World/Stores/IBuildingStore.cs, Core/Contracts/World/Stores/IEnemyStore.cs, Core/Contracts/World/Stores/INpcStore.cs |
| `Core/Contracts/World/Stores/INpcStore.cs` | interface INpcStore (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | - | Core/Contracts/World/IWorldState.cs, World/State/Stores/NpcStore.cs, World/State/WorldState.cs |
| `Core/Contracts/World/Stores/ITowerStore.cs` | interface ITowerStore (SeasonalBastion.Contracts) | Game.Contracts | AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1) | - | Core/Contracts/World/IWorldState.cs, World/State/Stores/TowerStore.cs, World/State/WorldState.cs |
| `Core/Events/EventBus.cs` | class EventBus (SeasonalBastion) | Game.Core | PATCH v0.1.3 — Minimal typed event bus | - | Core/Boot/GameServicesFactory.cs, Core/GameServices.cs |
| `Core/Events/GameEvents.cs` | - | Game.Core | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | - | Chưa thấy nơi gọi trực tiếp |
| `Core/GameServices.cs` | class GameServices (SeasonalBastion) | Game.Core | PATCH v0.1.3 — Runtime service container (lives in Game.Core assembly) | - | Build/BuildOrderService.cs, Combat/CombatService.cs, Combat/EnemySystem.cs, Combat/TowerCombatSystem.cs |
| `Core/Loop/GameLoop.cs` | class GameLoop (SeasonalBastion) | Game.Core | PATCH v0.1.3 — GameLoop uses runtime GameServices container | StartNewRun: void, Tick: void, Dispose: void | Core/Boot/GameBootstrap.cs |
| `Core/Loop/ITickables.cs` | interface ITickable; interface IResettable (SeasonalBastion) | Game.Core | PATCH v0.1.2 — runtime tick/reset helpers (NOT contracts) | - | Core/Loop/TickOrder.cs, Core/Loop/GameLoop.cs |
| `Core/Loop/NotificationService.cs` | class NotificationService (SeasonalBastion) | Game.Core | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | MaxVisible: int, Push: NotificationId, Dismiss: void, ClearAll: void, GetVisible: System.Collections.Generic.IReadOnlyList<NotificationViewModel> | Core/Boot/GameServicesFactory.cs, Core/GameServices.cs |
| `Core/Loop/RunClockService.cs` | class RunClockService (SeasonalBastion) | Game.Core | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | CurrentSeason: Season, DayIndex: int, CurrentPhase: Phase, TimeScale: float, DefendSpeedUnlocked: bool, Start: void, Tick: void, SetTimeScale: void | Core/Boot/GameServicesFactory.cs |
| `Core/Loop/TickOrder.cs` | class TickOrder (SeasonalBastion) | Game.Core | PATCH v0.1.3 — TickOrder uses runtime GameServices container | TickOrder: class, TickAll: void | Core/Loop/GameLoop.cs |
| `Core/Utils/DeterminismUtil.cs` | class DeterminismUtil (SeasonalBastion) | Game.Core | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | DeterminismUtil: class, Manhattan: int | Chưa thấy nơi gọi trực tiếp |
| `Core/Utils/Log.cs` | class Log (SeasonalBastion) | Game.Core | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | Log: class, J: void, E: void, C: void | Chưa thấy nơi gọi trực tiếp |
| `Core/Utils/TimeUtil.cs` | class TimeUtil (SeasonalBastion) | Game.Core | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | TimeUtil: class, Clamp01: float | Chưa thấy nơi gọi trực tiếp |

### 3.2) `Defs/`
Định nghĩa dữ liệu (catalog, nguồn defs). Gắn với data schema/registry.

| File | Types (namespace) | Assembly | Trách nhiệm | Public API (gợi ý) | Called by (tìm nhanh) |
|---|---|---|---|---|---|
| `Defs/DefsCatalog.cs` | class DefsCatalog (SeasonalBastion) | Game.Defs | PATCH v0.1.1 — DefsCatalog compile + assembly separation | - | Core/Boot/DataRegistry.cs, Core/Boot/GameBootstrap.cs, Core/Boot/GameServicesFactory.cs |

### 3.3) `World/`
World state/stores/ops: nơi giữ trạng thái runtime cho run (buildings, npcs, enemies, build sites...).

| File | Types (namespace) | Assembly | Trách nhiệm | Public API (gợi ý) | Called by (tìm nhanh) |
|---|---|---|---|---|---|
| `World/Index/WorldIndexService.cs` | class WorldIndexService (SeasonalBastion) | Game.World | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | Warehouses: System.Collections.Generic.IReadOnlyList<BuildingId>, Producers: System.Collections.Generic.IReadOnlyList<BuildingId>, Forges: System.Collections.Generic.IReadOnlyList<BuildingId>, Armories: System.Collections.Generic.IReadOnlyList<BuildingId>, Towers: System.Collections.Generic.IReadOnlyList<TowerId>, RebuildAll: void, OnBuildingCreated: void, OnBuildingDestroyed: void | Core/Boot/GameServicesFactory.cs |
| `World/Ops/WorldOps.cs` | class WorldOps (SeasonalBastion) | Game.World | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | CreateBuilding: BuildingId, DestroyBuilding: void, CreateNpc: NpcId, DestroyNpc: void, CreateEnemy: EnemyId, DestroyEnemy: void, CreateBuildSite: SiteId, DestroyBuildSite: void | Core/Boot/GameServicesFactory.cs, Core/Contracts/World/States/BuildSiteState.cs, Core/GameServices.cs, Core/Loop/GameLoop.cs |
| `World/State/States/BuildSiteState.cs` | - | Game.World | PATCH v0.1.5  Marker file | - | Chưa thấy nơi gọi trực tiếp |
| `World/State/States/BuildingState.cs` | - | Game.World | PATCH v0.1.2 — Marker file | - | Chưa thấy nơi gọi trực tiếp |
| `World/State/States/EnemyState.cs` | - | Game.World | PATCH v0.1.2 — Marker file | - | Chưa thấy nơi gọi trực tiếp |
| `World/State/States/NpcState.cs` | - | Game.World | PATCH v0.1.2 — Marker file | - | Chưa thấy nơi gọi trực tiếp |
| `World/State/States/RunModifiers.cs` | - | Game.World | PATCH v0.1.2 — Marker file | - | Chưa thấy nơi gọi trực tiếp |
| `World/State/States/TowerState.cs` | - | Game.World | PATCH v0.1.2 — Marker file | - | Chưa thấy nơi gọi trực tiếp |
| `World/State/Stores/BuildSiteStore.cs` | class BuildSiteStore (SeasonalBastion) | Game.World | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | - | World/State/WorldState.cs |
| `World/State/Stores/BuildingStore.cs` | class BuildingStore (SeasonalBastion) | Game.World | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | - | World/State/WorldState.cs |
| `World/State/Stores/EnemyStore.cs` | class EnemyStore (SeasonalBastion) | Game.World | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | - | World/State/WorldState.cs |
| `World/State/Stores/EntityStore.cs` | class EntityStore (SeasonalBastion) | Game.World | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | Exists: bool, Get: TState, Set: void, Create: TId, Destroy: void, Count: int, Ids: IEnumerable<TId> | World/State/Stores/BuildSiteStore.cs, World/State/Stores/BuildingStore.cs, World/State/Stores/EnemyStore.cs, World/State/Stores/NpcStore.cs |
| `World/State/Stores/NpcStore.cs` | class NpcStore (SeasonalBastion) | Game.World | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | - | World/State/WorldState.cs |
| `World/State/Stores/TowerStore.cs` | class TowerStore (SeasonalBastion) | Game.World | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | - | World/State/WorldState.cs |
| `World/State/WorldState.cs` | class WorldState (SeasonalBastion) | Game.World | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | Buildings: IBuildingStore, Npcs: INpcStore, Towers: ITowerStore, Enemies: IEnemyStore, Sites: IBuildSiteStore | Core/Boot/GameServicesFactory.cs, Core/GameServices.cs |

### 3.4) `Grid/`
Grid map, placement, road/entry/driveway rules, occupancy, coordinate conversion.

| File | Types (namespace) | Assembly | Trách nhiệm | Public API (gợi ý) | Called by (tìm nhanh) |
|---|---|---|---|---|---|
| `Grid/GridMap.cs` | class GridMap (SeasonalBastion) | Game.Grid | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | Width: int, Height: int, IsInside: bool, Get: CellOccupancy, IsRoad: bool, IsBlocked: bool, SetRoad: void, SetBuilding: void | Core/Boot/GameServicesFactory.cs, Core/Contracts/Grid/IGridMap.cs, Core/GameServices.cs |
| `Grid/PlacementService.cs` | class PlacementService (SeasonalBastion) | Game.Grid | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | CanPlaceRoad: bool, PlaceRoad: void, ValidateBuilding: PlacementResult, CommitBuilding: BuildingId | Core/Boot/GameServicesFactory.cs, Core/GameServices.cs |

### 3.5) `Economy/`
Kinh tế/tài nguyên: storage rules, resource flow, hauling, recipes (ammo pipeline hooks nếu có).

| File | Types (namespace) | Assembly | Trách nhiệm | Public API (gợi ý) | Called by (tìm nhanh) |
|---|---|---|---|---|---|
| `Economy/AmmoService.cs` | class AmmoService (SeasonalBastion) | Game.Economy | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | PendingRequests: int, NotifyTowerAmmoChanged: void, EnqueueRequest: void, TryDequeueNext: bool, TryStartCraft: bool, Tick: void | Core/Boot/GameServicesFactory.cs, Core/GameServices.cs |
| `Economy/ResourceFlowService.cs` | class ResourceFlowService (SeasonalBastion) | Game.Economy | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | TryPickSource: bool, TryPickDest: bool, Transfer: int | Core/Boot/GameServicesFactory.cs, Core/GameServices.cs, Core/Loop/TickOrder.cs |
| `Economy/StorageService.cs` | class StorageService (SeasonalBastion) | Game.Economy | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | GetStorage: StorageSnapshot, CanStore: bool, GetAmount: int, GetCap: int, Add: int, Remove: int, GetTotal: int | Core/Boot/GameServicesFactory.cs, Core/GameServices.cs |

### 3.6) `Jobs/`
Job system: job board/scheduler/executors, claim integration, NPC job lifecycle.

| File | Types (namespace) | Assembly | Trách nhiệm | Public API (gợi ý) | Called by (tìm nhanh) |
|---|---|---|---|---|---|
| `Jobs/ClaimService.cs` | class ClaimService (SeasonalBastion) | Game.Jobs | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | TryAcquire: bool, IsOwnedBy: bool, Release: void, ReleaseAll: void, ActiveClaimsCount: int | Core/Boot/GameServicesFactory.cs, Core/GameServices.cs |
| `Jobs/Executors/BuildDeliverExecutor.cs` | class BuildDeliverExecutor (SeasonalBastion) | Game.Jobs | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | Tick: bool | Jobs/Executors/JobExecutorRegistry.cs |
| `Jobs/Executors/BuildWorkExecutor.cs` | class BuildWorkExecutor (SeasonalBastion) | Game.Jobs | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | Tick: bool | Jobs/Executors/JobExecutorRegistry.cs |
| `Jobs/Executors/CraftAmmoExecutor.cs` | class CraftAmmoExecutor (SeasonalBastion) | Game.Jobs | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | Tick: bool | Jobs/Executors/JobExecutorRegistry.cs |
| `Jobs/Executors/HarvestExecutor.cs` | class HarvestExecutor (SeasonalBastion) | Game.Jobs | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | Tick: bool | Jobs/Executors/JobExecutorRegistry.cs |
| `Jobs/Executors/HaulBasicExecutor.cs` | class HaulBasicExecutor (SeasonalBastion) | Game.Jobs | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | Tick: bool | Jobs/Executors/JobExecutorRegistry.cs |
| `Jobs/Executors/JobExecutorRegistry.cs` | class JobExecutorRegistry (SeasonalBastion) | Game.Jobs | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | Get: IJobExecutor | Core/Boot/GameServicesFactory.cs, Jobs/JobScheduler.cs |
| `Jobs/Executors/ResupplyTowerExecutor.cs` | class ResupplyTowerExecutor (SeasonalBastion) | Game.Jobs | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | Tick: bool | Jobs/Executors/JobExecutorRegistry.cs |
| `Jobs/JobBoard.cs` | class JobBoard (SeasonalBastion) | Game.Jobs | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | Enqueue: JobId, TryPeekForWorkplace: bool, TryClaim: bool, TryGet: bool, Update: void, Cancel: void, CountForWorkplace: int | Core/Boot/GameServicesFactory.cs, Core/GameServices.cs |
| `Jobs/JobScheduler.cs` | class JobScheduler (SeasonalBastion) | Game.Jobs | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | AssignedThisTick: int, Tick: void, TryAssign: bool | Core/Boot/GameServicesFactory.cs, Core/GameServices.cs, Core/Loop/TickOrder.cs |

### 3.7) `Build/`
Build pipeline: build orders, build sites, delivery/commit, builder actions.

| File | Types (namespace) | Assembly | Trách nhiệm | Public API (gợi ý) | Called by (tìm nhanh) |
|---|---|---|---|---|---|
| `Build/BuildOrderService.cs` | class BuildOrderService (SeasonalBastion) | Game.Build | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | CreatePlaceOrder: int, CreateUpgradeOrder: int, CreateRepairOrder: int, TryGet: bool, Cancel: void, Tick: void | Core/Boot/GameServicesFactory.cs, Core/GameServices.cs |

### 3.8) `Combat/`
Combat: towers/enemies/waves, damage, ammo consumption, wave director.

| File | Types (namespace) | Assembly | Trách nhiệm | Public API (gợi ý) | Called by (tìm nhanh) |
|---|---|---|---|---|---|
| `Combat/CombatService.cs` | class CombatService (SeasonalBastion) | Game.Combat | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | IsActive: bool, OnDefendPhaseStarted: void, OnDefendPhaseEnded: void, Tick: void, SpawnWave: void, KillAllEnemies: void | Core/Boot/GameServicesFactory.cs, Core/GameServices.cs, Core/Loop/TickOrder.cs |
| `Combat/EnemySystem.cs` | class EnemySystem (SeasonalBastion) | Game.Combat | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | Tick: void | Chưa thấy nơi gọi trực tiếp |
| `Combat/TowerCombatSystem.cs` | class TowerCombatSystem (SeasonalBastion) | Game.Combat | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | Tick: void | Chưa thấy nơi gọi trực tiếp |
| `Combat/WaveDirector.cs` | class WaveDirector (SeasonalBastion) | Game.Combat | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | StartDayWaves: void, Tick: void | Combat/CombatService.cs |

### 3.9) `Rewards/`
Run outcome + rewards/meta progression hooks.

| File | Types (namespace) | Assembly | Trách nhiệm | Public API (gợi ý) | Called by (tìm nhanh) |
|---|---|---|---|---|---|
| `Rewards/RewardService.cs` | class RewardService (SeasonalBastion) | Game.Rewards | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | IsSelectionActive: bool, CurrentOffer: RewardOffer, GenerateOffer: RewardOffer, StartSelection: void, Choose: void | Core/Boot/GameServicesFactory.cs, Core/GameServices.cs |
| `Rewards/RunOutcomeService.cs` | class RunOutcomeService (SeasonalBastion) | Game.Rewards | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | Outcome: RunOutcome, Reset: void, Defeat: void, Victory: void, Abort: void, Tick: void | Core/Boot/GameServicesFactory.cs, Core/GameServices.cs, Core/Loop/GameLoop.cs, Core/Loop/TickOrder.cs |

### 3.10) `Save/`
Save/load, migration, persistence.

| File | Types (namespace) | Assembly | Trách nhiệm | Public API (gợi ý) | Called by (tìm nhanh) |
|---|---|---|---|---|---|
| `Save/SaveMigrator.cs` | class SaveMigrator (SeasonalBastion) | Game.Save | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | CurrentSchemaVersion: int, TryMigrate: bool | Core/Boot/GameServicesFactory.cs, Save/SaveService.cs |
| `Save/SaveService.cs` | class SaveService (SeasonalBastion) | Game.Save | AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1) | CurrentSchemaVersion: int, HasRunSave: bool, DeleteRunSave: void, SaveRun: SaveResult, LoadRun: SaveResult, SaveMeta: SaveResult, LoadMeta: SaveResult | Core/Boot/GameServicesFactory.cs, Core/GameServices.cs |

## 4) Chỉ mục Contracts (Core/Contracts)
### 4.1) Ammo
- `Core/Contracts/Ammo/AmmoTypes.cs` — AmmoRequestPriority, AmmoRequest
- `Core/Contracts/Ammo/IAmmoService.cs` — IAmmoService

### 4.2) Audio
- `Core/Contracts/Audio/AudioEventId.cs` — (no types?)
- `Core/Contracts/Audio/IAudioService.cs` — IAudioService

### 4.3) Build
- `Core/Contracts/Build/BuildTypes.cs` — BuildOrderKind, BuildOrder, CostProgress
- `Core/Contracts/Build/IBuildOrderService.cs` — IBuildOrderService

### 4.4) Claims
- `Core/Contracts/Claims/ClaimTypes.cs` — ClaimKind
- `Core/Contracts/Claims/IClaimService.cs` — IClaimService

### 4.5) Combat
- `Core/Contracts/Combat/ICombatService.cs` — ICombatService

### 4.6) Common
- `Core/Contracts/Common/CellTypes.cs` — Dir4
- `Core/Contracts/Common/IdTypes.cs` — (no types?)
- `Core/Contracts/Common/ResourceTypes.cs` — ResourceType
- `Core/Contracts/Common/RunEnums.cs` — Season, Phase, RunOutcome

### 4.7) Core
- `Core/Contracts/Core/GameServices.cs` — ContractsVersionMarker

### 4.8) Data
- `Core/Contracts/Data/DefDTOs_Missing.cs` — BuildingDef, EnemyDef, WaveDef, RewardDef, RecipeDef, CostDef
- `Core/Contracts/Data/IDataRegistry.cs` — IDataRegistry
- `Core/Contracts/Data/IDataValidator.cs` — IDataValidator

### 4.9) Economy
- `Core/Contracts/Economy/IResourceFlowService.cs` — IResourceFlowService
- `Core/Contracts/Economy/IStorageService.cs` — IStorageService
- `Core/Contracts/Economy/StorageDTOs.cs` — (no types?)

### 4.10) Events
- `Core/Contracts/Events/CommonEvents.cs` — (no types?)
- `Core/Contracts/Events/IEventBus.cs` — IEventBus

### 4.11) FX
- `Core/Contracts/FX/FXEventId.cs` — (no types?)
- `Core/Contracts/FX/IFXService.cs` — IFXService

### 4.12) Grid
- `Core/Contracts/Grid/GridTypes.cs` — CellOccupancyKind
- `Core/Contracts/Grid/IGridMap.cs` — IGridMap

### 4.13) Jobs
- `Core/Contracts/Jobs/IJobBoard.cs` — IJobBoard
- `Core/Contracts/Jobs/IJobExecutor.cs` — IJobExecutor
- `Core/Contracts/Jobs/IJobScheduler.cs` — IJobScheduler
- `Core/Contracts/Jobs/JobTypes.cs` — JobArchetype, JobStatus, Job

### 4.14) Notifications
- `Core/Contracts/Notifications/INotificationService.cs` — INotificationService
- `Core/Contracts/Notifications/NotificationTypes.cs` — NotificationSeverity
- `Core/Contracts/Notifications/NotificationViewModel.cs` — NotificationViewModel

### 4.15) Placement
- `Core/Contracts/Placement/IPlacementService.cs` — IPlacementService
- `Core/Contracts/Placement/PlacementTypes.cs` — PlacementFailReason

### 4.16) Rewards
- `Core/Contracts/Rewards/IRewardService.cs` — IRewardService
- `Core/Contracts/Rewards/IRunOutcomeService.cs` — IRunOutcomeService
- `Core/Contracts/Rewards/RewardTypes.cs` — (no types?)

### 4.17) Run
- `Core/Contracts/Run/IRunClock.cs` — IRunClock

### 4.18) Save
- `Core/Contracts/Save/ISaveService.cs` — ISaveService
- `Core/Contracts/Save/SaveDTOs.cs` — RunSaveDTO, MetaSaveDTO
- `Core/Contracts/Save/SaveDTOs_Missing.cs` — WorldDTO, BuildDTO, CombatDTO, RewardsDTO
- `Core/Contracts/Save/SaveTypes.cs` — SaveResultCode

### 4.19) World
- `Core/Contracts/World/IWorldIndex.cs` — IWorldIndex
- `Core/Contracts/World/IWorldOps.cs` — IWorldOps
- `Core/Contracts/World/IWorldState.cs` — IWorldState
- `Core/Contracts/World/States/BuildSiteState.cs` — BuildSiteState
- `Core/Contracts/World/States/BuildingState.cs` — BuildingState
- `Core/Contracts/World/States/EnemyState.cs` — EnemyState
- `Core/Contracts/World/States/NpcState.cs` — NpcState
- `Core/Contracts/World/States/RunModifiers.cs` — RunModifiers
- `Core/Contracts/World/States/RuntimeStates_Missing.cs` — (no types?)
- `Core/Contracts/World/States/TowerState.cs` — TowerState
- `Core/Contracts/World/Stores/IBuildSiteStore.cs` — IBuildSiteStore
- `Core/Contracts/World/Stores/IBuildingStore.cs` — IBuildingStore
- `Core/Contracts/World/Stores/IEnemyStore.cs` — IEnemyStore
- `Core/Contracts/World/Stores/IEntityStore.cs` — IEntityStore
- `Core/Contracts/World/Stores/INpcStore.cs` — INpcStore
- `Core/Contracts/World/Stores/ITowerStore.cs` — ITowerStore

## 5) Reading order (đề xuất cho dev mới)
1) **Contracts trước**: `Core/Contracts/*` để nắm API bề mặt (IRunClock, IWorldState, IJob*, IStorage*, ...).
2) **Boot graph**: `Core/Boot/GameServicesFactory.cs`, `Core/Boot/GameBootstrap.cs` để hiểu cách dựng service container.
3) **Loop/Tick**: `Core/Loop/GameLoop.cs`, `Core/Loop/TickOrder.cs`, `Core/Loop/RunClockService.cs` để hiểu nhịp chạy.
4) **World state & stores**: `World/*` (stores/ops) vì mọi hệ thống đều đọc/ghi world.
5) **Grid + Placement**: `Grid/*` (rule road/entry/driveway) để đi vào VS#1.
6) **Jobs/Economy/Build/Combat**: lần lượt theo dòng dữ liệu (job→resource→build→combat).

## 6) Quy ước thêm code để không phá kiến trúc (anti-regression)
- Thêm interface/DTO/state → **chỉ** ở `Core/Contracts/` (namespace `SeasonalBastion.Contracts`).
- Thêm implementation runtime → đặt theo module tương ứng (`Core`, `World`, `Grid`, ...) và đảm bảo asmdef module reference `Game.Contracts`.
- Không tạo lại các state struct trong runtime với cùng tên/fullname như Contracts (tránh collision).
- UI chỉ hiển thị: đặt ở `UI/Runtime/*`, không implement service interface nếu service đã thuộc Core.
- Nếu cần tick service: implement `ITickable` (runtime), và `TickOrder` chỉ tick qua interface `ITickable` để tránh “fake API”.
