# Task Breakdown - Hybrid Resource Zone Generation

## Goal
Move map resource placement from fully fixed `StartMapConfig` rectangles to a **hybrid zone generation** model:

- **Guaranteed starter ring** near HQ/start area so every seed is playable
- **Procedural outer ring** for replayability and expansion variety
- **Seeded + deterministic** so QA/debug can reproduce layouts
- **Backward-compatible** with current authored `zones` flow during rollout

This plan intentionally keeps the current **zone-based resource model** (`ZoneState`) instead of introducing resource nodes/deposits yet.

---

## Current State (as of this breakdown)

### Resource placement currently comes from
- `Assets/_Game/Core/RunStart/StartMapConfigDto.cs`
  - `StartMapConfigDto.zones`
- `Assets/_Game/Core/RunStart/RunStartZoneInitializer.cs`
  - Clears zone store
  - Maps authored zone `type` to `ResourceType`
  - Adds rectangular zones
  - Falls back to **hardcoded 4 starter rectangles** if config has no zones
- `Assets/_Game/Core/RunStart/RunStartRuntimeCacheBuilder.cs`
  - Copies zones into runtime cache for debug/runtime metadata

### Why this matters
The current approach is stable and easy to validate, but it has major downsides:
- replayability is low
- map layouts become learnable/static
- expansion decisions are less interesting
- seed currently does not meaningfully affect resource layout

---

## Design Direction

### Recommended approach
Adopt **Hybrid Resource Zone Generation**:

1. **Starter ring**
   - always guarantee enough early-game resources around HQ
   - keeps each seed playable and easier to balance

2. **Outer ring**
   - procedurally generate additional zones farther from HQ
   - vary count/placement/size by seed
   - drive replayability and scouting value

3. **Keep authored compatibility**
   - existing `zones` path must still work
   - rollout should support `AuthoredOnly`, `Hybrid`, and optionally `GeneratedOnly`

---

## Non-Goals for this batch
Do **not** do these in the same batch:
- replace zones with fully simulated resource deposits/nodes
- add biome/noise-based worldgen everywhere
- add regrowth/depletion simulation
- rewrite harvesting/building systems to use a different resource abstraction

This batch should stay focused on **how zones are created at run start**.

---

## Proposed Data Model Changes

## 1) Extend `StartMapConfigDto`
Add a new optional section:

```csharp
public ResourceGenerationDto resourceGeneration;
```

### New DTOs to add

```csharp
[Serializable]
internal sealed class ResourceGenerationDto
{
    public string mode; // "AuthoredOnly" | "Hybrid" | "GeneratedOnly"
    public int seedOffset;
    public ResourceSpawnRuleDto[] starterRules;
    public ResourceSpawnRuleDto[] bonusRules;
}

[Serializable]
internal sealed class ResourceSpawnRuleDto
{
    public string resourceType;

    public int countMin;
    public int countMax;

    public int minDistanceFromHQ;
    public int maxDistanceFromHQ;

    public int rectWidthMin;
    public int rectWidthMax;
    public int rectHeightMin;
    public int rectHeightMax;

    public string notes;
}
```

### Notes
- `starterRules` = guaranteed near-HQ zones
- `bonusRules` = outer ring procedural zones
- pass 1 should stay rectangle-based to match current `ZoneState` flow

---

## 2) Mode behavior

### `AuthoredOnly`
- use current `cfg.zones` path only
- no procedural generation
- preserve old behavior exactly

### `Hybrid`
- use generated starter + bonus zones
- may optionally merge with authored zones if desired
- recommended rollout target

### `GeneratedOnly`
- ignore authored `cfg.zones`
- all zones come from generation rules
- optional for later, not required first

---

## Proposed Code Structure

## 1) New generator
Add a new file/service:

- `Assets/_Game/Core/RunStart/RunStartResourceZoneGenerator.cs`

### Proposed responsibility
Given:
- `GameServices`
- `StartMapConfigDto`
- run `seed`
- HQ/start anchor

Generate a deterministic list of rectangular resource zones.

### Proposed API

```csharp
internal static class RunStartResourceZoneGenerator
{
    internal static bool TryGenerateZones(
        GameServices s,
        StartMapConfigDto cfg,
        int seed,
        out List<ZoneState> zones,
        out string error);
}
```

### Internal helpers

```csharp
private static void GenerateStarterZones(...)
private static void GenerateBonusZones(...)
private static bool TryResolveHqAnchor(...)
private static bool TryPickZoneRect(...)
private static bool TryAddZone(...)
```

---

## 2) Refactor `RunStartZoneInitializer`
Current file:
- `Assets/_Game/Core/RunStart/RunStartZoneInitializer.cs`

### Target shape after refactor
This class becomes the orchestration layer:

- clear zone store
- inspect `cfg.resourceGeneration.mode`
- choose one path:
  - authored only
  - hybrid generated
  - generated only
- apply resulting `ZoneState`s to world

### Important
Keep the current authored path available during rollout.
Do **not** delete authored zone support in the first pass.

---

## 3) Runtime cache update
Current file:
- `Assets/_Game/Core/RunStart/RunStartRuntimeCacheBuilder.cs`

### Update needed
Runtime cache must still reflect the final zone layout that was actually applied.

If generation is used, cache should contain:
- generated starter zones
- generated bonus zones
- zone ids/types/rects/cell counts like current runtime metadata

Optional later improvement:
- store `origin = authored | starter-generated | bonus-generated`

---

## Proposed Generation Rules

## Starter ring
Guarantee at least:
- Wood near HQ
- Food near HQ
- Stone near HQ or mid ring
- Iron slightly farther or smaller than other starter types

