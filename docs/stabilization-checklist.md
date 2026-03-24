# Stabilization Checklist

_Post-refactor stabilization plan for Jobs, Build, and RunStart._

## Goal

Before adding major features, ensure the repo is:

- compiling cleanly
- passing baseline tests
- stable across core gameplay loops
- safe across save/load and startup paths
- protected by regression coverage in refactored subsystems

---

## Phase 1 — Baseline Green

### 1. Compile + test baseline

- [x] Unity compile is clean across the whole project
- [x] No asmdef/reference/protection-level errors remain
- [x] Run all EditMode tests
- [x] Record baseline pass count (`29/29` green as of 2026-03-23)

### 2. Jobs smoke test

- [x] Idle NPCs receive jobs from the correct workplace
  - Quick check: assign 1 NPC to a producer (`bld_lumbercamp_t1` / `bld_farmhouse_t1`) and 1 NPC to HQ/warehouse
  - Pass if: producer NPC receives `Harvest`; HQ/warehouse NPC receives `HaulBasic` / build-role-appropriate work only
  - Fail if: NPC idles forever with valid setup, or receives a role-incompatible job
- [x] Harvest produces the correct resource type
  - Quick check: run 1 worker each at lumber/farm/quarry/iron hut and watch `DebugStorageHUD`
  - Pass if: Lumbercamp=>Wood, Farmhouse=>Food, Quarry=>Stone, IronHut=>Iron
  - Fail if: wrong resource increases, no resource is produced, or harvest loops without output
- [x] Haul jobs do not duplicate indefinitely
  - Quick check: keep 1 producer stocked, 1 HQ/warehouse with free cap, and 1 hauler active; watch active `HaulBasic` count/queue over time
  - Pass if: haul jobs appear when needed and stay bounded/stable
  - Fail if: active haul jobs or job ids keep climbing for the same unmet need
- [x] BuildWork handles delivery + build flow correctly
  - Quick check: place a new building with cost and observe delivery -> `BuildWork` -> completion
  - Pass if: delivery happens first when needed, then exactly one `BuildWork` progresses and the site completes cleanly
  - Fail if: site stalls after delivery, `BuildWork` duplicates, or completion leaves stale site/job state
- [x] RepairWork completes and clears job state correctly
  - Quick check: damage a constructed building (`K` in `DebugBuildingTool`), then create repair (`R`)
  - Pass if: exactly one `RepairWork` is created, HP returns, and NPC/job state clears on completion
  - Fail if: multiple repair jobs spawn, HP restores but job sticks, or NPC keeps stale `CurrentJob`
- [x] Armory priority still prefers `ResupplyTower`
  - Quick check: create simultaneous armory-related demand and tower low-ammo demand; inspect queue/behavior
  - Pass if: `ResupplyTower` is selected ahead of `HaulAmmoToArmory` / `HaulToForge`
  - Fail if: armory worker services lower-priority ammo work before tower resupply
- [x] Claims are released on complete/cancel/fail
  - Quick check: let a job complete naturally, then separately cancel/fail one via debug actions and watch reassignment
  - Pass if: NPC can receive a fresh job without manual claim clearing
  - Fail if: NPC/site softlocks until `Release All Claims` is pressed

### 3. Build smoke test

- [x] Place order creates site + placeholder correctly
- [x] Cancel place order clears site and placeholder
- [x] Cancel place order rolls back auto-road if applicable
- [x] Cancel place order refunds delivered resources if applicable
- [x] Upgrade order still completes correctly
- [x] Repair order creates and clears repair job correctly
- [x] `BuildWork` is not duplicated for the same site
- [ ] Rebuild-after-load still restores active orders correctly

### 4. RunStart smoke test

- [ ] `StartNewRun` succeeds with a valid config
- [ ] A real HQ exists after world apply
- [ ] Starting storage is seeded into HQ only
- [ ] NPCs do not spawn into blocked cells
- [ ] NPC workplace assignments are valid
- [ ] Spawn gates connect to the road graph
- [ ] Lane runtime is built correctly
- [ ] Invalid startup config fails clearly instead of producing half-valid runtime state

---

## Phase 2 — Regression Expansion

### 5. Jobs regression tests (round 2)

- [ ] `JobAssignmentService`: role filter works correctly
- [ ] `JobAssignmentService`: no assign when workplace roles are invalid
- [ ] `JobExecutionService`: missing job cleans up NPC state
- [ ] `JobExecutionService`: terminal job cleans up NPC state
- [ ] `JobStateCleanupService`: releases claims correctly
- [ ] `JobEnqueueService`: harvest enqueue respects slot caps / workplace NPC count
- [ ] `JobEnqueueService`: no harvest enqueue when local cap is full
- [x] Add more canonical `DefId` coverage beyond current producer/destination cases

### 6. Build regression tests (round 2)

