# Checklist ổn định hóa

_Trạng thái thực tế sau vòng smoke test + save/load pass đầu tiên._

## Baseline hiện tại

### Đã ổn ở mức manual/smoke

- [x] Unity compile sạch
- [x] Chạy EditMode tests
- [x] Smoke test Jobs
- [x] Smoke test Build
- [x] Smoke test RunStart
- [x] Rà soát save/load các case chính

### Những gì đã verify

#### Jobs
- [x] NPC nhận đúng job theo workplace
- [x] Harvest ra đúng resource
- [x] Haul jobs không duplicate vô hạn
- [x] `BuildWork` chạy đúng flow delivery + build
- [x] `RepairWork` hoàn tất và dọn state đúng
- [x] Armory vẫn ưu tiên `ResupplyTower`
- [x] Claim được nhả khi complete/cancel/fail

#### Build
- [x] Tạo place order sinh ra site + placeholder đúng
- [x] Cancel place order xóa site + placeholder đúng
- [x] Cancel place order rollback auto-road đúng
- [x] Cancel place order refund resource đúng
- [x] Upgrade order hoàn tất đúng
- [x] Repair order tạo/clear repair job đúng
- [x] `BuildWork` không bị duplicate cho cùng site
- [x] Save/load với build site / `BuildWork` / `RepairWork` / auto-road hoạt động ổn

#### RunStart
- [x] `StartNewRun` chạy thành công với config hợp lệ
- [x] Có HQ thật sau world apply
- [x] Starting storage chỉ seed vào HQ
- [x] NPC không spawn vào ô blocked
- [x] Workplace assignment hợp lệ
- [x] Spawn gate kết nối vào road graph
- [x] Lane runtime build đúng

#### Save/Load
- [x] Save/load với active build site
- [x] Save/load với active `BuildWork`
- [x] Save/load với active `RepairWork`
- [x] Save/load với queued haul jobs
- [x] Save/load với NPC đang giữ `CurrentJob`
- [x] Save/load sau khi auto-road được tạo
- [x] `BuildOrderService.RebuildActivePlaceOrdersFromSitesAfterLoad()` không duplicate order
- [x] `WorldIndex` và storage state nhất quán sau reload

---

## Regression / stabilization đã khóa thêm

### Jobs
- [x] `JobAssignmentService`: role filter hoạt động đúng
- [x] `JobAssignmentService`: không assign khi workplace roles không hợp lệ
- [x] `JobExecutionService`: missing job dọn state NPC đúng
- [x] `JobExecutionService`: terminal job dọn state NPC đúng
- [x] `JobStateCleanupService`: nhả claim đúng
- [x] `JobEnqueueService`: harvest enqueue tôn trọng slot caps / số NPC workplace
- [x] `JobEnqueueService`: không enqueue harvest khi local cap đã đầy

### Build
- [x] `BuildOrderCancellationService`: không xóa nhầm road cũ khi cancel nếu không có recorded auto-road
- [x] `BuildOrderService`: rebuild-after-load khôi phục đúng 1 active order cho 1 active site, không cộng dồn duplicate qua nhiều lần rebuild
- [x] `BuildOrderService`: rebuild-after-load được verify riêng như một smoke case độc lập
- [x] `BuildOrderCancellationService`: refund delivered resources về storage hợp lệ gần nhất
- [x] `BuildOrderCancellationService`: cancel repair xóa tracked repair job
- [x] `BuildJobPlanner`: stale tracked jobs được prune
- [x] `BuildJobPlanner`: work job được recreate sau terminal state
- [x] `BuildOrderTickProcessor`: path complete upgrade xử lý đúng
- [x] `BuildOrderCreationService`: case thiếu tài nguyên được cover
- [x] `BuildOrderCreationService`: case upgrade bị khóa được cover
- [x] `BuildOrderCreationService`: case placement/footprint không hợp lệ được cover

### RunStart / SaveLoad runtime
- [x] `RunStart`: config startup lỗi fail rõ ràng trước khi tạo partial world/runtime state
- [~] `SaveLoadApplier`: rebuild runtime cache (lane/spawn-gate) sau load — đã có regression test, nhưng hiện đang `Ignore` trong EditMode fixture rút gọn khi không đủ production defs/config để validate StartMapConfig thật
- [x] `SaveLoadApplier`: stale assignment `Npc.CurrentJob` được clear và NPC reset về idle sau load
- [x] `RunStartValidator`: `GATE_NOT_CONNECTED`
- [x] `RunStartValidator`: `GATE_NOT_ROAD`
- [x] `RunStartWorldBuilder`: invalid building def fail fast
- [x] `RunStartPlacementHelper`: relocation tìm được anchor hợp lệ gần đó
- [x] `RunStartPlacementHelper`: relocation tôn trọng `BuildableRect`
- [x] `RunStartStorageInitializer`: HQ hợp lệ nhận đúng lượng starting storage mong đợi
- [x] `RunStartValidator`: `NPC_WORKPLACE_UNBUILT`
- [x] `RunStartValidator`: `NPC_SPAWN_OOB`
- [x] `RunStartHqResolver`: deterministic HQ selection khi có nhiều candidate

