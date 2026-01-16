# PART 27 — VERTICAL SLICE #1 IMPLEMENTATION PLAN (FROM ZERO PROJECT) — LOCKED SPEC v0.1

> Mục tiêu Part 27: bạn có thể **khởi tạo dự án Unity sạch**, dựng kiến trúc theo Part 25–26, và chạy được **Vertical Slice #1**:
- Scene chạy + tick loop
- Place road + place building (HQ/House) đúng rule entry/driveway
- Spawn NPC + assign workplace
- Harvest (Farm/Lumber) → local storage → HaulBasic → HQ/Warehouse
- Minimal UI debug (không cần đẹp)
- Deterministic-ish: seed + tick order cố định

Không bao gồm trong VS#1:
- BuildOrder sites/commit (để Sprint 2)
- Ammo/Combat/Rewards (Sprint 3+)

---

## 0) Quy ước “Definition of Done” cho VS#1
VS#1 Done khi:
1) Mở game → scene chạy ổn định 60fps
2) Có thể đặt Road (orthogonal) và Building có EntryCell + driveway len=1
3) Có thể spawn NPC, gán NPC vào Farm/Lumber/Warehouse/HQ (manual)
4) Worker harvest tạo resource vào **local storage** của producer
5) Transporter haul resource về **HQ hoặc Warehouse** (resource tăng khi deliver)
6) Không có softlock do claim leak (debug: ReleaseAllClaims works)
7) Save/Load chưa cần (VS#1), nhưng logging + debug overlay có.

---

## 1) KHỞI TẠO DỰ ÁN (Day 0 / Step 0)

### 1.1 Unity version
- Unity **2022.3 LTS** (khuyến nghị cho stability + asset ecosystem)
- Create project: **2D (URP optional)**  
  - Nếu bạn muốn giữ đơn giản: 2D Built-in ok.

### 1.2 Packages (cài ngay từ đầu)
Required:
- Input System (New)
- TextMeshPro (for debug labels, optional)
Recommended:
- Unity 2D Tilemap (if using tilemap roads/interest)
- Addressables (optional, defer)
- Unity Test Framework (EditMode tests)

### 1.3 Project Settings (LOCKED baseline)
- Player:
  - Active Input Handling: **Input System Package (New)** (hoặc Both nếu bạn cần legacy)
- Editor:
  - Asset Serialization: **Force Text**
  - Version Control: **Visible Meta Files**
- Time:
  - Fixed Timestep 0.02 (default)
- Quality:
  - VSync off (dev), targetFrameRate 60
- URP (nếu dùng):
  - 2D Renderer, bật Pixel Perfect chỉ khi bạn chọn pixel art

### 1.4 Git (ngay lập tức, trước khi code)
- Init git repo
- `.gitignore` cho Unity (chuẩn)
- Commit 1: “init Unity project”
- Branch strategy:
  - `main` (stable), `dev` (integration), feature branches `feat/...`

### 1.5 Folder layout (theo Part 26)
- Tạo `Assets/Game/...` đúng tree trong Part 26
- Tạo asmdef skeletons (chưa cần reference đầy đủ)

**Acceptance check**
- Mở project, không error, git commit clean.

---

## 2) SPRINT 1 — BACKBONE + GRID + PLACEMENT (VS#1 core)

### Day 1 — Core Contracts + Boot Graph
**Tasks**
1) Copy Part 25 contracts vào `Assets/Game/Core/Contracts/`
2) Copy Part 26 skeletons vào folders tương ứng
3) Tạo `GameBootstrap` scene:
   - Empty object `Bootstrap` với `GameBootstrap` component
4) Compile clean

**Acceptance**
- Play mode chạy, không NullReference
- `GameLoop.Tick()` được gọi (log mỗi 2s, không spam)

---

### Day 2 — EventBus + Notifications minimal
**Tasks**
1) Implement `EventBus` (publish/subscribe)
2) Implement `NotificationService` tối thiểu:
   - stack max 3, newest first
   - cooldown per key (throttle)
3) Tạo Debug UI cực đơn giản:
   - `OnGUI()` tạm thời hoặc UI Toolkit label
   - show list visible notifications

