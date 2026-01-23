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

---

## Day 16 — RunStart (StartMapConfig 64x64) + Reset New Run + Validation HUD

### Mục tiêu
- [x] `StartNewRun` **reset sạch runtime** (không leak state giữa các run):
  - [x] Clear notifications
  - [x] Clear jobs / claims / build orders
  - [x] Clear grid occupancy (roads/buildings/sites)
  - [x] Clear world stores (buildings/sites/npcs/enemies/towers)
- [x] Implement **RunStartApplier**: load + apply `StartMapConfig_RunStart_64x64_v0.1`
  - [x] Apply `roads`
  - [x] Spawn `initialBuildings` (constructed) + occupy footprint
  - [x] Spawn `initialNpcs` + resolve `assignedWorkplaceDefId`
  - [x] Cache `buildableRect / zones / spawnGates` (RunStartRuntime)
- [x] Starting resources theo Deliverable B 2.3:
  - [x] HQ: **+30 Wood, +20 Stone, +10 Food**
- [x] Tower:
  - [x] Add `TowerArrow` vào `Buildings.json`
  - [x] Init tower ammo theo overrides (`ammo: FULL` hoặc `ammoPercent`)
- [x] Debug Validation: có HUD check nhanh Acceptance Day 16
- [x] Fix case: bấm `StartNewRun` rồi `Validate` bị rỗng → đảm bảo `StartNewRun(seed, cfg)` luôn apply config

### Đã làm
- [v] **StartMapConfig (Option 1 — canonical DefId)**
  - Chuẩn hoá config để chỉ dùng **1 hệ tên** theo `Buildings.json`:
    - `HQ / House / Farm / Lumber / TowerArrow`
  - Loại bỏ nhu cầu remap alias `bld_*` trong runtime.
- [v] **RunStartApplier (JsonUtility)**
  - Parse JSON bằng `UnityEngine.JsonUtility` (khắc phục lỗi `System.Text.Json` trên Unity 2022.3).
  - Apply deterministic theo thứ tự config:
    - Cache `RunStartRuntime`: `BuildableRect`, `SpawnGates`, `Zones`
    - Apply `roads` vào `GridMap`
    - Spawn buildings constructed + occupy footprint lên grid
    - Spawn NPCs + resolve workplace theo `assignedWorkplaceDefId`
  - Có “fallback placement” deterministic khi anchor invalid do footprint defs lớn hơn bản config (search theo vòng Manhattan, thứ tự quét cố định).
- [v] **ResetForNewRun**
  - Bổ sung API `ClearAll()` vào **interfaces + concrete implementations** (không reflection):
  - Thêm `ClearAll()` vào các contract (vd: `IJobBoard`, `IClaimService`, `IBuildOrderService`, `IGridMap`, `IEntityStore<,>` / store interfaces tương ứng).
    - `JobBoard.ClearAll()`
    - `ClaimService.ClearAll()`
    - `BuildOrderService.ClearAll()`
    - `GridMap.ClearAll()`
    - `EntityStore.ClearAll()` (virtual)
  - `GameLoop.ResetForNewRun()` gọi `ClearAll()` để StartNewRun luôn sạch.
- [v] **TowerArrow**
  - Thêm def `TowerArrow` vào `Buildings.json` (size 1x1, `isTower=true`, baseLevel=1).
  - RunStart init tower ammo:
    - `ammo: "FULL"` → ammo cap
    - hoặc `ammoPercent` → clamp theo cap
- [v] **DebugRunStartValidationHUD**
  - Nút `StartNewRun (Seed)` + `Validate Now` + list PASS/FAIL:
    - Map size 64x64
    - Road cells > 0
    - Có HQ/House/Farm/Lumber/TowerArrow + constructed=true
    - Check footprint overlap
    - NPC spawn + workplace reference valid
    - HQ storage >= (30/20/10)
  - Wiring vào `DebugHUDHub` (Home tab).
