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

## Day 3 — GridMap + Road Placement Tool (Orthogonal N/E/S/W)

### Mục tiêu
- [x] Có GridMap occupancy chạy ổn (bounds + get/set O(1)).
- [x] Có road placement cơ bản theo cell (grid-orthogonal N/E/S/W).
- [x] Có debug tool dùng New Input System (InputAction runtime, không sửa InputActions asset) để đặt road nhanh.
- [x] Có feedback khi thao tác sai (out-of-bounds / blocked), dùng NotificationService (cooldown theo key).

### Đã làm
- [v] Wire services.GridMap = new GridMap(64,64) trong boot graph (GameServicesFactory/Create).
- [v] Wire services.PlacementService = new PlacementService(...) và dùng PlaceRoad/CanPlaceRoad để đặt road.
- [v] Tạo DebugRoadTool (runtime InputAction):
  - `R`: bật/tắt road tool
  - `LMB`: đặt road theo cell dưới chuột (cell-based, không diagonal)
- [v] Thêm cơ chế toggle khi click cùng cell (đặt/xoá road) để debug nhanh.
- [v] Hiển thị road bằng Gizmos (vẽ các cell road) để quan sát trực tiếp trong Scene view.
- [v] Notification khi fail:
  - Road_OutOfBounds (cooldown)
  - Road_Blocked (cooldown)

### Kết quả / Acceptance
- [v] Play → `R` bật tool → click đặt road được, road hiển thị bằng Gizmos.
- [v] Click lại cùng cell → road bị xoá (toggle) và gizmos biến mất (đúng hành vi debug).
- [v] Click ngoài bounds → có notification (không spam nhờ cooldown).
- [v] Click cell blocked → có notification (không spam nhờ cooldown).

### Ghi chú / Pitfalls
- Nếu road click bị “lệch ô” cần kiểm tra mapping:
  - _useXZ (grid XY map sang world XZ)
  - _planeY (độ cao plane raycast) để khớp mặt đất.
- PlacementService dùng interface IPlacementService, tránh cast concrete trong debug tool.
- Toggle road là hành vi debug hợp lệ Day 3; gameplay tool sẽ tách “delete/bulldozer” sau.

### Việc tiếp theo (Day 3)
- Building footprint + validate overlap + commit vào WorldState + occupancy building.

---

## Day 4 — Building Placement (Footprint + Validation + Commit)

### Mục tiêu
- [x] Load **Building definitions** từ `DefsCatalog.Buildings` (TextAsset JSON) vào `DataRegistry`.
- [x] Implement **building footprint placement** (multi-cell).
- [x] Validate placement:
  - [x] Out-of-bounds
  - [x] Overlap road
  - [x] Overlap building
  - [x] Blocked by site
- [x] Commit building (Construction/Site flow):
  - [x] Convert driveway (entry) → road (nếu cần)
  - [x] Tạo **BuildOrder** (PlaceNew)
  - [x] Tạo **BuildSiteState** + occupy footprint = `Site`
  - [x] Tạo **placeholder BuildingState** (`IsConstructed=false`)
  - [x] `BuildingPlacedEvent` chỉ publish khi **xây xong** (BuildOrder complete)
- [x] Có **DebugBuildingTool** (New Input System) để test placement nhanh.
- [x] Fail có **notification spam-safe** (cooldown theo key).

---

### Đã làm
- [v] **DataRegistry**
  - Parse JSON từ `DefsCatalog.Buildings.text` (wrapper `{ buildings: [...] }`).
  - Map dữ liệu vào dictionary `_buildings[id] → BuildingDef`.
  - Validate dữ liệu tối thiểu (`id`, `sizeX`, `sizeY`, `baseLevel`).
  - Log số lượng building defs đã load khi boot.
- [v] **PlacementService**
  - Implement `ValidateBuilding(defId, anchor, rotation)`:
    - Loop footprint theo `SizeX/SizeY` từ `BuildingDef`.
    - Fail `OutOfBounds` nếu cell ngoài map.
    - Fail `Overlap` nếu cell là `Road` hoặc `Building`.
    - Fail `BlockedBySite` nếu cell là `Site`.
  - Implement `CommitBuilding(...)` (route qua BuildOrders):
    - Validate trước khi commit.
    - Convert driveway (entry cell) → road nếu cần (len=1).
    - Gọi `_buildOrders.CreatePlaceOrder(...)` để tạo:
      - `BuildSiteState` + occupy footprint = `Site`
      - placeholder `BuildingState` (`IsConstructed=false`)
    - `BuildingPlacedEvent` sẽ được publish **khi BuildOrder complete** (không publish tại commit).
