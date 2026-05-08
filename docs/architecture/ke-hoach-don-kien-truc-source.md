# Source Architecture Cleanup Plan

> Mục tiêu: tối ưu lại cây source code theo hướng dễ mở rộng hơn, nhưng vẫn thực dụng với codebase hiện tại của `SeasonalBastionV2`.
>
> Trọng tâm của pass này không phải “đập đi xây lại”, mà là xác định đúng chỗ nào nên tách trước để giảm God Object, giảm coupling, và làm test / iteration đỡ đau hơn.

---

## 1. Ảnh chụp nhanh hiện trạng

### Folder-level shape hiện tại
`Assets/_Game/` hiện đã chia module khá ổn:
- `Build`
- `Combat`
- `Core`
- `Economy`
- `Grid`
- `Jobs`
- `Population`
- `Rewards`
- `Save`
- `UI`
- `World`
- `Debug`
- `Tests`

Đây là điểm tốt. Vấn đề lớn hiện tại không còn là thiếu module, mà là:
- vài service/container trung tâm đang ôm quá nhiều dependency
- một số file runtime lớn vẫn là “multi-responsibility file”
- test/regression có vài file quá to, khó bảo trì
- `Debug` và `UI` vẫn có chỗ có nguy cơ kéo gameplay policy vào sai lớp

---

## 2. Điểm nóng nên ưu tiên refactor

### P0 - Composition / service boundaries

#### `Assets/_Game/Core/GameServices.cs`
**Vấn đề**
- vẫn là God Object runtime container
- rất nhiều system nhận cả `GameServices` dù chỉ dùng một phần nhỏ
- làm dependency mờ và kéo coupling ngang module

**Khuyến nghị**
- chưa cần bỏ ngay `GameServices`
- nhưng bắt đầu chia thành các interface/view hẹp hơn:
  - `ICoreServicesView`
  - `IWorldRuntimeView`
  - `IEconomyRuntimeView`
  - `ICombatRuntimeView`
  - `ISaveRuntimeView`
- các service mới hoặc refactor mới nên nhận interface hẹp thay vì nhận full container

**Mức ưu tiên**
- rất cao
- vì đây là nền để các refactor khác bớt đau

---

#### `Assets/_Game/Core/Boot/GameServicesFactory.cs`
**Vấn đề**
- composition root hiện vẫn làm quá nhiều việc trong một method `Create(...)`
- setup order khó đọc khi hệ thống lớn dần
- khó thấy nhóm dependency theo domain

**Khuyến nghị**
- giữ `GameServicesFactory` làm entrypoint
- tách nội bộ thành các helper nhỏ:
  - `ComposeCore(...)`
  - `ComposeWorldAndGrid(...)`
  - `ComposeEconomyAndJobs(...)`
  - `ComposeBuildAndCombat(...)`
  - `ComposeRewardsAndSave(...)`
- mục tiêu là readability + giảm rủi ro init order bug

**Mức ưu tiên**
- rất cao

---

### P1 - Large runtime classes

#### `Assets/_Game/Save/SaveService.cs`
**Vấn đề**
- file lớn, ôm nhiều concern:
  - save path policy
  - snapshot creation
  - file IO
  - DTO mapping
  - slot/autosave policy
- rất dễ thành điểm nghẽn khi sửa save logic

**Khuyến nghị**
- tách thành cụm:
  - `SavePathPolicy`
  - `SaveWriter`
  - `SaveReader`
  - `RunSnapshotMapper`
  - `SlotSavePolicy`
- `SaveService` giữ vai trò facade

**Mức ưu tiên**
- cao

---

#### `Assets/_Game/Save/SaveLoadApplier.cs`
**Vấn đề**
- likely là file apply runtime lớn, cross-domain mạnh
- dễ lẫn giữa deserialize, validation, rebuild runtime, và post-load sanitation

**Khuyến nghị**
- tách theo phase:
  - `SaveLoadStateApply`
  - `SaveLoadRuntimeRebuild`
  - `SaveLoadPostApplySanitizer`
- giữ API ngoài ổn định nếu có thể

**Mức ưu tiên**
- cao

---

#### `Assets/_Game/Grid/PlacementInputController.cs`
**Vấn đề**
- controller vừa làm input, UI gate, preview, validation, commit flow, notification
- đây là kiểu file rất dễ phình tiếp