- [v] **Fix “StartNewRun rồi Validate bị rỗng”**
  - Update `GameBootstrap.DebugStartNewRun(int seed)` gọi `_loop.StartNewRun(seed, cfg)` (truyền StartMapConfig vào), đảm bảo Apply chạy ngay sau reset.

### Kết quả / Acceptance
- [v] Validate ngay sau Play: `Road cells > 0`, buildings spawned đúng và `constructed=true`.
- [v] `StartNewRun(seed, cfg)` sau reset vẫn spawn lại đầy đủ (không còn case roads/buildings = 0).
- [v] Starting storage tại HQ đạt:
  - Wood ≥ 30, Stone ≥ 20, Food ≥ 10.
- [v] Không phát hiện overlap footprint qua HUD overlap check.
- [v] NPC spawn có workplace reference hợp lệ (nếu config set).

### Ghi chú / Pitfalls
- `System.Text.Json` không đảm bảo có sẵn trong Unity 2022.3 → dùng `JsonUtility` để chắc chắn build/run ổn định.
- `ClearAll()` đã được bổ sung vào **interfaces** để GameLoop có thể reset qua contract ổn định (tránh phải cast sang concrete).
- Nếu StartMapConfig anchor authored theo footprint cũ (HQ nhỏ hơn), cần validate + pick anchor gần nhất deterministic để tránh fail đặt building.
- Khi debug `StartNewRun` từ HUD: bắt buộc truyền đúng `cfg`, nếu không Apply sẽ không chạy/không có data.

### Việc tiếp theo (Day 17)
- Thêm DataSchemaValidator/Fail-fast rõ ràng cho StartMapConfig (thiếu field/defId sai → Notification + log).
- Dùng `RunStartRuntime.SpawnGates/Zones` để chuẩn bị wiring cho Wave/Defend (spawn pipeline VS2).

---

## Day 17 — DataRegistry mở rộng (NPC/Tower/Enemy/Recipe/Wave/Reward) + DataValidator + Debug Validate Data

### Mục tiêu
- [x] Chuẩn hoá **DefsCatalog** để có đầy đủ nguồn TextAsset cho VS2:
  - [x] `Buildings / Npcs / Towers / Enemies / Recipes / Waves / Rewards`
- [x] Tạo **JSON TextAsset chuẩn** (root wrapper + array) cho các nhóm thiếu:
  - [x] `Npcs.json`
  - [x] `Towers.json`
  - [x] `Enemies.json`
  - [x] `Recipes.json`
  - [x] `Waves.json`
  - [x] `Rewards.json`
- [x] **DataRegistry** load + cache tất cả defs:
  - [x] Load bằng `JsonUtility` (không dùng `System.Text.Json`)
  - [x] Dictionary lookup `OrdinalIgnoreCase`
  - [x] Có API `GetX(id)` + `GetAllXIds()` (phục vụ validator + debug)
- [x] **DataValidator** validate schema tối thiểu + cross-reference:
  - [x] Duplicate ID / empty ID
  - [x] Value range cơ bản (hp/speed/damage/cost > 0…)
  - [x] Wave entries tham chiếu enemyId phải tồn tại
- [x] Debug UX:
  - [x] Có nút **Validate Data** trong `DebugHUDHub` để xem pass/fail + danh sách lỗi
- [x] Boot gate:
  - [x] Validate data ở `GameBootstrap` (fail-fast)
  - [x] Nếu data invalid thì **không auto start run** (tránh chạy với defs lỗi)

### Đã làm
- [v] **DefsCatalog**
  - Bổ sung/chuẩn hoá các field TextAsset: `Npcs/Towers/Enemies/Recipes/Waves/Rewards`.
- [v] **Tạo Data TextAssets (VS2_DataTextAssets_v0.1)**
  - Tạo 6 file JSON theo pattern wrapper `"xxx":[...]` tương tự `Buildings.json`.
  - Import vào project dưới dạng TextAsset và gán vào `DefsCatalog`.
