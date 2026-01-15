# SEASONAL BASTION – HIGH-LEVEL PROJECT OVERVIEW
**Senior Software Auditor Analysis Report**  
**For:** ChatGPT GO (AI Handoff Context)  
**Date:** 2026-01-12  
**Repository:** tranngoc246/SeasonalBastion  

---

## 1. PROJECT SUMMARY

### Game Type
Grid-based city builder and tower defense hybrid built in Unity 2022.3 LTS. Players construct buildings, manage resources, and defend against waves of enemies on a seasonal tilemap grid.

### Core Simulation Philosophy
- **Deterministic grid-based logic:** All entities (NPCs, buildings, resources) operate on discrete cell coordinates.
- **Authority-based design:** Single sources of truth for critical systems (pathfinding, job lifecycle, tool modes, stand positions) prevent race conditions and state conflicts.
- **Agent-driven NPC system:** NPCs autonomously make decisions (idle → job selection → execution → completion) without centralized AI director.
- **Claim-based resource allocation:** Exclusive resource claims (interest tiles, resource nodes, grid cells) prevent crowding and contention.
- **No physical item logistics in Phase 1:** Resource production exists conceptually but items do not move through the world.
- **Session-based development:** Append-only design document with locked technical guarantees ensures consistency across AI-assisted development sessions.

### Current Phase
Pre-alpha, implementing core NPC behavior and tool systems. Combat, save/load, and UI systems are stubbed but not implemented.

### Technical Stack
C# with MonoBehaviour architecture (no ECS), New Input System (locked asset), asmdef modularization, Unity Tilemap for world representation.

---

## 2. CORE SYSTEM MAP

### 2.1 Major Systems and Responsibilities

#### World & Grid (Game.Map / Game.World)
- **GridService:** Tilemap reference holder, world↔cell coordinate conversion
- **Tilemaps:** Ground (walkable base), Road (speed boost), Interest (NPC interaction markers + obstacles), Building (placement), Highlight (visual feedback)
- **MinimalMapGenerator:** Runtime test map generator (ground fill + scattered interest tiles)

#### Pathfinding (Game.Pathfinding)
- **PathfindingService:** Scene-level facade for path requests
- **AStarPathfinder:** 4-directional A* with Manhattan heuristic
- **DefaultGridWalkability:** Single walkability authority (ground + not-occupied + not-interest + not-blocked)
- **Cost model:** Road cells have lower cost than ground (preference, not hard rule)

#### NPC System (Game.NPC)

**Movement**
- **GridAgentMover:** Single source of truth for AgentId, movement state, and path execution. Implements cell reservation (current + next or pass-through mode).
- **GridReservationService:** Prevents NPCs from colliding by reserving cells.
- **AgentIdProvider:** Issues unique agent IDs on spawn.

**Jobs**
- **JobManager:** Global FIFO queue for all jobs. Supports agent-bound jobs to prevent mis-assignment.
- **JobRunner:** Per-NPC job executor. Lifecycle manager (Moving → Working → Custom phases). ONLY authority for releasing claims on job completion/failure/cancellation.
- **IJob interface:** Defines job contract (TargetCell, WorkDurationSeconds, WorkingState, JobId).
- **Job types:** SimpleJob (unbound), RoamJob (wander), InspectJob (interest tile interaction, agent-bound), LeisureJob (building entry/vanish, agent-bound, custom-phase), ResourceWorkJob (resource node work, agent-bound).

**Decision**
- **IdleDecisionController:** Per-NPC decision-making. Scans environment, filters by claims/cooldowns, scores options (distance + variety), pre-selects targets, enqueues jobs.
- **NPCMood:** Read-only happiness value that influences decision priorities.

**Claim Services**
- **InterestClaimService:** Ensures 1 interest tile = 1 NPC. Includes tile-based cooldown after release (3s default, Time.time-based).
- **ResourceNodeClaimService:** Ensures 1 resource node = 1 NPC. Also claims stand cell to reduce crowd waiting.

**Presentation**
- **NpcAnimatorDriver:** Drives Animator based on state + facing. Decoupled from AI/Job logic.
- **AgentFacing:** Manages sprite flip direction.
- **NpcVanishController:** Hides NPC visuals (does NOT disable GameObject) during leisure inside-building phase.