- [ ] `BuildOrderCancellationService`: refund delivered resources to nearest valid storage
- [ ] `BuildOrderCancellationService`: cancel repair removes tracked repair job
- [ ] `BuildJobPlanner`: stale tracked jobs are pruned
- [ ] `BuildJobPlanner`: work job is recreated after terminal state
- [x] `BuildOrderTickProcessor`: missing site path behaves correctly
- [ ] `BuildOrderTickProcessor`: upgrade complete path behaves correctly
- [x] `BuildOrderReloadService`: missing placeholder is surfaced correctly
- [x] `BuildOrderReloadService`: multi-site rebuild remains deterministic
- [ ] `BuildOrderCreationService`: insufficient resources case is covered
- [ ] `BuildOrderCreationService`: locked upgrade case is covered
- [ ] `BuildOrderCreationService`: invalid placement/footprint case is covered

### 7. RunStart regression tests (round 2)

- [ ] `RunStartWorldBuilder`: invalid building def fails fast
- [x] `RunStartWorldBuilder`: tower-like fallback still works
- [ ] `RunStartPlacementHelper`: relocation finds a valid nearby anchor
- [ ] `RunStartPlacementHelper`: relocation respects `BuildableRect`
- [ ] `RunStartStorageInitializer`: valid HQ receives expected starting storage amounts
- [x] `RunStartValidator`: `NPC_WORKPLACE_MISSING`
- [ ] `RunStartValidator`: `NPC_WORKPLACE_UNBUILT`
- [x] `RunStartValidator`: `NPC_SPAWN_BLOCKED`
- [ ] `RunStartValidator`: `NPC_SPAWN_OOB`
- [ ] `RunStartValidator`: `GATE_NOT_CONNECTED`
- [ ] `RunStartValidator`: `GATE_NOT_ROAD`
- [ ] `RunStartHqResolver`: deterministic HQ selection when multiple candidates exist

---

## Phase 3 — Save/Load Stabilization

### 8. Mid-run save/load checks

- [ ] Save/load with active build site
- [ ] Save/load with active `BuildWork`
- [ ] Save/load with active `RepairWork`
- [ ] Save/load with queued haul jobs
- [ ] Save/load with NPCs holding `CurrentJob`
- [ ] Save/load after auto-road creation

### 9. Reload consistency checks

- [x] `BuildOrderService.RebuildActivePlaceOrdersFromSitesAfterLoad()` does not duplicate orders
- [ ] Tracked job maps do not keep orphan IDs
- [ ] `JobScheduler` does not reassign stale jobs after load
- [ ] `WorldIndex` and storage state remain consistent after reload

---

## Phase 4 — Cleanup Pass

### 10. Jobs cleanup

- [ ] Remove dead helpers/methods with no remaining call sites
- [ ] Remove/update stale comments after refactor
- [ ] Verify new service boundaries are still meaningful
- [ ] Ensure `JobScheduler` does not regain domain helper sprawl

### 11. Build cleanup

- [ ] Remove/update stale legacy comments (`BuildDeliver`, etc.)
- [ ] Confirm `_deliverJobsBySite` still serves a real compatibility purpose
- [ ] Review duplicate utility logic across cancellation/completion/creation
- [ ] Keep planner / processor / service naming consistent

### 12. RunStart cleanup

- [ ] Verify all HQ fallback logic is canonicalized consistently
- [ ] Decide whether hardcoded fallback zones remain valid policy
- [ ] Re-evaluate `assignedWorkplaceDefId` if maps can contain multiple same-def workplaces
- [ ] Add logging/warnings when placement relocation moves too far from requested anchor

---

## Phase 5 — Manual Gameplay Sanity Suite

### 13. Early game

- [ ] Start new run
- [ ] HQ has expected starting resources
- [ ] NPCs spawn correctly
- [ ] Harvest begins correctly
- [ ] Hauling begins correctly

### 14. Construction flow

- [ ] Place building
- [ ] Worker delivers and builds
- [ ] Complete construction
- [ ] Cancel construction
- [ ] Upgrade building
- [ ] Repair damaged building

### 15. Combat/support sanity

- [ ] Ammo/tower flow still works after canonical `DefId` cleanup
- [ ] Lane/spawn gate flow still routes correctly toward HQ
- [ ] Armory/tower resupply is not starved or stuck

---

## Phase 6 — Change Management

### 16. Baseline control

- [ ] Tag or note a stable post-stabilization commit
- [ ] Keep a short changelog for large refactor commits
- [ ] Establish one known-good green baseline before the next large feature push
- [ ] Avoid new large refactors before baseline is confirmed stable

---

## Suggested Order

### Immediate

- [x] Compile clean
- [x] Run EditMode tests
- [x] Smoke test Jobs
- [x] Smoke test Build
- [ ] Smoke test RunStart

### Next

- [ ] Add Jobs regression tests round 2
- [ ] Add Build regression tests round 2
- [ ] Add RunStart regression tests round 2

### Before next major feature

- [ ] Save/load sanity pass
- [ ] Dead-code / stale-comment cleanup pass
- [ ] Lock a known-good stable baseline commit
