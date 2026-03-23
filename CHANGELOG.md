# CHANGELOG

## 2026-03-23

### Summary
Stabilization pass focused on Jobs, Build/Repair workplace routing, tower ammo resupply behavior, and a much cleaner in-game debug workflow.

### Jobs / Build / Repair
- Builder workplace routing was corrected so **BuilderHut is preferred** for `BuildWork` / `RepairWork`.
- HQ now acts as **fallback** when BuilderHut has no idle worker available.
- Queued `BuildWork` / `RepairWork` jobs can retarget when workplace availability changes.
- `RepairWorkExecutor` was changed from chunky HP jumps to **continuous per-tick repair progress**, matching the feel of `BuildWork` more closely.
- Repair/build flow and related Jobs smoke scenarios were exercised and marked stable for the current pass.

### Tower ammo / Armory priority
- Tower resupply behavior was expanded from "only low/empty towers request ammo" to **"any non-full tower may request top-up"**.
- Towers under the urgent threshold are now treated as **urgent-first**.
- Soft preemption was added so a queued armory resupply job can be **retargeted to an urgent tower before the next delivery starts**.
- `ResupplyTower` remains top priority for Armory-role workers over other ammo-related jobs.

### Debug tools / QA workflow
- `DebugHUDHub` was simplified into a more practical **Essential Debug Panel**.
- Added or improved quick actions for:
  - resource grants
  - unlock all
  - building damage / heal / repair
  - complete hovered site / complete all sites
  - tower ammo drain/refill
  - time scale controls including `5x`
  - day / season advance shortcuts
  - enemy spawn by lane
  - quick NPC spawn
- Enemy debug spawn now **auto-enables combat debug mode** so spawned enemies start moving immediately.
- Added clearer **current target display** for hovered/selected buildings, including linked tower ammo info.
- Added **click-to-lock building target** plus `Clear Lock`, so debug actions can stick to one building instead of relying only on hover TTL.
- Enemy type selection in debug UI now uses a **preset list** instead of free-text typing.

### Checklist / stabilization docs
- `docs/stabilization-checklist.md` was updated to reflect existing regression coverage already present in the repo.
- Jobs smoke test steps were expanded with practical pass/fail notes.
- RunStart wording in the checklist was normalized to match actual invariant names in code.
- Jobs smoke test items that were verified today were checked off.

### Notes
- Current focus was practical stabilization, not broad refactor cleanup.
- Save/load stabilization and remaining Build / RunStart smoke passes are still pending.
