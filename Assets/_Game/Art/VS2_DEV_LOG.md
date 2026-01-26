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

---

## Day 19 — BuildOrderService = Job Provider (BuildDeliver/BuildWork) + Throttle

### Mục tiêu
- [x] Biến `BuildOrderService` thành **Job Provider** cho pipeline build (L1):
  - [x] Khi site **còn thiếu cost** → sinh `JobArchetype.BuildDeliver` theo **chunk**.
  - [x] Khi site **đủ cost** → sinh **1** `JobArchetype.BuildWork`.
- [x] Throttle: mỗi site tối đa **N pending delivery jobs** (tránh spam job).
- [x] Deterministic:
  - [x] Resolve workplace build ưu tiên **HQ constructed**, fallback workplace có `WorkRoleFlags.Build`.
  - [x] Pick remaining cost theo thứ tự resource (stable), không phụ thuộc frame.
- [x] Không over-scope: Day 19 chỉ “tạo job đúng lúc”, executor giao/thi công là Day 20.

### Đã làm
- [v] **BuildOrderService.Tick**
  - Không còn auto-progress work trong service.
  - Mỗi tick: resolve build workplace (HQ) → `EnsureBuildJobsForSite(...)`:
    - Nếu `RemainingCosts` còn → ensure tối đa `MaxPendingDeliveryJobsPerSite` jobs `BuildDeliver`.
    - Nếu `RemainingCosts` rỗng → huỷ delivery jobs còn dư → ensure 1 job `BuildWork`.
  - Complete order khi:
    - Site ready (`RemainingCosts` rỗng) **và** `WorkSecondsDone >= WorkSecondsTotal` (work do executor tăng).
- [v] **Job payload (BuildDeliver/BuildWork)**
  - BuildDeliver: set `Site`, `ResourceType`, `Amount`, `TargetCell=site.Anchor`, `Workplace=HQ`.
  - BuildWork: set `Site`, `TargetCell=site.Anchor`, `Workplace=HQ`.
- [v] **Cancel/Reset**
  - `Cancel(order)` / `CompletePlaceOrder` huỷ tracked jobs theo site để tránh “job mồ côi”.
  - `ClearAll()` clear thêm tracking dictionaries.

### Kết quả / Acceptance
- [v] Đặt building → site vào trạng thái `WAIT_COST`:
  - Jobs `BuildDeliver` xuất hiện trong workplace queue (HQ) theo chunk, không spam vô hạn.
- [v] Khi site đủ cost (RemainingCosts rỗng) → chỉ còn **1** `BuildWork` job.
- [v] Không còn tình trạng `BuildOrderService` tự cộng work khi chưa đủ cost.

### Ghi chú / Pitfalls
- `BuildOrder.RequiredCost` vẫn là field legacy dạng 1 cost line; pipeline VS2 dùng **Site.RemainingCosts** (list) là source of truth.
- Throttle delivery jobs là bắt buộc để tránh “peek jobId tăng nhanh” do enqueue mỗi tick.

### Việc tiếp theo (Day 20)
- Implement executor `BuildDeliverExecutor` / `BuildWorkExecutor` để:
  - trừ kho, giảm `RemainingCosts`, hoàn thành delivery
  - tăng `WorkSecondsDone` và hoàn thành build.

---

## Day 20 — BuildDeliver/BuildWork Executors (apply cost + work) + HaulBasic Gate Hardening

### Mục tiêu
- [x] Implement `BuildDeliverExecutor`:
  - [x] Pickup tài nguyên từ nguồn hợp lệ (ưu tiên workplace/HQ, fallback Warehouse/HQ gần nhất có hàng).
  - [x] Deliver vào `BuildSiteState`: giảm `RemainingCosts` (gate), refund phần dư nếu over-deliver.
  - [x] Hoàn thành job (Completed) và cleanup state nội bộ.
- [x] Implement `BuildWorkExecutor`:
  - [x] Chỉ work khi `RemainingCosts` rỗng.
  - [x] Tăng `WorkSecondsDone` trên site, complete job khi đủ `WorkSecondsTotal`.
- [x] Fix loop “HaulBasic enqueue/cancel” (peek jobId tăng liên tục) bằng **gate ở scheduler**:
  - [x] Không enqueue HaulBasic nếu **tất cả** Warehouse/HQ đều full cho resource đó (fail fast ở gate, tránh cancel trong executor).

### Đã làm
- [v] **BuildDeliverExecutor**
  - Phase 0 (Pickup):
    - Resolve `SourceBuilding` deterministic:
      - ưu tiên `Workplace` nếu đủ hàng
      - fallback scan Warehouse/HQ constructed có `GetAmount >= minRequired` (tie-break distance + id).
    - Move tới source bằng `GridAgentMoverLite.StepToward(...)`.
    - `StorageService.Remove(source, rt, amount)`; nếu rỗng → repick source tick sau.
  - Phase 1 (Deliver):
    - Move tới `site.Anchor`.
    - Apply:
      - clamp theo remaining hiện tại của resource
      - giảm `RemainingCosts` (remove line nếu về 0; nếu empty → set null để clean gate)
    - Refund phần dư về source (`StorageService.Add(...)`) nếu deliver vượt remaining.
    - Set `JobStatus.Completed` + cleanup.
- [v] **BuildWorkExecutor**
  - Gate: nếu `RemainingCosts != null && Count>0` → giữ `InProgress` và chờ delivery.
  - Khi arrived tại `site.Anchor`:
    - `site.WorkSecondsDone += dt` (clamp <= total)
    - complete job khi đủ total.
