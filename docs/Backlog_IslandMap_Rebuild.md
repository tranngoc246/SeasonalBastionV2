# Backlog triển khai Island Map Rebuild

Mục tiêu: chuyển map hiện tại sang mô hình hòn đảo quy mô lớn hơn, gameplay-safe, data-driven, có thể triển khai theo sprint.

## Epic MAP-00, Discovery và chốt spec

### MAP-001, Chốt target size giai đoạn 1
- Mô tả: Chốt kích thước bản đồ chạy trung gian.
- Output:
  - Quyết định chính thức: 96x96
  - Note lý do chọn 96x96 thay vì 128x128 ngay
- Acceptance Criteria:
  - Có ghi rõ trong tài liệu nội bộ rằng 96x96 là target rollout đầu tiên
  - Không còn task runtime nào hardcode giả định chỉ có 64x64
- Depends on: none
- Estimate: S

### MAP-002, Chốt target size giai đoạn cuối
- Mô tả: Chốt kích thước dài hạn của island map.
- Output:
  - Quyết định chính thức: 128x128
  - Có guideline về ngân sách mật độ content cho map lớn
- Acceptance Criteria:
  - Tài liệu backlog và config naming thống nhất target 128x128 cho phase cuối
- Depends on: MAP-001
- Estimate: S

### MAP-003, Viết island map design spec v1
- Mô tả: Tạo spec mô tả terrain, build rules, movement rules, combat fiction.
- Output:
  - 1 file spec ngắn trong Docs/
- Acceptance Criteria:
  - Có mô tả terrain types: Sea, Shore, Land
  - Có mô tả buildable, walkable, spawnable rules
  - Có mô tả landing gates thay cho edge road spawn
- Depends on: MAP-001, MAP-002
- Estimate: M

### MAP-004, Vẽ layout paper design cho island 96x96
- Mô tả: Phác layout khối chức năng trước khi đụng code.
- Output:
  - 1 sơ đồ text hoặc image mô tả HQ core, production belt, coastline, landing fronts
- Acceptance Criteria:
  - Có ít nhất 3 hướng landing
  - HQ nằm vùng trung tâm
  - Có safe zone đầu game và outer pressure ring
- Depends on: MAP-003
- Estimate: M

## Epic MAP-10, Terrain foundation

### MAP-101, Thêm enum TerrainType
- Mô tả: Tạo enum terrain dùng cho gameplay map.
- File dự kiến:
  - Assets/_Game/Core/Contracts/Grid/TerrainType.cs
- Acceptance Criteria:
  - Có ít nhất các giá trị: Sea, Shore, Land
  - Naming nhất quán với coding style hiện tại
- Depends on: MAP-003
- Estimate: XS

### MAP-102, Thêm interface ITerrainMap
- Mô tả: Tạo contract cho terrain layer runtime.
- File dự kiến:
  - Assets/_Game/Core/Contracts/Grid/ITerrainMap.cs
- Acceptance Criteria:
  - Có API lấy terrain theo CellPos
  - Có API IsInside
  - Có API ClearAll
  - Có API set terrain cấp thấp
- Depends on: MAP-101
- Estimate: S

### MAP-103, Implement TerrainMap runtime store
- Mô tả: Tạo runtime storage cho terrain, độc lập với GridMap occupancy.
- File dự kiến:
  - Assets/_Game/Grid/TerrainMap.cs
- Acceptance Criteria:
  - Khởi tạo theo width/height
  - Get/Set deterministic
  - ClearAll reset toàn bộ cell về default thống nhất
  - Không allocation thừa trong đường nóng cơ bản
- Depends on: MAP-102
- Estimate: M

### MAP-104, Quy định default terrain state
- Mô tả: Chốt default terrain khi init hoặc clear.
- Output:
  - decision note trong code/comment/spec
- Acceptance Criteria:
  - Default terrain được chọn rõ ràng, khuyến nghị là Sea
  - Mọi flow reset/new run hiểu cùng một mặc định
- Depends on: MAP-103
- Estimate: XS

### MAP-105, Thêm helper terrain rule cơ bản
- Mô tả: Tạo helper xác định walkable/buildable theo terrain.
- File dự kiến:
  - Assets/_Game/Grid/TerrainRules.cs hoặc tương đương
- Acceptance Criteria:
  - Có hàm IsBuildableTerrain
  - Có hàm IsWalkableTerrain
  - Sea luôn false cho buildable/walkable ở v1
