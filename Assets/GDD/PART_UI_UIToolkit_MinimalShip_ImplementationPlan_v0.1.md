# PART UI — UI TOOLKIT — MINIMAL SHIP UI IMPLEMENTATION PLAN — v0.1

> Mục tiêu PART UI: triển khai UI bằng **UI Toolkit** cho Seasonal Bastion (Unity 2022.3),
> theo hướng **tối thiểu để chơi được** nhưng **dễ mở rộng**, không over-engineer.
>
> UI bám vào architecture hiện có:
> - Scene có `GameBootstrap` tạo `GameServices`
> - Authority nằm ở services (RunClock/Placement/Storage/Unlock/Notifications/RunOutcome/SaveLoad…)
> - UI chỉ **render state + gửi command**, không chứa logic gameplay.

Không bao gồm trong PART UI v0.1:
- UI đẹp/polish (animation fancy, skinning phức tạp)
- Shop/Research/Perk tree lớn
- Drag-drop inventory, layout phức tạp

---

## 0) Quy ước “Definition of Done” cho UI v0.1

UI v0.1 Done khi:
1) Vào game có **HUD**: Year/Season/Day, Phase Build/Defend, Speed x1/x2/x3 hoạt động
2) Có **Resource Bar**: Wood/Stone/Food/Iron/Ammo (tổng kho) update đúng
3) Có **Notifications Stack**: max 3, newest first, throttle/cooldown hoạt động
4) Có **Build Panel**:
   - list building theo `UnlockService.IsUnlocked`
   - chọn building → vào placement mode → confirm/cancel
   - fail show reason (NoRoadConnection/Overlap/OutOfBounds…)
5) Có **Inspect/Selection panel** tối thiểu:
   - click building → show defId + HP (nếu có) + storage snapshot
6) Có **Season Summary modal** cuối mùa
7) Có **Run End (Victory/Defeat)** + **Main Menu** tối thiểu + Settings cơ bản
8) Không tạo vòng lặp asmdef: Core/Services không tham chiếu UI.

---

## 1) UI Architecture (LOCKED approach)

### 1.1 Nguyên tắc
- UI là "client": **không tự tính rule**, không sửa trực tiếp state data.
- UI giao tiếp thông qua:
  - đọc state từ services (poll nhẹ hoặc subscribe event)
  - gửi command qua methods service / controller MB (Placement controller)

### 1.2 Layers (1 UIDocument = 1 layer)
- `HUDDocument` (always-on): topbar, resources, noti, quick buttons
- `PanelDocument` (toggle): Build panel, Inspect panel
- `ModalDocument` (stack): SeasonSummary, RunEnd, Settings, Confirm dialogs

### 1.3 Update model
- Event-driven cho thứ “ít và quan trọng”:
  - DayStarted/PhaseChanged/TimeScaleChanged
  - NotificationsChanged
  - RunOutcomeChanged / RunEnded
- Poll interval (0.25–0.5s) cho “tổng resource” nếu chưa có event totals:
  - Storage totals / Ammo totals
  - Selected building storage snapshot

---

## 2) Folder layout + asmdef

### 2.1 Folder
Assets/_Game/UI/
  Runtime/
    Documents/
    UXML/
    USS/
    Scripts/
  Editor/ (optional)

### 2.2 asmdef
- `Game.UI.asmdef`:
  - reference: Core/Contracts asmdef (nơi chứa DTO/events/interfaces)
  - reference: Gameplay/Services asmdef (nơi chứa GameServices & services)
- Tuyệt đối: Gameplay/Services asmdef **không** reference Game.UI.

---

## 3) SPRINT UI-0 — UI Bootstrap + Wiring (Day 0–1)

### Day 0 — Setup UI Toolkit runtime
**Tasks**
1) Create `PanelSettings` (DefaultPanelSettings)
2) Create 3 UXML + USS base:
   - HUD.uxml / Theme.uss
   - PanelsRoot.uxml / Panels.uss
   - ModalsRoot.uxml / Modals.uss
