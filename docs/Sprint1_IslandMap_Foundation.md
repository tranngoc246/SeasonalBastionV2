# Sprint 1, Island Map Foundation

Mục tiêu sprint: tạo nền tảng kỹ thuật để game hiểu mô hình island map 96x96, có terrain layer riêng, boot được New Run với island config, và chặn các hành vi cơ bản không hợp lệ như build/đi trên biển.

## Sprint goal
- Có terrain model `Sea / Shore / Land`
- Có runtime terrain storage độc lập với `GridMap`
- Runtime size không còn khóa cứng theo 64x64 trong main path
- RunStart có thể apply island terrain trước khi đặt roads/buildings
- Placement cơ bản hiểu terrain
- Pathfinding cơ bản không đi xuyên biển
- Có test nền tảng cho terrain + placement + pathfinding

## Out of scope cho Sprint 1
- Combat landing gate hoàn chỉnh
- Visual polish shoreline
- Full save/load island model hoàn chỉnh
- 128x128 tuning
- Resource generation rebalance sâu

## Definition of Done cho Sprint 1
- New Run boot được config island 96x96
- Terrain layer tồn tại và truy cập được từ runtime services
- Không thể đặt road trên sea
- Building footprint chạm sea bị reject
- NPC pathfinding không tạo path xuyên sea
- Có island config v1 draft chạy được
- Unit/regression tests cốt lõi pass

---

# Sprint backlog

## Story S1-01, Tạo terrain model cơ bản

### Task S1-01-01, Tạo `TerrainType.cs`
- Mục tiêu: định nghĩa enum terrain gameplay.
- File dự kiến:
  - `Assets/_Game/Core/Contracts/Grid/TerrainType.cs`
- Việc cần làm:
  - thêm enum `Sea`, `Shore`, `Land`
  - naming bám style codebase hiện tại
- Acceptance Criteria:
  - compile pass
  - enum được dùng được ở runtime layer mới
- Estimate: XS

### Task S1-01-02, Tạo `ITerrainMap.cs`
- Mục tiêu: tạo contract truy cập terrain.
- File dự kiến:
  - `Assets/_Game/Core/Contracts/Grid/ITerrainMap.cs`
- Việc cần làm:
  - thêm `Width`, `Height`
  - thêm `IsInside(CellPos)`
  - thêm `Get(CellPos)`
  - thêm `Set(CellPos, TerrainType)`
  - thêm `ClearAll()`
- Acceptance Criteria:
  - interface đủ cho RunStart, Placement, Pathfinder dùng chung
- Depends on:
  - S1-01-01
- Estimate: S

### Task S1-01-03, Tạo `TerrainMap.cs`
- Mục tiêu: implement terrain grid runtime.
- File dự kiến:
  - `Assets/_Game/Grid/TerrainMap.cs`
- Việc cần làm:
  - implement `ITerrainMap`
  - lưu terrain bằng mảng 1 chiều giống style `GridMap`
  - default terrain thống nhất, khuyến nghị `Sea`
- Acceptance Criteria:
  - `Get/Set/ClearAll` deterministic
  - out-of-bounds behavior rõ ràng
  - không có allocation vô lý trong đường nóng cơ bản
- Depends on:
  - S1-01-02
- Estimate: M

### Task S1-01-04, Tạo terrain helper/rules cơ bản
- Mục tiêu: chuẩn hóa logic terrain walkable/buildable.
- File dự kiến:
  - `Assets/_Game/Grid/TerrainRules.cs` hoặc tương đương
- Việc cần làm:
  - thêm helper `IsBuildableTerrain`
  - thêm helper `IsWalkableTerrain`
  - v1 rule:
    - Sea: không build, không walk
    - Shore: có thể walk
    - Land: có thể walk/build
- Acceptance Criteria:
  - helper dùng được ở placement/pathfinding mà không lặp rule nhiều nơi
- Depends on:
  - S1-01-03
- Estimate: S

---

## Story S1-02, Wiring terrain vào runtime

### Task S1-02-01, Rà composition root tạo `GameServices`
- Mục tiêu: xác định đúng nơi inject terrain.
- File/module cần rà:
  - `GameBootstrap`
  - factory/service wiring liên quan
- Việc cần làm:
  - xác định chỗ tạo `GridMap`
  - xác định chỗ phù hợp tạo `TerrainMap`
  - note dependency chain cho Placement/RunStart/Pathfinding
