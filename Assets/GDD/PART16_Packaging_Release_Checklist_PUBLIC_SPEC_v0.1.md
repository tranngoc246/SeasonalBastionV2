# PART 16 — PACKAGING & RELEASE CHECKLIST (PUBLIC LAUNCH READY) — SPEC v0.1

> Mục tiêu: chuẩn bị game để public (Steam/Itch/GOG), giảm rủi ro “vỡ build”, “mất save”, “bị review xấu vì UX”.
- Build pipeline: dev/staging/release
- Versioning & save compatibility policy
- QA checklist + smoke tests
- Performance targets & profiling plan
- Crash reporting/telemetry (tối giản)
- Store assets checklist
- Compliance basics (EULA/Privacy)

---

## 1) Release channels & build variants

### 1.1 Build variants
- **Dev**: DEVELOPMENT_BUILD, DevPanel ON, verbose logs
- **Staging (Preview/Beta)**: DevPanel OFF, logs limited, extra asserts OFF
- **Release**: no dev tools, logs minimal, analytics/crash ON (optional)

### 1.2 Platform targets
- Windows x64 (primary)
- Optional later: Steam Deck / Linux (after stability)

---

## 2) Versioning scheme

### 2.1 SemVer + content schema version
- App version: `MAJOR.MINOR.PATCH`
  - MAJOR: breaking gameplay/save changes
  - MINOR: new content features (compatible)
  - PATCH: bugfix
- Save schema: integer `schemaVersion`
  - independent from app version

Example:
- App v0.8.2, schemaVersion 12

### 2.2 Build metadata
- embed:
  - git commit hash
  - build time
  - build channel
Shown in Settings/About.

---

## 3) Save compatibility policy (public-friendly)

### 3.1 Split saves
- `run_save.json` (ephemeral run)
- `meta_save.json` (persistent)

### 3.2 Policy (recommended)
- **MetaSave**: aim for forward migration for most updates (protect players)
- **RunSave**: allow invalidation on major gameplay changes
  - If incompatible: show friendly message, keep file backup

### 3.3 Migration strategy
- Migrator steps:
  - `schemaVersion N -> N+1`
  - pure data transforms
- If cannot migrate:
  - for RunSave: “Run không tương thích, bạn có thể bắt đầu run mới”
  - for MetaSave: attempt best-effort migrate; if fails, offer restore backup.

---

## 4) Packaging pipeline

### 4.1 Build automation (minimal)
- Use Unity Build Profiles / custom build script:
  - one-click produce builds for Dev/Staging/Release
- Output folder structure:
  - `Builds/Windows/Dev/`
  - `Builds/Windows/Staging/`
  - `Builds/Windows/Release/`

### 4.2 Steam upload
- SteamPipe depots (later):
  - Depot: Windows
  - Branches: default, beta

### 4.3 Itch/GOG
- Zip the build folder
- Provide changelog file inside.

---

## 5) QA plan (must-have)

### 5.1 Smoke test checklist (10–15 minutes)
1) New Run start
2) Place building via build pipeline (delivery+work)
3) Assign NPC to workplace
4) Harvest → local → haul → storage increments
5) Forge crafts ammo → armory hauls → tower resupply
6) Enter Defend phase: spawn wave, tower shoots, ammo consumes
7) Pause/Resume + speed 1x/2x/3x (Defend default 1x)
8) Save & Quit → Continue works
9) Defeat triggers on HQ destroy, summary shows
10) Reward modal appears end of defend day, pick applies

### 5.2 Regression suite (weekly)
- Run all EditMode tests
- Run 3 full runs with different seeds
- Verify no save corruption

### 5.3 Bug triage rubric
- P0: crash, corrupt save, stuck run, infinite loop
- P1: economy deadlock, tower never resupplies, placement broken
- P2: UI glitches, minor balance issues
- P3: cosmetic

---

## 6) Performance targets & profiling

### 6.1 Targets (PC mid-range)
- 60 FPS at 1080p
- GC alloc per frame near 0 in normal play
- Job/AI ticks scaled (0.5–1s cadence) not per-frame heavy

### 6.2 Profiling checklist
- Profiler snapshots:
  - Build phase (many NPC jobs)
  - Defend phase (many enemies/towers)
- Track:
  - allocations from UI rebuild
  - pathfinding spikes
  - JobScheduler scan cost
- Optimizations:
  - cap NPC count early
  - pre-allocate lists
  - throttle providers

---

## 7) Crash reporting & telemetry (minimal)

### 7.1 Crash reporting
Options:
- Unity Cloud Diagnostics (simple)
- Or Sentry (if you want)

v0.1 recommended: Unity Cloud Diagnostics (fast).

### 7.2 Telemetry (optional)
Keep opt-in/off by default (best for trust), or minimal anonymous:
- run start/end
- outcome
- days survived
- crash count

### 7.3 Privacy
- Provide simple privacy note in Settings/About.

---

## 8) Settings defaults (public)
- Fullscreen borderless default
- VSync ON by default
- Audio volumes 80%
- Tips ON
- Language VN default (auto detect optional)
- Defend speed default 1x, allow 2x/3x toggle (dev setting becomes user setting after polish)

---

## 9) Localization readiness
- All UI strings are keys
- Reward text in defs (or keys)
- Changelog bilingual optional.

---

## 10) Store assets checklist (Steam)

### 10.1 Mandatory assets
- Capsule (main, small, header)
- Screenshots (at least 5)
- Trailer (optional but recommended)
- Short description + long description
- Tags: City Builder, Tower Defense, Roguelite (careful)
- System requirements

### 10.2 Good practice
- Show core loop in first 15 seconds of trailer:
  - build → harvest → defend wave → reward pick
- Screens should include UI readable.

---

## 11) Legal & compliance (basic)
- EULA (simple)
- Privacy policy (if telemetry)
- Third-party licenses (open source list)
- Age rating: likely 3+ (no gore)

---

## 12) Launch strategy (solo-friendly)

### 12.1 Phase plan
- Closed demo (10–50 testers)
- Open beta branch (Steam beta)
- Early Access (recommended for solo)
- v1.0 release after content stability

### 12.2 Patch cadence
- Weekly patch early
- Biweekly once stable

### 12.3 Community
- Discord + feedback form
- Bug report template:
  - build version
  - steps
  - save file attach (optional)
  - screenshot

---

## 13) Pre-launch checklist (P0 gate)
- [ ] No known crashes in 10 consecutive runs
- [ ] Save migration tested (meta)
- [ ] New run funnel clear (tutorial/hints)
- [ ] Defend default 1x works; 2x/3x doesn’t break
- [ ] Towers never “softlock” due to ammo pipeline
- [ ] Build pipeline never loses resources on failure (or policy documented)
- [ ] Settings persist
- [ ] Store page complete

---

## 14) Next Part (Part 17 đề xuất)
**Content production pipeline**: how to add new buildings/enemies/rewards safely (templates + validation + balance workflow).

