# PART 21 — ART/FX DIRECTION MINIMAL (VFX CUES + DAMAGE STATES + PERFORMANCE RULES) — SPEC v0.1

> Mục tiêu: tăng “feel” và khả năng đọc trạng thái bằng hình ảnh, không phá performance:
- VFX cues cho: build start/complete, repair, harvest, haul delivery, craft ammo, resupply tower, tower shot, enemy hit/death
- Damage states cho buildings/HQ/towers (visual feedback)
- FX budget rules + pooling
- Art direction tối giản phù hợp solo dev (2D topdown grid)
- Implementation pattern: FXEventId (tương tự AudioEvent)

---

## 1) Art direction baseline (solo-friendly)

### 1.1 Visual style goals
- Đọc trạng thái rõ:
  - building đang xây, bị hư, storage full, tower low ammo
- Tone: “cozy survival” trong build phase, “tense but readable” trong defend
- FX không được che gameplay, tránh particle quá dày

### 1.2 Palette & readability
- Use limited palette; reserve saturated colors for alerts
- Do not rely solely on color; add icons/shapes (Part 19)

---

## 2) FXEvent system (data-driven)

### 2.1 FXEventId
```csharp
public enum FXEventId
{
    Build_SitePlaced,
    Build_WorkTick,
    Build_Complete,
    Build_Blocked,

    Repair_Start,
    Repair_Complete,

    Harvest_Complete,
    Haul_Deliver,

    Ammo_CraftComplete,
    Ammo_ResupplyDeliver,

    Combat_TowerShot,
    Combat_EnemyHit,
    Combat_EnemyDeath,
    Combat_BuildingHit,
    Combat_BuildingDestroyed,

    UI_ClickSpark,          // optional subtle
    Objective_CompleteBurst
}
```

### 2.2 FXEventDef
```csharp
[CreateAssetMenu(menuName="Game/FX/FXEventDef")]
public sealed class FXEventDef : ScriptableObject
{
    public FXEventId Id;

    // One of:
    public GameObject PrefabParticle;  // pooled
    public Sprite SpriteFlash;         // simple overlay
    public AnimationClip Clip;         // if using anim

    public float Duration = 0.5f;
    public bool FollowTarget;          // attach to transform
    public bool UseWorldPos;

    public int MaxPerSecond = 10;      // spam control
}
```

### 2.3 FXRegistry
- Similar to AudioRegistry:
  - Map FXEventId → FXEventDef
- Loaded by DataRegistry.

---

## 3) FXService (runtime)

### 3.1 Responsibilities
- Spawn FX by event id at:
  - world position
  - target transform (follow)
- Pool particle prefabs
- Apply spam caps / cooldowns

### 3.2 API
```csharp
public interface IFXService
{
    void Play(FXEventId id, UnityEngine.Vector3 worldPos);
    void PlayOn(FXEventId id, UnityEngine.Transform target);
}
```

### 3.3 Pooling rule
- No instantiate during gameplay.
- Prewarm pools for common FX:
  - tower shot muzzle flash
  - enemy hit sparks
  - build complete burst

---

## 4) Core VFX cues (minimal set)

### 4.1 Build/Repair
- Build_SitePlaced: dust puff at site
- Build_WorkTick: subtle hammer dust every 1s (throttled)
- Build_Complete: short burst + “pop” highlight
- Repair_Start/Complete: wrench sparkle

### 4.2 Economy
- Harvest_Complete:
  - small “+wood/+food” floating text (optional; if too heavy, use icon)
- Haul_Deliver:
  - crate icon pop at warehouse/hq

### 4.3 Ammo pipeline
- Ammo_CraftComplete:
  - small glow at forge
- Ammo_ResupplyDeliver:
  - ammo icon flying to tower (optional; can be line renderer)

### 4.4 Combat
- TowerShot:
  - muzzle flash + projectile tracer (simple line)
- EnemyHit:
  - sparks / small bloodless hit
- EnemyDeath:
  - fade + puff
- BuildingHit:
  - small debris puff
- BuildingDestroyed:
  - bigger debris + shake (optional)

---

## 5) Damage states (visual)

### 5.1 Building health thresholds
- > 70%: normal
- 70–30%: “damaged” variant (cracks overlay)
- < 30%: “critical” variant (smoke + flashing outline)
- 0%: destroyed (remove sprite, spawn rubble decal)

Implement by:
- `DamageVisualController` on building view prefab:
  - reads HP ratio from BuildingState
  - toggles child overlays (cracks/smoke)

### 5.2 HQ emphasis
- HQ critical:
  - strong pulsating outline
  - subtle screen vignette (optional)
- HQ hit:
  - small screen shake + red flash (respect ReduceMotion)

### 5.3 Tower ammo visual
- Ammo ratio indicator at tower base:
  - bar + icon
- Low ammo:
  - yellow icon (pattern) + small blink
- Empty:
  - red cross icon; disable muzzle flash

---

## 6) Performance budget rules (hard constraints)

### 6.1 FX budget
- Max active particles:
  - Build phase: 30
  - Defend phase: 60
- Max spawns per second:
  - TowerShot FX: 10/sec global
  - EnemyHit FX: 10/sec global
- If exceed: skip FX silently (gameplay unaffected)

### 6.2 No per-frame allocations
- Floating text pool or skip entirely.
- Tracer uses pooled LineRenderer objects.

### 6.3 Update cadence
- DamageVisualController update at 2–4 Hz (not per-frame):
  - only when HP changes (event-driven) is best.

---

## 7) Implementation pattern (Views vs State)

### 7.1 View prefabs
- BuildingView (sprite + overlays)
- EnemyView
- TowerView

### 7.2 State-driven update
- `ViewBinder` listens to state changes:
  - OnHPChanged -> update overlay thresholds
  - OnAmmoChanged -> update ammo indicator
  - OnBuildSiteProgress -> update scaffolding/progress bar

---

## 8) Minimal asset list (solo-friendly)

### 8.1 Particles (tiny set)
- Dust puff
- Spark hit
- Smoke
- Burst

### 8.2 Decals / overlays
- Cracks overlay
- Rubble decal
- Outline highlight shader (or sprite outline)

### 8.3 Tracers
- 1 tracer prefab + line renderer

---

## 9) UX integration (ties to tutorial & notifications)
- On objective complete: Objective_CompleteBurst at HUD or target building
- When notification clicked and focus building:
  - highlight ring + short pulse FX (optional)

---

## 10) QA Checklist (Part 21)
- [ ] Build/repair feedback visible but not noisy
- [ ] Combat FX readable at 1x and 2x
- [ ] FX respects ReduceMotion setting
- [ ] No instantiations during play (Profiler)
- [ ] Damage states update correctly across HP thresholds
- [ ] Ammo indicators correct and sync with logic

---

## 11) Next Part (Part 22 đề xuất)
**Art production pipeline**: sprite sheet conventions, prefab templates, animation naming, import settings, and how to add new building visuals safely.

