# UI_DEV_LOG — TEMPLATE — Seasonal Bastion — UI Toolkit — v0.1

> Dùng file này để ghi dev log UI theo format giống VS2/VS3.
> Mỗi ngày = 1 entry, ghi rõ mục tiêu, thay đổi file, test, known issues.
> Khuyến nghị: ghi ngay sau khi bạn commit.

---

## Header
- Project: Seasonal Bastion
- Unity: 2022.3 LTS
- UI Tech: UI Toolkit
- Scope: v0.1 minimal ship UI
- Owner: (your name)
- Branch: (optional)
- Date range: (optional)

---

## Day XX — <Title ngắn>
**Date:** YYYY-MM-DD  
**Goal (Mục tiêu)**
- (1) ...
- (2) ...

**Scope**
- In: ...
- Out: ...

### Work log (Chi tiết triển khai)
- [ ] Task 1: ...
  - Notes: ...
- [ ] Task 2: ...
  - Notes: ...

### Files changed (để dễ trace)
> Ghi theo dạng: `path` — mô tả thay đổi 1–2 dòng
- `Assets/_Game/UI/...` — ...
- `Assets/_Game/...` — ...

### UI hierarchy / Assets created
- UXML:
  - `...`
- USS:
  - `...`
- Prefabs / PanelSettings:
  - `...`
- Scripts:
  - `...`

### Integration points (Services/Events)
- Read state from:
  - `IRunClock`: (fields used)
  - `INotificationService`: (fields used)
  - `IStorageService`: (fields used)
- Events subscribed:
  - `DayStartedEvent`
  - `PhaseChangedEvent`
  - `TimeScaleChangedEvent`
  - `NotificationsChanged`
- Commands issued:
  - `RunClock.SetTimeScale(...)`
  - `NotificationService.Dismiss(...)`
  - (Placement later) `PlacementService.Validate/Commit`

### Testing (Acceptance)
**Manual tests**
- [ ] StartNewRun → HUD visible (time/phase/speed)
- [ ] Click x2/x3 → simulation speed changes + UI highlights correct button
- [ ] Notifications: push 5 → only 3 visible; dismiss works
- [ ] (If resource bar) deliver resources → totals update within 0.5s
- [ ] No errors in Console

**Performance / GC**
- [ ] Profiler: no per-frame alloc from UI idle
- Notes: ...

### Known issues / TODO
- (Issue) ...
  - Repro:
  - Suspected cause:
  - Fix plan:
- (TODO) ...

### Notes / Decisions
- Decision made today:
  - ...
- Rationale:
  - ...
- Follow-up:
  - ...

---

## Quick checklist (để không quên mấy lỗi hay gặp)
- [ ] UiRoot bind OK (late-bind, GameBootstrap reference optional)
- [ ] Initial paint không phụ thuộc event đầu tiên (không “Year ?”)
- [ ] Unbind sạch (không lambda inline, không leak)
- [ ] Notifications rebuild only on change (không rebuild per-frame)
- [ ] Poll interval cho totals (0.25–0.5s), tránh update per-frame
- [ ] No asmdef cycles (Core/Services không reference UI)

---

## Release notes UI (khi gần ship)
**Version:** v0.1.x  
**Added**
- ...
**Changed**
- ...
**Fixed**
- ...
**Known issues**
- ...

---

## Day 1 — UI-1: HUD Time/Phase/Speed (UI Toolkit)

**Date:** 2026-02-01  
**Goal (Mục tiêu)**
- (1) Có HUD hiển thị **Year/Season/Day** + **Phase (Build/Defend)**
- (2) Nút **x1/x2/x3** hoạt động thật (đổi simulation speed)
- (3) Không phụ thuộc event đầu tiên (vào game phải “paint” đúng ngay)

**Scope**
- In: HUD topbar + bind RunClock/Events + speed highlight/disable rule
- Out: Resource bar / Build panel / Modals

### Work log (Chi tiết triển khai)
- [x] Tạo HUD UXML/USS tối thiểu (topbar + buttons)
  - Notes: layout gọn, dùng class `is-active`, phase class `is-build/is-defend`.
