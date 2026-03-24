# Stabilization Checklist

_Kế hoạch ổn định hóa sau refactor cho Jobs, Build và RunStart._

## Mục tiêu

Trước khi thêm feature lớn, cần đảm bảo repo:

- compile sạch
- pass bộ test nền tảng
- ổn định ở các vòng lặp gameplay cốt lõi
- an toàn ở các luồng save/load và startup
- có regression coverage cho các subsystem đã refactor

---

## Giai đoạn 1 — Nền tảng xanh

### 1. Baseline compile + test

- [x] Unity compile sạch trên toàn project
- [x] Không còn lỗi asmdef/reference/protection-level
- [x] Chạy toàn bộ EditMode tests
- [x] Ghi lại baseline pass count (`29/29` xanh tính đến 2026-03-23)

### 2. Smoke test Jobs

- [x] NPC rảnh nhận job từ workplace đúng
  - Kiểm tra nhanh: gán 1 NPC vào producer (`bld_lumbercamp_t1` / `bld_farmhouse_t1`) và 1 NPC vào HQ/warehouse
  - Pass nếu: NPC producer nhận `Harvest`; NPC HQ/warehouse nhận `HaulBasic` / việc phù hợp role build
  - Fail nếu: NPC đứng idle mãi dù setup hợp lệ, hoặc nhận job sai role
- [x] Harvest tạo đúng loại resource
  - Kiểm tra nhanh: chạy 1 worker tại lumber/farm/quarry/iron hut và quan sát `DebugStorageHUD`
  - Pass nếu: Lumbercamp=>Wood, Farmhouse=>Food, Quarry=>Stone, IronHut=>Iron
  - Fail nếu: tăng sai resource, không tạo resource, hoặc harvest lặp mà không có output
- [x] Haul jobs không bị duplicate vô hạn
  - Kiểm tra nhanh: giữ 1 producer có hàng, 1 HQ/warehouse còn chỗ trống, và 1 hauler hoạt động; quan sát số `HaulBasic` active/queue theo thời gian
  - Pass nếu: haul jobs xuất hiện khi cần và giữ mức ổn định/có giới hạn
  - Fail nếu: số active haul jobs hoặc job ids cứ tăng mãi cho cùng một nhu cầu
- [x] `BuildWork` xử lý đúng flow delivery + build
  - Kiểm tra nhanh: đặt một building mới có cost và quan sát delivery -> `BuildWork` -> hoàn tất
  - Pass nếu: delivery diễn ra trước khi cần, sau đó chỉ có đúng một `BuildWork` chạy và site hoàn tất sạch
  - Fail nếu: site đứng sau delivery, `BuildWork` bị duplicate, hoặc hoàn tất xong vẫn còn state/job rác
- [x] `RepairWork` hoàn tất và dọn state job đúng
  - Kiểm tra nhanh: damage một building đã xây (`K` trong `DebugBuildingTool`), rồi tạo repair (`R`)
  - Pass nếu: chỉ tạo đúng một `RepairWork`, HP hồi lại, và state NPC/job được clear khi xong
  - Fail nếu: spawn nhiều repair jobs, HP hồi nhưng job vẫn kẹt, hoặc NPC giữ `CurrentJob` stale
- [x] Armory priority vẫn ưu tiên `ResupplyTower`
  - Kiểm tra nhanh: tạo đồng thời nhu cầu armory-related và nhu cầu tower low-ammo; kiểm tra queue/hành vi
  - Pass nếu: `ResupplyTower` được chọn trước `HaulAmmoToArmory` / `HaulToForge`
  - Fail nếu: worker armory xử lý ammo job ưu tiên thấp trước khi tiếp đạn tower
- [x] Claim được nhả ra khi complete/cancel/fail
  - Kiểm tra nhanh: để một job complete tự nhiên, rồi riêng một job khác bị cancel/fail qua debug actions và quan sát việc reassignment
  - Pass nếu: NPC nhận được job mới mà không cần clear claim thủ công
  - Fail nếu: NPC/site bị softlock cho đến khi bấm `Release All Claims`

### 3. Smoke test Build

- [x] Tạo place order sinh ra site + placeholder đúng
- [x] Cancel place order xóa site và placeholder đúng
- [x] Cancel place order rollback auto-road nếu có
- [x] Cancel place order refund resource đã giao nếu có
- [x] Upgrade order vẫn hoàn tất đúng
- [x] Repair order tạo và clear repair job đúng
- [x] `BuildWork` không bị duplicate cho cùng một site
- [ ] Rebuild-after-load vẫn khôi phục active orders đúng

### 4. Smoke test RunStart

- [x] `StartNewRun` chạy thành công với config hợp lệ
  - Bước test:
    1. Khởi động một run mới bằng map/config chuẩn đang dùng hằng ngày
    2. Chờ world apply hoàn tất
    3. Quan sát log/notification lúc vào run
  - Pass nếu: vào run thành công, không có exception đỏ, không bị kẹt ở trạng thái nửa chừng
  - Fail nếu: không vào được run, văng lỗi, hoặc world lên thiếu thành phần chính

