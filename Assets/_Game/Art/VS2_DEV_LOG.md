# Seasonal Bastion — VS#2 Dev Log (Nhật ký triển khai)

> File này dùng để ghi lại tiến độ theo ngày (Day 15, Day 16, …) khi triển khai **Vertical Slice #2** dựa trên nền VS1 (Day 1–14).
>
> Quy ước:
> - Mỗi Day gồm: **Mục tiêu**, **Đã làm**, **Kết quả/Acceptance**, **Ghi chú/Pitfalls**, **Việc tiếp theo**.
> - Khi hoàn thành hạng mục, tick ✅.


**Phạm vi thư mục (trong repo Unity):** `Assets/_Game/…`
- File dev log canonical nên đặt tại: `Assets/_Game/Art/VS2_DEV_LOG.md`.
- Debug UI/tool nên đi qua **DebugHUDHub** (tránh rải hotkey gây chồng input), trừ khi spec nói khác.

---

## Day 15 — RunClockService (Option B) + Clock Events + Debug HUD

### Mục tiêu
- [x] Implement `RunClockService` theo Deliverable B (Option B):
  - [x] **Dev (Spring+Summer): 180s/day**
  - [x] **Defend (Autumn+Winter): 120s/day**
  - [x] Calendar: **Spring 6**, **Summer 6**, **Autumn 4**, **Winter 4**
  - [x] Day rollover ổn: Day → Season → Year
- [x] Speed controls: **Pause/1x/2x/3x**
  - [x] Vào **Defend** tự set **1x** (vẫn cho Pause)
  - [x] Clamp 2x/3x trong Defend nếu chưa unlock (policy mặc định)
- [x] Phát event qua `IEventBus` để các hệ khác hook được:
  - [x] DayStart/DayEnd
  - [x] Season/Year/Phase changed
  - [x] TimeScale changed
- [x] Có Debug HUD hiển thị clock (Year/Season/Day/Phase + remaining seconds + speed buttons).

### Đã làm
- [v] **RunClockService**
  - Implement tick countdown theo `Time.unscaledDeltaTime` (không phụ thuộc timeScale để tránh drift khi pause/slow).
  - Rule:
    - Dev day length = 180s, Defend day length = 120s.
    - Season days: Spring 6, Summer 6, Autumn 4, Winter 4.
    - Rollover: hết seconds/day → `DayEnded` → tăng dayIndex → nếu vượt daysInSeason thì đổi season; nếu vượt Winter thì tăng year.
  - Speed:
    - Expose set speed 0/1/2/3.
    - Khi chuyển Phase sang Defend → auto set 1x (giữ đúng pacing).
    - Guard: nếu DefendSpeed chưa unlock thì clamp về 1x khi user cố set 2x/3x (Pause vẫn cho).
- [v] **Clock Events**
  - Bổ sung các event struct mới (typed) và publish tại các điểm chuyển:
    - DayStartedEvent / DayEndedEvent
    - SeasonDayChangedEvent (tick day index)
    - SeasonChangedEvent
    - YearChangedEvent
    - PhaseChangedEvent (Dev ↔ Defend)
    - TimeScaleChangedEvent
- [v] **DebugRunClockHUD**
  - Hiển thị:
    - `Year`, `Season`, `DayInSeason`, `Phase`, `TimeScale`, `RemainingSeconds`.
  - Buttons: `Pause (0x)`, `1x`, `2x`, `3x`.
  - Wiring vào **DebugHUDHub** (Home tab) để thống nhất debug UX.
- [v] **GameLoop**
  - Khi `StartNewRun(seed)`: reset clock bằng `RunClockService.Start(seed)` (đảm bảo year/season/day/timer ở trạng thái chuẩn).

### Kết quả / Acceptance
- [v] Play Mode chạy liên tục qua 2–3 ngày: timer giảm đúng, rollover ngày ổn định, không spam event.
- [v] Chuyển hết số ngày trong mùa → Season đổi đúng (Spring→Summer→Autumn→Winter) và Year tăng đúng khi qua Winter.
- [v] Speed buttons hoạt động:
  - Build phase: set 2x/3x OK.
  - Vào Defend: tự về 1x; nếu bấm 2x/3x thì bị clamp về 1x (Pause vẫn OK).
- [v] Debug HUD hiển thị đúng các giá trị và cập nhật real-time.

### Ghi chú / Pitfalls
- Dùng `Time.unscaledDeltaTime` cho countdown để clock không “đứng” khi pause/timeScale thay đổi; game simulation khác vẫn có thể dựa vào timeScale nếu cần.
- Event publish phải đảm bảo thứ tự ổn định (DayEnded → DayStarted, SeasonChanged trước DayStarted của season mới, …) để các hệ nghe event không lệch state.
- Nếu về sau muốn cho Defend > 1x, thêm flag/setting unlock rõ ràng (đừng nới rule ngầm).

### Việc tiếp theo (Day 16)
- StartNewRun spawn map theo **StartMapConfig 64x64**:
  - Clear/reset world & grid clean
  - Place roads/buildings/NPCs/initial storages/tower ammo đúng config
  - Chuẩn hoá “không bị về (0,0)” khi preview invalid (giữ ổn behavior VS1).
