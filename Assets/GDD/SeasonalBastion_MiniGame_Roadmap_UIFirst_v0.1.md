# Seasonal Bastion — MINI GAME SHIP ROADMAP (UI-first, no Debug) — v0.1

> Mục tiêu: **mini game end-to-end**: Menu → New Game/Continue → HUD/Noti → xây/đặt công trình → NPC (build/haul/khai thác) → enemy spawn/move → combat + resupply → Win/Lose → quay về Menu.
>
> Ràng buộc: Unity 2022.3 LTS, ưu tiên MonoBehaviour, grid logic deterministic, UI Toolkit; **CHỈ sử dụng New Input System** (không legacy Input Manager/StandaloneInputModule); **bỏ qua toàn bộ Debug/dev tools**.

---

## 0) Definition of Done (Ship Criteria)

### 0.1 App Flow
- Main Menu: New Game / Continue / Settings / Quit.
- New Game: tạo run theo StartMapConfig 64x64.
- Continue: load run_save.json nếu tồn tại; nếu không thì thông báo.
- Run End: Victory/Defeat modal → quay về Menu.

### 0.15 Input (New Input System only)
- Project Settings: **Active Input Handling = Input System Package (New)** (không “Both”, không “Old”).
- Runtime/UI: EventSystem phải dùng **InputSystemUIInputModule**; **không được** fallback sang StandaloneInputModule.
- Mọi input gameplay (placement/selection/camera/pause) dùng `UnityEngine.InputSystem` (Mouse/Keyboard/ActionMap) — tuyệt đối không dùng `UnityEngine.Input` / `Input.GetKey*` / `GetMouseButton*`.
- Nếu thiếu module/setting → hiển thị notification lỗi cấu hình và dừng vào run (tránh ship build lệch input).

### 0.2 Gameplay loop tối thiểu
- Clock/Season/Phase chạy đúng, có x1/x2/x3.
- Build/Placement: chọn def từ Build Panel → preview/validate → confirm/cancel.
- NPC: tối thiểu Builder + Hauler; có khai thác (forest/farm) + haul về storage.
- Enemy/Combat: spawn → move deterministic đến HQ; tower bắn tiêu hao ammo.
- Outcome: Defeat khi HQ chết; Victory theo rule tối thiểu (khuyến nghị rút gọn 1 defend season để demo nhanh).
- Save/Load: save thủ công + continue từ menu.

---

## 1) Hiện trạng dự án (từ _Game.zip, bỏ qua Debug)

### 1.1 Đã có (tận dụng)
- GameBootstrap: tạo GameServices + GameLoop; có thể StartNewRun từ StartMapConfig Resources.
- SaveService: serialize buildings/sites/npcs/towers/enemies + roads vào run_save.json.
- UI Runtime: UiRoot + presenters HUD/Notifications/ResourceBar/Inspect.
- RunStart config 64x64: roads/spawnGates/zones/initialBuildings.

### 1.2 Thiếu để ship đúng "mini game"
- Chưa có Main Menu / Settings / Run End / Season Summary bằng UI Toolkit.
- Chưa có Build Panel + Placement Controller để thay thế Debug tools.
- App flow chưa menu-driven (đang có auto-start run).

---

## 2) Lộ trình mới (Milestones) — đủ để ship Mini Game

### M0 — Menu-driven boot

**Input requirement:** kiểm tra & enforce New Input System trước khi cho UI vào gameplay (Active Input Handling, EventSystem module).
**Mục tiêu:** vào menu trước, UI quyết định New/Continue.
- Tắt auto-run mặc định trong GameBootstrap.
- Tạo GameAppController (MonoBehaviour) làm “router”: StartNewRun / ContinueLatest / BackToMenu.
- Chốt flow scene: khuyến nghị 2 scenes (MainMenu, Game).
**Acceptance:** build chạy vào menu; bấm New/Continue hoạt động.

### M1 — UI Toolkit foundation theo PART_UI

**Input requirement:** UIDocument/Pointer interaction phải chạy với `InputSystemUIInputModule` (không StandaloneInputModule).
**Mục tiêu:** chuẩn hóa 3 layer UI: HUD / Panels / Modals.
- Prefab UIDocument: HUD, Panels, Modals + PanelSettings.
- UiRoot bind GameServices; giữ nguyên presenters hiện có.
- Nền notification stack + resource bar polling interval.
**Acceptance:** vào game có HUD + noti + resources + inspect; không chặn click world khi không mở panel.

