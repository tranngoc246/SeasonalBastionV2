# PART 17 — CONTENT PRODUCTION PIPELINE (BUILDINGS / ENEMIES / REWARDS) + VALIDATION + BALANCE WORKFLOW — SPEC v0.1

> Mục tiêu: mở rộng content nhanh nhưng không phá game:
- Template rõ ràng cho BuildingDef / EnemyDef / RecipeDef / RewardDef
- Validator rules bắt buộc (Part 0)
- Workflow “add content” theo checklist
- Balance workflow: từ bảng (Deliverable C) → defs → playtest → adjust
- Naming conventions + folder layout + version control discipline

---

## 1) Golden rules (public-ready content)
1) **Data-driven first**: content mới không cần code mới (nếu có thể)
2) **Validator is gatekeeper**: content không qua validator = không merge
3) **Small incremental**: add 1 building → playtest → merge
4) **No hidden coupling**: building mới phải khai báo đầy đủ tags/roles
5) **Backwards safe**: thay đổi defs phải có migration consideration

---

## 2) Folder layout (source of truth)

```
Assets/Game/Defs/
  Buildings/
    Core/
    Production/
    Defense/
    Ammo/
  Enemies/
  Waves/
  Recipes/
  Rewards/
  Localization/
  BalanceTables/   (optional csv/json exports)
```

---

## 3) Naming conventions

### 3.1 Id format
- BuildingDef: `bld.<category>.<name>.<tier>`  
  e.g. `bld.prod.farmhouse.t1`
- EnemyDef: `enemy.<faction>.<name>.t1`
- RecipeDef: `recipe.ammo.arrow.t1`
- RewardDef: `reward.<category>.<name>.t1`
- WaveDef: `wave.<season>.<day>.<index>`

### 3.2 Asset filename
- Match id:
  - `bld.prod.farmhouse.t1.asset`
  - `recipe.ammo.arrow.t1.asset`

---

## 4) Building content template (required fields)

### 4.1 BuildingDef minimal
```csharp
public sealed class BuildingDef : ScriptableObject
{
    public string Id;

    // placement
    public Vector2Int Footprint;      // w,h
    public bool RequiresRoad;
    public int DrivewayLen;           // locked 1 (v0.1)
    public bool AllowRotate;

    // stats
    public int MaxHP;
    public int Tier;

    // storage
    public StorageCapsDef StorageCaps; // caps per resource type (including Ammo if allowed)

    // workplace
    public WorkplaceDef Workplace;     // slots + allowed roles + job types

    // build pipeline
    public CostDef BuildCost;
    public float BuildSeconds;
    public CostDef UpgradeCost;        // optional per tier, or separate UpgradeDef
    public float UpgradeSeconds;
}
```

### 4.2 Tags/Flags (must be explicit)
Add enum flags:
- HQ, Warehouse, Producer, Forge, Armory, Tower, Housing, BuilderHut, Road

These flags drive:
- IndexService lists (Part 7)
- Job providers (Part 10)
- UI categories (Part 13)

---

## 5) Producer buildings (Farm/Lumber/Stone/Iron)

### 5.1 ProducerDef (embedded or separate)
```csharp
[System.Serializable]
public struct ProducerDef
{
    public ResourceType OutputType;
    public int OutputPerCycle;
    public float WorkSeconds;
    public int LocalCap;
}
```

### 5.2 Checklist when adding producer
- StorageCaps includes `OutputType` cap = LocalCap
- Workplace slots >= 1
- Allowed role includes Worker
- Job types include HarvestX

---

## 6) Ammo buildings (Forge/Armory)

### 6.1 Forge template
- Flags: Forge
- StorageCaps: basic inputs optional (small), Ammo cap (local) required
- Workplace: Smith slots >=1
- RecipeId default

### 6.2 Armory template
- Flags: Armory
- StorageCaps: Ammo cap required; basic caps = 0
- Workplace: ArmoryRunner slots >=1
- Fields: ResupplyChunk, TargetBuffer

### 6.3 Ammo tower template
- Flags: Tower
- TowerCombatDef required
- Tower ammo max in TowerState/def