- [v] **GridMap**
  - Bổ sung guard `IsInside` cho `SetBuilding/SetSite/Clear...` để tránh crash khi thao tác out-of-bounds.
- [v] **DebugBuildingTool**
  - `B`: bật/tắt building tool.
  - `1..5`: chọn building def id (HQ / House / Farm / Lumber / Warehouse).
  - Hover chuột: preview footprint bằng Gizmos (wire).
  - LMB: validate + commit building.
  - Fail → notification theo key (`Build_OutOfBounds`, `Build_Overlap`, `Build_BlockedBySite`, …) với cooldown.
- [v] **Boot graph**
  - `GameServicesFactory` tạo `DataRegistry(catalog)` → auto-load Buildings.
  - `PlacementService` dùng `DataRegistry.GetBuilding(id)` để lấy footprint.

---

### Kết quả / Acceptance
- [v] Play Mode → Console log `[DataRegistry] Loaded Buildings: X` xác nhận JSON load thành công.
- [v] Hover preview footprint đúng kích thước theo def (`SizeX/SizeY`).
- [v] Click place thành công → occupancy grid chuyển sang `Site` cho toàn footprint (construction started).
- [v] Sau ~X giây (work time) → site biến mất, occupancy chuyển sang `Building` và `BuildingPlacedEvent` được publish (construction completed).
- [v] Không thể đặt building:
  - Chồng road → fail `Overlap` + notification.
  - Chồng building → fail `Overlap` + notification.
  - Chồng site → fail `BlockedBySite` + notification.
  - Ra ngoài map → fail `OutOfBounds` + notification.
- [v] Spam click → notification không spam (cooldown theo key).

---

### Ghi chú / Pitfalls
- `DefsCatalog.Buildings` phải là **TextAsset JSON có wrapper object**, Unity `JsonUtility` không parse tốt array top-level.
- `DefId` trong DebugBuildingTool phải **khớp chính xác** với `id` trong JSON.
- `PlacementFailReason` hiện dùng `Overlap` chung cho road/building; có thể tách chi tiết hơn ở phase sau nếu cần UX tốt hơn.
- Mapping chuột → cell phụ thuộc `_useXZ` và `_planeY`; camera nghiêng cần `_planeY` đúng độ cao ground để tránh lệch ô.
- Day 4 đã chuyển sang **construction/site flow**: commit chỉ tạo Site + placeholder building; `IsConstructed=true` chỉ khi BuildOrder complete.

---

### Việc tiếp theo (Day 5)
- EntryCell + Driveway len = 1:
  - Validate “entry có road trong bán kính 1 cell”.
  - Chọn driveway cell gần nhất (deterministic).
  - Commit driveway → convert thành road.
  - Reject placement nếu không nối road.


---

# Seasonal Bastion — VS#1 Day 5 Log (Entry + Driveway len=1)

> File này được viết để **match format** của `VS1_DEV_LOG.md` (Mục tiêu / Đã làm / Kết quả / Ghi chú / Việc tiếp theo),
> đồng thời tích hợp checklist Day 5 để bạn tick theo tiến độ.

---

## Day 5 — EntryCell + Driveway (len = 1) + Debug Preview

### Mục tiêu
- [x] Building **bắt buộc** phải nối road thông qua **Entry + Driveway dài 1 cell**.
- [x] Validate fail đúng `PlacementFailReason.NoRoadConnection`.
- [x] Driveway chọn **deterministic** và commit đúng: **1 cell** và cell đó **trở thành road**.
- [x] DebugBuildingTool:
  - [x] Rotate (Q/E) để đổi hướng entry.
  - [x] Preview footprint + preview driveway/entry cell rõ ràng.
  - [x] Notification fail rõ ràng, không spam (cooldown theo key).
- [x] Regression: không làm vỡ Day 3 (Road tool) và Day 4 (Footprint placement).

---

### Đã làm
#### 1) Placement Rules (Core)
- [v] Entry cell theo rotation (Dir4)
  - [v] N → entry ở cạnh Bắc (ngoài footprint)
  - [v] E → entry ở cạnh Đông
  - [v] S → entry ở cạnh Nam
  - [v] W → entry ở cạnh Tây
- [v] Validate Road Connectivity
  - [v] Pass nếu có road ở **EntryCell hoặc N/E/S/W** của EntryCell
  - [v] Fail nếu không có road gần entry → `NoRoadConnection`
  - [v] EntryCell không được là Building/Site; được là Empty/Road
