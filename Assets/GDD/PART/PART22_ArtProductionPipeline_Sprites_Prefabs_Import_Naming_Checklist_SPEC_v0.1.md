# PART 22 — ART PRODUCTION PIPELINE (SPRITES / PREFABS / IMPORT SETTINGS / NAMING / CHECKLIST) — SPEC v0.1

> Mục tiêu: thêm hình ảnh/animation/prefab nhanh, không vỡ style và không phá performance:
- Quy chuẩn spritesheet + slicing
- Naming conventions cho sprite/anim/prefab/material
- Prefab templates cho Building/Tower/Enemy/FX
- Import settings chuẩn (pixel art hoặc semi-flat 2D)
- Checklist add-new-visual để merge an toàn
- Version control: tránh conflict, giữ file nhẹ

---

## 1) Visual pipeline philosophy (solo + AI)
- Tối ưu “lặp nhanh”: art mới = duplicate template + replace sprites
- Tránh chỉnh tay hàng loạt: dùng presets (import preset + animator template)
- Data-driven binding: view prefab đọc state (HP/ammo/progress) từ controller scripts
- Nếu dùng AI art: phải đưa vào pipeline như asset bình thường (không đặc quyền)

---

## 2) Folder layout (recommended)

```
Assets/Art/
  Sprites/
    Buildings/
      Core/
      Production/
      Defense/
      Ammo/
    Enemies/
    UI/
    FX/
  Animations/
    Buildings/
    Enemies/
    UI/
  Prefabs/
    Buildings/
    Enemies/
    FX/
    UI/
  Materials/
  Shaders/ (optional)
  Atlas/   (SpriteAtlas assets)
```

---

## 3) Naming conventions (hard rules)

### 3.1 Sprite / spritesheet
- Sheet: `spr_<category>_<name>_<variant>_sheet`
  - `spr_bld_farmhouse_v1_sheet.png`
- Single sprites: `spr_<category>_<name>_<part>_<variant>`
  - `spr_bld_farmhouse_base_v1`
  - `spr_bld_farmhouse_cracks_v1`
  - `spr_tower_arrow_muzzle_v1`
  - `spr_enemy_goblin_walk_01`

### 3.2 Prefabs
- `pf_<type>_<id>`
  - `pf_building_bld_prod_farmhouse_t1`
  - `pf_tower_bld_def_arrowtower_t1`
  - `pf_enemy_enemy_bandit_raider_t1`
  - `pf_fx_dustpuff`

### 3.3 Animations
- Clips: `anim_<type>_<name>_<state>`
  - `anim_enemy_goblin_walk`
- Controllers: `ac_<type>_<name>`
  - `ac_enemy_goblin`

### 3.4 Materials
- `mat_<purpose>_<name>`
  - `mat_outline_default`
  - `mat_sprite_highcontrast`

### 3.5 SpriteAtlas
- `atlas_<group>_<res>`
  - `atlas_buildings_2048`
  - `atlas_enemies_2048`
  - `atlas_ui_1024`

---

## 4) Import settings (2D) — choose one style

### Option A: Pixel art (crisp)
- Texture Type: Sprite (2D and UI)
- Sprite Mode: Multiple (for sheets)
- Pixels Per Unit: fixed (e.g. 32 or 64)
- Filter Mode: Point (no filter)
- Compression: None or Low
- Mip Maps: Off
- Max Size: 2048/4096
- Wrap: Clamp

### Option B: Semi-flat / painted 2D
- Filter Mode: Bilinear
- Compression: Normal
- Mip Maps: Off (usually)
- Use SpriteAtlas for batching

> Chọn 1 style và giữ consistent.

### Presets
- Create Unity Import Preset assets:
  - `preset_sprite_pixel`
  - `preset_sprite_ui`
  - `preset_sprite_fx`

---

## 5) Spritesheet slicing workflow

### 5.1 Sheet layout rule
- Grid aligned (even for painted) to avoid slicing mistakes
- Padding 2–4px between sprites (avoid atlas bleed)
- Name slices inside Sprite Editor:
  - follow naming rules above

### 5.2 Animation frames
- Use sequential naming:
  - `spr_enemy_goblin_walk_00..07`
- Unity can auto-create clips if names match pattern.

---

## 6) Prefab templates (core)