## Còn actionable nếu muốn làm tiếp

- [x] Đã review lại boundary giữa Jobs / Build / RunStart services sau refactor
- [~] Kết luận review: `RunStart` hiện là cụm có boundary sạch nhất trong 3 cụm vì flow đã tách khá tuyến tính theo phase/helpers; nếu đi tiếp thì chủ yếu là giảm dần phụ thuộc `GameServices` bằng context/view hẹp, chưa cần mổ thêm theo responsibility.
- [~] Kết luận review: `Build` vẫn còn bleed-through `GameServices` ở `BuildOrderService` facade và đặc biệt là `BuildOrderTickProcessor`; đây là target đáng làm tiếp nhất cho pass boundary tightening.
- [~] Kết luận review: `Jobs` vẫn còn owner/container bleed-through trong `JobScheduler` và một số service con (`JobEnqueueService`, `JobExecutionService`, `NpcIdleRoamService`); sau `Build` thì đây là mặt trận hợp lý kế tiếp.
- [x] Đã bóc nốt dependency full-container khỏi `BuildOrderTickProcessor` và siết tiếp boundary ở `BuildJobPlanner`.
- [x] Đã bóc tiếp `JobEnqueueService` như quick-win kế tiếp: bỏ dependency full `GameServices`, chuyển sang dependency hẹp hơn và đổi seam harvest-target selection theo cùng hướng.
- [x] Đã bóc tiếp `NpcIdleRoamService`: bỏ dependency full `GameServices`, chuyển sang `IAgentMoverRuntime`, `IDataRegistry`, `IGridMap`, `IWorldState`.
- [x] Đã dọn pass compile-fix hậu refactor boundary cho cụm `Build` và `Jobs`:
  - `NpcIdleRoamService` đổi hẳn sang contract runtime hiện hành `IAgentMoverRuntime` thay vì tên cũ `IAgentMover`.
  - `BuildJobPlanner` được siết lại để gọi đúng overload dependency hẹp của `EntryCellUtil` và `JobReachabilityHelper` (`IDataRegistry`, `IGridMap`, `IPathfinderRuntime`) thay vì lẫn call-site kiểu `GameServices`/signature cũ.
  - Đã bổ sung constructor compatibility cho `BuildJobPlanner`, `BuildOrderTickProcessor`, và `JobEnqueueService` để giữ regression tests cũ compile được trong khi production path tiếp tục dùng constructor dependency-hẹp.
- [x] Đã bóc tiếp `JobExecutionService`: service này không còn giữ full `GameServices`, mà nhận dependency hẹp hơn (`IWorldState`, `IJobBoard`, `JobExecutorRegistry`, `JobStateCleanupService`, `IAgentMoverRuntime`, `IGridMap`) và vẫn giữ constructor compatibility cho call-site/test cũ.
- [x] Đã bóc `InteractionCellExitHelper` sang overload dependency hẹp (`IDataRegistry`, `IGridMap`, `IAgentMoverRuntime`) cho các path step-off building/site/cell, trong khi overload `GameServices` cũ vẫn giữ như forwarding shim để tránh làm nổ call-site hàng loạt.
- [x] Đã cập nhật `JobScheduler` sang wiring constructor hẹp cho `JobExecutionService`.
- [x] Đã bóc tiếp cụm executor low-risk trong `Jobs/Executors` khỏi full `GameServices`, đồng thời giữ constructor compatibility cho path cũ:
  - `HarvestExecutor`
  - `CraftAmmoExecutor`
  - `RepairWorkExecutor`
  - `HaulBasicExecutor`
  - `HaulAmmoToArmoryExecutor`
  - `ResupplyTowerExecutor`
  - `HaulToForgeExecutor`
  - `BuildDeliverExecutor`
- [x] `JobExecutorRegistry` hiện đã instantiate các executor trên bằng constructor dependency-hẹp, giảm owner/container bleed-through trong production wiring mà chưa cần churn test/call-site cũ.
- [~] Sau pass này, phần `Jobs` còn lại dùng full container chủ yếu co lại quanh một vài executor/service chưa đụng tới như `BuildWorkExecutor`; nên tiếp tục bằng các pass nhỏ thay vì đại phẫu.
- [~] Sau cụm `Build`, pass giảm dependency full `GameServices` trong `JobScheduler` và nhóm service/executor chính đã tiến được thêm một đoạn rõ rệt; phần còn lại nên ưu tiên theo executor/service còn giữ full container thay vì mổ ngang toàn cụm.
- [ ] Mở rộng thêm regression save/load cho tracked runtime state khác nếu thấy cần
- [ ] Polish thêm smoke coverage nếu có case manual nào còn thấy rủi ro