- [v] Driveway (len=1)
  - [v] SuggestedRoadCell = driveway target
  - [v] Commit: nếu driveway target chưa là road → SetRoad(true) + publish RoadPlacedEvent

#### 2) Commit Flow (Construction/Site)
- [v] Commit driveway road (nếu cần) và publish `RoadPlacedEvent`
- [v] Commit building (không đặt trực tiếp):
  - [v] `PlacementService.CommitBuilding()` gọi `BuildOrderService.CreatePlaceOrder(...)`
  - [v] Tạo `BuildSiteState` + occupy footprint = `Site` (construction started)
  - [v] Tạo placeholder `BuildingState` (`IsConstructed=false`)
  - [v] Khi BuildOrder complete → clear Site, set occupancy = `Building`, publish `BuildingPlacedEvent` (construction completed)

#### 3) DebugBuildingTool
- [v] Input
  - [v] Toggle tool (B)
  - [v] Select def (1–5)
  - [v] Rotate (Q/E)
  - [v] Click place (LMB)
- [v] Preview
  - [v] Preview footprint đúng sizeX/sizeY
  - [v] Preview driveway/entry cell (SuggestedRoadCell)
  - [v] Màu/hiển thị phân biệt OK vs Fail
- [v] Notification
  - [v] Fail NoRoadConnection có message rõ
  - [v] Không spam (cooldown theo key)

---

### Kết quả / Acceptance (Bắt buộc pass)
#### Case hợp lệ
- [v] Có road sát cạnh entry → đặt building thành công
- [v] EntryCell trống, road ở neighbor → driveway được tạo (1 cell) và trở thành road
- [v] EntryCell đã là road → không tạo road mới (không nhân đôi)

#### Case không hợp lệ
- [v] Không có road gần entry → fail `NoRoadConnection`
- [v] EntryCell là Site → fail `BlockedBySite`
- [v] EntryCell là Building → fail `Overlap`

---

### Ghi chú / Pitfalls
- Entry hiện suy ra từ `rotation` (chưa có EntryPoint per-building). Đây là lựa chọn tối thiểu cho VS#1.
- Driveway len=1 không dùng pathfinding; chỉ commit 1 cell road gần entry theo rule.
- Nếu click/hover bị lệch ô:
  - kiểm tra mapping `_useXZ` + `_planeY` trong debug tool
- Nếu notification spam:
  - đảm bảo dùng key cố định (`Build_NoRoad`, …) + cooldown > 0

---

### Regression Check (Không được vỡ)
- [v] Day 3: Road placement vẫn hoạt động (toggle, gizmos, notify)
- [v] Day 4: Footprint validation vẫn đúng (bounds/overlap/site)
- [v] GridMap không crash khi click ngoài bounds
- [v] Không phát sinh road ngoài dự kiến (chỉ driveway 1 cell khi place building)

---

### Day 6 — WorldIndex (derived lists) + Debug HUD

#### 1) WorldIndexService (IWorldIndex)
- [v] Implement `RebuildAll()`
  - [v] Quét `WorldState.Buildings` và phân loại theo BuildingDef tags
  - [v] Đảm bảo **deterministic order**: sort theo `id.Value` (EntityStore dùng Dictionary)
  - [v] Chỉ index building đã hoàn thành (`IsConstructed=true`)
- [v] Implement incremental hooks
  - [v] `OnBuildingCreated(BuildingId id)` (idempotent, không nhân đôi)
  - [v] `OnBuildingDestroyed(BuildingId id)` (cleanup list)
- [v] Fallback classification cho VS#1 nếu JSON chưa có tags
  - HQ → Warehouse (+HQ)
  - Farm/Lumber → Producer
  - Warehouse → Warehouse

#### 2) DataRegistry — hỗ trợ tags trong Buildings.json
- [v] Parse thêm các field tùy chọn: `isHQ/isWarehouse/isProducer/isForge/isArmory/isTower`
- [v] `Buildings.json` VS#1: gắn tags cơ bản cho HQ/Farm/Lumber/Warehouse

#### 3) Wiring / Hook
- [v] `GameServicesFactory`
  - [v] `WorldIndex.RebuildAll()` lúc init
  - [v] Subscribe `BuildingPlacedEvent` → `WorldIndex.OnBuildingCreated(...)`
- [v] `GameLoop.StartNewRun()`
  - [v] Rebuild index khi bắt đầu run (an toàn kể cả world rỗng)

#### 4) Debug overlay
- [v] `DebugWorldIndexHUD` (OnGUI)
  - [v] Hiển thị count: Warehouses / Producers / Forges / Armories / Towers
  - [v] Hiển thị danh sách id (giới hạn) để debug nhanh
  - [v] Toggle HUD bằng phím **I** (có notification)

