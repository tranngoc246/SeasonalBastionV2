# SEASONAL BASTION — WAVE 1 CODE MAP v1.0 (VN)

> Mục đích: Map các task của **M1 / Wave 1** sang codebase hiện tại để biết nên đọc/chạm file nào trước khi bắt đầu implementation.
> Phạm vi: Wave 1 backbone
> - `M1-A1` New Run flow tối thiểu
> - `M1-A2` Start map baseline hợp lệ
> - `M1-A3` Start package spawn đúng
> - `M1-A4` Season/day clock tối thiểu
> - `M1-G1` Top HUD tối thiểu

---

## 1. Tổng quan nhanh

Wave 1 của repo hiện tại **không phải bắt đầu từ số 0**.
Codebase đã có sẵn nhiều backbone quan trọng:
- app/game scene flow
- gameplay bootstrap
- run clock service
- run start pipeline
- HUD presenter + UXML

Việc chính của Wave 1 là:
- rà wiring hiện tại
- xác nhận flow thật sự chạy sạch
- chốt start data
- nối HUD với state thật
- loại bỏ các chỗ còn placeholder hoặc state drift

---

## 2. Bảng map: task ↔ file chính

| Task | File chính cần đụng | Vai trò |
|---|---|---|
| `M1-A1` New Run flow tối thiểu | `Assets/_Game/Core/Boot/GameAppController.cs` | App-level flow Menu → Game |
|  | `Assets/_Game/Core/Boot/GameBootstrap.cs` | Entry point cho `TryStartNewRun()` / `TryContinueLatest()` |
|  | `Assets/_Game/Core/Loop/GameLoop.cs` | Nơi `StartNewRun()` và tick loop thật sự chạy |
|  | `Assets/_Game/Core/Boot/GameServicesFactory.cs` | Tạo services sạch cho run mới |
| `M1-A2` Start map baseline hợp lệ | `Assets/_Game/Resources/RunStart/StartMapConfig_RunStart_64x64_v0.1.json` | Data map start chính |
|  | `Assets/_Game/Core/RunStart/RunStartFacade.cs` | Orchestrator apply config vào world |
|  | `Assets/_Game/Core/RunStart/RunStartWorldBuilder.cs` | Build roads/buildings vào world |
|  | `Assets/_Game/Core/RunStart/RunStartPlacementHelper.cs` | Hỗ trợ placement/relocation |
|  | `Assets/_Game/Core/RunStart/RunStartValidator.cs` | Validate runtime issues của map start |
| `M1-A3` Start package spawn đúng | `Assets/_Game/Resources/RunStart/StartMapConfig_RunStart_64x64_v0.1.json` | Chốt HQ/House/Farm/Lumber/Tower/NPC start package |
|  | `Assets/_Game/Core/RunStart/RunStartWorldBuilder.cs` | Spawn buildings/roads |
|  | `Assets/_Game/Core/RunStart/RunStartStorageInitializer.cs` | Seed starting storage |
|  | `Assets/_Game/Core/RunStart/RunStartTowerInitializer.cs` | Seed trạng thái tower/ammo start |
|  | `Assets/_Game/Core/RunStart/RunStartNpcSpawner.cs` | Spawn NPC start + workplace start |
|  | `Assets/_Game/Core/RunStart/RunStartHqResolver.cs` | HQ resolve nếu config/start flow liên quan |
| `M1-A4` Season/day clock tối thiểu | `Assets/_Game/Core/Loop/RunClockService.cs` | Source of truth cho Year/Season/Day/Phase/TimeScale |
|  | `Assets/_Game/Core/Contracts/Run/IRunClock.cs` | Contract clock |
|  | `Assets/_Game/Core/Contracts/Events/RunClockEvents.cs` | Event bus events cho clock/phase/time scale |
|  | `Assets/_Game/Core/Loop/GameLoop.cs` | Tick order / nơi RunClock được tick |
| `M1-G1` Top HUD tối thiểu | `Assets/_Game/UI/Runtime/Scripts/Presenters/HudPresenter.cs` | Presenter HUD chính |
|  | `Assets/_Game/UI/Runtime/UXML/HUD.uxml` | Cấu trúc HUD |
|  | `Assets/_Game/UI/Runtime/USS/Theme.uss` | Style HUD nếu cần chỉnh nhanh |
|  | `Assets/_Game/UI/Runtime/Scripts/Core/UiBootstrap.cs` | Boot UI documents |
|  | `Assets/_Game/UI/Runtime/Scripts/Services/GameServicesUiBridge.cs` | Nối UI ↔ GameServices |