- Depends on: MAP-103, MAP-104
- Estimate: S

## Epic MAP-20, Wiring services và bootstrap

### MAP-201, Mở rộng GameServices để chứa TerrainMap
- Mô tả: Bổ sung terrain layer vào container runtime.
- File dự kiến:
  - GameServices hoặc composition root liên quan
- Acceptance Criteria:
  - TerrainMap có thể được inject/truy cập từ Placement/RunStart/Pathfinding/Validator
- Depends on: MAP-103
- Estimate: M

### MAP-202, Khởi tạo TerrainMap trong bootstrap
- Mô tả: Tạo instance TerrainMap trong flow boot game.
- File dự kiến:
  - GameBootstrap / service factory / installer liên quan
- Acceptance Criteria:
  - Không null khi chạy New Run path chính
  - Kích thước map được truyền đúng
- Depends on: MAP-201
- Estimate: M

### MAP-203, Rà mọi điểm hardcode 64x64 trong runtime path
- Mô tả: Tìm và liệt kê các assumption 64x64 có thể phá map lớn.
- Output:
  - checklist vị trí cần sửa
- Acceptance Criteria:
  - Có danh sách file/module cần chỉnh
  - Đánh dấu chỗ nào là runtime, chỗ nào chỉ là test
- Depends on: MAP-201
- Estimate: M

### MAP-204, Cho kích thước GridMap/TerrainMap ăn theo config
- Mô tả: Runtime map size phải được quyết định bởi config/preset, không hardcode.
- Acceptance Criteria:
  - New Run dùng đúng map size theo config
  - Không mismatch width/height giữa occupancy và terrain layers
- Depends on: MAP-202, MAP-203
- Estimate: L

## Epic MAP-30, RunStart schema và config island

### MAP-301, Mở rộng StartMapConfigDto cho terrain data
- Mô tả: Bổ sung DTO để parse island config.
- File dự kiến:
  - Assets/_Game/Core/RunStart/StartMapConfigDto.cs
- Acceptance Criteria:
  - DTO parse được terrainRects hoặc terrainCells
  - DTO parse được landing gates mới
  - Không làm hỏng config cũ nếu còn cần tương thích
- Depends on: MAP-003
- Estimate: M

### MAP-302, Thiết kế schema v1 cho terrainRects
- Mô tả: Chốt dạng dữ liệu auth cho terrain khối lớn.
- Output:
  - schema field names rõ ràng
- Acceptance Criteria:
  - Có cách mô tả island body, shore band, sea area hợp lý
  - Không bắt buộc liệt kê từng cell cho toàn map
- Depends on: MAP-301
- Estimate: S

### MAP-303, Thiết kế schema v1 cho landing gates
- Mô tả: Chuyển từ spawnGates cũ sang landing gates hợp fiction đảo.
- Acceptance Criteria:
  - Có lane id
  - Có cell gốc
  - Có dirToHQ hoặc metadata tương đương
  - Có thể mở rộng staging zone sau này
- Depends on: MAP-301
- Estimate: S

### MAP-304, Tạo config StartMapConfig_Island_96x96_v1.json
- Mô tả: Authored island config đầu tiên để chạy end-to-end.
- File dự kiến:
  - Assets/_Game/Resources/RunStart/StartMapConfig_Island_96x96_v1.json
- Acceptance Criteria:
  - Có map.width/map.height = 96
  - Có terrain island cơ bản
  - Có roads, HQ, initial buildings, initial NPCs, landing gates
  - Không có building/road nằm trên sea theo authored data
- Depends on: MAP-302, MAP-303, MAP-004
- Estimate: L

### MAP-305, Tạo config StartMapConfig_Island_128x128_v1.json draft
- Mô tả: Soạn bản draft cho target dài hạn.
- Acceptance Criteria:
  - Có island shape sơ bộ
  - Có zoning sơ bộ cho late-game expansion
- Depends on: MAP-304
- Estimate: M

## Epic MAP-40, RunStart apply pipeline

### MAP-401, Tạo RunStartTerrainBuilder
- Mô tả: Tách bước apply terrain ra module riêng.
- File dự kiến:
  - Assets/_Game/Core/RunStart/RunStartTerrainBuilder.cs
- Acceptance Criteria:
  - Apply terrain chạy trước roads/buildings
  - Nếu dữ liệu terrain invalid thì fail sớm
- Depends on: MAP-301, MAP-103, MAP-204
- Estimate: M