### Patch: WorldIndex thêm nhóm Houses (Contract change)

**Quyết định:** Thêm `Houses` vào `IWorldIndex` như một nhóm index chính thức (giống `Warehouses/Producers`).
- Lý do: House là nền tảng dài hạn cho Population/HousingCap, điều kiện unlock, nhu cầu/hạnh phúc… Tránh phải scan toàn bộ buildings ở nhiều hệ thống → ổn định và ít bug state.

**Thay đổi chính:**
- `IWorldIndex`: thêm `IReadOnlyList<BuildingId> Houses`.
- `BuildingDef` + `DataRegistry`: hỗ trợ tag `isHouse` (`BuildingDef.IsHouse`), parse từ `Buildings.json`.
- `Buildings.json`: gắn `isHouse: true` cho Def `House`.
- `WorldIndexService`:
  - index **chỉ** buildings đã hoàn thành (`IsConstructed == true`), không index Site/placeholder.
  - list `Houses` deterministic (sort theo `id.Value`) + idempotent (chống add trùng).
- `DebugWorldIndexHUD`: hiển thị `Houses` count + list IDs (toggle phím `I`).

**Lưu ý compatibility:**
- Đây là thay đổi CONTRACT so với PART25 (có chủ đích). Các hệ thống đang dùng `IWorldIndex` cần compile lại theo signature mới.

**Manual Test:**
1) Place House → chờ xây xong (construction/site flow).
2) Bật HUD (phím `I`) → `Houses` tăng đúng số lượng House đã constructed.
3) Đặt thêm Warehouse/Farm/Lumber → các nhóm còn lại vẫn đếm đúng, không ảnh hưởng.

---

### Day 7 – Buffer Day (Stabilize + EditMode Tests)

**Mục tiêu (PART27 Day 7):** Ổn định hệ thống + bổ sung test cơ bản cho các hành vi đã khóa (placement/driveway/construction + notifications).

### Việc đã làm
1) **Setup Test Runner / NUnit cho asmdef test**
- Fix lỗi `NUnit` không nhận: bật **Test Assemblies** + thêm `optionalUnityReferences: ["TestAssemblies"]` cho asmdef test (EditMode).
- Fix lỗi constructor: `NotificationService` yêu cầu `IEventBus` → trong tests tạo `new NotificationService(bus)` thay vì ctor rỗng.

2) **Thêm EditMode tests (không cần Scene/MonoBehaviour)**
- File test chính: `Assets/_Game/Tests/EditMode/Day7_StabilityTests.cs`
- Các test cover:
  - `Notification_Max3_NewestFirst` (cap 3, newest-first)
  - `Notification_Dedupe_MoveToTop_StillCap3` (dedupe theo key + move-to-top + vẫn cap 3)
  - `Placement_NoRoadConnection_WhenNoRoadInEntryCross` (rule entry/driveway: không có road trong cross => fail NoRoadConnection)
  - `Placement_Commit_ConvertsDrivewayToRoad_AndPublishesRoadPlaced` (commit convert driveway -> road + publish RoadPlacedEvent)
  - `Placement_NoOverlap_BuildingOnBuilding` (không cho overlap footprint lên building đã có)
  - `BuildOrder_Completes_ClearsSite_SetsBuildingOccupancy_PublishesBuildingPlaced`
    - Construction/Site flow: create -> footprint là Site
    - tick đủ thời gian -> clear Site, set occupancy Building, `IsConstructed=true`, publish `BuildingPlacedEvent`

### Kết quả chạy test
- **EditMode: PASS sạch** (0 failed), chạy nhanh (~0.03s).
- Đã xuất file kết quả `TestResults_20260119_102205.xml` (Passed).

### Acceptance Day 7
- [v] Tất cả EditMode tests PASS
- [v] Console sạch khi Run All tests
- [v] Không thay đổi scope gameplay, chỉ harden bằng test (đúng “buffer day”)

---

## Day 8 — NPC Store + Manual Assignment UI (Debug) + Debug Tools 2D XY ổn định (Part 27)

### Mục tiêu (theo PART27 – Day 8)
- [x] Implement `NpcState` + `NpcStore`.
- [x] Spawn NPC bằng phím `P` tại HQ cell.
- [x] UI debug để:
  - [x] Chọn NPC
  - [x] Click building để assign workplace
  - [x] Fire `NPCAssignedEvent`
- [x] Acceptance:
  - [x] Spawn 3 NPC → assign 1 Farm, 1 Lumber, 1 Warehouse
  - [x] Hiển thị danh sách NPC + trạng thái unassigned/assigned trong debug list