---

## 3. Task-by-task notes

# M1-A1 — New Run flow tối thiểu

## File ưu tiên đọc đầu tiên
1. `Assets/_Game/Core/Boot/GameAppController.cs`
2. `Assets/_Game/Core/Boot/GameBootstrap.cs`
3. `Assets/_Game/Core/Loop/GameLoop.cs`
4. `Assets/_Game/Core/Boot/GameServicesFactory.cs`

## Vì sao
- `GameAppController` đang điều phối scene `MainMenu` ↔ `Game`
- `GameBootstrap` đã có `TryStartNewRun(...)`
- `GameLoop` là nơi run bắt đầu thật sự
- `GameServicesFactory` quyết định state mới có sạch không

## Điểm cần kiểm tra
- New Run có chỉ đi qua **một** đường flow duy nhất không?
- retry/new run nhiều lần có reset sạch world/services/runtime không?
- save cũ có bị dính sang New Run khi không muốn không?

---

# M1-A2 — Start map baseline hợp lệ

## File ưu tiên đọc đầu tiên
1. `Assets/_Game/Resources/RunStart/StartMapConfig_RunStart_64x64_v0.1.json`
2. `Assets/_Game/Core/RunStart/RunStartFacade.cs`
3. `Assets/_Game/Core/RunStart/RunStartWorldBuilder.cs`
4. `Assets/_Game/Core/RunStart/RunStartValidator.cs`
5. `Assets/_Game/Core/RunStart/RunStartPlacementHelper.cs`

## Vì sao
- phần lớn task này là **data task** ở file config
- phần còn lại là xác nhận runstart pipeline apply đúng và validator không báo lỗi runtime

## Điểm cần kiểm tra
- layout HQ / roads / producer có đọc được không?
- có buildable space đủ để test expansion không?
- map có choke/bad placement vô tình không?
- runstart validation có issue nào lộ ra ngay từ start map không?

---

# M1-A3 — Start package spawn đúng

## File ưu tiên đọc đầu tiên
1. `Assets/_Game/Resources/RunStart/StartMapConfig_RunStart_64x64_v0.1.json`
2. `Assets/_Game/Core/RunStart/RunStartWorldBuilder.cs`
3. `Assets/_Game/Core/RunStart/RunStartStorageInitializer.cs`
4. `Assets/_Game/Core/RunStart/RunStartTowerInitializer.cs`
5. `Assets/_Game/Core/RunStart/RunStartNpcSpawner.cs`
6. `Assets/_Game/Core/RunStart/RunStartHqResolver.cs`

## Vì sao
- start package là tổ hợp của data + builder + từng initializer chuyên trách

## Điểm cần kiểm tra
- HQ / 2 House / Farm / Lumber / Arrow Tower có spawn đúng không?
- tower start có full ammo thật không?
- 3 NPC start có workplace hợp lệ không?
- starting storage có đủ cho fantasy early game không?

---

# M1-A4 — Season/day clock tối thiểu

## File ưu tiên đọc đầu tiên
1. `Assets/_Game/Core/Loop/RunClockService.cs`
2. `Assets/_Game/Core/Contracts/Run/IRunClock.cs`
3. `Assets/_Game/Core/Contracts/Events/RunClockEvents.cs`
4. `Assets/_Game/Core/Loop/GameLoop.cs`

