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

- [ ] Review lại boundary giữa Jobs / Build / RunStart services sau refactor
- [ ] Mở rộng thêm regression save/load cho tracked runtime state khác nếu thấy cần
- [ ] Polish thêm smoke coverage nếu có case manual nào còn thấy rủi ro