---

### Đã làm
#### 1) NPC Store + State (core)
- [v] `NpcState` có các field tối thiểu phục vụ VS#1: `Id`, `DefId`, `Cell`, `Workplace`, `CurrentJob`, `IsIdle`.
- [v] `NpcStore` hỗ trợ:
  - Create / Exists / Get / Set
  - Enumerate `Ids` để render debug list
- [v] Wire vào `WorldState`/`IWorldState` (nếu chưa có thì dùng services/world hiện có).

#### 2) DebugNpcTool — Spawn + Select + Assign workplace (manual)
- [v] `N`: toggle NPC tool (ON/OFF).
- [v] `P`: spawn NPC tại HQ anchor cell (fallback: chọn HQ building id nhỏ nhất).
- [v] HUD debug hiển thị:
  - danh sách NPC (giới hạn hiển thị để không spam UI)
  - số lượng `Unassigned`
  - NPC đang được select
  - hover cell + occupancy (Building/Site/Empty)
- [v] `LMB`: assign workplace:
  - Chỉ assign khi click vào cell có `CellOccupancyKind.Building` và có `BuildingId`.
  - Set `NpcState.Workplace = buildingId`, reset job state tối thiểu.
  - Publish `NPCAssignedEvent(npcId, buildingId)`.
  - Push notification spam-safe.

#### 3) Bổ sung thêm ngoài PART27 (đã làm thêm để ổn định lâu dài)
**3.1. Chuẩn hoá 2D XY mapping (_useXZ=false)**
- [v] Fix triệt để sai mặt phẳng khi raycast mouse:
  - XY dùng `Plane(Vector3.forward, z = planeZ)` (không hardcode z=0).
  - Center cell XY dùng `z = planeZ`.
- [v] Fix gizmo vẽ đúng XY:
  - XY: ô cell “mỏng theo Z” (không còn kiểu XZ mỏng theo Y).

**3.2. GameView mouse → SceneView gizmos/label (Play Mode)**
- [v] `MouseCellSharedState` làm “bridge state” runtime.
- [v] `DebugGameViewMouseCellTracker`:
  - Đọc mouse position trong **Game View** (Play Mode)
  - Convert → cell/center theo mapping hiện tại
  - Ghi vào `MouseCellSharedState` để Scene View có thể đọc
- [v] (Optional/khuyến nghị) `SceneViewMouseCellGizmo` (Editor):
  - Khi Play Mode: ưu tiên vẽ theo `MouseCellSharedState` (tức là theo chuột ở Game View)
  - Khi Edit Mode: fallback ray theo chuột Scene View
  - Vẽ wire cube + label “Cell (x,y)” đúng XY

**3.3. Chuẩn hoá lại path + apply patch workflow**
- [v] Xác nhận folder debug thực tế trong repo: `Assets/Game/Debug/` (không phải `Assets/_Game/DebugTools/`)
- [v] Fix các lỗi apply patch do lệch đường dẫn (No such file or directory) bằng cách kiểm tra `git ls-files`.

---

### Kết quả / Acceptance
- [v] Play Mode:
  - Bấm `P` spawn được NPC tại HQ.
  - HUD hiển thị đúng số lượng NPC và số lượng unassigned.
  - Select NPC trong HUD → click lên building đã constructed → NPC được assign workplace.
  - `NPCAssignedEvent` được publish (phục vụ bước Job/Harvest/Haul về sau).
- [v] 2D XY:
  - Hover/click không còn lệch ô do sai plane.
  - Gizmos/label thể hiện đúng cell XY (không còn “ô kiểu XZ”).
- [v] Play Mode:
  - Di chuột trong Game View → Scene View vẫn vẽ gizmo/label theo đúng cell dưới chuột (khi có tracker + editor gizmo).

---

### Ghi chú / Pitfalls
- Với 2D (XY), bắt buộc set:
  - `_useXZ = false`
  - `_planeZ` đúng Z của tilemap/ground (thường là `0`)
  - Camera orthographic vẫn OK; nếu map ở Z khác 0 thì phải set `_planeZ` tương ứng.
- DebugNpcTool assign chỉ nhận `CellOccupancyKind.Building`; nếu click vào `Site`/`Road`/`Empty` sẽ warn (đúng kỳ vọng).
- Nếu Scene View không vẽ theo chuột Game View:
  - đảm bảo có `DebugGameViewMouseCellTracker` trong scene
  - đảm bảo `SceneViewMouseCellGizmo` nằm trong folder `Editor/` và đang enabled