- [v] **JobExecutorRegistry**
  - Đảm bảo map `JobArchetype.BuildDeliver` / `BuildWork` → đúng executor.
- [v] **JobScheduler HaulBasic gate (hardening)**
  - Trong `TryEnsureHaulJob`: thêm gate `AnyHaulDestinationHasFree(rt)` (scan `_buildingIds` sorted, filter HQ/Warehouse constructed):
    - nếu ALL destinations full → không enqueue → tránh vòng lặp cancel/enqueue.
  - Un-comment/enable `DestCap(...)` (LOCKED) để tính free capacity đúng theo spec.

### Kết quả / Acceptance
- [v] Site `WAIT_COST`:
  - NPC nhận `BuildDeliver` → kho giảm, `RemainingCosts` giảm dần → UI cost progress tăng (delivered/total).
- [v] Khi `RemainingCosts` rỗng:
  - `BuildWork` job xuất hiện → NPC work → `WorkSecondsDone` tăng dần.
  - Khi đủ total → build hoàn tất (building constructed), site bị remove đúng flow.
- [v] HaulBasic:
  - Khi kho đích full → không còn spam jobId peek tăng liên tục (queue ổn định).

### Ghi chú / Pitfalls
- Executor không tự tạo job; mọi spam job đều phải fix ở **provider/gate** (JobScheduler/BuildOrderService).
- Refund phần dư giúp tránh over-deliver gây “mất tài nguyên” khi nhiều NPC deliver đồng thời.
- Gate HaulBasic chỉ kiểm tra “có bất kỳ destination còn chỗ”, không gate theo workplace để giữ reroute logic.

### Việc tiếp theo
- Day 21+: wiring BuildDeliver/BuildWork với claim/interest (nếu mở rộng), và polish debug HUD hiển thị job/site progress chi tiết hơn.

---

## Day 21 — Cancel Build (Site/Order) + Cleanup + Refund + Rollback Driveway Road

### Mục tiêu
- [x] Cancel build site/order giữa chừng **không để lại “ghost state”**:
  - [x] Huỷ toàn bộ `BuildDeliver/BuildWork` jobs đang track theo site
  - [x] Clear tracking refs trong `BuildOrderService` (không spawn job lại cho site đã huỷ)
  - [x] Xoá site footprint khỏi `GridMap` + destroy `BuildSiteState`
  - [x] (Policy) Destroy placeholder building nếu cấu hình `_destroyPlaceholderOnCancel`
- [x] Refund tài nguyên theo policy Day 21:
  - [x] Refund **DeliveredSoFar** về kho/HQ gần nhất (best-effort, deterministic)
  - [x] Refund **carry** của NPC nếu job bị Cancel/Fail sau khi đã pickup
- [x] Rollback “driveway road” được auto-create khi Commit building:
  - [x] Chỉ rollback nếu cell road đó được auto tạo (không đụng road có sẵn)
  - [x] Không tạo cyclic asmdef (không reference concrete `BuildOrderService` trong `PlacementService`)
- [x] Hardening: executor **không “hồi sinh”** job đã `Cancelled/Failed`
- [x] Debug UX: có hotkey để cancel build site nhanh khi test

### Đã làm
- [v] **BuildOrderService.Cancel(orderId) hardening**
  - Cancel tracked jobs theo `SiteId` (delivery + work), đồng thời clear tracking dicts liên quan.
  - Refund `BuildSiteState.DeliveredSoFar` về kho/HQ gần nhất (distance + id tie-break → deterministic).
  - Clear footprint site trên `GridMap` và destroy site; destroy placeholder theo policy.
- [v] **Rollback driveway road (auto-created)**
  - `BuildOrderService` giữ map `orderId -> drivewayRoadCell` để rollback khi Cancel.
  - Khi order complete: remove mapping để driveway road trở thành “permanent”.
  - Khi reset: clear mapping.
- [v] **No-cyclic asmdef wiring bằng EventBus**
  - `PlacementService.CommitBuilding()`:
    - xác định `drivewayWasCreated` khi auto-convert driveway → road
    - publish `BuildOrderAutoRoadCreatedEvent(orderId, drivewayCell)` qua EventBus (không cast concrete).
  - `BuildOrderService` subscribe event thông qua `_s.EventBus` (lazy subscribe via `EnsureBusSubscribed()`), và best-effort unsubscribe trong `ClearAll()` để tránh double-handler khi re-init/domain reload.
- [v] **Executors hardening (Cancel/Fail)**
  - `BuildDeliverExecutor`:
    - nếu `job.Status` đã `Cancelled/Failed` → refund carry best-effort rồi cleanup.
    - nếu site vanished/invalid → refund carry rồi terminal.
  - `BuildWorkExecutor`:
    - nếu `job.Status` đã `Cancelled/Failed` → không set lại `InProgress`, cleanup state nội bộ.
- [v] **DebugBuildingTool**
  - Thêm hotkey **X**: cancel build site dưới cursor (không thay đổi contract `IBuildOrderService`; dùng wiring nội bộ/debug-safe).
  - Notification rõ ràng khi cancel thành công/không có order.

### Kết quả / Acceptance
- [v] Cancel build tại mọi thời điểm (đang WAIT_COST / đang deliver / đang work):
  - Site bị xoá sạch khỏi grid, không còn occupancy “kẹt”.
  - Không spawn thêm delivery/work jobs cho site đã huỷ (tracking đã clear).
- [v] Tài nguyên không bị mất:
  - `DeliveredSoFar` được trả về kho/HQ gần nhất (best-effort theo capacity).
  - NPC đang carry khi bị cancel/fail được refund (source ưu tiên, fallback kho/HQ).
