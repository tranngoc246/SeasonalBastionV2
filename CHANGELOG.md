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

### Ghi chú
- Hiện tại baseline manual/smoke đã khá ổn cho vòng stabilization đầu tiên.
- Đã khóa một mốc baseline ổn định cho batch stabilization ngày 2026-03-24.
- Những việc còn lại chủ yếu là:
  - invalid-config coverage cho RunStart
  - regression tests vòng 2
  - review boundary service / cleanup sâu hơn nếu tiếp tục refactor

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