---

### Việc tiếp theo (Day 9 — theo PART27)
- Implement Storage baseline:
  - storage amounts + caps trong `BuildingState` theo `BuildingDef`
  - `StorageService` (HQ/Warehouse không nhận Ammo)
  - Debug UI: chọn building → xem snapshot storage
  
---

## Day 9 — Storage Baseline (Caps L1/L2/L3) + Debug Storage HUD + AutoPlaceOnPlay

### Mục tiêu (theo PART27 – Day 9)
- [x] Implement Storage baseline:
  - [x] `BuildingState` có amounts theo resource.
  - [x] `BuildingDef` có caps theo level (L1/L2/L3).
  - [x] `StorageService`:
    - [x] `GetStorage` snapshot (amount + cap)
    - [x] `Add/Remove` clamp theo cap
    - [x] `GetTotal` tổng theo resource
    - [x] Rule: **HQ/Warehouse không chứa Ammo** (v0.1)
- [x] Debug:
  - [x] `DebugStorageHUD` hiển thị snapshot storage theo building.
  - [x] Có nút debug **Add resource** vào building.
  - [x] Fix input theo **New Input System** (không dùng `UnityEngine.Input`).

### Đã làm

#### 1) Storage caps (LOCKED data)
- [v] Bổ sung caps theo bảng **Local Storage Caps (L1/L2/L3)** vào `Buildings.json`:
  - Farm/Food: 30/60/90
  - Lumber/Wood: 40/80/120
  - Quarry/Stone: 40/80/120
  - IronHut/Iron: 30/60/90
  - Forge/Ammo: 50/100/150
  - Armory/Ammo: 300/600/1000
  - Warehouse: Wood/Food/Stone/Iron = 300/600/1000 each
  - HQ (core only): Wood/Food/Stone/Iron = 120/180/240 each, **Ammo = 0**
- [v] Giữ rule: HQ **không chứa Ammo** để rõ pipeline v0.1.

#### 2) `StorageService` (core)
- [v] Map theo interface hiện tại (`IWorldState`, `IDataRegistry`, `IEventBus`) và implement:
  - `CanStore(building, type)`:
    - check cap > 0 theo level
    - hard-rule: HQ/Warehouse **forbid ammo**
    - harden: ammo chỉ hợp lệ ở Forge/Armory (nếu def có tag)
  - `GetCap/GetAmount/GetStorage`
  - `Add/Remove` clamp theo cap/amount, trả về số thực tế add/remove
  - `GetTotal(type)` deterministic (iterate `_w.Buildings.Ids`)
- [v] Publish `ResourceDeliveredEvent` khi add vào **HQ/Warehouse** (phục vụ UI/notification về sau).

#### 3) `DebugStorageHUD` (debug UX)
- [v] Fix input: bỏ `UnityEngine.Input.GetKeyDown` → dùng `UnityEngine.InputSystem.Keyboard`.
- [v] Tái cấu trúc `OnGUI()`:
  - 1 panel duy nhất (không chồng `BeginArea`)
  - status debug: enabled/toggleKey + `Keyboard.current` + `MouseCellSharedState.HasValue`
- [v] Fix race boot:
  - Lazy resolve `GameBootstrap.Services` khi `_gs == null` (tránh Awake order).
- [v] Thêm “Lock building” để bấm UI không mất hover:
  - phím `L` lock/unlock target building
  - UI render theo `target` (locked/hover), hover chỉ làm info phụ
- [v] Thêm cụm nút debug **Add resource**:
  - nhập amount (TextField) + preset 10/50/100
  - buttons: Wood/Food/Stone/Iron/Ammo
  - gọi `StorageService.Add()` + log `added=...`
  - clamp + rule ammo(HQ/Warehouse) tự enforced (added=0).

#### 4) Auto place khi Play (tool runtime)
- [v] Tạo `AutoPlaceOnPlay` (MonoBehaviour debug) để đặt **RoadLine** + **Buildings** khi nhấn Play:
  - Road place qua `PlacementService.PlaceRoad/CanPlaceRoad`
  - Building commit qua `PlacementService.ValidateBuilding/CommitBuilding`
  - In log fail reason + `suggestedRoadCell` để chỉnh layout nhanh
- [v] Fix serialization Inspector:
  - dùng `Vector2Int` cho From/To/Anchor (Inspector-friendly) → convert sang `CellPos` runtime.

### Kết quả / Acceptance
- [v] Storage snapshot hiển thị đúng cap/amount theo building đang chọn (hover hoặc locked).
- [v] Add resource bằng nút trong HUD:
  - không vượt cap (clamp chuẩn)
  - HQ/Warehouse không nhận Ammo (added=0)