### MAP-402, Gắn apply terrain vào RunStartFacade
- Mô tả: Cập nhật flow TryApply để build terrain trước occupancy.
- File dự kiến:
  - Assets/_Game/Core/RunStart/RunStartFacade.cs
- Acceptance Criteria:
  - Order apply mới rõ ràng: terrain -> metadata -> roads -> buildings -> zones -> NPCs
  - Nếu terrain fail, toàn bộ apply fail
- Depends on: MAP-401
- Estimate: S

### MAP-403, Mở rộng RunStartRuntimeCacheBuilder cho terrain metadata
- Mô tả: Cache thêm thông tin island/landing vào runtime.
- File dự kiến:
  - Assets/_Game/Core/RunStart/RunStartRuntimeCacheBuilder.cs
- Acceptance Criteria:
  - Runtime có đủ metadata cho validator/combat/debug
  - Không chỉ cache buildableRect kiểu cũ
- Depends on: MAP-301, MAP-303, MAP-402
- Estimate: M

### MAP-404, Cập nhật RunStartWorldBuilder để validate terrain khi đặt roads
- Mô tả: Road authored không được rơi vào sea.
- File dự kiến:
  - Assets/_Game/Core/RunStart/RunStartWorldBuilder.cs
- Acceptance Criteria:
  - SetRoad trên sea trả lỗi apply rõ ràng
- Depends on: MAP-402
- Estimate: M

### MAP-405, Cập nhật RunStartWorldBuilder để validate terrain khi đặt buildings
- Mô tả: Building footprint và entry/driveway phải hợp terrain.
- Acceptance Criteria:
  - Building trên sea fail rõ ràng
  - Entry cell trên sea fail rõ ràng
- Depends on: MAP-404
- Estimate: M

## Epic MAP-50, Placement rules

### MAP-501, Cập nhật CanPlaceRoad theo terrain
- Mô tả: Chặn đặt road trên sea.
- File dự kiến:
  - Assets/_Game/Grid/PlacementService.cs
- Acceptance Criteria:
  - Road placement trên Sea luôn false
- Depends on: MAP-105, MAP-201
- Estimate: S

### MAP-502, Cập nhật ValidateBuilding footprint theo terrain
- Mô tả: Footprint building không được chạm sea.
- Acceptance Criteria:
  - Nếu bất kỳ cell footprint là Sea thì reject với reason phù hợp
- Depends on: MAP-501
- Estimate: M

### MAP-503, Cập nhật ValidateBuilding entry/driveway theo terrain
- Mô tả: Entry cell và driveway phải thuộc terrain hợp lệ.
- Acceptance Criteria:
  - Entry cell là Sea thì reject
  - Không tự tạo driveway ra biển
- Depends on: MAP-502
- Estimate: M

### MAP-504, Giảm phụ thuộc vào buildableRect trong placement
- Mô tả: Buildable phải do terrain/mask quyết định, rect chỉ là constraint phụ nếu còn cần.
- Acceptance Criteria:
  - Placement không dựa duy nhất vào buildableRect nữa
- Depends on: MAP-503
- Estimate: M

## Epic MAP-60, Pathfinding và movement

### MAP-601, Cập nhật NpcPathfinder walkability theo terrain
- Mô tả: Sea là non-walkable tuyệt đối.
- File dự kiến:
  - Assets/_Game/Grid/Navigation/NpcPathfinder.cs
- Acceptance Criteria:
  - Path không bao giờ đi qua sea
  - Land/Shore vẫn dùng occupancy để quyết định block
- Depends on: MAP-105, MAP-201
- Estimate: M

### MAP-602, Rà GridAgentMoverLite và helpers movement
- Mô tả: Đồng bộ runtime movement với luật pathfinding mới.
- File dự kiến:
  - Assets/_Game/Grid/Navigation/GridAgentMoverLite.cs
  - helpers liên quan
- Acceptance Criteria:
  - Movement runtime không bước vào sea dù path bug hay target lệch
- Depends on: MAP-601
- Estimate: M

### MAP-603, Benchmark sơ bộ pathfinding trên 96x96
- Mô tả: Kiểm tra chi phí tăng map size.
- Output:
  - note về hiệu năng chấp nhận được hay chưa
- Acceptance Criteria:
  - Có số liệu đơn giản hoặc profiler note cho case representative
- Depends on: MAP-601, MAP-304
- Estimate: M

## Epic MAP-70, Combat island lanes

