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

### Việc tiếp theo (Day 7)
- Storage snapshots + ResourceFlow (Harvest/Haul basic)
- JobScheduler: jobs dựa vào WorldIndex (nhận diện warehouse/producer)