- [v] Driveway road auto-create được rollback đúng:
  - Road do player đặt trước đó **không bị xoá**.
- [v] Không còn hiện tượng executor “resurrect” job Cancelled.

### Ghi chú / Pitfalls
- Tránh cyclic asmdef: `PlacementService` chỉ publish event, không reference `BuildOrderService`.
- Subscribe EventBus nên làm lazy (đợi `_s.EventBus` sẵn sàng), và best-effort unsubscribe khi reset để tránh double-subscribe.
- Refund policy là best-effort (nếu kho full có thể không refund hết) — đúng scope Day 21, không over-engineer reservation ledger.

### Việc tiếp theo
- Day 22+: polish debug HUD/site inspector (hiển thị orderId/siteId + driveway tracking), và bắt đầu wiring Defend/Waves pipeline theo VS2 plan.

---

## Day 22 — Building HP (Durability) + RepairWork (time-only) + Debug Damage/Repair

### Mục tiêu
- [x] Bổ sung **HP/MaxHP** tối thiểu cho Building (durability) để support Damage/Repair.
- [x] Data: đọc `hp` từ `Buildings.json` → map vào `BuildingDef.MaxHp`.
- [x] Repair pipeline tối thiểu (time-only, không tiêu tài nguyên):
  - [x] `IBuildOrderService.CreateRepairOrder(BuildingId)` tạo Repair order cho building bị hỏng.
  - [x] Sinh job `JobArchetype.RepairWork` và executor xử lý repair theo thời gian.
- [x] Role gating:
  - [x] `RepairWork` chỉ cho NPC có `WorkRoleFlags.Build`.
- [x] Debug test nhanh trong GameView:
  - [x] Hotkey **K**: damage building dưới mouse (giảm HP).
  - [x] Hotkey **R**: tạo repair order cho building dưới mouse.

### Đã làm
- [v] **Defs / States**
  - `BuildingDef`: thêm `MaxHp`.
  - `BuildingState`: thêm `HP/MaxHP` (durability).
  - `DataRegistry`: parse field `hp` từ `Buildings.json` vào `BuildingDef.MaxHp` (clamp >= 1).
- [v] **BuildOrderService — Repair Orders**
  - Implement `CreateRepairOrder(BuildingId)`:
    - Validate building tồn tại + `IsConstructed`.
    - Fix-up `MaxHP` từ `DataRegistry` nếu missing; clamp `HP` hợp lệ.
    - Không tạo duplicate repair order cho cùng building đang active.
    - Repair time = time-only minimal (chunk-based).
  - Tick repair:
    - Ensure 1 `RepairWork` job per repair order.
    - Complete order khi `HP >= MaxHP` hoặc target invalid.
  - Cancel/Reset:
    - Cancel repair job theo `orderId` khi huỷ order.
    - `ClearAll()` clear tracking repair jobs.
- [v] **Job System**
  - `JobArchetype`: thêm `RepairWork`.
  - `JobExecutorRegistry`: map `RepairWork` → `RepairWorkExecutor`.
  - `JobBoard` + `JobScheduler`: gate `RepairWork` theo `WorkRoleFlags.Build`.
- [v] **RepairWorkExecutor (NEW)**
  - Di chuyển tới `DestBuilding.Anchor` bằng `GridAgentMoverLite.StepToward(ref NpcState, CellPos)` (deterministic X then Y).
  - Khi đến nơi: mỗi ~4s heal theo chunk (~15% MaxHP/chunk), clamp tới MaxHP.
  - Hardening: nếu job terminal (Cancelled/Failed/Completed) → cleanup state nội bộ.
- [v] **DebugBuildingTool**
  - Hotkeys:
    - **K**: trừ HP (dmg=50) và notify `HP/MaxHP`.
    - **R**: gọi `CreateRepairOrder(...)` và notify kết quả.

### Kết quả / Acceptance
- [v] Damage:
  - Bấm **K** trên building constructed → HP giảm đúng, không xuống dưới 0.
- [v] Repair:
  - Bấm **R** khi `HP < MaxHP` → tạo repair order + spawn `RepairWork` job.
  - NPC có role Build di chuyển tới anchor, repair theo chunk đến full HP → job Completed + order complete.
- [v] Role gating:
  - NPC không có `WorkRoleFlags.Build` không nhận `RepairWork`.

### Ghi chú / Pitfalls
- `GridAgentMoverLite.StepToward(...)` trả `false` nếu bước ra ngoài bounds → repair job sẽ không tới được nếu anchor invalid/out-of-bounds; cần đảm bảo anchor hợp lệ.
- Durability là tối thiểu (Day22): repair **time-only**, chưa có cost/consumables và chưa có damage source từ combat (sẽ nối ở các day Defend/Waves).

### Việc tiếp theo (Day 23)
- Wiring Defend/Waves: spawn pipeline theo `Waves.json` + `SpawnGates` (RunStartRuntime), và damage apply lên Building HP trong combat loop.

---

## Day 23 — Forge craft ammo (Recipe + Craft job) + Local Input Storage + Supply (Armory preferred)

### Mục tiêu
- [x] Forge sản xuất **Ammo** theo recipe (consume inputs, output ammo) theo thời gian.
- [x] Input **phải nằm trong Forge**:
  - [x] Forge có `capWood/capIron/capAmmo` (local storage).
  - [x] NPC Craft tại Forge chỉ craft khi đủ input local.
