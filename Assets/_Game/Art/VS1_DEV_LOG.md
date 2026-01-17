# Seasonal Bastion — VS#1 Dev Log (Nhật ký triển khai)

> File này dùng để ghi lại tiến độ theo ngày (Day 1, Day 2, …) khi triển khai **Vertical Slice #1**.
> 
> Quy ước:
> - Mỗi Day gồm: **Mục tiêu**, **Đã làm**, **Kết quả/Acceptance**, **Ghi chú/Pitfalls**, **Việc tiếp theo**.
> - Khi hoàn thành hạng mục, tick ✅.

---

## Day 1 — Boot Graph chạy được (GameBootstrap → GameLoop.Tick)

### Mục tiêu
- [x] Project chạy **Play Mode** không lỗi.
- [x] `GameLoop.Tick()` được gọi đều (heartbeat log **mỗi ~2s**, không spam).
- [x] Dựng boot graph tối thiểu: `GameBootstrap` tạo `GameServices` + `GameLoop`.

### Đã làm
- [v] Tạo/đặt `GameBootstrap` (MonoBehaviour entry) vào scene.
- [v] `GameServicesFactory.Create(_defsCatalog)` tạo container `GameServices`.
- [v] `GameLoop` được khởi tạo bằng `GameServices`.
- [v] (Tuỳ chọn) `StartNewRun(seed)` khi `_autoStartRun`.

### Kết quả / Acceptance
- [v] Vào Play Mode không NullReferenceException.
- [v] Console có heartbeat log mỗi ~2 giây xác nhận `Tick()` chạy.

### Ghi chú / Pitfalls
- Guard null trong `Update()`/`OnDestroy()` để tránh edge-case domain reload.
- Nên expose read-only `Services`/`Loop` trong `GameBootstrap` để debug HUD truy cập được.

### Việc tiếp theo (Day 2)
- Implement EventBus + NotificationService (max 3, newest-first, cooldown theo key).

---

## Day 2 — EventBus + NotificationService (Max 3, Newest-first, Cooldown)

### Mục tiêu
- [x] Có `EventBus` typed publish/subscribe.
- [x] Có `NotificationService`:
  - [x] **MaxVisible = 3**
  - [x] **Newest-first**
  - [x] **Cooldown per key** (chống spam)
  - [x] (Tuỳ chọn) **Dedupe by key** (update + move-to-top)
- [x] Có debug input (New Input System) để test nhanh.

### Đã làm
- [v] Thêm debug HUD dùng **New Input System** bằng `InputAction` runtime (không sửa InputActions asset).
  - `N`: push 5 notifications (test max3)
  - `M`: spam 1 key (test cooldown)
- [v] Cập nhật `GameBootstrap` expose `Services` để HUD đọc `Services.Notifications`.
- [v] Kiểm tra `NotificationService.cs`:
  - Trước: chỉ có max3 + newest-first.
  - Sau: đã bổ sung **cooldown theo key** + **dedupe** (theo key).

### Kết quả / Acceptance
- [v] Bấm `N` push 5 cái → UI chỉ hiển thị **3 cái mới nhất**.
- [v] Bấm `M` liên tục → trong cooldown chỉ nhận tối đa 1 lần (không spam).

### Ghi chú / Pitfalls
- Khi test max3, nên dùng `cooldownSeconds = 0` và `dedupeByKey = false` để đảm bảo tạo đủ 5 cái.
- Khi test cooldown, dùng cùng `key` và `cooldownSeconds = 3`.
- Cooldown dùng `Time.realtimeSinceStartup` để không bị ảnh hưởng bởi `timeScale`.

### Việc tiếp theo (Day 3)
- GridMap + Road placement orthogonal (N/E/S/W).

---

## Day 3 — (Chưa làm)
- [ ] GridMap occupancy
- [ ] Road placement tool orthogonal
- [ ] Debug input đặt road

---

## Day 4 — (Chưa làm)
- [ ] Building footprint + occupancy
- [ ] Placement validation

---

## Day 5 — (Chưa làm)
- [ ] Entry/Driveway len=1 rule + deterministic commit