#### Building System (Game.Buildings)
- **BuildingDefinition:** ScriptableObject defining building type, size, entry offset.
- **BuildingInstance:** Runtime instance (cell position, entry cell, leisure slots).
- **BuildingRegistryService:** Tracks all placed buildings, queries (e.g., nearest leisure with free slot).
- **BuildingOccupancyService:** Marks cells occupied by buildings (walkability blocker).
- **BuildingEnterService:** Manages building entry logic (claim slot, resolve stand cell, spawn inside).
- **BuildingPlacer / BuildingGhostPreview:** Placement tool with ghost preview (valid/invalid visual states).
- **BuildingMoveTool:** Move existing buildings (hide source, ghost follow mouse, commit move).
- **BuildingDemolishTool:** Double-click confirm demolish (removes tile, clears occupancy, unregisters).

#### Road System (Game.Roads)
- **RoadService:** Tracks road cells, adjacency checks.
- **RoadBuilder:** Drag-to-build roads with connectivity validation (must connect to existing roads).
- **RoadPreview:** Shows valid/invalid ghost state during drag.

#### Tool System (Game.Tools)
- **ToolStateRouter:** SINGLE authority for tool mode switching (Select / Build / Road / Demolish / Move). Controls TileSelector blocking and TileHighlighter enable/disable per mode.
- **TileSelector:** Click-based cell selection (can be blocked by tools).
- **TileHighlighter:** Single-cell visual highlight (must be disabled when tools use shared highlight tilemap for ghost rendering).

#### Interaction Authority (Game.World)
- **StandCellResolver:** SINGLE authority for resolving where NPCs stand when interacting with targets (interest tiles, buildings, resource nodes). Uses interaction ring (4-dir priority, diagonal fallback). Checks walkability, occupancy, and optionally reservation via reflection to avoid asmdef cycles.

#### Resource System (Game.World – Phase 1 stub)
- **ResourceNode:** Cell + WorkDuration. Treated as obstacles.
- **ResourceNodeClaimService:** Single-worker-per-node enforcement.
- **No storage, no logistics, no production chains yet.**

#### Debug Tools (Game.DebugTools)
- **DebugToolModeController:** Play-mode hotkeys (1/2/3/4/5) for tool switching. All changes route through ToolStateRouter.
- Various test scripts for building, pathfinding, job spawning (editor/test only).

#### Planned / Stubbed (Game._Planned, not implemented)
- Combat & enemy waves (GateController, TowerController, WaveDirector)
- Save/Load (SaveService)
- Request/Logistics system (RequestManager)
- UI beyond debug icons (NotificationStackView)

### 2.2 System Interaction Flow (Textual)

#### NPC Decision → Job → Movement → Release
1. **IdleDecisionController** (per NPC) runs periodically while NPC is Idle.
2. Scans environment: interest tiles, leisure buildings, resource nodes.
3. Filters candidates: claim status (InterestClaimService, ResourceNodeClaimService), cooldown, walkability.
4. Scores options: distance (closer better), variety (avoid recent choices), mood-based priority.
5. Pre-selects target + stand cell via StandCellResolver (ensures NPC never stands on target).
6. Pre-claims resource (interest tile or resource node) BEFORE enqueue to prevent race.
7. Enqueues agent-bound job to JobManager (global FIFO queue).
8. **JobRunner** (per NPC) dequeues job matching its AgentId.
9. JobRunner validates job (e.g., ResourceWorkJob re-checks claim at TAKE time).
10. JobRunner requests path from GridAgentMover via TrySetDestinationCell. Fails fast if no path.
11. GridAgentMover queries PathfindingService → AStarPathfinder → DefaultGridWalkability.
12. Mover reserves cells (current + next or pass-through mode) via GridReservationService.
13. Mover executes path, updates state (Moving / Waiting / Arrived).
14. JobRunner transitions: Moving → Working → (Custom for LeisureJob) → Idle.
15. On job completion/failure/cancellation, JobRunner calls InterestClaimService.ReleaseAll(agentId) and ResourceNodeClaimService.ReleaseAll(agentId).

#### Tool Placement Flow
1. User switches mode via ToolStateRouter (hotkey or future UI).
2. Router enables target tool (RoadBuilder / BuildingPlacer / MoveTool / DemolishTool).
3. Router blocks/unblocks TileSelector and enables/disables TileHighlighter per mode.
4. Tool shows ghost preview (valid/invalid based on walkability, occupancy, blocker tilemaps).
5. User commits placement → tool updates tilemap → registers instance → marks occupancy.

#### Walkability Check Chain
- Any system needing walkability queries PathfindingService.IsWalkableCell(cell).
- PathfindingService delegates to DefaultGridWalkability.IsWalkable(cell).
- Walkability checks: ground tile exists AND interest tile NOT present AND no obstacle tilemap tile AND not occupied by building.
- Interest tiles and resource nodes are hard obstacles (no bypass).

---