### MAP-701, Thiết kế runtime model cho landing gates
- Mô tả: Xác định data runtime cần dùng cho lane combat kiểu đảo.
- Acceptance Criteria:
  - Có representation rõ cho landing cell và lane id
- Depends on: MAP-303
- Estimate: S

### MAP-702, Cập nhật RunStart lane builder sang landing gates
- Mô tả: Build lanes từ landing gates thay vì logic cũ bám edge road.
- File dự kiến:
  - RunStartHqResolver hoặc module tương ứng
- Acceptance Criteria:
  - Runtime lanes được build từ authored landing gates
- Depends on: MAP-701, MAP-403
- Estimate: L

### MAP-703, Cập nhật WaveDirector để spawn theo landing gates
- Mô tả: Địch bắt đầu từ bãi đổ bộ/cell hợp lệ trên đảo.
- File dự kiến:
  - Assets/_Game/Combat/WaveDirector.cs
- Acceptance Criteria:
  - Wave spawn không còn giả định edge-road cũ
- Depends on: MAP-702
- Estimate: L

### MAP-704, Cập nhật EnemySystem route validation theo terrain
- Mô tả: Địch không được chọn route xuyên sea.
- File dự kiến:
  - Assets/_Game/Combat/EnemySystem.cs
- Acceptance Criteria:
  - Từ mọi landing gate hợp lệ có route vào objective
  - Gate invalid bị xử lý rõ
- Depends on: MAP-703, MAP-601
- Estimate: L

### MAP-705, Rà CombatService assumptions sau load/new run
- Mô tả: Đảm bảo combat reset/load không giả định lane kiểu cũ.
- File dự kiến:
  - Assets/_Game/Combat/CombatService.cs
- Acceptance Criteria:
  - Không double-spawn hay route invalid vì metadata lane mới
- Depends on: MAP-703, MAP-704
- Estimate: M

## Epic MAP-80, Resource và zones

### MAP-801, Chặn zone init trên sea
- Mô tả: Zone authored/generated không được chứa sea cells.
- File dự kiến:
  - RunStartZoneInitializer hoặc tương đương
- Acceptance Criteria:
  - Zone invalid trên sea bị reject hoặc cắt bỏ có log rõ ràng
- Depends on: MAP-401, MAP-304
- Estimate: M

### MAP-802, Cập nhật ResourcePatchService cho island terrain
- Mô tả: Resource patch rebuild phải tôn trọng land mask.
- Acceptance Criteria:
  - Không tạo patch tài nguyên giữa biển
- Depends on: MAP-801
- Estimate: M

### MAP-803, Scale khoảng cách resource cho 96x96
- Mô tả: Chỉnh starter/mid/outer resource distance cho map lớn hơn.
- File dự kiến:
  - config JSON hoặc rules liên quan
- Acceptance Criteria:
  - Early game không quá xa
  - Outer ring có giá trị mở rộng thật
- Depends on: MAP-304
- Estimate: M

### MAP-804, Scale khoảng cách resource cho 128x128
- Mô tả: Tune balance cho target dài hạn.
- Acceptance Criteria:
  - Late game expansion hợp lý trên 128x128
- Depends on: MAP-305, MAP-803
- Estimate: M

## Epic MAP-90, Save/Load

### MAP-901, Chốt chiến lược save terrain theo config reference
- Mô tả: Không save full terrain nếu terrain được authored từ config.
- Output:
  - design note rõ ràng
- Acceptance Criteria:
  - Save/load spec ghi rõ terrain khôi phục từ config id/version
- Depends on: MAP-003, MAP-304
- Estimate: S

### MAP-902, Mở rộng save metadata để lưu map config id/version
- Mô tả: Snapshot phải biết nó thuộc island config nào.
- File dự kiến:
  - save DTO liên quan
- Acceptance Criteria:
  - Save có field nhận diện config island
- Depends on: MAP-901
- Estimate: M

### MAP-903, Apply terrain từ config trước khi restore occupancy
- Mô tả: SaveLoadApplier phải dựng lại terrain trước roads/buildings/sites.
- File dự kiến:
  - Assets/_Game/Save/SaveLoadApplier.cs
- Acceptance Criteria:
  - Terrain được restore hoặc rebuild trước occupancy layer
- Depends on: MAP-902, MAP-401
- Estimate: L

### MAP-904, Thêm validation road/building/site/NPC trên sea khi load
- Mô tả: Snapshot lỗi phải fail rõ.
- Acceptance Criteria:
  - Road trên sea fail
  - Building/site trên sea fail
  - NPC trên sea fail
