# Checklist bug can fix

> Tài liệu tổng hợp các bug/rủi ro kỹ thuật ưu tiên xử lý, phục vụ theo dõi tiến độ hardening và stabilization.

## Thông tin chung
- **Repo:** `SeasonalBastionV2`
- **Mục tiêu:** Checklist bug cần fix
- **Mức ưu tiên:** P0 → P2
- **Ngày tạo file gốc:** 2026-03-30

**Ghi chú sử dụng:** Đánh dấu khi bug đã được sửa, test lại, và không còn tái xuất hiện trong regression test.

---

## P0, fix ngay

### Save/Load chưa clear hết runtime state trước khi apply
- **File / vùng liên quan:** `SaveLoadApplier`, World State, runtime caches
- **Checklist fix:**
  - rà toàn bộ World State và service runtime để liệt kê hết store/cache đang tồn tại
  - thêm clear/reset cho resource zones, patches, piles, inspect selection, overlays hoặc runtime cache liên quan
  - viết regression test: save giữa lúc NPC đang harvest, load lại không bị duplicate state hoặc stale target
- **Trạng thái:** chưa làm / đang làm / đã fix + test lại

### Rebuild runtime cache sau load chưa được khóa chắc
- **File / vùng liên quan:** `SaveLoadApplier`, RunStart runtime, validation
- **Checklist fix:**
  - bỏ phụ thuộc reflection nếu có thể, hoặc ít nhất log rõ khi rebuild thất bại
  - tạo integration test chạy với `StartMapConfig` thật, không dùng test bị `Ignore`
  - sau load cần assert lại: lanes, spawn gates, HQ target cells và wave routing đều hợp lệ
- **Trạng thái:** chưa làm / đang làm / đã fix + test lại

### SaveMigrator mới là scaffold
- **File / vùng liên quan:** `SaveMigrator`, save schema, tests
- **Checklist fix:**
  - thêm migration theo version thay vì trả object gốc
  - log rõ quá trình migrate từ schema cũ sang schema mới
  - viết test cho save cũ thiếu field và save mới đầy đủ để kiểm tra backward compatibility
- **Trạng thái:** chưa làm / đang làm / đã fix + test lại

---

## P1, rất nên xử lý

### Reward system đã có nền effect thật, nhưng vẫn cần hardening thêm
- **File / vùng liên quan:** `RewardService`, `RunMods`, UI Reward
- **Checklist fix:**
  - rà lại các effect hiện có như build speed, ammo capacity, tower reload, NPC move speed để đảm bảo không chỉ là flow giả
  - cân nhắc mở rộng thêm effect thật nếu design yêu cầu như harvest yield, storage cap, tower damage
  - viết/siết test xác nhận chọn reward làm thay đổi gameplay state thật và sống qua save/load khi cần
- **Trạng thái:** đã có nền logic, còn cần hardening + test thêm

### WorldOps còn TODO ở create/destroy flow
- **File / vùng liên quan:** `WorldOps`, events, cleanup
- **Checklist fix:**
  - khi tạo building, cần fill đầy đủ state mặc định từ definition
  - khi destroy building, publish event để storage, jobs, UI và occupancy cleanup đồng bộ
  - viết test cho destroy building khi đang có assignment, claim hoặc job liên quan
- **Trạng thái:** chưa làm / đang làm / đã fix + test lại

### SaveLoadApplier đang rebuild metadata bằng reflection và có thể fail silent
- **File / vùng liên quan:** `SaveLoadApplier`, reflection, error handling
- **Checklist fix:**
  - không nuốt lỗi trống, ít nhất phải log warning/error cụ thể
  - nếu rebuild cache fail, save-load flow phải trả cảnh báo rõ
  - ưu tiên thay reflection bằng public integration entrypoint
- **Trạng thái:** chưa làm / đang làm / đã fix + test lại

---

## P1.5, khóa regression gameplay

### Chuỗi resource patch / harvest retargeting vừa thay đổi mạnh
- **File / vùng liên quan:** Harvest, resource patches, inspect UI
- **Checklist fix:**
  - test NPC đang đi tới patch A, patch A cạn tài nguyên thì retarget đúng sang patch B
  - test patch bị block path sau khi road/building thay đổi không làm NPC treo vĩnh viễn
  - test inspect panel không giữ stale reference sau khi patch cạn hoặc refresh
- **Trạng thái:** chưa làm / đang làm / đã fix + test lại

### Population / food upkeep đã có nền usable, nhưng vẫn cần test kỹ
- **File / vùng liên quan:** Population, food upkeep, HUD
- **Checklist fix:**
  - test chuyển ngày khi thiếu food không bị double-apply starvation
  - test save/load ngay trước và ngay sau daily tick
  - test HUD summary luôn đồng bộ với state sau load hoặc reset run
- **Trạng thái:** đã có nền logic + save/load cơ bản, còn cần hardening regression

---

## P2, debt nên lên kế hoạch

### Chưa có CI/CD khóa regression tự động
- **File / vùng liên quan:** GitHub Actions, tests, build validation
- **Checklist fix:**
  - thêm GitHub Actions chạy compile và EditMode tests tối thiểu
  - chặn merge khi pipeline đỏ
  - có smoke pipeline riêng cho Save/Load, Build/Jobs và RunStart core tests
- **Trạng thái:** chưa làm / đang làm / đã fix + test lại

### Thiếu integration scenario cho flow dài
- **File / vùng liên quan:** integration tests, gameplay loop, regression suite
- **Checklist fix:**
  - tạo integration fixture cho full run loop: New Run → Build → Harvest → Combat → Save → Load → Endgame
  - có scenario build active + queued haul + save/load
  - có scenario combat active + low ammo towers + save/load
- **Trạng thái:** chưa làm / đang làm / đã fix + test lại

---

## Đề xuất thứ tự thực hiện
1. Save/Load clear đủ state, rebuild runtime cache sau load, hoàn thiện `SaveMigrator`
2. Xử lý `RewardService`, `WorldOps` create/destroy flow, bỏ fail silent trong reflection
3. Khóa regression cho harvest retargeting, population/food upkeep và flow gameplay dài
4. Thiết lập CI/CD để không bị hồi quy khi commit nhanh

---

## Vai trò hiện tại
- **Phân loại đề xuất:** `docs/backlog/`
- **Vai trò:** bug backlog / hardening checklist
- **Lưu ý:** nên dùng bản markdown này thay cho `.docx` để dễ diff và review trong repo
