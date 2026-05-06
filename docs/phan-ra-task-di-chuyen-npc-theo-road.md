# Task Breakdown — NPC Road-Aware Movement

## Goal
Nâng cấp NPC movement từ Manhattan step đơn giản sang path-based movement có ưu tiên road, tránh obstacle, hỗ trợ ground-road-ground tự nhiên, và không chồng ô khi dừng.

---

## Scope

### Pass 1
- Weighted pathfinding cho NPC
- Ưu tiên road hơn ground
- Tránh building/site/out-of-bounds
- Path cache + repath khi target/road đổi
- Giữ nguyên job executor API hiện tại

### Pass 1.5 — Road-first tightening
- Nếu tồn tại route có road backbone hợp lệ, NPC phải dùng road backbone đó
- Ground chỉ là đoạn tiếp cận đầu/cuối (`ground -> road -> ground`)
- Không cho phép rời road giữa backbone chỉ để shortcut
- Fallback mixed path chỉ dùng khi không có road route usable
- Giảm brute-force / bất đối xứng khi chọn road entry-exit

### Pass 2
- Stop reservation để NPC không đè lên nhau khi dừng/interact
- Wait/retry khi target stop cell đang bị chiếm

### Pass 3
- Dùng path cost estimate thay Manhattan để chọn source/destination tốt hơn

---

## Design Defaults
- Movement: 4-direction only
- Traversable: `Empty`, `Road`
- Blocked: `Building`, `Site`
- Cost mặc định:
  - `Road = 10`
  - `Ground = 30`
- Heuristic: Manhattan
- Deterministic neighbor order: `N, E, S, W`
- Pass đầu: `stop cell == interaction target`
- Không save path cache / reservation state

---

# Pass 1 — Road-aware pathfinding + mover rewrite

## Task 1.1 — Create `NpcPathfinder`
**Files:**
- `Assets/_Game/Core/NpcPathfinder.cs`
- `Assets/_Game/Tests/EditMode/Movement/NpcPathfinderTests.cs`

**Status:** DONE

**Work:**
- Tạo class pathfinder dùng weighted A*
- API đề xuất:
  - `TryFindPath(CellPos from, CellPos target, out List<CellPos> path)`
  - optional: `TryEstimateCost(...)`
- Implement walkability:
  - walkable: empty, road
  - blocked: building, site, OOB
- Implement terrain cost:
  - road = 10
  - ground = 30
- Reconstruct path deterministic

**Acceptance:**
- Path hợp lệ giữa 2 điểm walkable
- Ưu tiên road khi có lợi
- Fail đúng khi target unreachable
- Đã pass Unity tests cơ bản cho pathfinder

---

## Task 1.2 — Add pathfinder tests
**Files:**
- `Assets/_Game/Tests/EditMode/Movement/NpcPathfinderTests.cs`

**Work:**
- Thêm test cases:
  - road corridor preferred over ground
  - no-road full ground fallback
  - mixed ground-road-ground
  - avoid building
  - avoid site
  - unreachable target returns false
  - deterministic tie-break

**Acceptance:**
- Test pass trong Unity Test Runner

---

## Task 1.3 — Rewrite `GridAgentMoverLite`
**Files:**
- `Assets/_Game/Core/GridAgentMoverLite.cs`

**Status:** DONE

**Work:**
- Giữ public API hiện tại:
  - `StepToward(ref NpcState st, CellPos target, float dt)`
  - `ClearAll()`
- Thêm route cache per NPC:
  - target
  - path
  - path index
  - roads version
  - no-progress counter
- Tích hợp `NpcPathfinder`
- Thay Manhattan stepping bằng path-follow stepping
- Giữ speed accumulation hiện tại
- Giữ road speed multiplier logic hiện tại
- Repath khi:
  - target đổi
  - path missing/invalid
  - roads version đổi
  - next step không còn walkable
  - NPC lệch khỏi path

**Acceptance:**
- NPC đi theo path thay vì Manhattan X-then-Y
- Không đi xuyên building/site
- Partial-road path hoạt động đúng
- Runtime Editor đã verify NPC đi theo road