**Khuyến nghị**
- tách ít nhất thành:
  - `PlacementInputBinding`
  - `PlacementPreviewRenderer`
  - `PlacementActionController`
  - `PlacementUiGate`
- mục tiêu: giảm risk khi sửa build mode / preview / endgame lock

**Mức ưu tiên**
- cao

---

#### `Assets/_Game/Combat/EnemySystem.cs`
**Vấn đề**
- file khá lớn, domain combat vốn dễ lan responsibility

**Khuyến nghị**
- rà xem có thể tách theo nhánh:
  - spawn/update
  - path/progression
  - HQ damage / despawn / cleanup
- nếu chưa refactor ngay, ít nhất bổ sung doc comments + internal helpers rõ hơn

**Mức ưu tiên**
- vừa đến cao

---

### P1 - Test architecture

#### `Assets/_Game/Tests/EditMode/Regression/Regression_P0P1_Tests.cs`
**Vấn đề**
- file quá lớn, 169 KB
- rất khó maintain, đọc diff khổ, khó biết test theo feature nào

**Khuyến nghị**
- tách theo feature:
  - `Regression_SaveLoad_Tests.cs`
  - `Regression_Endgame_Tests.cs`
  - `Regression_Build_Tests.cs`
  - `Regression_Jobs_Tests.cs`
  - `Regression_RunStart_Tests.cs`
- giữ `RegressionTestDoubles.cs` như shared fixture layer nếu hợp lý

**Mức ưu tiên**
- rất cao
- payoff lớn, rủi ro gameplay thấp

---

### P2 - Debug / UI hygiene

#### `Assets/_Game/Debug/HUD/DebugHUDHub.*`
**Vấn đề**
- đã partial rồi, đó là dấu hiệu tốt
- nhưng cụm debug HUD vẫn khá dày, có nguy cơ trộn quick actions, inspect, logging, và gameplay mutation

**Khuyến nghị**
- tiếp tục giữ partial-by-feature
- cân nhắc tách folder theo:
  - `Debug/HUD/Home`
  - `Debug/HUD/Combat`
  - `Debug/HUD/Build`
  - `Debug/HUD/SaveLoad`
- tránh một “hub” quá trung tâm

---

#### `Assets/_Game/UI/Runtime/Scripts/Presenters/AssignNpcModalPresenter.cs`
#### `Assets/_Game/UI/Runtime/Scripts/Presenters/InspectPanelPresenter.cs`
**Vấn đề**
- presenter khá to, dễ ôm policy thay vì chỉ bind/render/intents

**Khuyến nghị**
- nếu tiếp tục phình, tách thêm helper/model builder:
  - `InspectPanelViewModelBuilder`
  - `AssignNpcListBuilder`
  - `WorkforceActionPolicy`
- presenter nên thiên về glue hơn là gameplay reasoning

**Mức ưu tiên**
- vừa

---

## 3. Thứ tự refactor thực dụng nhất

### Wave A - ít rủi ro, payoff cao
1. Tách `Regression_P0P1_Tests.cs`
2. Tách `GameServicesFactory.Create(...)` thành helper composition methods
3. Viết doc ngắn cho dependency boundaries quanh `GameServices`

### Wave B - giảm God file runtime
4. Tách `SaveService.cs`
5. Tách `PlacementInputController.cs`
6. Rà `SaveLoadApplier.cs`

### Wave C - cleanup sâu hơn theo domain
7. Tách thêm `EnemySystem.cs`
8. Tách presenter/helpers ở UI nếu còn phình
9. Dọn `DebugHUDHub` theo feature folders

---

## 4. Những chỗ chưa nên đụng mạnh ngay

### `RunStart` cluster
Hiện `Core/RunStart` có nhiều file nhưng nhìn chung đã được tách khá ổn cho batch gần đây.
- chưa phải vùng xấu nhất lúc này
- chỉ nên đụng tiếp nếu có feature mới hoặc test regression lộ pain thật

### `Build` cluster
`Build` hiện đã có dấu hiệu được split tốt:
- `BuildOrderCreationService`
- `BuildOrderTickProcessor`
- `BuildJobPlanner`
- `BuildOrderCancellationService`
- ...

Nghĩa là đây không phải mặt trận cần ưu tiên đầu tiên nữa.

---

## 5. Đề xuất file/folder có thể thêm

