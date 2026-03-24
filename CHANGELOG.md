# CHANGELOG

## 2026-03-24

### Tóm tắt
Đợt cập nhật này tập trung vào việc hoàn thiện vòng **stabilization thủ công** cho Build, RunStart và Save/Load; đồng thời cải thiện workflow debug để test nhanh hơn ngay trong game.

### Build / UI / huỷ construction
- Thêm nút **`CANCEL CONSTRUCTION`** vào Inspect panel cho công trình đang xây.
- Mở rộng `IBuildOrderService` để UI có thể gọi trực tiếp:
  - `CancelBySite(...)`
  - `CancelByBuilding(...)`
- Nối flow UI chính để người chơi có thể huỷ construction mà không cần dùng debug tool.
- Fix rollback auto-road khi huỷ build:
  - huỷ đúng driveway được auto-create cho order đó
  - không xoá nhầm road cũ đã tồn tại từ trước nếu cell driveway bị trùng

### Save / Load
- Bổ sung quick actions vào **Essential Debug Panel**:
  - `Save Run`
  - `Load + Apply`
  - `Quick Save+Load`
  - `Delete Save`
  - `Run SaveLoad Matrix`
  - `Internal CI SaveLoad`
- Fix refresh road sau load bằng cách publish `RoadsDirtyEvent()` sau `Load + Apply`.
- Rebuild lại runtime cache cần thiết sau load để tránh mất trạng thái runtime phụ thuộc RunStart config.
- Bổ sung refresh resource UI sau load để HUD/inspect phản ánh lại tài nguyên đúng trạng thái save.
- Tích hợp **resume combat tự động sau load** trong debug flow để tránh phải spawn enemy thêm lần nữa mới đánh thức combat loop.

### Smoke test / stabilization
- Hoàn tất smoke test thủ công cho:
  - Jobs
  - Build
  - RunStart
- Save/load hiện đã pass ở mức manual cho các case chính:
  - active build site
  - active `BuildWork`
  - active `RepairWork`
  - queued haul jobs
  - NPC đang giữ `CurrentJob`
  - auto-road sau khi đặt build site
- `docs/stabilization-checklist.md` đã được:
  - dịch sang tiếng Việt
  - cập nhật trạng thái pass thực tế
  - rút gọn lại theo các mục còn actionable

### Regression / baseline cập nhật thêm
- Jobs:
  - `JobAssignmentService`: role filter đúng, workplace roles invalid thì không assign và notify đúng
  - `JobExecutionService`: current job missing/terminal sẽ dọn state NPC đúng sau tick
  - `JobStateCleanupService`: cleanup NPC job sẽ clear current job, set idle, và release claims
  - `JobEnqueueService`: harvest respect slot cap theo số NPC workplace và không enqueue khi local cap đã đầy
- Build:
  - `BuildOrderCreationService`: fail sớm đúng cho thiếu tài nguyên / placement invalid / upgrade bị khóa
  - `BuildOrderCancellationService`: không xóa nhầm road cũ, refund đúng storage, cancel repair xóa tracked repair job
  - `BuildJobPlanner`: stale tracked `BuildWork` được prune và `BuildWork` được recreate sau terminal state
  - `BuildOrderTickProcessor`: complete path của upgrade order xử lý đúng
  - rebuild-after-load không duplicate active order
  - smoke case độc lập verify rebuild-after-load khôi phục đúng progress + placeholder binding của place order
- RunStart / SaveLoad runtime:
  - `RunStartValidator`: `GATE_NOT_CONNECTED`, `GATE_NOT_ROAD`, `NPC_SPAWN_OOB`, `NPC_WORKPLACE_UNBUILT`
  - `RunStartPlacementHelper`: relocation đúng và vẫn tôn trọng `BuildableRect`
  - `RunStartStorageInitializer`: seed đúng starting storage vào HQ constructed duy nhất
  - `RunStartHqResolver`: chọn HQ target deterministically khi có nhiều candidate
  - `RunStartFacade`: config header invalid fail trước khi tạo partial world/runtime state
  - `RunStartWorldBuilder`: invalid building def fail fast
  - `SaveLoadApplier`: stale `Npc.CurrentJob` từ save bị clear và NPC được reset về idle để runtime assignment rebuild sạch
  - rebuild runtime cache sau load đã có regression test, nhưng hiện vẫn `Ignore` có chủ đích trong EditMode fixture rút gọn khi thiếu production defs/config đầy đủ
- `docs/stabilization-checklist.md` đã được cập nhật lại theo trạng thái pass mới nhất.

### GDD / planning / backlog
- Hợp nhất và viết lại bộ GDD working set hiện tại thành cấu trúc gọn, thực dụng và dễ dùng hơn trong `Docs/GDD`:
  - `Docs/GDD/00_Master/SEASONAL_BASTION_GDD_COMPLETE_v1.0_VN.md`
  - `Docs/GDD/10_Specs/SEASONAL_BASTION_UI_SPEC_v1.0_VN.md`
  - `Docs/GDD/10_Specs/SEASONAL_BASTION_UX_ONBOARDING_SPEC_v1.0_VN.md`
  - `Docs/GDD/10_Specs/SEASONAL_BASTION_CONTENT_SCOPE_SPEC_v1.0_VN.md`
  - `Docs/GDD/20_Roadmap/SEASONAL_BASTION_VERTICAL_SLICE_ROADMAP_v1.0_VN.md`
  - `Docs/GDD/30_Backlog/SEASONAL_BASTION_BACKLOG_M1_VERTICAL_SLICE_v1.0_VN.md`
  - `Docs/GDD/30_Backlog/SEASONAL_BASTION_BACKLOG_M2_YEAR1_COMPLETE_v1.0_VN.md`
  - `Docs/GDD/30_Backlog/SEASONAL_BASTION_BACKLOG_M3_BASE_RUN_COMPLETE_v1.0_VN.md`