---

## 7) Enemy content template

### 7.1 EnemyDef
- Id, HP, speed, damage, interval, armor (optional)
- Size (optional), lane constraints (optional)

### 7.2 WaveDef
- Batches: enemy id, count, interval, lane
- Day mapping: which day triggers which wave(s)

---

## 8) Reward content template

### 8.1 Reward constraints
Each reward must declare:
- Category
- Rarity
- Effects (typed)
- Stack policy (optional)
- Unlock prerequisites (optional)

Add fields:
```csharp
public bool Unique;          // can pick once per run
public int MaxStacks;        // if stackable
public string RequiresUnlock;// optional gating
```

### 8.2 Reward pool table
Define in data:
- DayIndex → rarity weights
- Category weights by day
- Exclude sets if buildings not unlocked

---

## 9) Validator rules (hard gates)

### 9.1 Global ID rules
- Unique ids per def type
- No empty ids
- No whitespace, lowercase recommended

### 9.2 Building rules
- Footprint > 0
- If RequiresRoad => DrivewayLen == 1 (v0.1)
- Warehouse: Ammo cap must be 0
- HQ: Ammo cap must be 0 (v0.1)
- Armory: Ammo cap > 0; basic caps == 0 recommended
- Forge: Ammo local cap > 0; RecipeId exists
- Producer: OutputType cap > 0 and matches ProducerDef.LocalCap
- Workplace:
  - slot count >= 0
  - roles allowed match building flags

### 9.3 Recipe rules
- Input cost non-zero
- Output amount > 0
- Output type Ammo
- WorkSeconds > 0

### 9.4 Reward rules
- At least 1 effect
- Effects must be supported (switch exhaustive)
- TargetId required for effects that need target

### 9.5 Wave rules
- EnemyDefId exists
- Count > 0
- SpawnInterval > 0
- Lane within lanes count

---

## 10) Balance workflow (safe & fast)

### 10.1 Single source of truth for numbers
- Keep one balance table (Deliverable C):
  - produce rates, caps, costs, craft times, tower stats, enemy stats
- Export to ScriptableObjects:
  - Option A: manual entry with checklist
  - Option B: CSV importer tool (later)

v0.1 recommend: manual + validator + small increments.

### 10.2 Tuning loop
1) Choose metric to tune (e.g., ammo shortage time)
2) Adjust 1–2 numbers (craft output or resupply chunk)
3) Run 3 playtests with same seed
4) Compare metrics (Part 10/12 telemetry)
5) Merge if improved

### 10.3 Avoid “balance drift”
- Always change numbers in one place (table → defs)
- Keep changelog entry.

---

## 11) Content PR template (Git discipline)
Each PR adding content must include:
- List of new/changed defs
- Validator output screenshot/log (pass)
- 1–3 test steps
- Expected gameplay impact
- If changes break run save: mark clearly

---

## 12) Tooling recommended (v0.1)
- In-editor “Def Browser” window:
  - search by id
  - open asset
  - run validator
- Quick “Spawn building by def id” (Part 15)

---

## 13) Minimal content roadmap (to reach public v0.1)
Buildings:
- Core: HQ, House, Warehouse
- Production: Farmhouse, LumberCamp, StonePit, IronMine
- Defense: ArrowTower (later 1–2 more)
- Ammo: Forge, Armory

Enemies:
- 2–3 basic types + 1 elite
Waves:
- 10–20 waves across Autumn/Winter
Rewards:
- 30–50 rewards total (mix economy/combat)

---

## 14) QA Checklist (Part 17)
- [ ] New building appears in build menu with correct unlock gating
- [ ] Placement rules correct (entry/road)
- [ ] Storage caps correct and warehouse no-ammo enforced
- [ ] Jobs created for its workplace/role
- [ ] Validator passes with no warnings for required rules
- [ ] At least one playtest run validates loop impact

---

## 15) Next Part (Part 18 đề xuất)
**Tutorial & Onboarding design**: objectives, hints, first-run tasks, teach assignment, teach ammo pipeline, teach defend.