---

## Task 1.4 — Add mover invalidation hook
**Files:**
- nơi bootstrap/composition root tạo `GameServices.AgentMover`
- có thể thêm/sub vào event bus nếu phù hợp

**Status:** DONE

**Work:**
- Thêm `NotifyRoadsDirty()` vào mover
- Hook `RoadsDirtyEvent` -> tăng roads version trong mover
- Đảm bảo `ClearAll()` được gọi khi New Run / Retry / Load nếu phù hợp với lifecycle hiện có

**Acceptance:**
- Khi road thay đổi, NPC repath lazy ở tick kế tiếp
- Không giữ stale path qua new run/load

---

## Task 1.5 — Smoke test job executor compatibility
**Files review:**
- `Assets/_Game/Jobs/Executors/HarvestExecutor.cs`
- `Assets/_Game/Jobs/Executors/HaulBasicExecutor.cs`
- `Assets/_Game/Jobs/Executors/BuildWorkExecutor.cs`
- `Assets/_Game/Jobs/Executors/ResupplyTowerExecutor.cs`

**Work:**
- Verify tất cả target cell mà executor dùng đều walkable
- Confirm mover mới không làm vỡ settle/deliver/build loop
- Nếu executor nào target không ổn định giữa nhiều tick, cân nhắc lock target ở phase tương ứng

**Acceptance:**
- Harvest / Haul / Build / Resupply vẫn chạy đúng trong runtime

---

## Task 1.6 — Add mover/runtime regression tests
**Files:**
- `Assets/_Game/Tests/EditMode/Movement/NpcMoverTests.cs`
  hoặc thêm có chọn lọc vào regression suite hiện có

**Status:** DONE

**Work:**
- Test target change -> repath
- Test roads dirty -> repath
- Test next step blocked -> repath
- Test `ClearAll()` reset route state
- Test road speed multiplier vẫn có hiệu lực

**Acceptance:**
- Test pass ổn định
- `GridAgentMoverLiteTests` đã được thêm và pass trong Unity Test Runner

---

# Pass 2 — Stop reservation / no-stack-on-stop

## Task 2.1 — Add stop reservation state
**Files:**
- `Assets/_Game/Core/GridAgentMoverLite.cs`
  hoặc tách helper riêng nếu muốn sạch code

**Status:** DONE

**Work:**
- Thêm reservation maps:
  - cell -> owner npc
  - npc -> reserved stop cell
- Chỉ reserve cho target stop cell
- Không reserve cho transit path cells

**Acceptance:**
- Cấu trúc dữ liệu reservation hoạt động độc lập với path cache

---

## Task 2.2 — Integrate stop reservation into `StepToward`
**Files:**
- `Assets/_Game/Core/GridAgentMoverLite.cs`

**Status:** DONE

**Work:**
- Trước khi bước vào target cell cuối, thử acquire stop reservation
- Nếu target đang bị NPC khác giữ:
  - wait/retry
  - không move vào target
- Nếu acquire được:
  - cho move vào target
  - return arrived khi tới nơi

**Acceptance:**
- 2 NPC không cùng đứng trên một target stop cell
- Runtime Editor đã verify stop-cell overlap được chặn ở target cuối

---

## Task 2.3 — Release reservation correctly
**Files:**
- `Assets/_Game/Core/GridAgentMoverLite.cs`
- chỗ cleanup lifecycle nếu cần

**Status:** DONE

**Work:**
- Release reservation khi:
  - target đổi
  - NPC rời target cell
  - job complete/cancel/fail
  - mover clear all
  - new run/load

**Acceptance:**
- Không có reservation leak
- NPC sau có thể dùng lại target cell khi NPC trước rời đi

---

## Task 2.4 — Add reservation tests
**Files:**
- `Assets/_Game/Tests/EditMode/Movement/NpcMoverReservationTests.cs`
  hoặc thêm vào mover tests

**Status:** DONE

**Work:**
- NPC A acquire target cell
- NPC B cùng target -> wait
- A release -> B arrive được
- Target đổi -> reservation cũ được release