3) Create prefabs:
   - `HUDDocument.prefab` (UIDocument + VisualTree HUD)
   - `PanelsDocument.prefab`
   - `ModalsDocument.prefab`
4) Implement `UiRoot` MB:
   - auto EnsureEventSystem
   - bind GameBootstrap.Services (late bind/retry)
   - create presenters (HUD/Noti)

**Acceptance**
- Play mode không lỗi
- Console có log: `[UiRoot] Bound to GameServices successfully.`
- HUD hiển thị layout (tạm text placeholder)

---

## 4) SPRINT UI-1 — Time/Phase/Speed HUD (Day 1)

### Day 1 — Clock HUD
**Tasks**
1) HUD elements:
   - Label: Year/Season/Day
   - Chip: Phase Build/Defend
   - Buttons: x1 x2 x3
2) Bind events:
   - DayStartedEvent → update Year/Season/Day + Phase
   - PhaseChangedEvent → update chip + speed availability
   - TimeScaleChangedEvent → highlight x1/x2/x3
3) Clamp rules:
   - nếu DefendSpeedUnlocked=false, disable x2/x3 trong defend

**Acceptance**
- Vào game thấy ngay `Year 1 • Spring D1` (không “Year ?”)
- Click x2/x3 → simulation chạy nhanh thật + log debug (tạm)
- Chuyển defend: x2/x3 bị disable (nếu chưa unlock)

---

## 5) SPRINT UI-2 — Notifications Stack (Day 2)

### Day 2 — Noti rendering + dismiss
**Tasks**
1) Create NotificationItem.uxml template
2) Presenter subscribe `NotificationService.NotificationsChanged`
3) Render:
   - max 3 items
   - newest first
   - severity style class (info/warn/error)
4) Click to dismiss (optional)

**Acceptance**
- Spam push 5 noti → UI chỉ hiện 3 cái mới nhất
- Cooldown/throttle hoạt động (không bùng)
- Click dismiss item biến mất

---

## 6) SPRINT UI-3 — Resource Bar + Selected Inspect (Day 3–4)

### Day 3 — Resource bar (tổng kho)
**Tasks**
1) HUD resource bar elements:
   - Wood/Stone/Food/Iron/Ammo
2) Update strategy:
   - Poll 0.25–0.5s: `StorageService.GetTotal(type)`
   - (Nếu có event) dùng event để update tức thời
3) Minimal formatting:
   - `Wood: 120`…

**Acceptance**
- Khi NPC haul deliver, số tăng đúng
- Ammo chỉ tăng khi deliver đúng pipeline (nếu ammo đã có)

### Day 4 — Inspect panel tối thiểu
**Tasks**
1) PanelsRoot: add Inspect panel (toggle)
2) Input:
   - click building in world → set SelectedBuildingId (controller MB)
3) UI show:
   - defId / buildingId
   - HP (nếu có)
   - storage snapshot (per resource)
4) Poll update khi selected building tồn tại

**Acceptance**
- Click HQ/Warehouse/Producer thấy info đúng
- Không spam GC alloc mỗi frame (không string concat liên tục trong Update)

---

## 7) SPRINT UI-4 — Build Panel + Placement Controller (Day 5–7)

> Đây là bước biến prototype thành game (thay debug tool).

### Day 5 — Build list theo unlock
**Tasks**
1) BuildPanel.uxml:
   - button Build (open/close)
   - list container + search (optional)
2) Data:
   - DataRegistry list defs
   - filter: `UnlockService.IsUnlocked(defId)`
3) OnClick item → set `SelectedBuildDefId` (UI state)

**Acceptance**
- List chỉ hiện defs unlocked
- Click item đổi trạng thái “selected”

### Day 6 — Placement mode (preview + validate)
**Tasks**
1) `BuildPlacementController` MB:
   - receive selected def
   - read mouse cell → `PlacementService.ValidateBuilding(...)`
   - preview: ok/fail