- Depends on: MAP-903
- Estimate: M

### MAP-905, Rebuild runtime caches island sau load
- Mô tả: Sau load cần rebuild lane, zone metadata, index tương thích island model.
- Acceptance Criteria:
  - Runtime caches sau load khớp map island mới
- Depends on: MAP-903, MAP-403, MAP-702
- Estimate: M

## Epic MAP-100, Validation và debug

### MAP-1001, Thêm validation HQ không nằm trên sea
- Mô tả: RunStart validator phải chặn HQ invalid.
- File dự kiến:
  - Assets/_Game/Core/RunStart/RunStartValidator.cs
- Acceptance Criteria:
  - HQ on sea -> error rõ ràng
- Depends on: MAP-401
- Estimate: S

### MAP-1002, Thêm validation road/building không nằm trên sea
- Mô tả: Validator quét occupancy vs terrain.
- Acceptance Criteria:
  - Road on sea -> error
  - Building on sea -> error
- Depends on: MAP-1001
- Estimate: M

### MAP-1003, Thêm validation landing gates hợp lệ
- Mô tả: Gate phải trong bounds, đúng terrain, route được.
- Acceptance Criteria:
  - Gate on sea invalid theo rule nếu không phải loại shore-allowed đã định nghĩa
  - Gate không nối được objective -> error
- Depends on: MAP-702, MAP-704
- Estimate: M

### MAP-1004, Thêm validation island connectivity
- Mô tả: Kiểm tra playable landmass không bị gãy vô nghĩa.
- Acceptance Criteria:
  - Có phát hiện island fragment gây unreachable core areas
- Depends on: MAP-1002, MAP-1003
- Estimate: M

### MAP-1005, Tạo debug overlay terrain
- Mô tả: Debug tool để xem sea/shore/land và cell invalid.
- File dự kiến:
  - debug HUD/gizmo modules
- Acceptance Criteria:
  - Có thể bật overlay terrain trong editor/runtime debug
- Depends on: MAP-403
- Estimate: M

### MAP-1006, Tạo debug overlay landing gates và unreachable cells
- Mô tả: Hỗ trợ tune island combat routes.
- Acceptance Criteria:
  - Nhìn được gate positions
  - Nhìn được khu unreachable quan trọng
- Depends on: MAP-1005, MAP-704
- Estimate: M

## Epic MAP-110, Visual rebuild

### MAP-1101, Tạo mapping terrain -> visual tiles/sprites
- Mô tả: Quy định renderer đọc terrain data như thế nào.
- Acceptance Criteria:
  - Sea, Shore, Land có visual mapping rõ
- Depends on: MAP-103
- Estimate: S

### MAP-1102, Implement terrain rendering pass cơ bản
- Mô tả: Render island shape từ data terrain.
- File dự kiến:
  - world view 2D / tilemap rendering modules
- Acceptance Criteria:
  - Chạy New Run nhìn thấy biển bao quanh đảo
- Depends on: MAP-1101, MAP-304
- Estimate: L

### MAP-1103, Thêm shoreline transitions cơ bản
- Mô tả: Làm rìa đảo đỡ thô.
- Acceptance Criteria:
  - Shoreline có phân biệt với sea và land rõ ràng
- Depends on: MAP-1102
- Estimate: M

### MAP-1104, Đánh dấu visual cho landing beaches
- Mô tả: Người chơi nhìn vào hiểu nơi địch đổ bộ.
- Acceptance Criteria:
  - Ít nhất 3 landing points có visual marker rõ ràng
- Depends on: MAP-1102, MAP-703
- Estimate: M

## Epic MAP-120, Tests và regression

### MAP-1201, Thêm unit test TerrainMap
- Mô tả: Test Get/Set/ClearAll/Bounds cho terrain layer.
- Acceptance Criteria:
  - Pass deterministic
- Depends on: MAP-103
- Estimate: S

### MAP-1202, Thêm test PlacementService chặn road trên sea
- Mô tả: Regression placement cơ bản cho island rules.
- Acceptance Criteria:
  - Case đặt road xuống sea fail
- Depends on: MAP-501
- Estimate: S

### MAP-1203, Thêm test PlacementService chặn building footprint chạm sea
- Mô tả: Building validation island rule.
- Acceptance Criteria:
  - Footprint chứa sea -> fail
- Depends on: MAP-502
- Estimate: S