## Vì sao
- `RunClockService` hiện đã có gần như đủ backbone cho Wave 1
- cần kiểm tra contract/event exposure có đủ cho HUD và systems khác không

## Điểm cần kiểm tra
- Year/Season/Day/Phase có start đúng không?
- Tick có chạy thật qua `GameLoop` không?
- Build/Defend phase rule hiện tại có khớp design không?
- speed control có bị state drift khi New Run không?

---

# M1-G1 — Top HUD tối thiểu

## File ưu tiên đọc đầu tiên
1. `Assets/_Game/UI/Runtime/Scripts/Presenters/HudPresenter.cs`
2. `Assets/_Game/UI/Runtime/UXML/HUD.uxml`
3. `Assets/_Game/UI/Runtime/Scripts/Core/UiBootstrap.cs`
4. `Assets/_Game/UI/Runtime/Scripts/Services/GameServicesUiBridge.cs`
5. `Assets/_Game/UI/Runtime/USS/Theme.uss`

## Vì sao
- `HudPresenter` hiện đã có binding cho time/phase/resources/speed/notifications
- `HUD.uxml` đã có top bar + center time + speed controls + bottom bar
- UI bootstrap/bridge quyết định HUD có lấy đúng services thật không

## Điểm cần kiểm tra
- `LblTime` và `LblPhase` có update đúng từ `RunClock` không?
- `YearIndex` có đang được cập nhật thật không, hay vẫn chỉ default 1?
- resource counters có bind vào storage totals đúng không?
- HUD có lên state đúng ngay sau New Run không?

---

## 4. File phụ trợ rất đáng đọc thêm

Ngoài các file chính trên, nên mở thêm:

### Core services / data
- `Assets/_Game/Core/GameServices.cs`
- `Assets/_Game/Core/Contracts/Core/GameServices.cs`

### Boot / view bridge
- `Assets/_Game/Core/Boot/ViewServicesProvider_Bootstrap.cs`

### UI state / panel behavior
- `Assets/_Game/UI/Runtime/Scripts/Core/UIStateStore.cs`

### Notifications
- `Assets/_Game/Core/Loop/NotificationService.cs`

---

## 5. Thứ tự đọc/sửa đề xuất cho Wave 1

### Nhóm 1 — Backbone runtime
1. `Assets/_Game/Core/Boot/GameAppController.cs`
2. `Assets/_Game/Core/Boot/GameBootstrap.cs`
3. `Assets/_Game/Core/Loop/GameLoop.cs`
4. `Assets/_Game/Core/Loop/RunClockService.cs`

### Nhóm 2 — Run start data/world
5. `Assets/_Game/Resources/RunStart/StartMapConfig_RunStart_64x64_v0.1.json`
6. `Assets/_Game/Core/RunStart/RunStartFacade.cs`
7. `Assets/_Game/Core/RunStart/RunStartWorldBuilder.cs`
8. `Assets/_Game/Core/RunStart/RunStartNpcSpawner.cs`
9. `Assets/_Game/Core/RunStart/RunStartStorageInitializer.cs`
10. `Assets/_Game/Core/RunStart/RunStartTowerInitializer.cs`

### Nhóm 3 — HUD
11. `Assets/_Game/UI/Runtime/Scripts/Presenters/HudPresenter.cs`
12. `Assets/_Game/UI/Runtime/UXML/HUD.uxml`
13. `Assets/_Game/UI/Runtime/Scripts/Core/UiBootstrap.cs`
14. `Assets/_Game/UI/Runtime/Scripts/Services/GameServicesUiBridge.cs`

---

## 6. Kết luận thực dụng

Nếu bắt đầu implementation Wave 1 ngay, thứ tự đụng code hợp lý nhất là:
- chốt **New Run flow**
- chốt **start map + start package**
- verify **RunClock**
- nối **HUD** với state thật

Nói cách khác:
- **runtime backbone trước**
- **data start run sau**
- **HUD cuối cùng**

Đừng làm ngược theo kiểu polish HUD trước khi run flow/start state còn chưa sạch.