**Acceptance**
- Press key `N` → push 5 notifications → chỉ thấy 3 cái mới nhất
- Cooldown key hoạt động (spam không bùng)

---

### Day 3 — GridMap + Road placement tool (orthogonal)
**Tasks**
1) Implement `GridMap` with occupancy array
2) Implement `PlacementService.PlaceRoad`:
   - only if inside + not blocked
3) Create debug input:
   - Mouse click place road on hovered cell
   - Hold Shift to erase road (optional)

**Acceptance**
- Place road across map, no overlaps with buildings (chưa có building)
- Road cannot be placed outside bounds

---

### Day 4 — Building footprint + occupancy
**Tasks**
1) Create minimal `BuildingDef` (ScriptableObject):
   - Id, FootprintW/H
   - EntrySide (Dir4) + EntryCell offset rule
   - Tags: IsHQ / IsWarehouse / IsProducer / IsForge / IsArmory / IsTower
   - Storage caps per resource type
2) Implement `PlacementService.ValidateBuilding`:
   - bounds + overlap + blocked by site
3) Implement occupancy apply (commit):
   - set building cells -> occupancy building id
   - create BuildingState in World

**Acceptance**
- Place HQ footprint (e.g., 3x3) and House (2x2)
- Cannot place overlapping
- Occupancy reflects correct cells

---

### Day 5 — Entry/Driveway rule (LOCKED)
Rule recap:
- EntryCell is world point along building edge; not required to fall on cell center
- Condition “connected to road”: nearest road cell within **driveway length = 1**
- Driveway conversion: only **1** nearest road cell becomes road (if it wasn’t), deterministic
- Road placement: N/E/S/W only

**Tasks**
1) In `BuildingDef`, define entry side + entry offset; derive:
   - `EntryAnchorCell` (cell adjacent to building edge)
   - `DrivewayCandidateCells` = cells within manhattan distance 1 from entry anchor
2) Validate:
   - if no road within driveway cells → fail `NoRoadConnection`
3) Commit:
   - choose nearest candidate deterministically:
     - priority: distance then stable order N,E,S,W then cell coords
   - set that cell to Road (if not road)

**Acceptance**
- Place building without road near entry → blocked with notification reason
- Place building with road 1 cell away from entry → OK + driveway cell becomes road
- Deterministic: same placement always picks same driveway cell

---

### Day 6 — WorldIndex + derived lists
**Tasks**
1) Implement `WorldIndexService.RebuildAll`:
   - scan buildings, classify by def tags
2) Hook to building created event:
   - On BuildingPlacedEvent → Index incremental add (optional)
3) Debug overlay: show counts producers/warehouses/HQ/towers

**Acceptance**
- Placing Farm updates producers list
- Placing Warehouse updates warehouses list

---

### Day 7 — Buffer day (stabilize + tests)
**Tasks**
1) Create EditMode tests (Unity Test Framework):
   - `Placement_NoOverlap`
   - `Placement_DrivewayRule`
   - `Notifications_Max3_NewestFirst`
2) Fix any compile/perf issues

**Acceptance**
- Tests pass
- Enter/exit play mode no errors

---

## 3) SPRINT 2 — NPC + JOBS + RESOURCE FLOW (VS#1 complete)

### Day 8 — NPC store + manual assignment UI (debug)
**Tasks**
1) Implement `NpcState` + `NpcStore`
2) Spawn NPC with key `P` at HQ cell
3) Workplace assignment:
   - select NPC then click building to assign
   - fire `NPCAssignedEvent`

**Acceptance**
- Spawn 3 NPCs, assign 1 to Farm, 1 to Lumber, 1 to Warehouse
- Unassigned NPC shown in debug list

---

### Day 9 — StorageService baseline (HQ/Warehouse no ammo)
**Tasks**
1) Implement storage amounts in `BuildingState` + caps from `BuildingDef`
2) Implement `StorageService`:
   - `CanStore` enforces:
     - Warehouse/HQ: `Ammo` forbidden
   - Add/Remove clamp to caps
3) Debug UI: show selected building storage snapshot

**Acceptance**
- Add wood to warehouse works
- Add ammo to warehouse returns 0
- Add ammo to armory works (even if armory not built yet, use tag on def)