- [x] Có HQ thật sau khi world apply
  - Bước test:
    1. Sau khi StartNewRun, chọn công trình trung tâm
    2. Kiểm tra building đó tồn tại như một building hợp lệ trong world
    3. Kiểm tra không chỉ là placeholder/site
  - Pass nếu: HQ tồn tại thật, đã constructed, có thể inspect bình thường
  - Fail nếu: không có HQ, HQ chỉ là placeholder, hoặc có nhiều HQ bất thường ngoài config mong đợi

- [x] Starting storage chỉ được seed vào HQ
  - Bước test:
    1. Start run mới hoàn toàn
    2. Mở inspect HQ và các storage/building liên quan ngay đầu run
    3. So sánh Wood/Food/Stone/Iron/Ammo giữa HQ và các nơi khác
  - Pass nếu: tài nguyên khởi đầu chỉ nằm ở HQ
  - Fail nếu: warehouse/building khác cũng được seed tài nguyên ban đầu, hoặc HQ bị thiếu seed

- [x] NPC không spawn vào ô bị chặn
  - Bước test:
    1. Start run mới
    2. Kiểm tra vị trí spawn ban đầu của tất cả NPC
    3. Quan sát vài giây đầu khi NPC bắt đầu di chuyển
  - Pass nếu: NPC đứng ở ô hợp lệ, không đè lên building/site/cell blocked, không kẹt ngay khi spawn
  - Fail nếu: NPC spawn chồng vật thể, đứng trong ô blocked, hoặc bị kẹt ngay từ đầu

- [x] Workplace assignment của NPC là hợp lệ
  - Bước test:
    1. Start run mới
    2. Inspect một lượt các NPC hoặc dùng debug hiện workplace
    3. So đối chiếu workplace với building thực tế trên map
  - Pass nếu: workplace tồn tại, đúng loại building mong đợi, không trỏ vào building chưa hợp lệ
  - Fail nếu: workplace missing, trỏ sai building, hoặc assignment không khớp cấu hình run

- [x] Spawn gate kết nối vào road graph
  - Bước test:
    1. Start run mới
    2. Xác định từng spawn gate trên map
    3. Kiểm tra từ gate có đường nối vào mạng road chính
  - Pass nếu: gate nối được vào road graph và không bị cô lập
  - Fail nếu: gate đứng tách rời, không có đường hợp lệ để enemy/pathing dùng

- [x] Lane runtime được build đúng
  - Bước test:
    1. Start run mới
    2. Chạy đến lúc có spawn hoặc dùng debug spawn theo lane nếu tiện
    3. Quan sát enemy/path runtime theo từng lane
  - Pass nếu: lane tồn tại đúng, spawn/pathing theo lane hoạt động, không đi lạc khỏi flow chính
  - Fail nếu: lane thiếu, spawn sai lane, hoặc runtime lane bị null/sai tuyến

- [ ] Config startup lỗi phải fail rõ ràng thay vì tạo runtime nửa hợp lệ _(tạm bỏ qua / chưa test được)_
  - Bước test:
    1. Chuẩn bị một config/map cố ý lỗi nhẹ (ví dụ workplace thiếu, spawn ngoài map, gate không nối road)
    2. Chạy StartNewRun với config đó
    3. Quan sát lỗi trả ra/log/notification
  - Pass nếu: hệ thống fail sớm, báo lỗi rõ, không dựng world ở trạng thái nửa hợp lệ
  - Fail nếu: vẫn vào run nhưng world sai một phần, hoặc lỗi quá mơ hồ khó chẩn đoán

---

## Giai đoạn 2 — Mở rộng regression

### 5. Regression tests Jobs (vòng 2)

- [ ] `JobAssignmentService`: role filter hoạt động đúng
- [ ] `JobAssignmentService`: không assign khi workplace roles không hợp lệ
- [ ] `JobExecutionService`: missing job dọn state NPC đúng
- [ ] `JobExecutionService`: terminal job dọn state NPC đúng
- [ ] `JobStateCleanupService`: nhả claim đúng
- [ ] `JobEnqueueService`: harvest enqueue tôn trọng slot caps / số NPC của workplace
- [ ] `JobEnqueueService`: không enqueue harvest khi local cap đã đầy
- [x] Thêm coverage canonical `DefId` ngoài các case producer/destination hiện có

### 6. Regression tests Build (vòng 2)

- [ ] `BuildOrderCancellationService`: refund delivered resources về storage hợp lệ gần nhất
- [ ] `BuildOrderCancellationService`: cancel repair xóa tracked repair job
- [ ] `BuildJobPlanner`: stale tracked jobs được prune
- [ ] `BuildJobPlanner`: work job được recreate sau terminal state
- [x] `BuildOrderTickProcessor`: path missing site xử lý đúng
- [ ] `BuildOrderTickProcessor`: path complete upgrade xử lý đúng
- [x] `BuildOrderReloadService`: missing placeholder được surface đúng
- [x] `BuildOrderReloadService`: rebuild multi-site giữ tính deterministic
- [ ] `BuildOrderCreationService`: case thiếu tài nguyên được cover
- [ ] `BuildOrderCreationService`: case upgrade bị khóa được cover
- [ ] `BuildOrderCreationService`: case placement/footprint không hợp lệ được cover