- [x] Implement `UiRoot` bind `GameBootstrap.Services` (late-bind) + ensure EventSystem
  - Notes: fix lỗi “GameServices missing” bằng retry + hỗ trợ kéo reference bootstrap trong inspector.
- [x] Implement `HudPresenter`:
  - Subscribe: `DayStartedEvent`, `PhaseChangedEvent`, `TimeScaleChangedEvent`
  - Initial paint: đọc state từ `IRunClock` ngay khi bind (không chờ event)
  - Speed: click x1/x2/x3 → `RunClock.SetTimeScale()`
  - Defend rule: nếu `DefendSpeedUnlocked=false` thì disable x2/x3 trong defend + clamp về x1
  - Notes: tránh leak bằng cách lưu delegate click để Unbind clean.

### Files changed (để dễ trace)
- `Assets/_Game/UI/Runtime/Scripts/UiRoot.cs`
  - Late-bind GameBootstrap/GameServices + retry + EnsureEventSystem
- `Assets/_Game/UI/Runtime/Scripts/HudPresenter.cs`
  - Bind events + initial paint + speed click logic + defend speed gating
- `Assets/_Game/UI/Runtime/UXML/HUD.uxml`
  - Topbar: LblTime, LblPhase, BtnSpeed1/2/3
- `Assets/_Game/UI/Runtime/USS/Theme.uss`
  - Style topbar + chip phase + speed active state

### UI hierarchy / Assets created
- UXML:
  - `HUD.uxml`
- USS:
  - `Theme.uss`
- Prefabs / PanelSettings:
  - `DefaultPanelSettings.asset` (nếu chưa có)
  - `HUDDocument` (UIDocument dùng HUD.uxml) (nếu bạn đã tạo prefab)

### Integration points (Services/Events)
- Read state from:
  - `IRunClock`: `CurrentSeason`, `DayIndex`, `CurrentPhase`, `TimeScale`, `DefendSpeedUnlocked`
- Events subscribed:
  - `DayStartedEvent` → update Year/Season/Day + Phase
  - `PhaseChangedEvent` → update chip + enable/disable speed
  - `TimeScaleChangedEvent` → highlight speed button
- Commands issued:
  - `IRunClock.SetTimeScale(float)`

### Testing (Acceptance)
**Manual tests**
- [v] StartNewRun → HUD hiện ngay: `Year 1 • Spring D1` (không còn “Year ?”)
- [v] Click x2/x3 (Build phase) → simulation chạy nhanh + button highlight đúng
- [v] Khi vào Defend và `DefendSpeedUnlocked=false` → x2/x3 bị disable + clamp về x1
- [v] Không còn log lỗi `[UiRoot] GameServices missing...`

**Performance / GC**
- [v] Idle HUD không update per-frame (update theo event; chỉ Paint khi có event/click)
- Notes: vẫn cần check profiler sau khi thêm Resource Bar/Build Panel

### Known issues / TODO
- (TODO) Thêm Resource Bar (poll interval 0.25–0.5s) — Sprint UI-3 Day 3
- (TODO) Notifications Stack — Sprint UI-2 Day 2
- (TODO) Build Panel + Placement Controller — Sprint UI-4 Day 5–7

### Notes / Decisions
- Decision: UI bind theo **services + event bus**, UI không giữ gameplay logic.
- Decision: YearIndex không nằm trong interface → hiển thị Year qua `DayStartedEvent` + initial paint từ RunClock (fallback Year 1).
- Follow-up: khi cần Year chuẩn ngay lập tức ở mọi scene, cân nhắc expose YearIndex trong RunClock DTO/event (không vội ở v0.1).

---

## Day 2 — UI-2: Notifications Stack (Render + Dismiss)

**Date:** 2026-02-01  
**Goal (Mục tiêu)**
- (1) Render notifications trong HUD: **max 3**, **newest first** (theo thứ tự service trả về)
- (2) Presenter subscribe `NotificationService.NotificationsChanged` (không rebuild per-frame)
- (3) Severity style class (info/warn/error)
- (4) Click để dismiss (optional nhưng bật trong v0.1 để test nhanh)