- Viết trọn bộ implementation checklist cho toàn bộ M1:
  - `Docs/GDD/40_Implementation/SEASONAL_BASTION_IMPLEMENTATION_CHECKLIST_M1_WAVE1_v1.0_VN.md`
  - `Docs/GDD/40_Implementation/SEASONAL_BASTION_IMPLEMENTATION_CHECKLIST_M1_WAVE2_v1.0_VN.md`
  - `Docs/GDD/40_Implementation/SEASONAL_BASTION_IMPLEMENTATION_CHECKLIST_M1_WAVE3_v1.0_VN.md`
  - `Docs/GDD/40_Implementation/SEASONAL_BASTION_IMPLEMENTATION_CHECKLIST_M1_WAVE4_v1.0_VN.md`
  - `Docs/GDD/40_Implementation/SEASONAL_BASTION_IMPLEMENTATION_CHECKLIST_M1_WAVE5_v1.0_VN.md`
- Bổ sung `Docs/GDD/40_Implementation/SEASONAL_BASTION_WAVE1_CODE_MAP_v1.0_VN.md` để map từng task Wave 1 sang các file code hiện tại cần đụng tới.
- Thêm `Docs/GDD/README.md` giải thích cấu trúc thư mục, thứ tự đọc, và quy ước cập nhật.
- Di chuyển toàn bộ bộ GDD working set ra khỏi `Assets/` sang `Docs/GDD` để tránh `.meta` noise và tách tài liệu khỏi Unity asset tree.

### Ghi chú
- Baseline manual/smoke hiện đã khá chắc cho vòng stabilization đầu tiên.
- Đã khóa một mốc baseline ổn định cho batch stabilization ngày 2026-03-24.
- Bộ tài liệu trong `Assets/GDD` hiện đã đủ để chuyển sang implementation theo milestone mà không cần viết thêm tài liệu lớn ngay.

## 2026-03-23

### Tóm tắt
Đợt stabilization này tập trung vào Jobs, routing workplace cho Build/Repair, hành vi tiếp đạn của tower, và làm sạch đáng kể workflow debug trong game.

### Jobs / Build / Repair
- Sửa workplace routing để **BuilderHut được ưu tiên** cho `BuildWork` / `RepairWork`.
- HQ giờ đóng vai trò **fallback** khi BuilderHut không còn worker rảnh.
- Các job `BuildWork` / `RepairWork` đang queue có thể retarget khi workplace availability thay đổi.
- `RepairWorkExecutor` được đổi từ kiểu hồi máu theo từng cục sang **repair progress liên tục theo tick**, cho cảm giác gần với `BuildWork` hơn.
- Flow repair/build và các smoke scenario liên quan tới Jobs đã được chạy và đánh dấu ổn định cho pass này.

### Tower ammo / Armory priority
- Hành vi resupply tower được mở rộng từ kiểu “chỉ tower low/empty mới xin đạn” thành **“mọi tower chưa full đều có thể xin top-up”**.
- Các tower dưới ngưỡng urgent giờ được xử lý theo kiểu **urgent-first**.
- Thêm soft preemption để một job resupply đang queue có thể **retarget sang tower urgent trước khi đợt giao tiếp theo bắt đầu**.
- `ResupplyTower` vẫn là ưu tiên cao nhất cho worker role Armory, cao hơn các ammo job khác.

### Debug tools / QA workflow
- `DebugHUDHub` được đơn giản hoá thành **Essential Debug Panel** thực dụng hơn.
- Thêm hoặc cải thiện quick actions cho:
  - cấp tài nguyên
  - unlock all
  - damage / heal / repair building
  - complete hovered site / complete all sites
  - drain/refill ammo tower
  - điều khiển time scale, gồm cả `5x`
  - nhảy ngày / mùa
  - spawn enemy theo lane
  - spawn NPC nhanh
- Spawn enemy bằng debug giờ sẽ **tự bật combat debug mode** để enemy di chuyển ngay.
- Bổ sung phần hiển thị **current target** rõ hơn cho building đang hover/chọn, gồm cả thông tin ammo của tower liên kết.
- Thêm **click-to-lock building target** và `Clear Lock`, để debug actions có thể bám vào một building cụ thể thay vì chỉ phụ thuộc hover TTL.
- Phần chọn loại enemy trong debug UI giờ dùng **preset list** thay vì nhập text tự do.

### Checklist / tài liệu stabilization
- `docs/stabilization-checklist.md` được cập nhật để phản ánh regression coverage đã có sẵn trong repo.
- Phần Jobs smoke test được mở rộng với ghi chú pass/fail thực tế hơn.
- Wording của RunStart trong checklist được chuẩn hóa để khớp với tên invariant trong code.
- Các mục Jobs smoke test đã verify trong ngày được tick hoàn thành.

### Ghi chú
- Trọng tâm hiện tại là stabilization thực dụng, chưa phải cleanup/refactor diện rộng.
- Ở thời điểm pass này, save/load stabilization và các smoke pass còn lại của Build / RunStart vẫn còn pending.