- [x] Supply pipeline:
  - [x] Khi Forge thiếu input → tạo job vận chuyển input **từ kho gần nhất** vào Forge.
  - [x] **Ưu tiên Armory** làm supply; nếu không có Armory worker thì **fallback HQ/Warehouse hauler**.
- [x] Throttle + ổn định:
  - [x] Không spam job: 1 pending supply job cho mỗi (Forge, ResourceType) + 1 pending craft job mỗi Forge.
  - [x] Buffered hauling (Option 2): mỗi lần chở **đổ đầy đến mức buffer**, tránh tăng 1–2 đơn vị lắt nhắt.

### Đã làm
- [v] **Buildings.json**
  - Bổ sung `capWood` + `capIron` cho **Forge** (theo level), enabling local input storage.
- [v] **Job System (Contracts + Registry)**
  - Thêm `JobArchetype.HaulToForge`.
  - Register executor: `JobExecutorRegistry` map `HaulToForge` → `HaulToForgeExecutor`.
  - Role gating:
    - `JobBoard` + `JobScheduler`: `HaulToForge` allowed cho `WorkRoleFlags.Armory` **hoặc** `WorkRoleFlags.HaulBasic` (fallback).
- [v] **HaulToForgeExecutor (NEW)**
  - Job payload:
    - `Workplace`: Armory ưu tiên, fallback HQ/Warehouse.
    - `DestBuilding`: Forge.
    - `ResourceType`: input cần chở (Wood/Iron).
    - `Amount`: amount request (được clamp theo carry cap + free cap).
  - Flow:
    - Pick `SourceBuilding` = kho/HQ gần nhất có đủ (deterministic tie-break distance + id).
    - Move source → remove resource → move dest (Forge) → add resource.
  - Hardening:
    - Cancel nếu Forge không store được loại resource hoặc Forge full.
    - Best-effort claim keys để tránh collision (StorageSource/StorageDest).
- [v] **CraftAmmoExecutor**
  - Recipe (Day23 scope, locked):
    - Input: **2 Iron + 1 Wood**
    - Output: **10 Ammo**
    - Time: **6s**
  - Consume input **tại Forge** (local), sau `CraftTime` add ammo vào Forge.
  - Nếu thiếu input local hoặc output full → job Cancelled (AmmoService sẽ enqueue supply rồi retry).
- [v] **AmmoService (Job Provider)**
  - Tick scan tất cả Forge constructed:
    - Nếu thiếu input local → `EnsureSupplyJobToForge(...)` (Armory preferred, fallback Warehouse/HQ hauler).
    - Nếu đủ input + đủ ammo space + có NPC tại Forge workplace → `TryStartCraft(...)` tạo `CraftAmmo` job.
  - Anti-spam dicts:
    - `_supplyJobByForgeAndType` + `_craftJobByForge`.
- [v] **Buffered hauling tweak (Option 2)**
  - `EnsureSupplyJobToForge(...)` đổi từ “need-only” sang “fill-to-buffer”:
    - Iron target ~10, Wood target ~5 (clamp theo cap và carry cap), giảm số chuyến haul.
- [v] **Tick wiring**
  - Add `AmmoService.Tick(dt)` vào tick order (đảm bảo provider chạy mỗi frame).

### Kết quả / Acceptance
- [v] Khi kho có đủ Wood/Iron:
  - Armory worker (nếu có) sẽ nhận `HaulToForge` và chở input vào Forge; nếu không có Armory worker thì HQ/Warehouse hauler sẽ chở.
- [v] Khi Forge đủ input local:
  - NPC Craft tại Forge craft theo thời gian, mỗi ~6s tăng **+10 Ammo** vào kho Forge.
- [v] Buffered hauling:
  - Forge input tăng theo “đợt” lên gần mức buffer (Iron ~10, Wood ~5) thay vì nhích 1–2 rồi dừng.

### Ghi chú / Pitfalls
- `HaulBasicExecutor` hiện bị “special-case” (Harvest → Warehouse/HQ) nên không reuse cho supply Forge; dùng `HaulToForge` riêng để tránh phá logic cũ.
- Ưu tiên ResupplyTower trước HaulToForge cho Armory worker sẽ triển khai sau (Day Defend), không làm trong Day 23 để tránh mở rộng scope.

### Việc tiếp theo (Day 24)
- Wiring Defend/Waves spawn + combat damage apply lên Building HP, và chuẩn hoá resupply tower pipeline (Armory priority).

---

## Day 24 — Armory buffer + HaulAmmo (Forge → Armory)

### Mục tiêu
- [x] Ammo logistics chạy vào kho trung tâm (Armory).
- [x] Armory buffer rule:
  - [x] Target ammo trong Armory = **80% cap**.
  - [x] Chỉ haul khi Forge có “dư” ammo: Forge ammo **>= 20% cap** và chỉ lấy phần **trên ngưỡng 20%** (không rút xuống dưới).
- [x] HaulAmmo job + executor (transporters):
  - [x] Chuyển ammo theo **chunk** (Deliverable C): **L1=40, L2=60, L3=80**.
  - [x] Throttle: 1 pending job / Armory (tránh spam).

### Đã làm
- [v] **JobBoard**
  - Cho phép `JobArchetype.HaulAmmoToArmory` chỉ claim bởi `WorkRoleFlags.Armory`.
  - Update priority Armory: `ResupplyTower` ưu tiên cao nhất, sau đó `HaulAmmoToArmory`, rồi `HaulToForge` (giữ đúng hướng “resupply trước” về sau).
- [v] **JobExecutorRegistry**
  - Register mapping: `HaulAmmoToArmory` → `HaulAmmoToArmoryExecutor`.