- [v] HUD không còn mất target khi rê chuột sang UI (lock bằng `L`).
- [v] Không còn lỗi `UnityEngine.Input` khi project dùng New Input System.
- [v] Auto place road/buildings chạy khi Play để test nhanh pipeline placement + storage.

### Ghi chú / Pitfalls
- Nếu HUD báo `MouseCellSharedState.HasValue = false`:
  - cần có `DebugGameViewMouseCellTracker` trong scene để cập nhật cell hover.
- Nếu `_gs == null` lúc đầu:
  - do Awake order; đã fix bằng lazy resolve (sau 1–2 frame sẽ tự có).
- Khi HQ size lớn (ví dụ 5x5), layout auto place dễ gặp `Overlap`:
  - dựa `suggestedRoadCell` trong log để chỉnh road/anchor nhanh.

### Việc tiếp theo (Day 10)
- Resource flow/transfer baseline:
  - transfer giữa producer local storage → warehouse/HQ
  - chuẩn bị cho CarryAmount + Haul jobs (Day 11–12).

---

## Day 10 — ResourceFlowService (Pick Source/Dest) + Transfer Atomic + Debug Flow Buttons

### Mục tiêu (theo PART27 – Day 10)
- [x] Implement `TryPickSource` / `TryPickDest`:
  - [x] Deterministic “nearest” theo Manhattan distance
  - [x] Tie-break theo `BuildingId.Value` (ascending)
- [x] Implement `Transfer(src, dst, type, amount)` với atomic semantics:
  - [x] Không âm resource
  - [x] Respect cap (clamp)
  - [x] Không mất tài nguyên (refund phần dư)
- [x] Acceptance:
  - [x] Nhiều warehouse → pick nearest đúng
  - [x] Không overflow / không negative

### Đã làm

#### 1) `ResourceFlowService` (core)
- [v] Tạo `ResourceFlowService.cs` implement `IResourceFlowService`:
  - `TryPickSource(from, type, minAmount, out StoragePick)`:
    - duyệt candidate set theo resource type
    - filter: building tồn tại + `IsConstructed` + `StorageService.CanStore` + amount >= minAmount
    - chọn best theo: dist Manhattan nhỏ nhất, tie-break `BuildingId.Value`
  - `TryPickDest(from, type, minSpace, out StoragePick)`:
    - filter: `CanStore` + cap > 0 + (cap - amount) >= minSpace
    - chọn best theo: dist Manhattan, tie-break `BuildingId.Value`
  - `Transfer(src, dst, type, amount)`:
    - remove từ src (clamp)
    - add vào dst (clamp cap)
    - refund remainder về src (atomic semantics)
- [v] Candidate sets v0.1:
  - Basic (Wood/Food/Stone/Iron):
    - Source: Producers + Warehouses (HQ được treat như warehouse)
    - Dest: Warehouses (HQ included)
  - Ammo:
    - Source: Forges + Armories
    - Dest: Armories only
- [v] Determinism:
  - dùng WorldIndex lists đã sorted theo `BuildingId.Value`
  - merge lists theo id để giữ order ổn định

#### 2) Wiring / Services
- [v] Xác nhận `GameServicesFactory` đã wire:
  - `StorageService`
  - `ResourceFlowService = new ResourceFlowService(WorldState, WorldIndex, StorageService)`
- [v] Không thay đổi boot/tick order.

#### 3) Debug test buttons (Game View)
- [v] Bổ sung vào `DebugStorageHUD` nhóm nút test Day10:
  - Pick Dest: `Pick Dest (Wood/Food/Ammo)` → log `id/dist`, lưu `LastDest`
  - Pick Source: `Pick Src (Wood/Food/Ammo)` → log `id/dist`, lưu `LastSrc`
  - Transfer:
    - `Transfer Wood (Target->LastDest)`
    - `Transfer Wood (LastSrc->Target)`
- [v] “FromCell” cho pick:
  - ưu tiên hover cell (MouseCellSharedState)
  - fallback theo anchor của building target
- [v] Giữ cơ chế lock target (phím `L`) để bấm UI không mất selection.

### Kết quả / Acceptance
- [v] Pick nearest warehouse/armory đúng theo Manhattan distance.
- [v] Tie-break ổn định theo `BuildingId.Value` khi dist bằng nhau.
- [v] Transfer không làm âm resource, không vượt cap, không mất phần dư (refund).
- [v] Có debug buttons để verify trực tiếp trong Play mode (không cần viết test runner).

