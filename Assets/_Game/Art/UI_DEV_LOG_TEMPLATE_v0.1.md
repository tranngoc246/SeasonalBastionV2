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
