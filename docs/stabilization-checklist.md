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

## Pending thực tế

### 1. Chưa test / tạm bỏ qua

- [ ] `RunStart`: config startup lỗi phải fail rõ ràng thay vì tạo runtime nửa hợp lệ
- [ ] `Build`: rebuild-after-load được verify riêng như một smoke case độc lập
- [ ] Reload consistency sâu hơn cho tracked jobs / stale assignment

### 2. Regression tests vòng 2

#### Jobs
- [ ] `JobAssignmentService`: role filter hoạt động đúng
- [ ] `JobAssignmentService`: không assign khi workplace roles không hợp lệ
- [ ] `JobExecutionService`: missing job dọn state NPC đúng
- [ ] `JobExecutionService`: terminal job dọn state NPC đúng
- [ ] `JobStateCleanupService`: nhả claim đúng
- [ ] `JobEnqueueService`: harvest enqueue tôn trọng slot caps / số NPC workplace
- [ ] `JobEnqueueService`: không enqueue harvest khi local cap đã đầy

#### Build
- [x] `BuildOrderCancellationService`: không xóa nhầm road cũ khi cancel nếu không có recorded auto-road
- [x] `BuildOrderService`: rebuild-after-load khôi phục đúng 1 active order cho 1 active site, không cộng dồn duplicate qua nhiều lần rebuild
- [x] `BuildOrderCancellationService`: refund delivered resources về storage hợp lệ gần nhất
- [ ] `BuildOrderCancellationService`: cancel repair xóa tracked repair job
- [ ] `BuildJobPlanner`: stale tracked jobs được prune
- [ ] `BuildJobPlanner`: work job được recreate sau terminal state
- [ ] `BuildOrderTickProcessor`: path complete upgrade xử lý đúng
- [ ] `BuildOrderCreationService`: case thiếu tài nguyên được cover
- [ ] `BuildOrderCreationService`: case upgrade bị khóa được cover
- [ ] `BuildOrderCreationService`: case placement/footprint không hợp lệ được cover

#### RunStart / SaveLoad runtime
- [~] `SaveLoadApplier`: rebuild runtime cache (lane/spawn-gate) sau load — đã có regression test, nhưng hiện đang `Ignore` trong EditMode fixture rút gọn khi không đủ production defs/config để validate StartMapConfig thật
- [x] `RunStartValidator`: `GATE_NOT_CONNECTED`
- [ ] `RunStartWorldBuilder`: invalid building def fail fast
- [ ] `RunStartPlacementHelper`: relocation tìm được anchor hợp lệ gần đó
- [ ] `RunStartPlacementHelper`: relocation tôn trọng `BuildableRect`
- [ ] `RunStartStorageInitializer`: HQ hợp lệ nhận đúng lượng starting storage mong đợi
- [ ] `RunStartValidator`: `NPC_WORKPLACE_UNBUILT`
- [ ] `RunStartValidator`: `NPC_SPAWN_OOB`
- [ ] `RunStartValidator`: `GATE_NOT_CONNECTED`
- [ ] `RunStartValidator`: `GATE_NOT_ROAD`
- [ ] `RunStartHqResolver`: deterministic HQ selection khi có nhiều candidate

### 3. Cleanup trước feature lớn tiếp theo

- [x] Dọn dead-code / stale-comment nhẹ cho batch stabilization hiện tại
- [ ] Review lại boundary giữa Jobs / Build / RunStart services sau refactor
- [x] Khóa một stable baseline commit đã biết tốt
- [x] Ghi changelog ngắn cho batch stabilization này

---

## Thứ tự khuyến nghị từ đây

1. `RunStart` invalid-config coverage
2. Build regression tests vòng 2
3. RunStart regression tests vòng 2
4. Jobs regression tests vòng 2
5. Cleanup + khóa stable baseline
