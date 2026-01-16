# PART 14 — IMPLEMENTATION PLAN (MILESTONES + SPRINT TASKS + VERTICAL SLICE ORDER) — SPEC v0.1

> Mục tiêu: biến toàn bộ SPEC thành roadmap code thật, tối ưu cho solo dev + AI support:
- Ưu tiên “vertical slice playable” càng sớm càng tốt
- Mỗi milestone có acceptance criteria rõ ràng
- Chia sprint theo hệ thống (Data → Core sim → UI → Content)
- Quy tắc branch + testing + CI tối thiểu

Giả định: Unity 2022.3 LTS, MonoBehaviour ưu tiên, asmdef.

---

## 0) Project hygiene (ngày 0–1)

### 0.1 Repo structure (stable)
- `Assets/Game/Core/` (WorldState, Stores, RunClock, JobBoard)
- `Assets/Game/Defs/` (ScriptableObject defs)
- `Assets/Game/Services/` (ResourceFlow, Placement, BuildOrder, Combat)
- `Assets/Game/UI/` (UXML/USS/Binder/Screens)
- `Assets/Game/Tests/` (EditMode tests)
- `Assets/Game/Debug/` (cheats, debug panels)

### 0.2 Coding rules (solo friendly)
- No LINQ in runtime loops
- No allocations per tick in schedulers
- Use “single source of truth” services (one instance)
- One feature = one PR = one test set

### 0.3 CI minimal (optional)
- Unity Test Runner in GitHub Actions (EditMode only)

---

## 1) Milestone A — Data + Boot + Validator (foundation)

### Scope
- DataRegistry + BootFlow (Part 1)
- Schema/Validator (already spec)
- RunClock + SpeedControls (Part 2)
- Notifications stack (Part 4) minimal

### Acceptance criteria
- Game boots → loads defs → validates
- Can start a “run session” with RunClock ticking
- Notification stack shows 3 items, pushes old

### Tasks
1. Implement DataRegistry (load defs, maps by id)
2. Validator: unique ids, missing refs, caps non-negative
3. RunClock service: seasons/days, speed apply
4. NotificationService: key cooldown, dedupe
5. Debug overlay: show season/day/timeScale

---

## 2) Milestone B — Grid/Placement/Road/Driveway (playable building placement)

### Scope
- GridMap + occupancy + road layer
- Placement validation + Entry/Driveway conversion (Part 5)
- Simple build ghost UI (no delivery yet)

### Acceptance criteria
- Place road (orthogonal)
- Place building only if entry near road (driveway len=1)
- Driveway converts nearest road cell deterministically
- Occupancy blocks overlaps

### Tasks
1. GridMap core arrays + API
2. RoadPlacementService
3. BuildingPlacementService (Validate/Commit)
4. Debug visualization (gizmos/tile overlay)
5. UI: build tool + blocked reason

---

## 3) Milestone C — Entity Stores + WorldState (runtime backbone)

### Scope
- Entity stores (Part 6)
- WorldOps: place/destroy building -> state created
- Basic save/load skeleton (optional but recommended)

### Acceptance criteria
- Place building creates BuildingState in store
- Destroy building clears occupancy and store
- Spawn NPC state in store
- Save+Load restores grid + entities

### Tasks
1. EntityStore generic
2. BuildingState/NpcState/TowerState/EnemyState structs
3. WorldState container
4. WorldOps place/destroy
5. SaveService minimal (json) + version field

---

## 4) Milestone D — Resource Flow primitives (economy plumbing)

### Scope
- ResourceFlowService + StorageSelector (Part 7)
- WorldIndexService rebuild
- Storage rules (warehouse no ammo)

### Acceptance criteria
- Can query nearest storage with amount/space
- Transfer basic resources between buildings
- Ammo cannot be stored in warehouse/hq

### Tasks
1. Storage accessor + rule enforcement
2. Selector deterministic
3. Indices rebuild hooks on place/destroy
4. Debug commands: add resources, print totals

---

## 5) Milestone E — Job system v0 (assignment + execution minimal)

### Scope
- JobBoard + ClaimService + Scheduler (Part 8)
- Executor for 2 archetypes:
  - Harvest (food/wood)
  - HaulBasic

### Acceptance criteria
- Worker harvest adds to producer local storage (no auto income)
- Transporter hauls to HQ (or Warehouse if exists)
- No duplication due to claims
- Notifications for local full / warehouse full

### Tasks
1. JobBoard create/queue
2. Scheduler assigns idle NPC by workplace
3. Executor templates for harvest/haul
4. Producer provider + Warehouse provider (simple)
5. Debug UI: show current job per NPC