**Scope**
- In: Noti stack HUD + item template + presenter bind service + dismiss
- Out: Resource bar / Build panel / Modals

### Work log (Chi tiết triển khai)
- [x] Tạo template `NotificationItem.uxml` (LblTitle/LblBody)
  - Notes: đồng bộ name query trong presenter (`LblTitle`, `LblBody`).
- [x] Bổ sung container `NotiStack` trong `HUD.uxml`
  - Notes: đặt top-right dưới topbar để không che thông tin clock/speed.
- [x] Update `Theme.uss` cho noti:
  - `.noti-stack`, `.noti-item`, `.sev-info/.sev-warning/.sev-error`, `.noti-title/.noti-body`
- [x] Implement `NotificationStackPresenter`:
  - Subscribe `NotificationService.NotificationsChanged` → `Rebuild()`
  - `Rebuild()` render tối đa 3 item (foreach + break)
  - Apply class theo `NotificationSeverity`
  - Click item → `Dismiss(vm.Id)` (NotificationId type)
  - Notes: Fix compile mismatch type (`NotificationViewModel`, `NotificationId`), không so sánh Id.

### Files changed (để dễ trace)
- `Assets/_Game/UI/Runtime/UXML/NotificationItem.uxml`
  - Template notification item (title/body)
- `Assets/_Game/UI/Runtime/UXML/HUD.uxml`
  - Add `VisualElement name="NotiStack" class="noti-stack"`
- `Assets/_Game/UI/Runtime/USS/Theme.uss`
  - Add noti layout + severity classes
- `Assets/_Game/UI/Runtime/Scripts/NotificationStackPresenter.cs`
  - Subscribe NotificationsChanged + render max 3 + dismiss
- `Assets/_Game/UI/Runtime/Scripts/UiRoot.cs`
  - Wire `_notificationItemTemplate` + create/bind `_notiPresenter` (nếu trước đó chưa)

### UI hierarchy / Assets created
- UXML:
  - `HUD.uxml` (NotiStack container)
  - `NotificationItem.uxml` (item template)
- USS:
  - `Theme.uss` (noti styles)
- Scripts:
  - `NotificationStackPresenter.cs`
- Inspector wiring:
  - `UiRoot._notificationItemTemplate` → assign `NotificationItem.uxml`

### Integration points (Services/Events)
- Read state from:
  - `INotificationService.GetVisible()` → trả danh sách notifications đã qua policy (cooldown/throttle)
- Events subscribed:
  - `NotificationService.NotificationsChanged` → trigger UI rebuild
- Commands issued:
  - `NotificationService.Dismiss(NotificationId id)` (click dismiss)

### Testing (Acceptance)
**Manual tests**
- [v] Spam push 5 noti → UI chỉ hiện 3 cái (max 3)
- [v] Thứ tự hiển thị đúng “newest first” (theo thứ tự `GetVisible()` trả về)
- [v] Cooldown/throttle không bùng (UI chỉ render `GetVisible()` nên tuân policy service)
- [v] Click dismiss item → item biến mất ngay (Dismiss hoạt động)
- [v] Không rebuild per-frame (chỉ rebuild khi NotificationsChanged)

**Performance / GC**
- [v] Không dùng LINQ; rebuild chỉ khi change; không alloc per-frame ở idle.

### Known issues / TODO
- (TODO) UI-3 Day 3: Resource Bar (poll interval 0.25–0.5s) + formatting
- (TODO) UI-4 Day 5–7: Build Panel + Placement Controller (thay debug tool)
- (TODO) Optional: noti auto-expire animation (out of scope v0.1)

### Notes / Decisions
- Decision: không suy luận order bằng Id (vì `NotificationId` không comparable); tin vào `GetVisible()` order (spec service).
- Decision: giới hạn max 3 tại UI để đảm bảo đúng UX ngay cả khi service trả nhiều item.

---

## Day 3 — UI-3: Resource Bar polish + Pause button + Bind stability fixes