- [v] **HaulAmmoToArmoryExecutor (NEW)**
  - Flow 2-phase:
    - Move tới Forge → pickup Ammo (Remove).
    - Move tới Armory → deposit Ammo (Add).
  - Clamp amount theo:
    - Requested chunk từ provider
    - `Forge.Available` (chỉ lấy phần “takeable”)
    - `Armory.FreeCap`
  - Best-effort refund remainder về Forge nếu Armory full đột ngột.
- [v] **AmmoService (Provider)**
  - Bổ sung nhánh “Armory buffer”:
    - Scan Armories constructed + có NPC workplace.
    - Nếu ammo < target(80% cap) → pick Forge source gần nhất có takeable ammo (>= 20% cap).
    - Enqueue `HaulAmmoToArmory` với `Workplace=Armory`, `SourceBuilding=Forge`, `DestBuilding=Armory`, `Amount=chunk`.
  - Anti-spam: dict `_haulAmmoJobByArmory` (1 pending job mỗi Armory).

### Kết quả / Acceptance
- [v] Forge craft ammo (Day23) hoạt động → khi Forge ammo đạt ngưỡng, Armory bắt đầu nhận ammo.
- [v] Ammo chuyển từ Forge sang Armory theo **chunk** (40/60/80) cho tới khi Armory đạt ~80% cap.
- [v] Forge không bị “hút cạn”: luôn giữ lại tối thiểu **20% cap** ammo tại Forge.
- [v] Queue ổn định, không spam job theo frame (1 job/armory).

### Ghi chú / Pitfalls
- `HaulBasicExecutor` không reuse được vì hardcode Harvest→Warehouse/HQ; Day24 dùng executor riêng để tránh phá logic cũ.
- Priority ResupplyTower trước HaulAmmo/HaulToForge đã chuẩn bị sẵn cho day Defend (chưa bật Resupply provider trong Day24).

### Việc tiếp theo (Day 25)
- Wiring Defend/Waves + Combat damage apply lên Building HP và ResupplyTower provider dùng ammo trong Armory.

---

## Day 25 — Tower low-ammo monitor + request queue (HUD + Gizmos, no resupply yet)

### Mục tiêu
- [x] Tower tự tạo **Ammo Request** khi ammo thấp.
  - [x] Threshold: `<= 25%` ammo cap
  - [x] Cooldown per tower
  - [x] Priority: `0 ammo` > `low ammo`
- [x] Notifications/HUD: hiển thị low/empty ammo **không spam**.
- [x] (Dev hook) Có cách test dù chưa có tower placement/combat thật.

### Đã làm
- [v] **AmmoService — Low ammo monitor + Request Queue**
  - Thêm scan tower ammo theo tick (đọc từ `WorldState.Towers` / `WorldIndex.Towers`).
  - Tính threshold theo `ammoCap` (ceil 25% và tối thiểu 1 nếu cap>0).
  - Khi `ammo <= threshold` → enqueue request `Normal`; khi `ammo==0` → enqueue request `Urgent`.
  - Cooldown riêng cho Low/Empty theo tower (không tạo request mỗi frame).
  - Anti-dup: 1 pending request/tower; promote `Normal → Urgent` nếu tụt về 0 trong lúc đang pending.
- [v] **NotificationService wiring (anti-spam)**
  - Push theo key per tower (`TowerAmmo_Low_<id>`, `TowerAmmo_Empty_<id>`) + cooldown + dedupe để tránh spam.
  - Severity theo trạng thái (empty > low) và có thể nâng severity khi Combat active (nếu hệ combat đã expose flag).
- [v] **Debug HUD + Gizmos (Tower visibility)**
  - `DebugTowerAmmoHUD`: hiển thị số tower, ammo/cap, trạng thái low/empty, và pending request (urgent/normal).
  - `DebugTowerGizmos`: vẽ marker tower trong Scene View (XY), màu theo state (OK/LOW/EMPTY), có label ammo/cap.
- [v] **RunStart tower creation (TowerStore)**
  - Fix RunStart để **StartNewRun tạo TowerState** từ `Towers.json` (TowerStore) ngay cả khi `Buildings.json` chưa có TowerArrow.
  - Dùng `TryPickValidTowerCell` để tránh overlap Building/Site; đặt deterministic theo vòng Manhattan.
  - Rebuild `WorldIndexService` sau khi tạo tower để debug/hud nhìn thấy ngay.
- [v] **DataRegistry towers hardening**
  - Clamp `ammoMax/ammoPerShot` hợp lý:
    - `ammoMax==0` → `ammoPerShot=0`
    - `ammoMax>0` → `ammoPerShot` min 1, max `ammoMax`
  - Default `needsAmmoThresholdPct` về 0.25 nếu thiếu/0, clamp01.
  - Thêm `ValidateTowers()` đẩy lỗi vào `_loadErrors` để debug dễ thấy.

### Kết quả / Acceptance
- [v] StartNewRun chạy xong có Tower trong store/index → thấy marker bằng Gizmos trong Scene View.
- [v] Dev hook / giảm ammo → khi ammo xuống `<=25%` tạo request đúng **1 lần/cooldown**.
- [v] Khi ammo về 0 → request được ưu tiên (urgent) và notification không spam (cooldown + dedupe).
- [v] DebugTowerAmmoHUD quan sát được state và queue.

### Ghi chú / Pitfalls
- Day 25 chỉ monitor + request queue; **NPC Armory chưa tự mang ammo tới tower** (resupply pipeline là Day 26).
- Tower hiện tại là store riêng (TowerStore), không phải building prefab → “thấy tower” bằng Gizmos là đủ cho test.
- Nếu Scene/Game view không thấy: bật nút **Gizmos** và đảm bảo Debug components đã có trong scene (DebugHUDHub/DebugTowerGizmos).