### M2 — Main Menu + Settings + Run End
**Mục tiêu:** đóng vòng menu → run → end → menu.
- MainMenuScreen (New/Continue/Settings/Quit).
- Settings modal (tối thiểu: volume stub + default speed).
- RunEnd modal (Victory/Defeat) + actions quay menu.
**Acceptance:** end-to-end flow chạy được.

### M3 — Build Panel + Placement Mode (thay Debug)
**Mục tiêu:** người chơi xây bằng UI chính.
- BuildPanel: list defs (lọc theo UnlockService nếu có), chọn def.
- BuildPlacementController: preview/validate/rotate/confirm/cancel.
- Placement commit tạo Site/Building theo kiến trúc hiện có.
**Acceptance:** đặt được 5–10 công trình liên tiếp; reason invalid hiển thị rõ.

### M4 — NPC loops: build/haul/harvest
**Mục tiêu:** có kinh tế tối thiểu.
- Runtime zones (forest/farm) → tạo harvest jobs.
- Hauler pickup/deliver về storage (HQ/Warehouse).
- Site delivery + build work seconds → complete.
**Acceptance:** resource tăng theo thời gian; công trình hoàn thiện qua site pipeline.

### M5 — Enemy spawn/move + Combat trong Defend
**Mục tiêu:** defend phase có combat thật.
- Spawn từ spawnGates theo lane.
- Movement deterministic tới HQ.
- Tower bắn tiêu hao ammo; enemy đánh HQ/building.
- RunOutcomeService: defeat khi HQ hp=0; victory rule demo.
**Acceptance:** có giao tranh; win/lose trigger đúng.

### M6 — Transport/Resupply Ammo
**Mục tiêu:** đóng vòng ammo economy.
- Tower low ammo → enqueue resupply request.
- Armory-role NPC pickup ammo ở Armory → deliver tower (clamp theo cap).
- Cooldown/dedupe để tránh spam job.
**Acceptance:** tower hết ammo sẽ được resupply, combat hồi phục.

### M7 — Save/Load shipable + regression
**Mục tiêu:** continue ổn định, không crash/softlock.
- UI nút Save (pause/settings) + 1 auto-save checkpoint (end day/season).
- Continue từ menu → load state đúng.
- Regression: StartNewRun×3, Save/Load×2 checkpoint, Defeat path, Victory path, 30 phút không softlock.

---

## 3) Mapping nhanh với PART_UI
- M1: HUD/Noti/Resource/Inspect nền tảng.
- M2: Menu/Settings/RunEnd.
- M3: BuildPanel + Placement.
- M7: harden UI + save/load flows.

---

## 4) Điểm cần bổ sung (Gap List)

### 4.1 UI/Flow
- Enforce **New Input System only** (Active Input Handling + EventSystem dùng InputSystemUIInputModule, không fallback legacy).
- Menu + Settings persist.
- RunEnd modal + (nếu theo GDD) Season Summary.
- BuildPanel + PlacementController.

### 4.2 Gameplay
- Harvest zone runtime + job chain.
- Enemy spawn/move/combat ổn định.
- Resupply ammo end-to-end.
- Rule thắng thống nhất (demo rút gọn hoặc full Winter Y2).

### 4.3 Data/Content
- Đủ defs tối thiểu: HQ/House/Farmhouse/Lumbercamp/Armory/Warehouse/Tower + 1–2 enemy.
- Costs/production/carry/caps tối thiểu để loop không “kẹt”.

---

## 5) Thứ tự triển khai khuyến nghị
1) M0 → M2 để đóng loop menu-run-end.
2) M3 để bỏ hoàn toàn debug.
3) M4 → M6 để có gameplay đầy đủ.
4) M7 harden + regression.

## 6) File/Folder dự kiến (không chạm Debug)
- Assets/_Game/App/
- Assets/_Game/UI/Runtime/Documents/
- Assets/_Game/UI/Runtime/UXML/
- Assets/_Game/UI/Runtime/USS/
- Assets/_Game/UI/Runtime/Scripts/

(End)