**Acceptance:**
- Test pass ổn định
- Reservation coverage đã được thêm vào `GridAgentMoverLiteTests`

---

# Pass 3 — Smarter source/destination selection

## Task 3.1 — Add travel cost estimate API
**Files:**
- `Assets/_Game/Core/NpcPathfinder.cs`

**Status:** DONE

**Work:**
- Thêm `TryEstimateCost(from, target, out cost)`
- Có thể dùng chung A* hoặc pathfinding rút gọn

**Acceptance:**
- Có thể query cost tương đối giữa nhiều candidate

---

## Task 3.2 — Upgrade `ResourceFlowService`
**Files:**
- `Assets/_Game/Economy/ResourceFlowService.cs`

**Status:** DONE

**Work:**
- Thay Manhattan selection bằng travel-cost-aware selection
- Nếu estimate fail:
  - fallback Manhattan
  - hoặc bỏ candidate unreachable

**Acceptance:**
- Nguồn/đích được chọn hợp lý hơn theo network road thực tế
- Đã có test coverage cho path-cost pick + Manhattan fallback

---

## Task 3.3 — Upgrade executor-local source/dest pickers
**Files:**
- `Assets/_Game/Jobs/Executors/BuildWorkExecutor.cs`
- `Assets/_Game/Jobs/Executors/HaulBasicExecutor.cs`
- các helper chọn source/dest tương tự nếu có

**Status:** DONE (initial pass)

**Work:**
- Đổi các helper chọn source/destination từ Manhattan sang path cost estimate

**Acceptance:**
- NPC logistics bớt chọn candidate Manhattan-near nhưng path-xấu
- `HaulBasicExecutor` và `BuildWorkExecutor` đã được cập nhật ở pass đầu
- Đã có regression coverage cho selection behavior của `HaulBasicExecutor` / `BuildWorkExecutor`

---

# Manual Smoke Checklist

## Runtime movement
- [ ] NPC bám road khi có road corridor rõ ràng
- [ ] NPC đi ground khi không có road
- [ ] NPC đi ground-road-ground khi chỉ có road ở giữa
- [ ] NPC đi vòng obstacle thay vì cắt xuyên building/site

## Dynamic changes
- [x] Remove road trong lúc NPC đang đi -> NPC repath
- [x] Add road mới -> path mới tận dụng road nếu phù hợp

## Target contention
- [ ] 2 NPC cùng một entry cell -> không đứng chồng nếu pass 2 đã xong

## Job flows
- [ ] Harvest hoạt động đúng
- [ ] Haul hoạt động đúng
- [ ] Build work hoạt động đúng
- [ ] Resupply tower hoạt động đúng

---

# Suggested Commit Breakdown

## Commit 1
- `movement: add weighted NPC pathfinder`

## Commit 2
- `movement: route NPC mover through road-aware paths`

## Commit 3
- `movement: invalidate cached NPC routes on road changes`

## Commit 4
- `tests: add NPC pathfinding and mover coverage`

## Commit 5
- `movement: prevent NPC stop-cell overlap`

## Commit 6
- `logistics: prefer path-cost-aware source and destination picks`

## Commit 7
- `movement: enforce road-first NPC pathing`

## Commit 8
- `movement: reduce road-first pathfinder jitter and asymmetry`

## Commit 9
- `tests: add regression coverage for road-first pathing`

## Commit 10
- `tests: add dynamic road change mover coverage`

---

# Definition of Done

## Pass 1 DONE khi
- NPC không còn dùng Manhattan step đơn giản
- NPC tránh building/site/OOB
- NPC ưu tiên road rõ ràng
- Partial road path hoạt động
- Nếu có road backbone usable thì NPC bám road backbone thay vì ground shortcut
- Job executors hiện tại vẫn chạy ổn
- Regression/unit tests pass

## Pass 2 DONE khi
- NPC không đứng chồng ô khi dừng/interact
- Wait/retry ổn định
- Không leak reservation state

## Pass 3 DONE khi
- Source/destination selection phản ánh đường đi thực tế tốt hơn Manhattan
- Runtime vẫn ổn định sau thay đổi