### Việc tiếp theo (Day 26)
- Implement **ResupplyTower**: consume request → tạo job → executor lấy ammo từ Armory/HQ/Warehouse → add vào `TowerState.Ammo` theo chunk, ưu tiên urgent.

---

## Day 26 — ResupplyTower job + executor (Armory → Tower)

### Mục tiêu
- [x] Transporter **resupply tower từ Armory** khi tower low/empty ammo.
- [x] Provider chọn tower theo **priority (EMPTY > LOW)** + **distance tie-break**.
- [x] Executor: **pickup ammo → move → deliver to tower**.

### Đã làm
- [v] **AmmoService — ResupplyTower provider**
  - Consume request từ queue (ưu tiên `Urgent` trước `Normal`).
  - Chọn tower theo:
    - Priority: `EMPTY (ammo<=0)` > `LOW (ammo<=threshold)`.
    - Tie-break: khoảng cách Manhattan từ Armory anchor tới tower cell, sau đó theo `TowerId` để deterministic.
  - Gate tránh spam:
    - 1 in-flight job / tower (anti-dup) và 1 in-flight job / armory (nếu cấu hình).
    - Chỉ tạo job khi `ArmoryAmmo > 0` và có NPC `Workplace=Armory`.

- [v] **ResupplyTowerExecutor (NEW)**
  - Flow 2-phase:
    1) Move tới Armory → `StorageService.Remove(Armory, Ammo, amount)` (pickup).
    2) Move tới TowerCell → cộng ammo vào `TowerState.Ammo` (clamp theo `AmmoCap`) → `JobStatus.Completed`.
  - Hardening:
    - Nếu tower invalid/vanished hoặc armory không đủ ammo tại thời điểm pickup → cancel/fail deterministic + cleanup.
    - Deliver luôn clamp theo free cap để không vượt `AmmoCap`; refund remainder về Armory nếu cần.

- [v] **Job wiring**
  - Bổ sung `JobArchetype.ResupplyTower` và map vào `JobExecutorRegistry`.
  - Role gating: chỉ NPC có `WorkRoleFlags.Armory` claim job.

- [v] **Debug / Test hooks**
  - Test bằng cách drain ammo tower về low/empty để quan sát:
    - Request được enqueue đúng priority.
    - Job xuất hiện trong JobBoard.
    - Armory ammo giảm khi pickup và tower ammo tăng khi deliver.

### Kết quả / Acceptance
- [v] Tower low ammo / empty → job `ResupplyTower` xuất hiện.
- [v] NPC Armory thực hiện pickup ammo → di chuyển → deliver.
- [v] Ammo của tower tăng lại; ammo Armory giảm tương ứng.

### Ghi chú / Pitfalls
- Khi stress-test (tower bắn tiêu hao lớn, ví dụ 10 ammo/shot), có thể gặp tình huống **tower về 0 lần 2 nhưng không có job mới** do cooldown `ReqCooldownEmpty` chặn enqueue request trong một khoảng ngắn. Với gameplay thực tế (1 ammo/shot) thì ít xảy ra.
- Nếu về sau tăng DPS/burst, nên harden logic cooldown theo “state transition” (chỉ cooldown khi tower đang EMPTY liên tục; chuyển sang EMPTY phải enqueue ngay) để tránh miss request trong stress.

### Việc tiếp theo (Day 27)
- Defend/Waves wiring: đảm bảo tower resupply hoạt động ổn định trong combat loop, polish debug HUD (show in-flight jobs + cooldown timers), và cân nhắc hardening cooldown theo state transition nếu DPS cao.

---

## Day 27 — Combat map lanes/spawn gates (từ StartMapConfig)

### Mục tiêu
- [x] Parse spawn points/gates từ `StartMapConfig` (StartMapConfig v0.1).
- [x] Runtime lane table: `laneId → start cell → direction → target HQ`.
- [x] Debug: spawn 1 enemy theo lane, enemy có target.

### Đã làm
- [v] **Khởi tạo RunStartRuntime (fix root-cause HUD báo null)**
  - `GameServicesFactory` tạo `services.RunStartRuntime = new RunStartRuntime()` để RunStart có nơi cache `SpawnGates/Zones/Lanes`.
  - Khắc phục tình trạng `DebugHUDHub` hiển thị `RunStartRuntime = null` (không debug được lane).

- [v] **RunStartRuntime — lane table runtime**
  - Bổ sung `Lanes: Dictionary<int, LaneRuntime>` và struct `LaneRuntime { laneId, startCell, dirToHQ, targetHQ }`.
  - Giữ nguyên các cache cũ: `SpawnGates`, `Zones`, `BuildableRect`.

- [v] **RunStartApplier — build lane table từ StartMapConfig**
  - Clear `RunStartRuntime.Lanes` khi apply config.
  - Sau khi spawn buildings (đã có HQ), resolve `targetHQ` theo rule mới:
    - Target là **1 cell kề footprint HQ (4-neighbor, nằm ngoài footprint)**.
    - Ưu tiên cell theo hướng `dirToHQ` của gate (deterministic), fallback sang các cạnh còn lại theo thứ tự cố định nếu cell ưu tiên invalid.
    - Check occupancy bằng `GridMap.Get(cell).Kind` (không dùng `HasBuilding`).
  - Từ `cfg.spawnGates[]`: map vào `RunStartRuntime.Lanes[laneId] = LaneRuntime(...)`.