### MAP-1204, Thêm test pathfinding không đi xuyên sea
- Mô tả: Regression pathfinding core.
- Acceptance Criteria:
  - Path result không chứa sea cells
- Depends on: MAP-601
- Estimate: M

### MAP-1205, Thêm test RunStart island config hợp lệ pass validator
- Mô tả: End-to-end config island v1 phải qua validator.
- Acceptance Criteria:
  - Config island 96x96 valid pass
- Depends on: MAP-304, MAP-1004
- Estimate: M

### MAP-1206, Thêm test RunStart config invalid nếu road/building trên sea
- Mô tả: Validator phải bắt lỗi terrain occupancy mismatch.
- Acceptance Criteria:
  - Road on sea invalid
  - Building on sea invalid
- Depends on: MAP-1002
- Estimate: M

### MAP-1207, Thêm test save/load fail nếu snapshot có occupancy trên sea
- Mô tả: Hardening save/load island.
- Acceptance Criteria:
  - Snapshot invalid fail rõ ràng
- Depends on: MAP-904
- Estimate: M

### MAP-1208, Thêm test combat landing gate route validity
- Mô tả: Đảm bảo mỗi gate spawn usable.
- Acceptance Criteria:
  - Enemy route từ gate vào objective tồn tại với config valid
- Depends on: MAP-704
- Estimate: M

### MAP-1209, Rà và cập nhật regression cũ đang assume map phẳng toàn land
- Mô tả: Sửa các test cũ không còn đúng với island model.
- Acceptance Criteria:
  - Regression suite phản ánh terrain-aware gameplay
- Depends on: MAP-1202, MAP-1204
- Estimate: L

## Epic MAP-130, Performance và scale-up

### MAP-1301, Smoke test 96x96 gameplay loop
- Mô tả: Chạy vòng lặp New Run -> Build -> Save -> Load -> Combat trên island 96x96.
- Acceptance Criteria:
  - Không crash
  - Không mismatch logic lớn
- Depends on: MAP-904, MAP-703
- Estimate: M

### MAP-1302, Profile pathfinding và enemy routing trên 96x96
- Mô tả: Kiểm tra frame cost của path/route.
- Acceptance Criteria:
  - Có note vấn đề hotspot nếu có
- Depends on: MAP-1301
- Estimate: M

### MAP-1303, Nâng island config/playtest lên 128x128
- Mô tả: Bật target dài hạn sau khi 96x96 ổn.
- Acceptance Criteria:
  - Runtime không vỡ vì kích thước lớn hơn
- Depends on: MAP-305, MAP-1301
- Estimate: L

### MAP-1304, Tune density content cho 128x128
- Mô tả: Chỉnh road/resource/expansion/combat spacing để map lớn không bị loãng.
- Acceptance Criteria:
  - 128x128 vẫn có nhịp chơi hợp lý
- Depends on: MAP-1303
- Estimate: L

## Suggested execution order
1. MAP-001 -> MAP-004
2. MAP-101 -> MAP-105
3. MAP-201 -> MAP-204
4. MAP-301 -> MAP-305
5. MAP-401 -> MAP-405
6. MAP-501 -> MAP-504
7. MAP-601 -> MAP-603
8. MAP-801 -> MAP-804
9. MAP-901 -> MAP-905
10. MAP-1001 -> MAP-1006
11. MAP-701 -> MAP-705
12. MAP-1201 -> MAP-1209
13. MAP-1301 -> MAP-1304
14. MAP-1101 -> MAP-1104

## Milestone gợi ý

### Milestone A, Island foundation playable
- Bao gồm:
  - MAP-001 -> MAP-405
  - MAP-501 -> MAP-504
  - MAP-601
  - MAP-1001 -> MAP-1002
  - MAP-1201 -> MAP-1204
- Kết quả:
  - Boot được island 96x96, không build/đi trên biển

### Milestone B, Island gameplay complete
- Bao gồm:
  - MAP-801 -> MAP-905
  - MAP-1003 -> MAP-1006
  - MAP-701 -> MAP-705
  - MAP-1205 -> MAP-1209
- Kết quả:
  - New Run, Save/Load, Combat, Validation đều hiểu island model

### Milestone C, Scale + polish
- Bao gồm:
  - MAP-1301 -> MAP-1304
  - MAP-1101 -> MAP-1104
  - MAP-305
- Kết quả:
  - Island map lớn hơn, nhìn ra đảo thật, gameplay scale ổn