- [v] **Def DTOs**
  - Bổ sung DTO còn thiếu để match schema JSON: `NpcDef`, `TowerDef` và các struct con phục vụ wave/recipe/reward nếu cần.
- [v] **DataRegistry**
  - Thêm loader cho các nhóm defs mới (Npcs/Towers/Enemies/Recipes/Waves/Rewards) theo đúng pipeline:
    - Parse root wrapper bằng `JsonUtility.FromJson<Root>()`
    - Add vào dictionary `StringComparer.OrdinalIgnoreCase`
    - Track lỗi: missing root, null array, duplicate id, invalid field
  - Thêm API access:
    - `GetNpc(id)`, `GetTower(id)` (fix compile cho validator)
    - `GetAllNpcIds()`, `GetAllTowerIds()` + các nhóm còn lại
- [v] **DataValidator**
  - Validate “schema tối thiểu” + cross-ref:
    - `Wave.entries[].enemyId` phải tồn tại trong registry
    - `Recipe` amounts > 0, resourceType hợp lệ
    - `Tower` cost/params hợp lệ (tối thiểu)
  - Khi invalid: trả `false` + populate `errors`.
- [v] **DebugHUDHub**
  - Thêm section “Validate Data” ở Home tab:
    - Button `Validate Data`
    - Hiển thị `OK/FAIL (n errors)` + list lỗi (cap để tránh spam UI).
- [v] **GameBootstrap**
  - Validate data at boot:
    - Nếu `ValidateAll` fail → log errors + tắt `_autoStartRun`
  - Chuẩn hoá lại `DebugStartNewRun(seed)` để luôn gọi `_loop.StartNewRun(seed, cfg)` (cfg đã cache).

### Kết quả / Acceptance
- [v] Play mode: DataRegistry load đủ nhóm defs, không crash khi thiếu optional.
- [v] Bấm **Validate Data**:
  - Nếu defs thiếu/ID trùng/ref sai → hiện FAIL + list lỗi rõ ràng.
  - Nếu OK → hiện OK (0 errors).
- [v] Boot fail-fast:
  - Data invalid → không auto start run, tránh state “chạy nửa vời”.
- [v] `DebugRunStartValidationHUD` không còn lỗi gọi `DebugStartNewRun` (bootstrap đã expose method).

### Ghi chú / Pitfalls
- `JsonUtility` yêu cầu **wrapper root** cho array (`{ "enemies":[...] }`…), không parse trực tiếp array root.
- Dictionary dùng `OrdinalIgnoreCase` để tránh lỗi casing (đặc biệt def id nhập tay trong JSON).
- Validator nên chỉ check “tối thiểu” (không over-validate) để không chặn dev workflow; lỗi nghiêm trọng mới fail boot.
- UI debug list lỗi nên cap số dòng để tránh lag khi lỗi cascade.

### Việc tiếp theo (Day 18)
- Dùng `Waves.json` + `SpawnGates` (RunStartRuntime) để wiring **Wave spawn pipeline** (Defend phase).
- Add “Editor menu Validate Data” (optional) để validate trước khi Play / CI.

---

## Day 18 — Build Site Cost Tracking (L1) + Gate Work + Debug Site Progress

### Mục tiêu
- [x] Chuẩn hoá **build cost L1** cho building defs (nguồn: `Buildings.json`):
  - [x] Thêm `buildCostsL1` (mảng cost theo resource)
  - [x] Thêm `buildChunksL1` (số chunk chuẩn bị cho Day 19 delivery/jobs)
- [x] `DataRegistry` parse cost L1 vào `BuildingDef` (không làm mất nội dung cũ).
- [x] `BuildSiteState` có progress giao hàng:
  - [x] `DeliveredSoFar` (mirror để UI ổn định)
  - [x] `RemainingCosts` (còn cần giao)
  - [x] `IsReadyToWork` (gate work = chỉ work khi RemainingCosts rỗng)
