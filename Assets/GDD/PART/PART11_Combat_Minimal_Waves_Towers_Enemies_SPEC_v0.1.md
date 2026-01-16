# PART 11 — COMBAT MINIMAL (ENEMY SPAWN → PATH → TOWER FIRING → DAMAGE → WAVES) — SPEC v0.1

> Mục tiêu: đủ “Defend phase” chạy thật trong 1 run:
- WaveDirector: schedule waves theo RunClock (Autumn/Winter)
- Enemy spawn (lanes / edges) + target selection (HQ priority)
- Enemy movement (grid stepping contract) + attack buildings
- Tower targeting + firing cadence + ammo consumption per shot
- Damage application + death cleanup
- Hooks: speed controls (default 1x in Defend), notifications, reward drops (optional)

Phần này cố ý tối giản để ship. Cân bằng số liệu nằm ở Deliverable C.

---

## 1) Combat phase contract (RunClock integration)

### 1.1 When combat active
- Combat active during **Autumn + Winter**
- At start of each defend day:
  - WaveDirector builds wave schedule for the day
  - Reset per-day counters

### 1.2 Speed controls
- On entering Defend:
  - force time scale to 1x (default)
  - allow player switch 2x/3x (dev toggles)
- Combat simulation uses scaled dt, but UI/notifications use unscaled.

Events:
- `DefendPhaseStartedEvent`
- `DefendPhaseEndedEvent`
- `WaveStartedEvent`
- `WaveEndedEvent`

---

## 2) Data defs required

### 2.1 EnemyDef
```csharp
public sealed class EnemyDef : ScriptableObject
{
    public string Id;
    public int MaxHP;
    public float MoveSecondsPerCell; // base (before road modifiers)
    public int Damage;               // per hit
    public float AttackInterval;     // seconds between attacks
    public int Priority;             // for wave composition
}
```

### 2.2 WaveDef / WaveStep
v0.1: wave is a list of spawn batches.

```csharp
[System.Serializable]
public struct SpawnBatch
{
    public string EnemyDefId;
    public int Count;
    public float SpawnInterval; // seconds between spawns
    public int Lane;            // 0..N-1
}

public sealed class WaveDef : ScriptableObject
{
    public string Id;
    public SpawnBatch[] Batches;
    public float RewardMultiplier; // optional
}
```

### 2.3 Tower combat fields
TowerDef needs:
- Range (cells)
- FireInterval (seconds)
- Damage per shot
- AmmoPerShot (typically 1)
- AmmoThresholdRatio (0.25)

```csharp
[System.Serializable]
public struct TowerCombatDef
{
    public int RangeCells;
    public float FireInterval;
    public int Damage;
    public int AmmoPerShot;
    public float LowAmmoRatio; // 0.25
}
```

---

## 3) Runtime components (state)

### 3.1 Enemy combat runtime (in EnemyState)
Add fields:
```csharp
public struct EnemyState
{
    public EnemyId Id;
    public string DefId;
    public CellPos Cell;

    public EnemyStatus Status;

    public int HP;

    public BuildingId TargetBuilding; // 0 means HQ
    public CellPos TargetCell;        // entry cell of target

    public float MoveTimer;           // progress to next cell
    public float AttackTimer;         // interval timer
}
```

### 3.2 Tower runtime (in TowerState)
Add:
```csharp
public float FireTimer; // accumulates dt
```

---

## 4) Spawn lanes & target model

### 4.1 Lane definition
v0.1: lanes are pre-authored spawn points (cells) on map edges.
Define in data:
```csharp
public struct LaneDef
{
    public int LaneId;
    public CellPos SpawnCell;
    public CellPos EntryCell; // optional
}
```

`CombatMapDef` holds lanes.

### 4.2 Target selection (minimal)
- Default target: **HQ** (always exists)
- If you have “decoy” or “outer buildings” later, can target nearest building of priority.
v0.1 recommended:
- Enemy moves toward HQ EntryCell.
- If blocked path, fallback to attack nearest building in adjacency.

---

## 5) Pathing contract (minimal)
Bạn đã có pathfinding + road speed boost (from earlier sessions). Combat uses same mover contract:
- Compute path from spawn to target cell.
- If no path:
  - Enemy goes into “Siege” mode: attack nearest blocking building cell (or HQ if adjacent).
  - If still none: despawn after timeout (rare).