## 3. KEY AUTHORITIES & WHY THEY EXIST

### 3.1 StandCellResolver (Game.World.Runtime.Interaction)
**Why centralized:**  
NPCs must never stand on target cells (interest tiles, building footprints, resource nodes) to avoid visual/logic conflicts. Multiple systems (IdleDecisionController, BuildingEnterService, LeisureJob, ResourceWorkJob) need consistent stand-cell resolution.

**Centralization ensures:**
- Interaction ring priority (4-dir → diagonal) is uniform.
- Walkability, occupancy, and reservation checks are authoritative.
- No duplicate logic = fewer bugs when walkability rules change.

**Authority scope:**  
All stand cell resolution for interactions. No job, decision, or behavior may scan neighbor cells independently.

### 3.2 JobRunner (Game.NPC.Jobs)
**Why centralized per-agent:**  
Prevents multiple jobs executing simultaneously on one agent, orphaned claims when jobs fail or NPCs are disabled/destroyed, and state confusion.

**Authority scope:**
- Job execution phases (Idle → Moving → Working → Custom → Idle).
- ONLY entity that releases claims (InterestClaimService, ResourceNodeClaimService) on job end.
- Fail-fast on no-path conditions.
- LeisureJob custom-phase handling (enter / inside / exit).

**Why not global:**  
Global job execution requires complex mapping and distributed claim release, increasing bug surface area.

### 3.3 ToolStateRouter (Game.Tools)
**Why centralized:**  
Prevents tools from independently blocking TileSelector or managing highlight state, which caused ghost rendering conflicts.

**Authority scope:**
- Tool mode switching (Select / Build / Road / Demolish / Move).
- TileSelector blocking per mode.
- TileHighlighter enable/disable per mode.
- Tool enable/disable and preview clearing on mode exit.

### 3.4 PathfindingService (Game.Pathfinding)
**Why centralized:**  
All movement and reachability must use identical walkability rules.

**Centralization ensures:**
- Consistent walkability checks (ground + not-interest + not-occupied + not-blocked).
- Single A* implementation prevents divergent path logic.
- Uniform road cost preference.

**Authority scope:**
- Path finding (TryGetPath).
- Walkability checks (IsWalkableCell).
- Reachability probing (TryCanReach).

### 3.5 Claim Services (InterestClaimService, ResourceNodeClaimService)
**Why centralized per resource type:**  
Prevents crowding and contention.

**InterestClaimService scope:**
- 1 interest tile = 1 agent at a time.
- Tile-based cooldown after release (3s default, Time.time-based, pause-aware).
- No TTL on active claims (release tied to agent/job lifecycle).

**ResourceNodeClaimService scope:**
- 1 resource node = 1 agent at a time.
- Also claims stand cell to reduce reservation conflicts.
- No cooldown in current implementation.

**Why release authority is JobRunner only:**  
Centralizing release in JobRunner covers all exits (completion, cancellation, abandonment, disable, destroy).

### 3.6 GridAgentMover (Single Source of AgentId)
**Why AgentId authority:**  
Avoids cached/stale IDs leading to starvation.

**Authority scope:**
- AgentId property (read from mover, not cached).
- Movement state (Moving / Waiting / Arrived).
- Path execution and reservation lifecycle.

---

## 4. NPC LIFECYCLE OVERVIEW

### 4.1 Lifecycle Phases

**Idle**
- NPC has no job, no path, no goal.
- IdleDecisionController runs periodically (thinkInterval, default 1s).
- State: AgentState.Idle.

**Decision**
- Scans environment (interest tiles, leisure buildings, resource nodes).
- Filters by claim status, cooldown, walkability.
- Scores options (distance, variety, mood).
- Pre-selects target + resolves stand cell via StandCellResolver.
- Pre-claims resource BEFORE enqueue.
- Enqueues agent-bound job to JobManager.
- If all options fail: enqueues minimal wait job (0.2s InspectJob at current cell).

**Job Dequeue**
- JobRunner calls JobManager.TryDequeueForAgent(agentId) in Idle phase.
- PASS 1: scan queue for agent-bound jobs matching agentId.
- PASS 2: if no match and no other-agent-bound jobs in queue, dequeues first unbound job.
- If other-agent-bound jobs exist: mismatch cooldown (50ms default).

**Job Start**
- JobRunner validates job (e.g., ResourceWorkJob re-checks node claim at TAKE time).
- Requests path via mover.TrySetDestinationCell(job.TargetCell).
- Fail-fast if no path.