- [x] `BuildOrderService`:
  - [x] Khi tạo site: init `DeliveredSoFar/RemainingCosts` từ `BuildingDef.BuildCostsL1`
  - [x] **Chặn tiến độ work** nếu site chưa `IsReadyToWork`
- [x] Debug HUD:
  - [x] `DebugWorldIndexHUD` hiển thị **Build Sites** + cost progress `delivered/total`

### Đã làm
- [v] **Buildings.json**
  - Bổ sung field cho từng building cần build theo VS2:
    - `buildCostsL1`: danh sách `{ res, amt }` (resource index theo enum runtime)
    - `buildChunksL1`: số chunk (dùng cho Day 19 delivery/work split)
  - Giữ nguyên các field cũ, chỉ thêm field mới theo Day 18.
- [v] **DefDTOs**
  - Bổ sung vào `BuildingDef`:
    - `CostDef[] BuildCostsL1`
    - `int BuildChunksL1`
- [v] **DataRegistry**
  - Thêm DTO parse: `BuildingCostJson { res, amt }`
  - Thêm field vào `BuildingJson`: `buildCostsL1`, `buildChunksL1`
  - Map vào `BuildingDef`: `BuildCostsL1`, `BuildChunksL1`
  - Implement overload `ToCosts(BuildingCostJson[])` (tương tự Tower/Recipe costs)
  - Patch theo nguyên tắc: **chỉ add, không xoá code cũ** (nếu không tìm được điểm chèn thì báo warning khi patch).
- [v] **BuildSiteState**
  - Thêm:
    - `DeliveredSoFar` (mirror của total cost, amount ban đầu = 0)
    - `IsReadyToWork` => `RemainingCosts == null || RemainingCosts.Count == 0`
- [v] **BuildOrderService**
  - Khi tạo `BuildSiteState`:
    - `DeliveredSoFar = BuildDeliveredMirror(def.BuildCostsL1)`
    - `RemainingCosts = CloneCostsOrEmpty(def.BuildCostsL1)`
  - Trong Tick:
    - Nếu `!site.IsReadyToWork` => **không progress work**, giữ site/state đồng bộ.
  - Bổ sung helper nội bộ:
    - `CloneCostsOrEmpty()`
    - `BuildDeliveredMirror()`
- [v] **DebugWorldIndexHUD**
  - Hiển thị Build Sites (cap số lượng để tránh spam):
    - id, defId, anchor, trạng thái `READY/WAIT_COST`
    - cost line: `W/F/S/I/A delivered/total`
  - Bổ sung helper:
    - `FormatCostProgress(BuildSiteState)`
    - `Accum(List<CostDef>, int[])`

### Kết quả / Acceptance
- [v] Đặt building tạo **Build Site** sẽ có cost tracked:
  - `DeliveredSoFar` ban đầu = 0, `RemainingCosts` = total.
  - `IsReadyToWork = false` khi còn cost.
- [v] BuildOrder **không tự hoàn thành** nữa khi chưa có delivery (đúng chuẩn để Day 19 nối pipeline giao hàng).
- [v] DebugWorldIndexHUD hiển thị rõ progress `delivered/total` cho từng site.

### Ghi chú / Pitfalls
- `JsonUtility` yêu cầu schema DTO khớp tên field; dùng `{ res, amt }` để parse ổn định.
- Đảm bảo `BuildCostsL1/BuildChunksL1` đã có trong `BuildingDef` trước khi map trong DataRegistry.
- Helper (CloneCosts/DeliveredMirror/FormatCostProgress) phải nằm trong đúng file, tránh lỗi “method not found”.

### Việc tiếp theo (Day 19)
- Implement delivery pipeline:
  - Job lấy tài nguyên từ storage → giao vào BuildSite (giảm Remaining, tăng Delivered)
  - Khi RemainingCosts rỗng → unlock WorkOnSite job để hoàn thành build.