- [v] **DebugCombatLaneHUD (NEW) + wiring vào DebugHUDHub**
  - Thêm module debug trong Home tab:
    - Hiển thị `Lane count` + list lanes (start/dir/targetHQ).
    - Cho phép nhập `EnemyDefId` + `LaneId` và bấm **Spawn** để spawn 1 enemy vào `WorldState.Enemies`.
    - Log ra `(spawn cell, laneId, targetHQ, dir)` để verify.

- [v] **Fix compile/hardening nhỏ**
  - Tránh lỗi CS0136 (trùng tên biến `hqTarget` trong scope lồng nhau) bằng đổi tên biến local (vd: `hqAdjTarget`).
  - Sửa kiểm tra footprint/occupancy targetHQ theo API `GridMap` hiện tại (`CellOccupancyKind.Building/Site`).

### Kết quả / Acceptance
- [v] F1 → Home hiển thị:
  - `Day27: Combat Lanes (from StartMapConfig)` + `Lane count` và từng lane có `start/dir/targetHQ`.
- [v] Bấm **Spawn** theo lane:
  - Enemy được tạo trong `WorldState.Enemies`.
  - Console log xác nhận enemy có `laneId` + `targetHQ` đúng rule “cell kề footprint HQ”.

### Ghi chú / Pitfalls
- `RunStartRuntime` phải được khởi tạo từ `GameServicesFactory`; nếu null thì mọi cache spawnGates/lanes sẽ không có chỗ ghi.
- `targetHQ` hiện là “cell kề footprint HQ” (không bắt buộc road). Nếu về sau muốn enemy chỉ target vào road/entry cụ thể, cần thêm rule gate theo road connectivity.

### Việc tiếp theo (Day 28)
- Wiring movement/steering tối thiểu cho enemy theo lane → tiến về `targetHQ` (deterministic step), và chuẩn bị hook vào WaveDirector/spawn schedule.

---

## Day 28 — WaveDirector schedule + Defend day trigger

### Mục tiêu
- [x] Vào **Defend day** (Autumn/Winter) thì wave chạy theo defs.
- [x] Wave schedule: spawn theo interval/group.
- [x] Events: `WaveStarted` / `WaveEnded`.
- [x] Pause handling: timing wave theo **RunClock.TimeScale**.

### Đã làm
- [v] **WaveCalendarResolver (Hướng A — tránh cyclic asmdef)**
  - Giữ `IWaveCalendarResolver` ở `Game.Contracts`.
  - Implement resolver ở **Game.Boot** (cùng assembly với `DataRegistry`) để scan wave defs theo (Year/Season/Day) mà không buộc `Game.Combat` reference `DataRegistry`.
  - Expose resolver qua `GameServices.WaveCalendarResolver`.

- [v] **WaveDirector — schedule + pause**
  - `StartDayWaves(year, season, day)` resolve danh sách wave theo calendar (sort deterministic).
  - Spawn theo entry/interval; có gap giữa group và giữa wave (nếu có nhiều wave trong ngày).
  - Tick dùng `scaledDt = dt * RunClock.TimeScale`; `TimeScale <= 0` thì dừng tiến trình (pause).

- [v] **CombatService — Defend trigger + events**
  - Khi vào **Defend** hoặc đổi day trong Defend → gọi `WaveDirector.StartDayWaves(...)`.
  - Publish `WaveStartedEvent(waveId)` / `WaveEndedEvent(waveId)` qua `IEventBus`.

- [v] **DebugRunClockHUD (Day28 test) + wiring**
  - Home tab có panel RunClock: hiển thị Year/Season/Day/Phase + DayTimer.
  - Buttons: Pause/1x/2x/3x + Force `Spring/Summer/Autumn/Winter D1` + Prev/Next Day để test nhanh.
  - `DebugHUDHub` wire module này (không rải hotkey riêng).

### Kết quả / Acceptance
- [v] Force `Autumn D1` / `Winter D1` → Phase=Defend → wave auto-start theo defs.
- [v] Spawn diễn ra theo schedule; pause/resume ảnh hưởng trực tiếp tới timing wave.
- [v] `WaveStarted` và `WaveEnded` được emit đúng.

### Ghi chú / Pitfalls
- Notification trong project hiện tại chủ yếu dùng `NotificationService.Push(...)` trực tiếp; nếu muốn hiện noti cho wave thì cần bridge từ events → push (tuỳ policy UI).

### Việc tiếp theo (Day 29)
- Enemy movement/steering theo lane tới `targetHQ`, và nối combat/damage loop.

---

## Day 29 — Enemy: move + attack HQ/buildings (gizmos-only)

### Mục tiêu
- [x] Enemy di chuyển theo grid 4-dir tới target.
- [x] Enemy tới target và gây damage lên HQ / buildings.
- [x] Enemy chết cleanup.
- [x] HQ HP về 0 → Defeat.
- [x] Debug: chỉ cần hiển thị enemy bằng Gizmos trong Scene View (không cần prefab/renderer).

### Đã làm
- [v] **EnemySystem (NEW)**
  - Runtime state tối thiểu per-enemy:
    - `Hp`
    - `MoveProgress01` (tích luỹ bước theo dt)
    - `AttackCooldown` (map runtime theo `EnemyId`)
  - Resolve target theo **lane runtime table**:
    - Đọc trực tiếp `RunStartRuntime.Lanes[laneId].TargetHQ` và `DirToHQ` (single source of truth từ Day 27).
  - Movement:
    - Ưu tiên greedy step giảm Manhattan (4-dir) nếu neighbor walkable.
    - Fallback BFS minimal khi greedy không tìm được bước tiến (treat blocked as wall).
    - Clamp số bước mỗi tick để tránh “teleport” khi dt lớn.
  - Attack:
    - Khi tới `TargetHQ` → đánh HQ theo interval (dmg từ enemy def hoặc default safe).
    - Nếu bước kế tiếp bị chặn bởi building → đánh building (giảm HP) theo interval (v0.1).
    - Khi `HQ.HP <= 0` → gọi `RunOutcomeService.Defeat()`.
  - Cleanup:
    - Nếu `Hp <= 0` → remove khỏi store + clear runtime cooldown.