**Moving**
- Mover executes path, reserves cells via GridReservationService.
- Stuck detection: if cell doesn't change for failTimeoutSeconds (default 3s), abandon job.
- Reservation blocking: wait; if blocked near target too long, abandon.
- Path lost detection: if HasPath becomes false while moving, abandon.

**Working**
- NPC arrived at target stand cell.
- JobRunner ticks work timer.
- State: job.WorkingState.
- Complete when duration reached; abandon on return-path failure if displaced.

**Custom Phase (LeisureJob only)**
- Phases: GoingToEntry → Entering → Inside → Exiting → Done.
- JobRunner calls leisure.Start() and leisure.Tick(dt).
- Fail immediately if unreachable or slot unavailable.

**Completion / Failure**
- JobRunner clears job and returns NPC to Idle.
- Releases ALL claims.
- Released interest tiles enter cooldown (3s default).

### 4.2 Failure Paths and Handling

**Failure can occur at:**
- Decision stage (no targets, claim refuses, stand resolve fails)
- Dequeue stage (queue empty, other-agent jobs block queue)
- Start stage (claim mismatch at TAKE, no path)
- Movement (stuck, path lost, reservation blocked too long)
- Working (displaced + cannot return)
- Leisure custom phase (entry unreachable, slot unavailable)

**Handling philosophy:**
- Fail-fast: no infinite retries, no requeue for failed jobs.
- Release on all exits: JobRunner releases on completion/cancel/abandon/disable/destroy.
- Cooldown after release: Interest tiles unavailable for 3s to prevent ping-pong.
- Decision fallback: controller tries next option in priority order.

---

## 5. JOB SYSTEM SEMANTICS

### 5.1 Global Queue Implications
- Queue type: FIFO, single global queue.
- Benefit: simplicity, fair distribution for unbound jobs.
- Problem: agent-bound job mismatch churn.

**Solution (2-pass dequeue):**
- PASS 1: scan for agent-bound jobs matching agentId.
- PASS 2: if no match and no other-agent-bound jobs exist, return first unbound job.
- If other-agent-bound jobs exist: return false, set blockedByOtherAgent.
- JobRunner applies mismatch cooldown (50ms).

### 5.2 Why Agent-Bound Jobs Exist
Without binding, a different NPC can dequeue another NPC’s claimed job, causing claim leakage and failure. Agent-binding ties claim+job to same agent atomically.

**Jobs requiring binding:**
- InspectJob
- LeisureJob
- ResourceWorkJob

**Jobs not requiring binding:**
- RoamJob
- SimpleJob

### 5.3 Common Failure Patterns
- Claim exhaustion → minimal wait job then re-think
- Pathfinding failure → abandon at start, release, re-decide
- Reservation contention → wait then abandon; claim services prevent crowding
- Leisure stand cell unavailable → resolve neighbors; fail if none
- Historical starvation due to AgentId caching → fixed by reading from mover

---

## 6. KNOWN RISK AREAS (NO FIXES)

### 6.1 Fragile Areas
- **StandCellResolver reflection:** can break silently if signatures change.
- **Global queue + agent-binding:** complex logic; adding new job types incorrectly can reintroduce race conditions.
- **LeisureJob custom phase:** special-cased in JobRunner; adding new custom-phase jobs requires care.
- **Claim release authority:** only JobRunner releases; external cancellation paths must route through JobRunner.
- **Shared ghost/highlight tilemap:** TileHighlighter must be disabled for Build/Move modes.
- **Walkability consistency:** scattered checks risk divergence; discipline needed.

### 6.2 Regression-Prone Changes
- Changing interest tiles from obstacles
- Modifying StandCellResolver ring priority
- Adding tool modes without ToolStateRouter updates
- Changing JobManager dequeue semantics
- Adding exclusive-resource jobs without agent-binding
- Modifying mover reservation modes
- Changing Time.time vs Time.unscaledTime assumptions

---

## 7. NON-GOALS / OUT-OF-SCOPE (AS INFERRED)

**Phase 1 excludes:**
- Physical item logistics
- Enemy pathfinding (NPC-only currently)
- Save/Load
- Request/Logistics system
- Advanced runtime UI
- Multiplayer
- NPC-driven construction/demolition

**Design intentionally avoids:**
- ECS
- Priority job queues
- Dynamic tool UI (debug hotkeys only)
- Agent-to-agent communication
- Requeuing failed jobs
- Complex AI frameworks (BT/FSM)

---

## 8. HANDOFF NOTES FOR ANOTHER AI

