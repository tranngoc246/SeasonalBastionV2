# PART 15 — DEBUG TOOLING & DEV CHEATS (SOLO DEV ACCELERATOR) — SPEC v0.1

> Mục tiêu: tăng tốc dev x3–x10, giảm thời gian “đoán bug”.
- Debug panels / cheats an toàn (chỉ dev build)
- Visual overlays: grid/occupancy/road/claims/jobs
- Sim controls: force season/day, spawn wave, time scale
- Inspectors: entity inspector (buildings/npcs/towers/enemies), job inspector
- Logging utilities + perf counters (GC alloc)

**Nguyên tắc**: debug tooling không được làm rối core logic; chỉ gọi service public APIs.

---

## 1) Dev build gating

### 1.1 Define flags
- `#if UNITY_EDITOR || DEVELOPMENT_BUILD`
- `DEV_TOOLS_ENABLED` compile define (optional)

### 1.2 Entry
- Press `F1` to toggle DevPanel
- In builds: DevPanel only if DEVELOPMENT_BUILD

---

## 2) DevPanel UI (UI Toolkit recommended)

### 2.1 Tabs
1) **Run/Time**
2) **Spawn**
3) **Resources**
4) **Entities**
5) **Jobs**
6) **Combat**
7) **Overlays**
8) **Save/Load**

### 2.2 Core layout
- Left: tab list
- Right: tab content
- Bottom: console log feed (last 20 lines)

---

## 3) Run/Time tab

### 3.1 Controls
- Pause / Resume sim
- Set time scale: 0.5x/1x/2x/3x/5x
- Force season: Spring/Summer/Autumn/Winter
- Force day index: +1 / -1 / set
- Jump to Defend start (Autumn day 1)
- Trigger end-of-day (fire day end event)

### 3.2 Display
- Current season/day
- SimTimeScale
- Real time since run start

---

## 4) Spawn tab

### 4.1 NPC spawn
- Spawn 1 NPC (unassigned)
- Spawn N NPC (N input)
- Assign spawned NPC to:
  - HQ / Warehouse / Forge / Armory / BuilderHut
- Teleport NPC to selected cell (optional)

### 4.2 Building spawn (bypass build pipeline)
For quick testing:
- Spawn building by DefId at cursor cell (still uses PlacementService validate/commit).
- Option: ignore cost (still create BuildSite? off by default)

### 4.3 Tower spawn
- Spawn tower at cursor cell with full ammo

---

## 5) Resources tab

### 5.1 Global add
- Add resource to:
  - HQ
  - nearest Warehouse
  - selected building
Options:
- Resource type dropdown
- Amount input
- Clamp to cap toggle

### 5.2 Producer fill/drain
- Fill local storage for selected producer
- Drain local storage (set to 0)

### 5.3 Ammo debug
- Set Forge ammo local to X
- Set Armory ammo to X
- Set selected tower ammo to X

---

## 6) Entities tab (Inspector)

### 6.1 Selection source
- From currently selected object in game (building/tower/npc/enemy)
- Or search by Id (text field)

### 6.2 Building inspector fields
- Id, DefId, Tier
- Anchor, Entry, EntryCell
- HP/MaxHP
- Flags
- Storage amounts/caps
- Workplace slots + assigned NPCs
Actions:
- Damage -10 / -100
- Heal full
- Toggle destroyed (danger)
- Dump storage to log
- Rebuild indices (WorldIndexService.RebuildAll)

### 6.3 NPC inspector
- Id, Role, Status, Cell
- AssignedWorkplace
- CurrentJobId
- Carry (type/amount)
Actions:
- Clear job (forces cleanup)
- Teleport to cell
- Assign role quick-set

### 6.4 Tower inspector
- Id, ammo current/max, fire timer
- NeedsAmmo flag
Actions:
- Set ammo to 0 / full
- Force ammo request event

### 6.5 Enemy inspector
- Id, HP, cell, status, target
Actions:
- Kill enemy
- Teleport

---

## 7) Jobs tab (Job inspector)

### 7.1 Queue viewer
- Pick workplace id dropdown (HQ/Warehouse/Forge/Armory/selected building)
- Show:
  - queue count
  - list first 20 jobs:
    - jobId, archetype, status, claimedBy, siteId, resource, amount
Actions:
- Cancel job (destroy)
- Reset job to Created (danger)
- Clear queue stale ids (optional cleanup)

### 7.2 Claims viewer
- Show total claims count
- For selected job:
  - show source/dest/tower claim keys
Actions:
- Release all claims (panic button)

### 7.3 Job provider counters
- Display per workplace throttling:
  - active harvest/haul/craft/resupply counts
Actions:
- Reset provider counters

---

## 8) Combat tab

### 8.1 Wave controls
- Spawn wave by WaveDefId
- Spawn enemy batch:
  - enemyDefId, count, lane
- Kill all enemies
- Toggle “god mode” (HQ invincible) (dev only)

### 8.2 Combat metrics
- Active enemies
- Waves cleared today
- HQ HP
- Towers firing count

---

## 9) Overlays tab (visual debug)

### 9.1 Grid overlays (toggle)
- Show grid coordinates
- Show road cells
- Show occupancy (building id)
- Show driveway conversions (highlight)
- Show build sites occupancy

### 9.2 Job overlays
- Show NPC job target cell
- Show lines from NPC → source/dest (Gizmos)
- Show tower ranges (range rings)

### 9.3 Claims overlays
- Display claimed buildings/towers with red border
- Tooltip: claimed by npc id

Implementation notes:
- Use `OnDrawGizmos` in a `DevOverlayRenderer`
- Avoid allocations: precompute colors & strings, throttle updates

---

## 10) Save/Load tab

### 10.1 Controls
- Save Run
- Load Run
- Delete Run Save
- Save Meta
- Load Meta
- Reset Meta (confirm)

### 10.2 Display
- schemaVersion
- last save timestamp
- file sizes

---

## 11) Logging & perf counters

### 11.1 Log channels
- `LOG.Economy`, `LOG.Jobs`, `LOG.Combat`, `LOG.UI`, `LOG.Save`
- Toggle channels in DevPanel

### 11.2 GC alloc monitoring
- Unity Profiler API (editor) or `GC.GetAllocatedBytesForCurrentThread()`
- Display “alloc per second” (rough)

### 11.3 Assertions
- Placement determinism assertion:
  - same input → same result across frames
- Claim leak assertion:
  - after job completion, claim count should drop

---

## 12) Implementation checklist (Part 15)
- [ ] DevPanel toggles with F1 in editor/dev build
- [ ] Can force seasons/days safely
- [ ] Can spawn buildings with validation
- [ ] Can inspect NPC/job states without exceptions
- [ ] Overlays do not allocate per frame
- [ ] Save/load buttons work

---

## 13) Next Part (Part 16 đề xuất)
**Packaging & Release Checklist**: steam build pipeline, settings defaults, crash reporting, minimum spec, save compatibility policy.