---

## 6) Milestone F — Build pipeline (orders + sites + delivery + complete)

### Scope
- BuildOrderService + BuildSiteStore (Part 9)
- Delivery jobs + Work jobs
- PlaceNew complete creates building (using Part 5+6)

### Acceptance criteria
- Player selects build → creates build site
- Builder fetches resources and delivers to site
- When delivered satisfied, builder works and building completes
- Cancel order cleans site + claims

### Tasks
1. BuildSite entity + occupancy mask
2. BuildSiteJobProvider (delivery + work)
3. Builder executor for delivery/work
4. UI: build queue list + site progress
5. Notifications on insufficient resources

---

## 7) Milestone G — Ammo pipeline (forge/armory + tower requests)

### Scope
- Forge craft ammo (recipe-based)
- Armory haul ammo + resupply towers (Part 10)
- Tower ammo monitor (<=25%)

### Acceptance criteria
- Smith crafts ammo only with inputs
- Ammo stored at forge local then moved to armory
- Towers consume ammo per shot later (combat)
- When tower low, armory runner prioritizes resupply

### Tasks
1. RecipeDef + craft job provider
2. Smith executor (fetch inputs seq + craft)
3. Armory provider + executor (haul ammo, resupply)
4. UI: ammo counters in building inspect

---

## 8) Milestone H — Combat minimal (waves + towers + enemies)

### Scope
- WaveDirector (Part 11)
- Enemy movement/attack
- Tower targeting/firing + ammo consumption

### Acceptance criteria
- Defend day spawns enemies from lanes
- Towers shoot if ammo available
- Enemies damage buildings/HQ
- Run ends on HQ destroyed

### Tasks
1. LaneDef + spawn points
2. WaveDef batches + director
3. Enemy mover + attack logic
4. Tower firing + ammo per shot
5. Notifications: wave start/end, HQ under attack

---

## 9) Milestone I — Rewards + Meta + Run end screens

### Scope
- Reward offering (deterministic) + modal UI (Part 12/13)
- RunSummary screen
- MetaSave persistence (unlock tree minimal)

### Acceptance criteria
- After defend day end: reward modal shows 3 choices
- Player picks, modifier applied
- Victory/Defeat shows summary and grants meta currency
- MetaSave persists across sessions

### Tasks
1. RewardDef + pools + OfferService
2. RewardSelectionService + modal binder
3. RunOutcomeService + summary UI
4. MetaSave json + migration
5. Unlock tree basic (Warehouse/Forge/Armory)

---

## 10) Milestone J — Polish + Public readiness

### Scope
- Tutorial guidance (notifications + highlight)
- Performance pass (allocation scan)
- UX: keybinds, settings, audio cues
- QA tests on core services

### Acceptance criteria
- First run funnel clear, no deadlocks
- 30–60 minutes playable loop (depending pacing)
- No critical bugs in save/load + defend transition
- Stable 60fps typical map sizes

---

## 11) Sprint slicing (solo-friendly)

### Sprint 1 (Vertical Slice Economy)
- Milestone A + B + C (boot + placement + world)
Deliver: place HQ/farm/lumber/tower on map; HUD shows day.

### Sprint 2 (NPC Economy)
- Milestone D + E
Deliver: worker harvest + transporter haul; resources visible.

### Sprint 3 (Build pipeline)
- Milestone F
Deliver: builder consumes resources to construct new building.

### Sprint 4 (Ammo)
- Milestone G
Deliver: craft ammo + armory resupply pipeline works.

### Sprint 5 (Combat)
- Milestone H
Deliver: defend day with waves + tower shooting.

### Sprint 6 (Roguelite loop)
- Milestone I + UI polish (Part 13)
Deliver: rewards + meta + run summary.

---

## 12) Branching & PR workflow (simple)
- `main` stable
- `feature/<milestone>-<topic>`
- PR checklist:
  - acceptance criteria met
  - playtest notes
  - no GC alloc spikes in Update loops
  - minimal editmode tests for services

---

## 13) Test plan (minimum)
EditMode tests:
- Placement validate/commit deterministic
- Storage rules: warehouse cannot store ammo
- Selector tie-break by id
- ClaimService exclusivity
- Reward offer determinism with seed

Playmode smoke:
- Start run → place buildings → run 1 defend day

---

## 14) Next Part (Part 15 đề xuất)
**Debug tooling & dev cheats**: spawn resources, force season, spawn wave, show path overlays, job inspector — để dev nhanh hơn x3.