### docs / planning side
- `docs/architecture/ke-hoach-don-kien-truc-source.md` (file này)

### code side, nếu đi tiếp wave A/B
- `Assets/_Game/Core/Boot/GameServicesFactory.Core.cs`
- `Assets/_Game/Core/Boot/GameServicesFactory.World.cs`
- `Assets/_Game/Core/Boot/GameServicesFactory.EconomyJobs.cs`
- `Assets/_Game/Core/Boot/GameServicesFactory.BuildCombat.cs`
- `Assets/_Game/Core/Boot/GameServicesFactory.RewardsSave.cs`

- `Assets/_Game/Save/SaveWriter.cs`
- `Assets/_Game/Save/SaveReader.cs`
- `Assets/_Game/Save/RunSnapshotMapper.cs`
- `Assets/_Game/Save/SavePathPolicy.cs`

- `Assets/_Game/Grid/PlacementPreviewRenderer.cs`
- `Assets/_Game/Grid/PlacementActionController.cs`
- `Assets/_Game/Grid/PlacementUiGate.cs`

- `Assets/_Game/Tests/EditMode/Regression/Regression_SaveLoad_Tests.cs`
- `Assets/_Game/Tests/EditMode/Regression/Regression_Endgame_Tests.cs`
- `Assets/_Game/Tests/EditMode/Regression/Regression_Build_Tests.cs`
- `Assets/_Game/Tests/EditMode/Regression/Regression_Jobs_Tests.cs`
- `Assets/_Game/Tests/EditMode/Regression/Regression_RunStart_Tests.cs`

---

## 6. Kết luận ngắn

Nếu chỉ chọn **3 việc đáng làm nhất ngay bây giờ**, mình chọn:
1. tách `Regression_P0P1_Tests.cs`
2. tách nhỏ `GameServicesFactory.Create(...)`
3. lên kế hoạch split `SaveService.cs`

### Cập nhật 2026-05-06
- [x] Đã refactor bước đầu `Assets/_Game/Core/Boot/GameServicesFactory.cs` bằng cách tách `Create(...)` thành các helper composition nhỏ hơn:
  - `ComposeCore(...)`
  - `ComposeRunStartAndWorld(...)`
  - `ComposeGrid(...)`
  - `ComposeEconomyAndJobs(...)`
  - `ComposeBuild(...)`
  - `ComposeCombatAndRewards(...)`
  - `ComposeSave(...)`
- [x] Đã đi tiếp bước 2: tách `GameServicesFactory` thành nhiều file `partial` theo cụm composition:
  - `GameServicesFactory.cs`
  - `GameServicesFactory.Core.cs`
  - `GameServicesFactory.World.cs`
  - `GameServicesFactory.EconomyJobs.cs`
  - `GameServicesFactory.BuildCombatSave.cs`