### Ghi chú
- Nếu `PickDest` trả NONE:
  - không có building constructed phù hợp hoặc không còn space theo resource.
- Ammo không vào HQ/Warehouse là expected (rule v0.1 từ Day9).

---

## Day 11 — Claims + JobBoard Hardening + DebugHub (giảm HUD chồng nhau)

### Mục tiêu (theo PART27 – Day 11)
- [x] Claims:
  - [x] `ClaimService.TryAcquire` ngăn 2 NPC claim cùng key.
  - [x] `ReleaseAll(npcId)` hoạt động (không leak) để tránh softlock.
  - [x] Debug button “Release all claims for selected NPC”.
- [x] JobBoard:
  - [x] Queue theo workplace.
  - [x] `TryPeekForWorkplace()` deterministic (FIFO) + không kẹt bởi job stale.
  - [x] Debug hiển thị `Jobs in workplace queue` + `Peek`.
- [x] Debug UX:
  - [x] Giảm “HUD chồng nhau” → gom về 1 Hub (Mức 2), tránh lắp phím.

---

### Đã làm

#### 1) ClaimService — ReleaseAll chống softlock
- [v] Implement `ReleaseAll(NpcId owner)`:
  - Duyệt map claim, collect keys thuộc owner → remove sau (tránh modify khi đang enumerate).
  - Không throw/không leak claim.
- [v] Regression: `TryAcquire/Release` giữ hành vi cũ, chỉ bổ sung “panic button” ReleaseAll.

#### 2) JobBoard — skip stale, peek không kẹt
- [v] Harden `JobBoard`:
  - Thêm `CleanFront(queue)`:
    - Dequeue các job id không còn tồn tại / status stale (Completed/Failed/Cancelled).
  - `TryPeekForWorkplace()`:
    - CleanFront trước khi peek
    - FIFO deterministic, không stuck vì stale ở đầu hàng.
  - `CountForWorkplace()`:
    - CleanFront trước khi trả count (count phản ánh hàng đợi “live”).

#### 3) DebugNpcTool — UI Day11 (Selected NPC)
- [v] Hiển thị:
  - `ActiveClaimsCount`
  - `Workplace` của NPC selected
  - `Jobs in workplace queue` + `Peek job`
- [v] Thêm button:
  - `Release All Claims (Selected NPC)` → gọi `ClaimService.ReleaseAll(selectedNpc)`
  - Push notification xác nhận (spam-safe).

#### 4) DebugHUDHub (Mức 2) — 1 panel duy nhất, tránh lắp phím
- [v] Tạo Hub quản lý mode/tabs:
  - Toggle UI + chuyển mode bằng F-keys (không dùng lại các phím debug rải rác).
  - Exclusive mode: chỉ 1 tool active tại 1 thời điểm.
- [v] Chuyển các HUD/Tool sang “hub-controlled”:
  - Tắt standalone OnGUI khi Hub bật.
  - Disable các toggle keys cũ (B/R/N/I/S/H/M...) để tránh chồng input.
  - Guard input theo `_enabled` (đặc biệt NPC spawn P chỉ chạy khi NPC tool active).
- [v] Fix GUI mismatch cho Storage/NPC khi render trong Hub:
  - `DrawContent()` không gọi `GUILayout.EndArea()` nội bộ (Hub không BeginArea).
  - Standalone OnGUI có guard `if (DebugHubState.Enabled) return;`.

---

### Kết quả / Acceptance
- [v] Có thể bấm `Release All Claims` cho NPC selected mà không crash.
- [v] `TryPeekForWorkplace` không kẹt do job stale ở đầu queue.
- [v] Debug UI gọn: chỉ còn 1 Hub, không còn nhiều panel chồng nhau.
- [~] Hiện tại `ActiveClaimsCount = 0` và `Jobs in workplace queue = 0` là expected vì Day12 mới bắt đầu enqueue jobs / tạo claim thực tế qua job pipeline.

---

### Ghi chú / Pitfalls
- Nếu muốn “pass acceptance enqueue 10 jobs” ngay Day11:
  - cần thêm debug button enqueue jobs (để Day12 làm tiếp theo kế hoạch).
- Khi render vào Hub: tuyệt đối không `EndArea()` trong hàm content; Begin/End chỉ ở wrapper.

---

### Việc tiếp theo (Day 12)
- Bắt đầu pipeline job thực:
  - Enqueue jobs theo workplace (Harvest/Haul/Builder fetch…).
  - Claim keys thực sự (storage slot / job id / interest tile).
  - Debug button enqueue jobs để verify `Peek/Count` + claim contention.

  ---