### 7. Regression tests RunStart (vòng 2)

- [ ] `RunStartWorldBuilder`: invalid building def fail fast
- [x] `RunStartWorldBuilder`: fallback kiểu tower vẫn hoạt động
- [ ] `RunStartPlacementHelper`: relocation tìm được anchor hợp lệ gần đó
- [ ] `RunStartPlacementHelper`: relocation tôn trọng `BuildableRect`
- [ ] `RunStartStorageInitializer`: HQ hợp lệ nhận đúng lượng starting storage mong đợi
- [x] `RunStartValidator`: `NPC_WORKPLACE_MISSING`
- [ ] `RunStartValidator`: `NPC_WORKPLACE_UNBUILT`
- [x] `RunStartValidator`: `NPC_SPAWN_BLOCKED`
- [ ] `RunStartValidator`: `NPC_SPAWN_OOB`
- [ ] `RunStartValidator`: `GATE_NOT_CONNECTED`
- [ ] `RunStartValidator`: `GATE_NOT_ROAD`
- [ ] `RunStartHqResolver`: deterministic HQ selection khi có nhiều candidate

---

## Giai đoạn 3 — Ổn định save/load

### 8. Kiểm tra save/load giữa run

- [x] Save/load với active build site
- [x] Save/load với active `BuildWork`
- [x] Save/load với active `RepairWork`
- [x] Save/load với queued haul jobs
- [x] Save/load với NPC đang giữ `CurrentJob`
- [x] Save/load sau khi auto-road được tạo

### 9. Kiểm tra tính nhất quán sau reload

- [x] `BuildOrderService.RebuildActivePlaceOrdersFromSitesAfterLoad()` không duplicate order
- [ ] Tracked job maps không giữ orphan IDs
- [ ] `JobScheduler` không reassign stale jobs sau load
- [x] `WorldIndex` và storage state vẫn nhất quán sau reload

---

## Giai đoạn 4 — Dọn dẹp

### 10. Dọn Jobs

- [ ] Xóa helper/method chết không còn call site
- [ ] Xóa/cập nhật comment cũ sau refactor
- [ ] Xác minh boundary giữa các service mới vẫn còn có ý nghĩa
- [ ] Đảm bảo `JobScheduler` không phình lại thành nơi chứa helper domain lung tung

### 11. Dọn Build

- [ ] Xóa/cập nhật comment legacy cũ (`BuildDeliver`, v.v.)
- [ ] Xác nhận `_deliverJobsBySite` vẫn còn mục đích tương thích thật sự
- [ ] Review utility logic bị lặp giữa cancellation/completion/creation
- [ ] Giữ naming của planner / processor / service nhất quán

### 12. Dọn RunStart

- [ ] Xác minh toàn bộ logic fallback HQ đã được canonicalize nhất quán
- [ ] Quyết định xem fallback zones hardcoded còn là policy hợp lệ không
- [ ] Đánh giá lại `assignedWorkplaceDefId` nếu map có thể chứa nhiều workplace cùng def
- [ ] Thêm logging/warning khi relocation đi quá xa anchor ban đầu

---

## Giai đoạn 5 — Bộ sanity manual cho gameplay

### 13. Early game

- [ ] Start run mới
- [ ] HQ có đúng lượng tài nguyên khởi đầu
- [ ] NPC spawn đúng
- [ ] Harvest bắt đầu đúng
- [ ] Hauling bắt đầu đúng

### 14. Flow xây dựng

- [ ] Đặt building
- [ ] Worker giao hàng và build
- [ ] Hoàn tất xây dựng
- [ ] Hủy xây dựng
- [ ] Upgrade building
- [ ] Repair building hỏng

### 15. Sanity combat/support

- [ ] Ammo/tower flow vẫn hoạt động sau canonical `DefId` cleanup
- [ ] Flow lane/spawn gate vẫn route đúng về HQ
- [ ] Armory/tower resupply không bị đói hoặc kẹt

---

## Giai đoạn 6 — Quản lý thay đổi

### 16. Khóa baseline

- [ ] Tag hoặc ghi chú một commit ổn định sau stabilization
- [ ] Duy trì changelog ngắn cho các commit refactor lớn
- [ ] Thiết lập một baseline xanh đã biết trước khi vào feature lớn tiếp theo
- [ ] Tránh refactor lớn mới trước khi baseline được xác nhận ổn định

---

## Thứ tự đề xuất

### Việc cần làm ngay

- [x] Compile sạch
- [x] Chạy EditMode tests
- [x] Smoke test Jobs
- [x] Smoke test Build
- [x] Smoke test RunStart

### Tiếp theo

- [ ] Thêm Jobs regression tests vòng 2
- [ ] Thêm Build regression tests vòng 2
- [ ] Thêm RunStart regression tests vòng 2

### Trước feature lớn tiếp theo

- [x] Rà soát save/load
- [ ] Dọn dead-code / stale-comment
- [ ] Khóa một stable baseline commit đã biết tốt