- [~] Refactor này hiện ưu tiên readability/composition clarity, chưa đổi sang interface views hẹp hơn.
- [~] Đã bắt đầu tách `Regression_P0P1_Tests.cs` bằng cách đưa shared helper vào `RegressionTestBase.cs` để chuẩn bị chia file theo feature.
- [~] Đã tách cụm đầu tiên sang file partial `Regression_P0P1_OutcomePlacement_Tests.cs`, gồm nhóm placement/outcome/simulation guard cơ bản.
- [~] Đã tách tiếp cụm `Build` sang `Regression_P0P1_Build_Tests.cs`.
- [~] Đã tách tiếp cụm `Jobs` sang `Regression_P0P1_Jobs_Tests.cs`.
- [~] Đã tách tiếp cụm `RunStart` sang `Regression_P0P1_RunStart_Tests.cs`.
- [x] Đã tách nốt cụm `SaveLoad` sang `Regression_P0P1_SaveLoad_Tests.cs`.
- [x] `Regression_P0P1_Tests.cs` không còn là file gom lớn, giờ chỉ giữ shell partial class mỏng.
- [ ] Bước tiếp theo hợp lý: nếu muốn gọn hơn nữa, có thể rename file shell hoặc gom lại naming strategy cho toàn bộ regression partials.
- [x] Đã đi tiếp cụm `PlacementInputController` bằng cách bóc action/commit flow sang `PlacementActionController`, để controller gốc tập trung hơn vào input/update state + preview orchestration.
- [x] Đã giảm bind/reflection path trong `PlacementInputController` bằng cách bóc service resolution sang `PlacementServicesBinder` và ưu tiên `IUiServicesProvider`/`GameServices` path thay vì giữ reflection helper cục bộ trong controller.
- [x] Đã dọn tiếp duplication state/reset trong `PlacementInputController` bằng cách gom enter/exit placement state transition về helper riêng, giảm lặp giữa `CancelAll`, `OnBeginPlaceBuilding`, và `OnToolModeRequested`.
- [x] Đã bắt đầu chuẩn hoá service-binding pattern sang cụm khác bằng cách áp dụng binder riêng cho `WorldViewRoot2D`, bỏ reflection helper cục bộ và chuyển sang `WorldViewServicesBinder` dùng cùng hướng `IUiServicesProvider`/`GameServices`.
- [x] Đã đi tiếp `WorldSelectionController` bằng binder nhỏ `WorldSelectionServicesBinder`, gom `UiSystem.Ctx.Services` fallback + `IUiServicesProvider` fallback về một chỗ thay vì để controller tự resolve trực tiếp.
- [x] Đã review nhanh các controller/view còn lại trong `UI/World/Grid` để tìm chỗ còn dùng `GameServices` theo kiểu ad-hoc.
- [x] Đã fix lỗi asmdef boundary ở `WorldViewServicesBinder`: binder này không còn phụ thuộc namespace UI, giữ được hướng binder riêng cho `World/View2D` mà không kéo `SeasonalBastion.UI` sang assembly không phù hợp.
- [x] Đã dọn nốt các ad-hoc path nổi bật đã review trước đó:
  - `UiSystem`: gom `GameServices` access qua `UiServicesAccessor` thay vì rải `Ctx.Services as GameServices` ở nhiều call-site
  - `UiGameplayFlowController`: bỏ constructor-cast trực tiếp, chuyển qua `UiServicesAccessor`
  - `GameServicesUiBridge`: bỏ lặp `GetServices() as GameServices` trong pause/resume path, gom về `EnsureServices()`
- [x] Đã dọn warning deprecate mới phát sinh trong binder pass bằng cách chuyển `PlacementServicesBinder` và `WorldViewServicesBinder` từ `FindObjectsOfType(...)` sang overload mới `FindObjectsByType(FindObjectsInactive.Exclude)`.
- [~] Sau pass này, các ad-hoc resolve/binding path lớn trong cụm `UI/World/Grid` đã được dọn đáng kể; phần còn lại chủ yếu là dependency runtime trực tiếp của một số presenter/binder vào `GameServices`, và một cụm debt Unity deprecated API cũ hơn (`FindObjectOfType`, TMP wrapping API, một số debug tools/core boot call-site), nên nên tách riêng workstream cleanup deprecated API thay vì trộn tiếp vào binder cleanup.
- [~] Các presenter/binder như `InspectPanelPresenter`, `AssignNpcModalPresenter`, `RewardSelectionModalPresenter`, `RunEndedModalPresenter`, `SettingsModalPresenter`, `HudRuntimeBinder`, `BuildPanelRuntimeBinder` vẫn dùng `GameServices` trực tiếp, nhưng đây hiện là dependency runtime tường minh của UI layer hơn là service-resolution ad-hoc; nếu muốn đi tiếp nên tách riêng một workstream giảm phụ thuộc `GameServices` trong UI thay vì trộn với pass binder hiện tại.
- [~] `PlacementInputController` hiện đã có các lát cắt chính gồm `PlacementUiGate`, `PlacementPreviewRenderer`, `PlacementActionController`, và `PlacementServicesBinder`; phần còn lại đáng cân nhắc nếu đi tiếp là chuẩn hoá tiếp service-binding pattern cho các controller/view khác hoặc cân nhắc move các state-transition helper sang lớp nhỏ riêng nếu cụm placement còn nở tiếp.