### 8.1 Critical Rules (DO NOT BREAK)
- StandCellResolver is ONLY stand position authority.
- LeisureJob is NOT duration-based.
- GridAgentMover is AgentId source of truth (do not cache).
- JobRunner must release all claims.
- Interest/Resource tiles are hard obstacles.
- ToolStateRouter is only tool mode authority.
- No physical item logistics in Phase 1.
- No runtime UI unless explicitly requested.

### 8.2 Development Workflow
- Session-based, append-only context.
- Read GDD + Project Context before coding.
- Implement only requested feature.
- Manual play-mode testing; write session report; append to changelog.

### 8.3 Testing Approach
Manual testing via debug tools; no automated tests. Multi-NPC contention and tool ghost rendering are priority scenarios.

### 8.4 Scene Hierarchy Organization (Critical)
Standard layout:
- **[BOOTSTRAP]**: GameBootstrap, InputInstaller, ToolStateRouter, DebugToolModeController
- **[WORLD]**: Grid, MapSystems, Roads, Buildings, Pathfinding, Tools, Resources
- **[NPC_SYSTEMS]**: JobManager, GridReservationService, InterestTileScanner, InterestClaimService, StandCellResolver
- **[CAMERA]**: Main Camera, CameraController
- **[DEBUG_ONLY]**: Debug/test objects

### 8.5 When to Ask for Clarification
Ask if changes conflict locked decisions, require InputActions edits, or touch stubbed systems without explicit scope.

### 8.6 Key Files to Reference
- NPC: IdleDecisionController, JobRunner, JobManager, GridAgentMover, StandCellResolver
- Pathfinding: PathfindingService, AStarPathfinder, DefaultGridWalkability
- Tools: ToolStateRouter + tool scripts
- Exclusive resources: claim service patterns
- Authority: Project Context + GDD

### 8.7 Common Pitfalls
- Forgetting claim release
- Caching AgentId
- Bypassing StandCellResolver
- Editing InputActions
- Disabling GameObject for vanish
- Assuming reachability without checking
- Not pre-claiming exclusive resources
- Treating LeisureJob as duration-based

---

## 9. ARCHITECTURE QUALITY ASSESSMENT

### Strengths
- Clear authority boundaries
- Fail-fast philosophy
- Claim-based exclusivity
- Modular asmdef structure
- Append-only context preserves rationale

### Weaknesses
- Global job queue + agent-binding complexity
- Reflection-based asmdef workarounds
- Scattered walkability checks
- No automated tests
- LeisureJob special-casing

### Scalability Concerns
- Global FIFO scans may not scale to 100+ NPCs
- Reservation contention in dense areas
- No spatial partitioning for scans
- MonoBehaviour Update per NPC cost

### Technical Debt
- Hard-coded special cases in JobRunner
- Mismatch cooldown as band-aid
- Cooldowns as behavior band-aids
- Reflection-based service access

---

## 10. RISK MATRIX (SUMMARY)

| Area | Fragility | Impact if Broken | Likelihood of Change |
|---|---|---|---|
| StandCellResolver reflection | High | Critical (NPCs fail to interact) | Low |
| Global job queue dequeue logic | High | Critical (starvation, claim leaks) | Medium |
| LeisureJob custom phase | Medium | High | Low |
| Claim release authority | High | Critical (permanent locks) | Medium |
| Walkability consistency | Medium | High | Medium |
| Shared tilemap ghost + highlight | Medium | Medium | Low |
| Interest tiles as obstacles | Low | High | Low |
| AgentId source of truth | Low | Critical | Low |

---

## 11. CONCLUSION
Seasonal Bastion is a grid-based city builder + tower defense hybrid with a deterministic, authority-based architecture designed for AI-assisted session-based development. The codebase prioritizes single sources of truth (StandCellResolver, JobRunner, ToolStateRouter, PathfindingService) to prevent race conditions and state conflicts. The NPC system is agent-driven with global FIFO job queue, agent-bound override, exclusive claims, and fail-fast handling.

**Locked technical guarantees (do NOT break):**
- StandCellResolver is the ONLY stand position authority.
- LeisureJob is NOT duration-based (custom phase).
- GridAgentMover is the SINGLE source of truth for AgentId.
- JobRunner MUST release all claims on job end.
- Interest and Resource tiles are hard obstacles.
- ToolStateRouter is the ONLY tool mode authority.

**Primary risk areas:**
- Global job queue + agent-binding complexity.
- Reflection-based asmdef cycle workarounds.
- Scattered walkability checks.
- LeisureJob special-casing.
- Manual-only regression detection.

For a new AI joining:
Read GDD + Project Context before coding, respect guarantees, use authorities, pre-claim exclusive resources, route cleanup through JobRunner, test crowded scenarios, and clarify conflicts early.
