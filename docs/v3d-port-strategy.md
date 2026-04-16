# Seasonal Bastion 2D -> 3D Migration Strategy

## Project roles

- Unity 2D source project: `E:\Projects\SeasonalBastionV2`
- Unity 3D target/prototype project: `E:\Projects\DemoSeasonalBastion3D`

## Goal

Migrate Seasonal Bastion from a Unity 2D project into a Unity 3D project.

This is not only a presentation-layer upgrade. It is a project-model migration:
- 2D scene/setup -> 3D scene/setup
- 2D camera/input/world picking -> 3D camera/input/raycast/world picking
- tilemap/sprite presentation -> mesh/prefab/material presentation
- 2D spatial assumptions -> 3D spatial assumptions

The main intent is to preserve gameplay behavior from V2 where practical while building a real 3D runtime target.

## What should come from V2

Use V2 as the source/reference for:
- gameplay rules
- grid semantics
- placement validation behavior
- world state shape
- build flow
- run-start flow
- save/load contracts
- combat behavior
- population/economy/job behavior
- defs/data meanings

## What likely needs 3D-native implementation

Expect to rework or replace these areas in the 3D target:
- scene bootstrap
- camera rig
- world-to-cell mapping through 3D raycast
- ground representation
- prefab-based building presentation
- NPC/enemy world presentation
- collider-based or raycast-driven selection
- focus/highlight behavior
- terrain/map presentation
- 3D debug visualization

## Practical role of each repo

### SeasonalBastionV2
Use as:
- behavior reference
- data/contracts source
- validation oracle for parity

Do not treat it as the final runtime destination if the goal is a real Unity 3D project.

### DemoSeasonalBastion3D
Use as:
- 3D runtime target/prototype
- integration surface for new 3D architecture
- testbed for new camera, mapping, view, terrain, and interaction flows

Do not let prototype shortcuts replace important gameplay behaviors from V2 without deliberate review.

## Migration method

1. Identify the V2 behavior to preserve.
2. Decide whether that behavior can be reused directly or only reimplemented conceptually in 3D.
3. Build the feature in the 3D target.
4. Verify parity against V2 where applicable.
5. Move to the next vertical slice.

## Suggested slice order

1. 3D spatial foundation
   - cell/world mapper
   - raycast resolver
   - ground layer
   - camera rig
2. 3D placement interaction
   - hover cell
   - preview ghost
   - footprint overlay
   - click-build
3. 3D world presentation
   - building/build-site views
   - prefab registry
   - construction state visuals
4. 3D actor presentation
   - NPC views/movement
   - enemy views/movement
5. 3D interaction/UI bridge
   - selection
   - highlight
   - focus
   - info-panel wiring
6. 3D combat feedback
7. 3D terrain/worldgen integration
8. save/load rebuild in 3D runtime
9. debug/hardening/performance cleanup

## Anti-patterns to avoid

- assuming 2D project structure must remain the runtime host forever
- treating 3D migration as only a `View3D` skin pass
- duplicating gameplay logic unnecessarily in the 3D target
- copying 2D scene/input assumptions directly into 3D
- allowing prototype-only shortcuts to silently redefine gameplay behavior

## Immediate next step

Re-evaluate the active implementation plan with this framing:
- source of behavior: `SeasonalBastionV2`
- target of execution: `DemoSeasonalBastion3D`
- priority: make the 3D target playable while preserving core behavior intentionally