### Cập nhật 2026-05-06, pass SaveService
- [x] Đã bắt đầu tách `SaveService.cs` theo hướng structural, chưa đổi behavior.
- [x] Đã tách path/policy helpers ra `SaveService.Paths.cs`.
- [x] Đã tách các JsonUtility disk models ra `SaveService.Models.cs`.
- [~] `SaveService.cs` hiện đã nhẹ hơn ở phần path/model ownership, nhưng chưa tách tiếp reader/writer/mapper.
- [x] Đã tách `CreateImmutableRunSnapshot(...)` sang `SaveService.Snapshot.cs` để cô lập snapshot writer path mà chưa đổi behavior.
- [x] Đã tách `TryReadRunFile(...)` và `AtomicWriteRunSave(...)` sang `SaveService.IO.cs` để cô lập lớp I/O helpers.
- [x] Đã tách `LoadRun(...)`, `LoadRunFromSlot(...)`, và mapper DTO trung tâm sang `SaveService.LoadMapping.cs` để `SaveService.cs` giữ orchestration mỏng hơn.
- [x] Đã tách `ReadSlotInfo(...)`, `ListRunSaves()`, và `GetLatestValidSlot()` sang `SaveService.Slots.cs` để gom slot policy/inspection về một chỗ.
- [x] Đã tách `SaveMeta(...)` và `LoadMeta(...)` sang `SaveService.Meta.cs` để hoàn tất nhóm trách nhiệm meta persistence.
- [x] Đã review/chốt lại `SaveService.cs` như facade mỏng hơn: giữ entrypoint save/delete/check đơn giản, gom helper nhỏ cho save-target/delete policy, và để phần IO/load/meta/slots/snapshot tiếp tục nằm ở các partial chuyên trách.
- [~] Cụm `SaveService*` hiện đã khá rõ boundary theo trách nhiệm; nếu đi tiếp thì chủ yếu là polish naming hoặc giảm thêm phụ thuộc `GameServices`/service reach-through ở snapshot path.
- [x] Đã bắt đầu wave giảm phụ thuộc trực tiếp vào `GameServices` bằng `PopulationService` pass 1.
- [x] `PopulationService` không còn nhận full `GameServices`; hiện constructor nhận tập dependency hẹp hơn: `IEventBus`, `IDataRegistry`, `IRunClock`, `INotificationService`, `IWorldState`, `IGridMap`, `IStorageService`, `IRunOutcomeService`.
- [x] Đã đi tiếp `PopulationService` wave 2 bằng cách bóc `PopulationHousingPolicy` và `PopulationGrowthPolicy`, để service gốc tập trung hơn vào event flow / consume food / spawn orchestration thay vì giữ toàn bộ housing-growth rule cục bộ.
- [x] Đã đi tiếp `RewardService` pass 1 theo cùng hướng: bỏ constructor nhận `GameServices`, chuyển sang `IWorldState`, `IDataRegistry`, `IEventBus`.
- [x] Đã đi tiếp `RewardService` wave 2 bằng cách bóc `RewardModifierPolicy`, chuyển reward application/modifier logic và tower-ammo-cap apply path ra khỏi service gốc để `RewardService` nghiêng hơn về selection/event orchestration.
- [x] Đã review/chốt lại cả `PopulationService` và `RewardService` theo hướng facade mỏng hơn: service gốc giữ event/day-selection flow và orchestration, còn housing/growth/modifier rule nằm ở helper/policy riêng; các helper lặp nhỏ trong service gốc cũng được gom lại cho luồng đọc rõ hơn.
- [~] Cụm `PopulationService` / `RewardService` hiện đã khá sạch ở level policy/facade; nếu đi tiếp thì chủ yếu là polish nhỏ hoặc chuyển sang cụm lớn khác.
- [x] Đã bóc `SaveAutosaveService` như một quick win nhỏ: constructor hiện nhận `IEventBus`, `ISaveService`, `IWorldState`, `IRunClock`, `INotificationService` thay vì `GameServices`.
- [x] Đã bóc tiếp `BuildOrderWorkplaceResolver`: constructor hiện nhận `BalanceService`, `IWorldState`, `IDataRegistry`, `IJobWorkplacePolicy` thay vì `GameServices`.
- [x] Đã đi tiếp `BuildOrderCreationService` pass 1: bỏ dependency trực tiếp vào `GameServices`, chuyển sang tập dependency hẹp hơn gồm `IDataRegistry`, `IWorldState`, `IGridMap`, `IEventBus`, `INotificationService`, `IStorageService`, `IUnlockService`, `IPlacementService`, `IPathfinderRuntime`.
- [x] Đã bổ sung overload hẹp cho `EntryCellUtil` và `JobReachabilityHelper` để hỗ trợ cụm `Build*` gọi theo dependency thật sự dùng thay vì container full `GameServices`.
- [x] Đã refactor `BuildOrderEventBridge` để chỉ nhận `IEventBus` thay vì giữ dependency vào full `GameServices`.
- [x] Đã refactor `BuildOrderCancellationService` để bỏ dependency trực tiếp vào `GameServices`, chuyển sang các dependency hẹp hơn: `IWorldState`, `IGridMap`, `IWorldIndex`, `IStorageService`, `IDataRegistry`, `IEventBus`, `INotificationService`, `IJobBoard`.
- [x] Đã refactor `BuildOrderReloadService` để bỏ dependency trực tiếp vào `GameServices`, hiện chỉ nhận `IWorldState` và `INotificationService` cùng state/callback nội bộ cần cho rebuild-after-load.
- [x] Đã refactor `BuildOrderCompletionService` để bỏ dependency trực tiếp vào `GameServices`, chuyển sang các dependency hẹp hơn: `IWorldState`, `IGridMap`, `IDataRegistry`, `IWorldIndex`, `IEventBus`, `INotificationService`, `ISaveService`, `IRunClock`.
- [x] Đã tách compute/time policy khỏi `BuildOrderService` sang `BuildOrderTimePolicy`, gom các helper `ComputeWorkSecondsTotal(...)`, `ComputeWorkSecondsTotalFromChunks(...)`, `ComputeRepairSeconds(...)` quanh `BalanceService` mà không làm đổi behavior.
- [x] Đã bóc `TickRepairOrder(...)` khỏi `BuildOrderService` sang `BuildOrderRepairService`, gom repair-order lifecycle, queued-job retarget, và complete/cancel handling về một chỗ.
- [x] Đã review/chốt lại `BuildOrderService` theo vai trò facade mỏng hơn: chủ yếu giữ state nhỏ, wiring các service con, và các entrypoint `Create/Cancel/Tick/Rebuild`.
- [x] Đã bóc nốt dependency full-container khỏi `BuildOrderTickProcessor`: processor này giờ chỉ nhận `IWorldState` cùng các callback/delegate cần thiết thay vì giữ `GameServices`.
- [x] Đã siết tiếp boundary ở `BuildJobPlanner`: planner này giờ nhận dependency hẹp hơn (`IWorldState`, `IJobBoard`, `IPathfinderRuntime`, runtime maps) thay vì giữ full `GameServices`.
- [x] Đã bóc tiếp `JobEnqueueService` như quick-win nối giữa `Build` và `Jobs`: service này không còn giữ full `GameServices`, mà nhận dependency hẹp hơn (`ResourcePatchService`, `IPathfinderRuntime`, `IResourceFlowService`, `IDataRegistry`, `IGridMap`, `IWorldState`, `IJobBoard`, ...).
- [x] Đã đổi seam harvest-target selection sang dependency hẹp thay vì `GameServices`: `IHarvestTargetSelector` / `DefaultHarvestTargetSelector` / `HarvestTargetSelectionHelper` giờ đi qua `ResourcePatchService` + `IPathfinderRuntime`, giúp `JobEnqueueService` không cần container full chỉ để chọn patch harvest.
- [~] Cụm `Build*` hiện đã có dependency boundary rõ hơn rõ rệt; phần còn lại đáng cân nhắc chủ yếu là rà tiếp các helper/executor còn giữ `GameServices` vì lý do path/build interaction, hoặc chuyển trọng tâm sang `Jobs*` với các quick-win kế tiếp như `NpcIdleRoamService`.
- [x] Đã chuyển sang `Ammo*` pass 1 bằng cách bóc monitor/threshold/request-notification path khỏi `AmmoService` sang `AmmoMonitorPolicy`, giảm bớt phần state-machine cục bộ trong service gốc mà chưa đụng flow job/planner nặng.
- [x] Đã đi tiếp recipe/craft-start path: `AmmoRecipeProvider` không còn bám `AmmoService`, và `AmmoCraftService` giờ nhận dependency hẹp hơn (`IWorldState`, `IStorageService`, `IJobBoard`, recipe provider, runtime state callbacks) thay vì giữ full owner/service container.
- [x] Đã bóc tiếp recovery/observability path: `AmmoMetricsReporter` và `AmmoRecoveryService` giờ nhận dependency hẹp hơn thay vì giữ owner full `AmmoService`, đồng thời status aggregation được tách sang `AmmoObservabilityReporter`.
- [x] Đã đi tiếp planner path quanh armory buffer: `ArmoryBufferPlanner` không còn bám owner/service full, thay vào đó nhận các dependency hẹp hơn (`IWorldState`, `IWorldIndex`, `IStorageService`, `IJobBoard`, runtime maps, topology callbacks, craft callback).
- [x] Đã bóc tiếp tower resupply flow: `TowerResupplyPlanner` giờ nhận dependency hẹp hơn thay vì giữ owner/service full, còn `AmmoService` chủ yếu wire request/job/state callbacks cho planner này.
- [x] Đã review/chốt lại `AmmoService` theo vai trò facade-orchestrator: sửa lại thứ tự khởi tạo constructor để tránh dùng `_recoveryService` trước khi tạo xong, gom lại các facade helper/read-model nhỏ, và giữ service gốc tập trung vào `Tick / Notify / Rebuild / Clear` cùng orchestration cấp cao.
- [~] Cụm `Ammo*` hiện đã có boundary rõ hơn đáng kể ở monitor, craft-start, recovery, observability, armory buffer, và tower resupply; nếu đi tiếp thì chủ yếu là polish nhỏ hoặc giảm thêm `GameServices` ở các lớp phụ còn lại.
- [x] Đã chuyển sang `EnemySystem` pass 1 bằng cách bóc cụm HQ/target resolution sang `EnemyTargetResolver`, giữ nguyên behavior nhưng tách bớt nhánh tương đối độc lập khỏi flow tick/attack/path chính.
- [x] Đã đi tiếp `EnemySystem` pass 2 bằng cách bóc attack/building-damage path sang `EnemyAttackResolver`, gom `TryAttackHQ`, `TryAttackBuilding`, và adjacent-blocking attack về helper riêng nhưng giữ nguyên timing/cooldown/clear-footprint behavior.
- [x] Đã đi tiếp `EnemySystem` pass 3 bằng cách bóc cleanup/despawn/cache-prune path sang `EnemyLifecycleResolver`, gom cleanup enemy chết và prune runtime map (`_attackCd`, `_pathFailStreak`) ra khỏi flow tick chính.
- [x] Đã đi tiếp `EnemySystem` pass 4 bằng cách bóc movement/path progression sang `EnemyMovementResolver`, gom greedy step, BFS step, fallback step, local escape và BFS buffer state ra helper riêng nhưng giữ nguyên behavior tick hiện tại.
- [x] Đã review/chốt lại `EnemySystem` và `CombatService` theo hướng facade/orchestrator rõ hơn: `EnemySystem` gom `TickEnemy`, `PrepareTick`, cooldown/next-step helper để luồng tick dễ đọc hơn, còn `CombatService` tách phase/day/deferred-wave guard thành các helper nhỏ thay vì nhồi hết trong `Tick`.
- [~] Cụm combat quanh `EnemySystem` / `CombatService` hiện đã khá sạch ở level facade + sub-resolver; nếu đi tiếp thì chủ yếu là giảm thêm tie `GameServices` hoặc chuyển sang cụm lớn khác.
- [x] Đã chuyển sang `PlacementInputController` pass 1 bằng cách bóc UI gate sang `PlacementUiGate`, đồng thời gom hide/reset preview state dùng chung để giảm noise trong `Update()` và giữ controller gốc tập trung hơn vào input/preview/action flow.
- [x] Đã đi tiếp `PlacementInputController` pass 2 bằng cách bóc preview renderer sang `PlacementPreviewRenderer`, gom tile/ghost/front-marker preview state và render logic ra helper riêng; controller gốc giữ preview hint + input/action orchestration.
- [~] `PlacementInputController` vẫn còn lớn vì đang ôm input, placement action, và bind/reflection; các lát tiếp theo hợp lý nhất là action/commit flow hoặc bind/reflection nếu muốn giảm tiếp độ phình.
- [ ] Bước tiếp theo hợp lý sau pass này: nếu tiếp tục cụm này, ưu tiên action/commit flow trước; bind/reflection path nên để sau vì đụng bootstrap/runtime wiring nhạy hơn.

Đây là bộ 3 có tỷ lệ **giảm đau / rủi ro thấp / hiệu quả dài hạn** tốt nhất cho codebase hiện tại.