### 6.1 BuildingView prefab template
Hierarchy:
- Root (`pf_building_*`)
  - SpriteRenderer (Base)
  - Overlays
    - CracksOverlay (SpriteRenderer)
    - SmokeOverlay (Particle or sprite anim)
    - HighlightRing (sprite/line)
  - UIAnchors
    - FloatingIconAnchor
    - HPBarAnchor (optional)
  - Components
    - `BuildingViewBinder`
    - `DamageVisualController`
    - `StorageIconController` (optional)
    - `SelectionOutlineController`

Must have:
- Sorting layer set
- Pivot consistent (center of footprint)

### 6.2 TowerView prefab template
- Base sprite
- Muzzle flash anchor
- Ammo indicator (bar/icon)
- `TowerViewBinder`
- `TowerAmmoIndicatorController`

### 6.3 EnemyView prefab template
- SpriteRenderer + Animator
- Hit flash overlay
- `EnemyViewBinder`
- `HitFlashController`

### 6.4 FX prefab template
- Root with ParticleSystem or SpriteAnimation
- Auto return to pool script: `FXAutoReturn`
- No Update allocations

---

## 7) View binding contract (state → visuals)

### 7.1 Common binder interface
```csharp
public interface IWorldViewBinder<TId>
{
    void Bind(TId id, IWorldReadAPI world, IFXService fx);
    void Unbind();
}
```

### 7.2 Update triggers
Prefer event-driven:
- `OnHPChanged(BuildingId id, int hp)`
- `OnAmmoChanged(TowerId id, int ammo)`
- `OnBuildSiteProgress(siteId, delivered, workDone)`

If not available:
- Poll at low frequency (2–4 Hz) from binder.

---

## 8) SpriteAtlas & batching

### 8.1 Atlas grouping
- Buildings atlas
- Enemies atlas
- UI atlas
- FX atlas (optional)

### 8.2 Rules
- Keep UI separate to prevent unnecessary loads
- Avoid too many atlases (2–4 max early)

### 8.3 Verification
- Frame Debugger: check draw calls reduce
- Profiler: texture memory stable

---

## 9) Style guidelines (consistency)

### 9.1 Scale
- Buildings footprints align to grid cell size
- Tower projectile/tracer uses consistent thickness

### 9.2 Lighting/shadows
- If add shadows: simple drop shadow sprite under object
- Keep consistent direction (say, bottom-left)

### 9.3 Damage overlays
- Cracks overlay must align with base sprite bounds
- Smoke overlay appears only <30% HP

### 9.4 Readability priorities
- Selected building always highlighted
- Low ammo icon must be visible even when zoomed out

---

## 10) Add-new-building-visual checklist (merge gate)

1) Create/duplicate building prefab template
2) Assign base sprite + overlays
3) Set pivot, sorting layer/order
4) Link prefab to BuildingDef (view reference) *(wherever your system stores it)*
5) Verify:
   - placement footprint matches visuals
   - entry side indicator makes sense (optional arrow)
6) Run:
   - build pipeline (site → complete) shows correct visuals
   - damage thresholds show overlays
7) Atlas:
   - add sprite(s) to correct SpriteAtlas
8) Performance:
   - no new materials per instance
   - no Instantiate during play
9) Validator:
   - BuildingDef has required tags/workplace/storage
10) Commit:
   - include screenshots (before/after)
   - PR notes

---

## 11) Handling AI-generated art (practical rules)
- Normalize resolution and PPU to your style
- Remove backgrounds / transparency correct
- Do not ship inconsistent styles in same tier
- Keep source prompt/credits in `Assets/Art/README.md` (optional)

---

## 12) Version control & conflict avoidance
- Use `Force Text` serialization for Unity (Project Settings)
- Prefer small atomic commits:
  - one prefab + its spritesheet
- Avoid editing SpriteAtlas from multiple branches concurrently:
  - batch atlas changes in dedicated PR

---

## 13) QA Checklist (Part 22)
- [ ] All sprites use correct import preset
- [ ] Pivot/sorting consistent; no z-fighting
- [ ] Atlas packing reduces draw calls
- [ ] Damage and ammo indicators readable at min zoom
- [ ] Prefabs use shared materials; no leaks
- [ ] No runtime Instantiate from visuals

---

## 14) Next Part (Part 23 đề xuất)
**Economy/Combat tuning playbook**: how to use metrics + test seeds to balance toward target run length & difficulty curve.