### Recommended intent
- early game always playable
- enough nearby resources for opening loop
- no seed should fail because a critical resource is missing

---

## Outer ring
Generate extra zones farther away with seed-based variation:
- 1..N bonus zones per resource type
- variable rectangle sizes
- variable positions in allowed distance bands

### Recommended intent
- creates replayability
- creates expansion choices
- lets some runs be richer in one resource than another without breaking opener

---

## Placement Constraints
Each generated rectangle should respect:
- inside map bounds
- preferably inside buildable/world-valid region if appropriate
- not obviously overlapping HQ footprint
- not obviously overlapping spawn gates / critical authored structures
- not degenerate (non-zero size)

For pass 1, avoid over-engineering overlap rules.
Simple deterministic sanity checks are enough.

---

## Seed / Determinism
Generation must be deterministic.

### Requirements
- same run seed + same config => same zone layout
- different seed => different layout
- `seedOffset` allows changing generation behavior without changing whole run seed

### Recommendation
Use a small deterministic PRNG helper scoped to generator input:
- `seed`
- `resourceType`
- rule index
- optional `seedOffset`

Do not rely on Unity global random state.

---

## Validation Tasks
Update:
- `Assets/_Game/Core/RunStart/RunStartConfigValidator.cs`

### Add validation for `resourceGeneration`
- mode must be valid
- rule arrays can be null only when mode allows authored fallback
- `countMin <= countMax`
- `rectWidthMin <= rectWidthMax`
- `rectHeightMin <= rectHeightMax`
- `minDistanceFromHQ <= maxDistanceFromHQ`
- `resourceType` must map to a supported `ResourceType`

### Also validate
If generation mode requires starter ring:
- HQ/start anchor must be resolvable from config/world bootstrap

---

## Implementation Checklist

## Batch A - Data + Validator
- [x] Add `resourceGeneration` to `StartMapConfigDto`
- [x] Add `ResourceGenerationDto`
- [x] Add `ResourceSpawnRuleDto`
- [x] Extend `RunStartConfigValidator` for new fields
- [x] Keep backward compatibility when `resourceGeneration == null`

## Batch B - Generator
- [x] Create `RunStartResourceZoneGenerator.cs`
- [x] Implement deterministic PRNG usage
- [x] Implement HQ anchor resolution
- [x] Implement starter ring generation
- [x] Implement bonus/outer ring generation
- [x] Emit `List<ZoneState>` as output
- [x] Add same-resource minimum separation
- [x] Add cross-resource minimum separation
- [x] Reject placement on road/building/site cells

## Batch C - Zone Initializer Wiring
- [x] Refactor `RunStartZoneInitializer` to route by mode
- [x] Preserve current authored `cfg.zones` path
- [ ] Replace hardcoded fallback rectangles with generated starter fallback in hybrid/generated paths
- [x] Keep current zone-store apply path reusable

## Batch D - Runtime Cache + Debug
- [x] Ensure `RunStartRuntimeCacheBuilder` reflects final applied zones
- [ ] Add optional metadata for generation mode/seed if helpful
- [x] Update map presentation so resource zones are visible in runtime (`WorldViewRoot2D` tilemap overlay)

## Batch E - Tests
- [x] Determinism test: same seed => same zones
- [x] Variation test: different seed => different zones
- [x] Starter guarantee test: required resources always present near HQ
- [x] Bounds test: all generated rects inside map
- [x] Sanity test: non-empty zones and valid cell counts
- [x] Hybrid mode test: authored mode still works unchanged

---

## Suggested Test Cases

### 1. Same seed reproducibility
- start run with seed `111`
- capture final runtime zones
- restart with seed `111`
- assert exact same zones

### 2. Different seed variation
- seed `111`
- seed `222`
- assert at least one zone differs in position or size

### 3. Guaranteed opener
- for a set of seeds
- verify starter ring always includes wood/food/stone and intended iron coverage

### 4. Backward compatibility
- `AuthoredOnly` config behaves exactly like current authored config

### 5. Fallback safety
- missing/empty authored zones in hybrid/generated mode still produce valid starter resources

---

## Rollout Strategy

## Step 1
Introduce DTOs + validator + generator, but keep current map config in `AuthoredOnly`.

## Step 2
Create a hybrid test config or update current run-start config to `Hybrid`.

## Step 3
Tune starter ring distances and bonus zone counts using runtime/debug validation.

## Step 4
Only after stability, consider phasing out hardcoded fallback rectangles entirely.

---

## Risks / Watchouts

### 1. Unplayable seeds
Mitigation:
- guaranteed starter ring
- strong validator
- deterministic fallback

### 2. Overlap with important authored structures
Mitigation:
- reject candidate rects near HQ/spawn gates/initial buildings
- start simple, expand rules only if needed

### 3. Too much randomness hurting balance
Mitigation:
- keep starter ring tightly constrained
- push most variation into outer ring only

### 4. Debug complexity
Mitigation:
- always show seed
- keep deterministic generation
- cache final zone result into runtime metadata

---

## Recommended First Implementation Scope
To keep risk low, the first implementation should do exactly this:

- support `resourceGeneration.mode`
- implement **rectangle-based generated zones only**
- guarantee starter ring around HQ
- generate outer ring bonus zones from seed
- keep authored path intact
- update runtime cache + add tests

Do **not** expand to deposits/nodes/terrain-aware generation in this first batch.

---

## Deliverable Definition of Done
This batch is done when:
- a new run can generate resource zones deterministically from seed
- early-game resources are always guaranteed near HQ
- different seeds produce noticeably different outer resource layouts
- authored configs still work
- tests cover determinism + starter guarantee + bounds