- Acceptance Criteria:
  - có danh sách file runtime path chính cần sửa
- Depends on:
  - none
- Estimate: S

### Task S1-02-02, Thêm `TerrainMap` vào `GameServices`
- Mục tiêu: runtime services có terrain layer.
- File dự kiến:
  - `GameServices` và builder/factory liên quan
- Việc cần làm:
  - thêm property/reference cho `ITerrainMap` hoặc concrete `TerrainMap`
  - đảm bảo null-safety hợp lý
- Acceptance Criteria:
  - Placement/RunStart/Pathfinder đọc được terrain map từ services
- Depends on:
  - S1-01-03
  - S1-02-01
- Estimate: M

### Task S1-02-03, Gỡ hardcode size runtime path chính
- Mục tiêu: main path không còn mặc định 64x64 cố định.
- File/module cần rà:
  - bootstrap runtime
  - `GameLoop`
  - run start init path
- Việc cần làm:
  - xác định width/height lấy từ config hoặc preset runtime
  - đảm bảo `GridMap` và `TerrainMap` cùng size
- Acceptance Criteria:
  - New Run island 96x96 khởi tạo đúng size
- Depends on:
  - S1-02-02
- Estimate: L

---

## Story S1-03, Mở rộng RunStart schema cho island config

### Task S1-03-01, Mở rộng `StartMapConfigDto.cs` cho terrain data
- Mục tiêu: parse được terrain từ config.
- File dự kiến:
  - `Assets/_Game/Core/RunStart/StartMapConfigDto.cs`
- Việc cần làm:
  - thêm DTO cho terrain data, ưu tiên `terrainRects`
  - thêm DTO cho island/landing metadata tối thiểu nếu cần
- Acceptance Criteria:
  - parse được config island v1
  - không phá config cũ nếu còn dùng song song
- Depends on:
  - S1-01-01
- Estimate: M

### Task S1-03-02, Chốt schema terrain v1 cho config 96x96
- Mục tiêu: không sa đà procedural, ưu tiên authored dễ kiểm soát.
- Việc cần làm:
  - chọn field names cuối cùng cho `terrainRects`
  - định nghĩa thứ tự apply rect và override nếu có
- Acceptance Criteria:
  - schema đủ để mô tả island cơ bản mà không cần list mọi cell
- Depends on:
  - S1-03-01
- Estimate: S

### Task S1-03-03, Tạo `StartMapConfig_Island_96x96_v1.json` draft
- Mục tiêu: có config island thật để chạy thử.
- File dự kiến:
  - `Assets/_Game/Resources/RunStart/StartMapConfig_Island_96x96_v1.json`
- Việc cần làm:
  - set `map.width = 96`, `map.height = 96`
  - authored terrain island cơ bản
  - đặt HQ gần trung tâm
  - đặt roads/buildings starter hợp lệ trên land
- Acceptance Criteria:
  - file parse được
  - island shape rõ ràng
  - không authored road/building xuống sea
- Depends on:
  - S1-03-02
- Estimate: L

---

## Story S1-04, RunStart apply terrain trước occupancy

### Task S1-04-01, Tạo `RunStartTerrainBuilder.cs`
- Mục tiêu: tách logic apply terrain ra module riêng.
- File dự kiến:
  - `Assets/_Game/Core/RunStart/RunStartTerrainBuilder.cs`
- Việc cần làm:
  - apply `terrainRects`
  - clear/reset terrain trước khi apply
  - validate bounds cơ bản
- Acceptance Criteria:
  - terrain có thể được dựng hoàn chỉnh trước roads/buildings
- Depends on:
  - S1-01-03
  - S1-03-01
- Estimate: M

### Task S1-04-02, Gắn terrain apply vào `RunStartFacade.cs`
- Mục tiêu: đổi thứ tự apply run start.
- File dự kiến:
  - `Assets/_Game/Core/RunStart/RunStartFacade.cs`
- Việc cần làm:
  - gọi `RunStartTerrainBuilder` trước `RunStartWorldBuilder`
  - fail sớm nếu terrain invalid
- Acceptance Criteria:
  - flow apply là terrain -> metadata -> world placement
- Depends on:
  - S1-04-01
- Estimate: S