We don't implement A* here, just call existing `GridPathfinder.FindPath(from,to)`.

---

## 6) WaveDirector (spawning logic)

### 6.1 Responsibilities
- Choose wave defs for day (from pacing tables)
- Spawn enemies over time according to batches
- Track active enemies count
- Emit events wave start/end

### 6.2 Runtime schedule
Create `WaveSchedule`:
```csharp
public struct WaveSchedule
{
    public WaveDef Def;
    public int BatchIndex;
    public int SpawnedInBatch;
    public float SpawnTimer;
    public bool Started;
    public bool Completed;
}
```

### 6.3 Director loop
Pseudo:
- If no current wave and time to start next wave → start
- For current wave:
  - tick spawn timer
  - when timer reaches interval, spawn one enemy in current batch
  - when batch done, move to next batch
  - when all batches done and all enemies of this wave dead → wave ended

---

## 7) Enemy movement + attack

### 7.1 Movement
- Enemy moves cell-by-cell, using MoveSecondsPerCell (scaled by speed controls)
- Use `MoveTimer += dt; if MoveTimer >= MoveSecondsPerCell => step to next cell`
- If next cell occupied by building:
  - switch to Attacking that building

### 7.2 Attack
- Attack timer increments; when >= AttackInterval:
  - apply damage to target building HP
  - if building destroyed, clear occupancy and retarget (HQ)
- If target is HQ and HQ destroyed: run ends (Defeat)

Damage function:
```csharp
public static void ApplyDamage(ref BuildingState b, int dmg)
{
    b.HP = System.Math.Max(0, b.HP - dmg);
}
```

On destroyed:
- spawn notification `combat.building_destroyed` (optional)
- create “Repair needed” signals (later)

---

## 8) Tower targeting + firing

### 8.1 Target selection (simple)
Each tower selects target enemy in range:
- Range check: Manhattan distance <= RangeCells
- Prioritize:
  1) closest enemy
  2) lowest HP (optional)
  3) tie-break by EnemyId
v0.1 choose closest + id tie-break.

### 8.2 Firing cadence
- `FireTimer += dt`
- if `FireTimer >= FireInterval`:
  - if AmmoCurrent >= AmmoPerShot:
    - consume ammo
    - deal damage to enemy
    - reset FireTimer (or subtract interval)
  - else:
    - cannot fire; ensure ammo request exists (Part 7 tower monitor)
    - optionally notify tower empty once (cooldown)

### 8.3 Ammo threshold request
TowerAmmoMonitor from Part 10 does:
- if ammo <= LowAmmoRatio * AmmoMax => request
- if ammo == 0 => urgent request

---

## 9) Rewards & drops (optional v0.1)
To keep roguelite feel, at end of wave/day:
- Reward basic resources (or meta currency) based on killed enemies.
But this can be in Part 12.

v0.1 minimal:
- No drops; survival is goal.
Or: fixed reward at day end.

---

## 10) Notifications mapping (Part 4)
- `combat.wave_started` (Info)
- `combat.wave_ended` (Info)
- `ammo.tower_low.<id>` (Warning)
- `ammo.tower_empty.<id>` (Error)
- `combat.hq_under_attack` (Warning) (throttled)
- `combat.defeat` (Error)

---

## 11) Save/Load considerations
- Save WaveDirector state:
  - current wave id, batch index, timers, spawned counts
- Save active enemies states (already in WorldState)
- On load, WaveDirector resumes schedule.

---

## 12) QA Checklist (Part 11)
- [ ] Wave spawns enemies correctly by batches and lanes
- [ ] Enemies move toward HQ and attack when blocked
- [ ] Towers target enemies in range and deal damage
- [ ] Ammo consumed per shot and requests generated at <=25%
- [ ] If tower out of ammo, stops firing; resupply restores
- [ ] Defeat triggers when HQ HP reaches 0
- [ ] Default defend speed 1x; 2x/3x works

---

## 13) Next Part (Part 12 đề xuất)
**Run Outcome + Rewards + Meta progression hooks** (premium roguelite loop): win/lose, reward selection, unlocks persistence, run summary.