**Date:** 2026-02-01  
**Goal (Mục tiêu)**
- (1) Chỉnh HUD layout gọn hơn: topbar rõ ràng, resource bar dễ nhìn
- (2) Thêm nút **Pause/Resume** (0x ↔ speed trước đó)
- (3) Fix lỗi **click bị chạy 2 lần** do bind/retry (UiRoot)
- (4) Dọn cảnh báo USS: sửa property font style đúng chuẩn UI Toolkit

**Scope**
- In: HUD layout (UXML/USS), HudPresenter (pause + paint), UiRoot (bind retry safety), cleanup USS warnings
- Out: Gameplay pause sâu (đã dựa vào RunClockService timeScale=0)

### Work log (Chi tiết triển khai)
- [x] HUD layout polish
  - Căn lại topbar/speed group/resources/noti stack cho hợp lý, giảm cảm giác “2 tầng” rời rạc.
- [x] Add Pause button (UI Toolkit)
  - Thêm `BtnPause` vào `SpeedGroup`.
  - Style `.pause-btn` + `.pause-btn.is-active`.
  - HudPresenter: toggle pause/resume bằng `RunClock.SetTimeScale(0)` + resume về speed trước đó (x1/x2/x3).
  - Khi paused: không highlight x1/x2/x3.
- [x] Fix click double-trigger (root cause)
  - Nguyên nhân: `UiRoot` retry/bind có thể gọi `TryBind()` nhiều lần sau `yield`, khiến presenter `Bind()` bị chạy 2 lần → button click handler bị subscribe 2 lần.
  - Sửa `UiRoot`:
    - Re-check `_s` ngay sau `yield return null`
    - Stop/clear coroutine khi bind thành công
    - Guard trong `TryBind()` để không bind lại khi `_s` đã set
    - (Optional) Unbind presenters khi `OnDisable`/`OnDestroy` để tránh lingering
- [x] Fix USS warnings
  - Thay `unity-font-style` → `-unity-font-style` để đúng property của UI Toolkit (xóa 5 cảnh báo console).

### Files changed (để dễ trace)
- `Assets/_Game/UI/Runtime/UXML/HUD.uxml`
  - Add `BtnPause` (trong SpeedGroup)
  - Adjust layout/anchors cho HUD (topbar/resources/noti)
- `Assets/_Game/UI/Runtime/USS/Theme.uss`
  - Add `.pause-btn` styles
  - Fix warnings: `unity-font-style` → `-unity-font-style`
  - (Có thể) tinh chỉnh spacing/height cho topbar/resources
- `Assets/_Game/UI/Runtime/Scripts/HudPresenter.cs`
  - Query + bind `BtnPause`
  - TogglePause(): pause/resume + paint state
  - PaintSpeed(): paused -> no active speed highlight
- `Assets/_Game/UI/Runtime/Scripts/UiRoot.cs`
  - Fix retry/bind stability: tránh Bind() nhiều lần gây duplicate click handler

### Integration points (Services/Events)
- Commands:
  - `IRunClock.SetTimeScale(0f)` để pause
  - `IRunClock.SetTimeScale(prevScale)` để resume
- Events:
  - `TimeScaleChangedEvent` → update speed highlight + pause highlight
  - `DayStartedEvent` / `PhaseChangedEvent` → refresh time/phase UI

### Testing (Acceptance)
- [v] Click Pause 1 lần → **chỉ 1 action** (không còn log/trigger 2 lần)
- [v] Pause → Year/Day dừng tiến (RunClockService tick early-out khi timescale=0)
- [v] Resume → quay lại đúng speed trước đó
- [v] Click x2/x3 → mỗi click chỉ chạy 1 lần (không duplicate logs)
- [v] Console sạch: hết warning `unity-font-style`

### Notes / Decisions
- Pause dựa trên cơ chế có sẵn của `RunClockService` (TimeScale=0) để tối thiểu thay đổi, không thêm interface mới.
- Ưu tiên fix “root cause” (UiRoot bind/retry) thay vì chỉ chặn double-bind ở presenter.