### Task S1-04-03, Mở rộng `RunStartRuntimeCacheBuilder.cs`
- Mục tiêu: runtime cache hiểu map island 96x96.
- File dự kiến:
  - `Assets/_Game/Core/RunStart/RunStartRuntimeCacheBuilder.cs`
- Việc cần làm:
  - cache map size từ island config
  - chuẩn bị chỗ cho terrain/island metadata cần thiết ở sprint sau
- Acceptance Criteria:
  - runtime metadata không còn chỉ tư duy map 64x64 cũ
- Depends on:
  - S1-04-02
- Estimate: S

### Task S1-04-04, Chặn authored roads trên sea trong `RunStartWorldBuilder.cs`
- Mục tiêu: roads từ config phải hợp terrain.
- File dự kiến:
  - `Assets/_Game/Core/RunStart/RunStartWorldBuilder.cs`
- Việc cần làm:
  - validate terrain trước `SetRoad`
  - log/fail message rõ cell nào sai
- Acceptance Criteria:
  - road trên sea làm RunStart fail rõ ràng
- Depends on:
  - S1-04-02
  - S1-01-04
- Estimate: M

### Task S1-04-05, Chặn authored buildings trên sea trong `RunStartWorldBuilder.cs`
- Mục tiêu: building footprint/anchor không được nằm trên sea.
- Việc cần làm:
  - validate footprint terrain trước create building state
  - validate entry/driveway cơ bản nếu flow hiện tại cần
- Acceptance Criteria:
  - building invalid trên sea fail sớm
- Depends on:
  - S1-04-04
- Estimate: M

---

## Story S1-05, Placement terrain-aware

### Task S1-05-01, Update `CanPlaceRoad` trong `PlacementService.cs`
- Mục tiêu: player không thể đặt road trên biển.
- File dự kiến:
  - `Assets/_Game/Grid/PlacementService.cs`
- Việc cần làm:
  - thêm terrain check trước logic placement hiện tại
- Acceptance Criteria:
  - road placement trên Sea trả false
- Depends on:
  - S1-02-02
  - S1-01-04
- Estimate: S

### Task S1-05-02, Update `ValidateBuilding` footprint theo terrain
- Mục tiêu: building không được overlap Sea.
- Việc cần làm:
  - kiểm từng cell footprint với terrain map
- Acceptance Criteria:
  - có reject rõ ràng nếu footprint đè sea
- Depends on:
  - S1-05-01
- Estimate: M

### Task S1-05-03, Update `ValidateBuilding` entry cell theo terrain
- Mục tiêu: driveway/entry không đâm ra biển.
- Việc cần làm:
  - check entry cell không phải Sea
- Acceptance Criteria:
  - entry cell là Sea thì reject
- Depends on:
  - S1-05-02
- Estimate: M

---

## Story S1-06, Pathfinding terrain-aware

### Task S1-06-01, Update `NpcPathfinder.cs` để chặn Sea
- Mục tiêu: NPC path không xuyên biển.
- File dự kiến:
  - `Assets/_Game/Grid/Navigation/NpcPathfinder.cs`
- Việc cần làm:
  - sửa `IsWalkableMixed`
  - sửa logic road-only fallback nếu cần để respect terrain
- Acceptance Criteria:
  - path result không chứa Sea cells
- Depends on:
  - S1-02-02
  - S1-01-04
- Estimate: M

### Task S1-06-02, Rà `GridAgentMoverLite.cs` và movement helper
- Mục tiêu: runtime movement không bước vào Sea dù edge case.
- File dự kiến:
  - `Assets/_Game/Grid/Navigation/GridAgentMoverLite.cs`
- Việc cần làm:
  - rà chỗ assume empty cell là đi được
  - thêm guard cần thiết
- Acceptance Criteria:
  - movement runtime an toàn với terrain rules mới
- Depends on:
  - S1-06-01
- Estimate: M

---

## Story S1-07, Validation nền tảng cho island config

### Task S1-07-01, Thêm validate HQ không trên sea
- Mục tiêu: island config hỏng bị bắt sớm.
- File dự kiến:
  - `Assets/_Game/Core/RunStart/RunStartValidator.cs`
- Việc cần làm:
  - check HQ footprint hợp terrain
- Acceptance Criteria:
  - HQ on Sea -> error rõ ràng