---

### Day 10 — ResourceFlowService (pick source/dest)
**Tasks**
1) Implement `TryPickSource` / `TryPickDest`:
   - deterministic nearest by manhattan
   - tie-break by building id
2) Implement `Transfer` atomic semantics

**Acceptance**
- When multiple warehouses, pick nearest
- No negative amounts; caps respected

---

### Day 11 — Claims + JobBoard
**Tasks**
1) Implement `ClaimService.ReleaseAll` (no leaks)
2) Implement `JobBoard` queue per workplace
3) Add debug panel:
   - show jobs per workplace
   - button “Release all claims for selected NPC”

**Acceptance**
- Enqueue 10 jobs; peek returns deterministic first
- Claim prevents two NPC claim same key

---

### Day 12 — JobScheduler skeleton to working (Harvest + HaulBasic)
**Tasks**
1) Decide minimal NPC movement model for VS#1:
   - Option A: instant teleport between cells (fast to validate logic)
   - Option B: simple grid stepper (no pathfinding yet)
> v0.1 recommended for speed: **A trong 2 ngày**, sau đó upgrade movement.

2) Implement HarvestExecutor:
   - NPC at producer: work timer
   - On complete: Add to producer local storage
3) Implement HaulBasicExecutor:
   - pick source producer local, pick dest warehouse/hq
   - take amount into carry
   - deliver increases dest storage

**Acceptance**
- Farm worker harvest -> farm local food increases
- Warehouse transporter moves food -> warehouse food increases
- Resource only increases on deliver (not per time without worker)

---

### Day 13 — Workplace rules (HQ roles, warehouse roles)
LOCKED:
- HQ NPC can do Build/Repair/HaulBasic (VS#1 only uses HaulBasic)
- Producer NPC can do Harvest
- Warehouse NPC can do HaulBasic (basic resources only)
(Ammo roles later)

**Tasks**
1) Add workplace role flags in BuildingDef:
   - `WorkRoles` bitmask
2) Scheduler assignment:
   - NPC only pulls jobs allowed by workplace roles
3) Add notifications when NPC has no valid jobs:
   - “NPC không có việc để làm” (throttle)

**Acceptance**
- NPC at HQ does hauling only
- NPC at Farm does harvest only
- If no jobs exist, notification appears once

---

### Day 14 — Replace teleport with simple mover (optional but recommended)
**Tasks**
1) Implement `GridAgentMoverLite`:
   - move 1 cell per tick along Manhattan path ignoring obstacles for now
2) Executors use mover:
   - states: Moving → Working → Carrying → Delivering
3) Keep deterministic path: horizontal then vertical (or vice versa)

**Acceptance**
- You can see NPC walking cell-by-cell
- Still stable and no stuck due to obstacles (because we keep map open)

---

## 4) VS#1 FINALIZATION — What you demo
- New scene: place roads, place HQ/House/Farm/Lumber/Warehouse
- Spawn NPCs, assign them
- Observe production & hauling working
- Notifications show blocked placement/no jobs/storage full (optional)
- Debug overlay shows storages and job queues

---

## 5) Debug/Dev tools (minimum needed for VS#1)
Create `DevPanel` (IMGUI ok for now):
- Buttons:
  - Spawn NPC
  - Give resources to selected building
  - Drain resources
  - Clear notifications
  - Release all claims for NPC
- Toggles:
  - Log Jobs/Economy
- Readouts:
  - Selected building def + storage
  - Selected NPC workplace + job

---

## 6) “Common pitfalls” checklist (để khỏi làm lại)
- Placement: driveway conversion must happen at **commit**, not at validate only
- ResourceFlow: never allow Warehouse/HQ store ammo
- Claims: always release on job complete/fail/cancel
- Determinism: iterate entities in ascending id order
- UI: đừng build đẹp trước; chỉ cần debug panels

---

## 7) What happens after VS#1 (Part 28 đề xuất)
**Vertical Slice #2**: BuildOrder + BuildSite + builder fetch/deliver/work + commit (Part 9)  
Sau đó: Ammo pipeline → Combat → Rewards → Run loop.