- [v] **CombatService wiring**
  - Khởi tạo `EnemySystem` và tick trong combat loop (cùng với WaveDirector).
  - Enemy tick dùng `RunClock.TimeScale` (pause => không tiến hành move/attack).

- [v] **DebugEnemyGizmos (NEW)**
  - Vẽ Gizmos sphere theo `CellPos` (hỗ trợ XY/XZ qua `_useXZ`).
  - Draw theo thứ tự `EnemyId` tăng dần để deterministic quan sát.
  - (Optional) Editor label `E#id` để debug nhanh.

### Kết quả / Acceptance
- [v] Debug HUD (Day27) spawn enemy theo lane → enemy di chuyển về `LaneRuntime.TargetHQ`.
- [v] Khi enemy chạm target → log “hit HQ” và HQ HP giảm.
- [v] HQ HP về 0 → Defeat được trigger.
- [v] Scene View thấy enemy bằng Gizmos (không cần prefab).

### Ghi chú / Pitfalls
- Một số layout có thể khiến enemy “kẹt”/đứng xa HQ nếu:
  - `TargetHQ` bị blocked, hoặc đường đi bị blocked bởi building/site.
  - BFS treat blocked là wall (v0.1) và chưa có cơ chế siege/phá tường đầy đủ.
  → Đây vẫn nằm trong scope Day29 (movement/attack tối thiểu); hardening nâng cấp sẽ làm ở các day combat tiếp theo.

### Việc tiếp theo (Day 30)
- Enemy combat polish: target selection (building vs HQ), siege rules nếu bị blocked lâu, và damage/aggro cho towers.

---

## Day 30 — TowerCombatSystem: target + fire + consume ammo

### Mục tiêu
- [x] Tower bắn enemy trong range và tiêu ammo.
- [x] Target selection deterministic:
  - [x] Nearest in range, tie-break theo `EnemyId` (ổn định giữa các frame).
- [x] Fire interval + damage.
- [x] Consume ammo; ammo = 0 thì không bắn.
- [x] Low-ammo request (Day25/26) vẫn hoạt động ổn định khi tower tiêu hao ammo.

### Đã làm
- [v] **TowerCombatSystem (implement)**
  - Tick theo `scaledDt = dt * RunClock.TimeScale` (Pause => không fire).
  - Duyệt tower deterministic theo `TowerId` tăng dần.
  - Target selection:
    - Scan enemies deterministic theo `EnemyId` tăng dần.
    - Pick enemy có `dist^2` nhỏ nhất trong range; tie-break `EnemyId`.
  - Fire:
    - `fireInterval = 1 / rof`; quản lý cooldown per-tower.
    - Apply damage lên `EnemyState.Hp`; nếu `Hp <= 0` → destroy enemy (cleanup).
  - Ammo:
    - Gate: `tower.Ammo < AmmoPerShot` → skip fire.
    - Khi fire: trừ ammo theo `AmmoPerShot`, clamp >= 0.
    - Mirror ammo sang `BuildingState.Ammo` (nếu tower được represent bởi Building) để HUD/debug nhất quán.

- [v] **CombatService wiring**
  - Thêm `_towers` vào loop combat.
  - Tick order khuyến nghị để test:
    1) WaveDirector spawn
    2) TowerCombatSystem fire
    3) EnemySystem move/attack

- [v] **Debug rõ ràng (SceneView Gizmos)**
  - `DebugTowerCombatGizmos`:
    - Vẽ wire-range quanh tower.
    - Vẽ line tới target hiện tại (theo cùng rule deterministic).
    - Giúp verify nhanh: tower chỉ bắn khi có ammo và target trong range.

### Kết quả / Acceptance
- [v] Khi enemy vào range:
  - Tower chọn đúng target (nearest, tie EnemyId) và bắn theo interval.
  - Enemy bị giảm HP và chết (remove khỏi store).
- [v] Ammo giảm đúng theo số phát bắn; khi ammo về 0 tower ngừng bắn.
- [v] Khi ammo giảm xuống low/empty:
  - AmmoService vẫn enqueue request theo threshold/cooldown (Day25) và ResupplyTower vẫn có thể refill (Day26) nếu Armory có ammo.
- [v] Debug:
  - SceneView thấy range + line target, dễ đối chiếu hành vi tower khi spawn enemy theo lane.

### Ghi chú / Pitfalls
- Nếu tower được spawn như “TowerStore riêng” (không mapping 1-1 với Building), mirror ammo sang Building chỉ là best-effort; source of truth vẫn là `WorldState.Towers`.
- Với dt lớn / timeScale cao: cần clamp cooldown/step để tránh “burst fire” quá nhanh (hiện đã gate theo cooldown per-tower).

### Việc tiếp theo (Day 31)
- Tower ↔ Enemy polish:
  - Ưu tiên target theo threat (vd: enemy gần HQ nhất) nếu spec yêu cầu.
  - Thêm projectile/FX (nếu mở scope) và damage apply rõ ràng lên Building durability khi enemy siege.