- Depends on:
  - S1-04-05
- Estimate: S

### Task S1-07-02, Thêm validate road/building occupancy không trên sea
- Mục tiêu: validator hiểu terrain occupancy mismatch.
- Việc cần làm:
  - quét roads/buildings sau apply
- Acceptance Criteria:
  - road on Sea -> error
  - building on Sea -> error
- Depends on:
  - S1-07-01
- Estimate: M

---

## Story S1-08, Tests cốt lõi

### Task S1-08-01, Thêm test cho `TerrainMap`
- Mục tiêu: khóa behavior nền của terrain runtime.
- File test dự kiến:
  - `Assets/_Game/Tests/EditMode/.../TerrainMapTests.cs`
- Case tối thiểu:
  - Get mặc định trả default terrain
  - Set rồi Get đúng giá trị
  - ClearAll reset đúng
  - out-of-bounds hành xử nhất quán
- Acceptance Criteria:
  - test pass stable
- Depends on:
  - S1-01-03
- Estimate: S

### Task S1-08-02, Thêm test placement chặn road trên sea
- Mục tiêu: regression placement island.
- File test dự kiến:
  - test placement/editmode phù hợp
- Acceptance Criteria:
  - `CanPlaceRoad` false trên Sea
- Depends on:
  - S1-05-01
- Estimate: S

### Task S1-08-03, Thêm test placement chặn building footprint chạm sea
- Mục tiêu: regression building validation island.
- Acceptance Criteria:
  - `ValidateBuilding` fail nếu footprint có Sea
- Depends on:
  - S1-05-02
- Estimate: S

### Task S1-08-04, Thêm test pathfinding không đi xuyên sea
- Mục tiêu: regression movement cốt lõi.
- Acceptance Criteria:
  - path kết quả không đi qua Sea
  - nếu bị biển chặn hoàn toàn thì path fail đúng
- Depends on:
  - S1-06-01
- Estimate: M

### Task S1-08-05, Thêm test RunStart island config boot được
- Mục tiêu: end-to-end sanity cho sprint.
- Acceptance Criteria:
  - apply island config 96x96 thành công
  - HQ/roads/buildings starter được tạo trên land hợp lệ
- Depends on:
  - S1-03-03
  - S1-04-05
  - S1-07-02
- Estimate: M

---

# Execution order đề xuất trong sprint

## Phase 1, Foundation types
1. S1-01-01
2. S1-01-02
3. S1-01-03
4. S1-01-04

## Phase 2, Runtime wiring
5. S1-02-01
6. S1-02-02
7. S1-02-03

## Phase 3, Config và RunStart
8. S1-03-01
9. S1-03-02
10. S1-03-03
11. S1-04-01
12. S1-04-02
13. S1-04-03
14. S1-04-04
15. S1-04-05

## Phase 4, Gameplay rules
16. S1-05-01
17. S1-05-02
18. S1-05-03
19. S1-06-01
20. S1-06-02
21. S1-07-01
22. S1-07-02

## Phase 5, Tests và sanity
23. S1-08-01
24. S1-08-02
25. S1-08-03
26. S1-08-04
27. S1-08-05

---

# Deliverables cuối sprint
- `TerrainType.cs`
- `ITerrainMap.cs`
- `TerrainMap.cs`
- terrain rules helper
- runtime wiring có terrain layer
- island config `StartMapConfig_Island_96x96_v1.json`
- run start apply terrain trước occupancy
- placement và pathfinding terrain-aware ở mức cơ bản
- validator nền tảng cho terrain occupancy
- test nền tảng pass

# Risk checklist trong Sprint 1
- Quên đồng bộ size giữa `GridMap` và `TerrainMap`
- Cập nhật placement nhưng quên pathfinding
- Config island parse được nhưng apply order sai
- Test cũ assume map toàn land và bắt đầu fail dây chuyền
- Đụng quá sâu combat/save-load trong sprint này làm scope phình

# Khuyến nghị vận hành sprint
- Chốt nhanh schema terrain v1, đừng tối ưu quá sớm
- Ưu tiên island config chạy được trước khi polish validator
- Giữ combat/save-load full rework sang sprint sau
- Nếu phát hiện runtime còn hardcode 64x64 nhiều hơn dự kiến, tách riêng bugfix list thay vì nhét chung vào task lớn