2) UI feedback:
   - if fail: show reason text (small)
   - push notification throttle khi confirm fail
3) Controls:
   - LMB confirm
   - RMB cancel
   - Q/E rotate (nếu rotation có)

**Acceptance**
- Place building thành công khi rule đúng
- Fail do NoRoadConnection/Overlap/OutOfBounds hiển thị rõ
- Không commit khi invalid

### Day 7 — Commit + Cancel + clean state
**Tasks**
1) Confirm commit gọi PlacementService.Commit (hoặc PlaceBuilding)
2) Sau commit:
   - clear selection / exit placement
   - refresh world index (nếu cần)
3) Cancel:
   - reset preview state
   - no claims leak, no leftover gizmos

**Acceptance**
- Có thể build liên tục 5–10 building không lỗi
- Không softlock do placement state “kẹt”

---

## 8) SPRINT UI-5 — Season Summary Modal (Day 8)

### Day 8 — Summary cuối mùa
**Tasks**
1) SeasonSummary.uxml modal
2) Bind:
   - Event: SeasonEnded/SeasonSummaryReady (hoặc service query)
3) Display:
   - built/upgraded/repair counts
   - gained/spent
   - damage/hits (nếu có)
4) Button Continue:
   - close modal
   - resume timescale (x1)

**Acceptance**
- Cuối mùa popup summary
- Continue → gameplay tiếp tục bình thường

---

## 9) SPRINT UI-6 — Run End + Main Menu + Settings (Day 9–10)

### Day 9 — Run End (Victory/Defeat)
**Tasks**
1) RunEnd.uxml modal
2) Bind:
   - RunOutcomeService / RunEndedEvent
3) Buttons:
   - Start New Run
   - Load Latest (optional)
   - Back to Main Menu

**Acceptance**
- Victory cuối Winter Y2 → hiện Victory modal
- HQ dead → Defeat modal

### Day 10 — Main Menu tối thiểu + Settings
**Tasks**
1) MainMenu.uxml scene or modal:
   - Start New Run
   - Continue (load latest)
   - Settings
   - Quit
2) Settings:
   - Master volume (stub ok)
   - Speed default (x1)
   - Save persist (PlayerPrefs/SettingsService)

**Acceptance**
- Có flow đầy đủ: menu → run → end → menu
- Settings lưu lại sau restart

---

## 10) SPRINT UI-7 — Polish tối thiểu + Performance (Day 11)

### Day 11 — Hardening
**Tasks**
1) Reduce GC alloc:
   - cache label refs
   - avoid string concat per-frame (use event/poll interval)
2) Add toggles:
   - show/hide Debug overlay
   - show grid (nếu có)
3) Visual consistency:
   - spacing tokens (Theme.uss)
   - consistent button states

**Acceptance**
- Profiler: UI không alloc mỗi frame ở idle
- 30 phút không leak VisualElement (không rebuild list vô hạn)

---

## 11) Debug/Dev tools tối thiểu (không bắt buộc nhưng rất hữu ích)
- Toggle “UI Debug”:
  - show selected cell
  - show placement validation reason
- Button “Push test notification”
- Button “Give resources” (dev only)

---

## 12) Common pitfalls checklist (để khỏi tốn 2 ngày)

- UI bind sau khi service publish event đầu tiên → phải paint initial state từ service
- Dùng lambda inline cho `Button.clicked` → Unbind không remove được → leak
- Rebuild notification list mỗi frame → chỉ rebuild khi NotificationsChanged
- Resource update mỗi frame → poll interval 0.25–0.5s là đủ
- Core/Services reference UI → tạo vòng lặp asmdef (KHÔNG)
- Placement controller giữ state sau cancel → kẹt preview

---

## 13) What you demo (UI v0.1)
- Vào game thấy HUD time/phase/speed + resources + notifications
- Mở build panel, chọn building, đặt theo rule road/entry
- Click building xem inspect
- End season có summary
- Win/lose có run end modal + quay về menu
