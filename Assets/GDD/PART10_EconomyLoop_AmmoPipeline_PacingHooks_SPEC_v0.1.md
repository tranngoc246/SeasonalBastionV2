# PART 10 — ECONOMY LOOP END-TO-END + AMMO PIPELINE + PACING HOOKS — SPEC v0.1

> Mục tiêu: ghép tất cả Part 6–9 thành “loop kinh tế” chạy thật:
- Harvest → Local storage → HaulBasic → Warehouse/HQ
- Consumption (Build/Repair/Upgrade + Craft recipes)
- Ammo pipeline: Forge craft → Forge local ammo → HaulAmmo → Armory → ResupplyTower
- Pacing hooks theo RunClock (day/season) + Defend mode speed
- Failure surfaces: notifications + player guidance

Phần này **không** cân bằng số liệu chi tiết (Deliverable C đã có table), nhưng xác định *where* numbers live và flow đúng.

---

## 1) Economy State Diagram (v0.1)

### 1.1 Basic resources (Wood/Food/Stone/Iron)
1) Worker harvest tại producer
2) Resource tăng vào **producer local storage** (cap nhỏ)
3) TransporterBasic (Warehouse NPC) haul từ local → Warehouse/HQ
4) Consumers (Builder/Smith/HQ) lấy từ Warehouse/HQ/Producer theo priority
5) Consumption:
   - BuildSites delivery consumes from storages to site “delivered”
   - Recipes consume from storages to Forge craft

### 1.2 Ammo
1) Smith lấy inputs (basic) từ Warehouse/HQ/Producer → Forge
2) Smith craft ammo → tăng vào **Forge local ammo storage**
3) ArmoryRunner haul ammo từ Forge → Armory storage (ammo)
4) ArmoryRunner resupply towers thiếu ammo (<=25%)

---

## 2) Building Def fields required (data-driven)

> Nếu trong GDD/Deliverables đã có, coi như “source of truth”; ở đây chỉ liệt kê fields runtime cần.

### 2.1 ProducerDef additions
```csharp
public struct ProducerDef
{
    public ResourceType OutputType;
    public int OutputPerCycle;     // amount per harvest job completion
    public float WorkSeconds;      // time to harvest per cycle
    public int LocalCap;           // local storage cap for output type
}
```

### 2.2 ForgeDef additions
```csharp
public struct ForgeDef
{
    public string RecipeId;        // default ammo recipe
    public int AmmoLocalCap;       // forge local ammo cap
}
```

### 2.3 ArmoryDef additions
```csharp
public struct ArmoryDef
{
    public int AmmoCap;
    public int ResupplyChunk;      // how many ammo per delivery to tower
    public int TargetBuffer;       // desired ammo buffer in armory
}
```

### 2.4 Warehouse/HQ caps
- Storage caps in `StorageCapsDef` per tier.

---

## 3) Runtime “need detectors” (pull-based job creation)

> v0.1 philosophy: create jobs when **need** exists, not time-based autoproduce.

### 3.1 ProducerNeed
Need = producer has worker assigned AND local storage has space.
- JobProvider creates Harvest jobs until local near full (throttle).

### 3.2 HaulNeed (basic)
Need = exists producer local amount > 0 AND exists dest storage has space.
- JobProvider at Warehouse creates HaulBasic jobs.

### 3.3 CraftNeed (ammo)
Need = Forge ammo local has space AND inputs available in storages.
- JobProvider at Forge creates CraftAmmo job.

### 3.4 HaulAmmoNeed
Need = Armory ammo < target buffer OR towers have active requests AND forge has ammo.
- Armory creates HaulAmmo job (Forge -> Armory).

### 3.5 ResupplyNeed
Need = any tower ammo <= threshold.
- Armory creates ResupplyTower job.

---

## 4) Throttling strategy (avoid infinite jobs)

### 4.1 Global caps per workplace
- Per workplace max active jobs (created but not completed) by type:
  - Producer: max 2 harvest jobs pending
  - Warehouse: max 3 haul jobs pending
  - Forge: max 1 craft job pending
  - Armory: max 1 haulAmmo pending + max 2 resupply pending
- Implement: counters in JobProvider (not in JobBoard)

### 4.2 Stale job cleanup
Jobs may become invalid (source empty). Rule:
- Executor fails job with FailReason.SourceEmpty; provider can recreate later.

---

## 5) Ammo Request Queue (tower-driven)

### 5.1 Tower monitor system
Runs every 0.5–1s:
- For each tower:
  - if ammo <= 25% and `!hasOutstandingRequest` → enqueue request
  - if ammo == 0 → enqueue urgent request
- Use cooldown per tower to avoid spam (notification keys)

Data:
```csharp
public struct TowerAmmoRequestState
{
    public bool Outstanding;
    public float LastRequestAt;
}
```

### 5.2 Request priority
- Towers with ammo == 0 higher priority
- then lowest ammo ratio
Tie-break: TowerId.Value ascending

Armory provider picks best towers first.

---

## 6) “Consume from storage” contract (atomicity)

### 6.1 BuildSite delivery
- When builder takes from storage: it is removed immediately.
- If delivery fails to reach site (path fail), v0.1 policy:
  - Carry remains on NPC; NPC returns to nearest basic storage and dumps back (best effort) on cancel/timeout.
  - Avoid “resource loss” for public.

### 6.2 Craft inputs consumption
- On start craft job:
  - Smith must acquire all inputs (sequentially) and then “consume” into Forge (or directly consume from storage)
- Craft output:
  - Add Ammo to Forge local; if full, craft job should not start (provider check).

---

## 7) Pacing hooks (RunClock + Defend speed rules)

### 7.1 RunClock phases
- Build phase: Spring/Summer (build focus)
- Defend phase: Autumn/Winter (combat focus)
- Speed controls:
  - In Defend: default 1x, allow 2x/3x (dev can expose)
  - In Build: allow pause/1x/2x/3x

### 7.2 Job cadence scaling
- WorkSeconds progress is scaled by SimTimeScale:
  - In Defend, if 2x/3x: economy continues faster too (design decision)
  - v0.1 recommendation: economy continues (makes runs shorter), but keep combat readable by default 1x.

### 7.3 Event hooks
- On SeasonChanged:
  - Adjust allowed speeds (TimeScale service)
  - Push notification “Bước vào phòng thủ” / “Bước vào xây dựng”
- On DayStarted:
  - Spawn new NPC if capacity available (your rule)
  - Trigger “new NPC unassigned” notify

---

## 8) Player guidance (first-run funnel)

### 8.1 Early warnings
- Producer local full → suggest build Warehouse:
  - `storage.local_full.<farm>` message body: “Hãy xây Warehouse để vận chuyển tài nguyên”
- No transporter → suggest assign NPC to Warehouse:
  - `workplace.none` with context “Warehouse cần người vận chuyển”
- Tower low ammo → suggest build Forge + Armory (if unlocked):
  - `ammo.tower_low` + hint rules from Part 3/4

### 8.2 Auto-fill only at run start
- At run start: assign initial NPCs to HQ / producers
- Later NPC births: notify and let player assign manually

---

## 9) Minimal “complete run economy” checklist (v0.1)

### 9.1 Must-have systems
- ProducerJobProvider (Harvest)
- WarehouseJobProvider (HaulBasic)
- ForgeJobProvider (CraftAmmo)
- ArmoryJobProvider (HaulAmmo + ResupplyTower)
- TowerAmmoMonitor (Requests)
- BuildSiteJobProvider (Deliver + Work) (from Part 9)
- NotificationService (Part 4)
- UnlockService (Part 3)

### 9.2 Must-have content defs
- Farmhouse producer (Food)
- LumberCamp producer (Wood)
- HQ storage + basic build actions
- Warehouse (unlock soon)
- Forge + Armory (unlock mid-run)
- 1 ArrowTower (ammo consumer)

---

## 10) Edge cases & policies

### 10.1 Warehouse full
- HaulBasic provider stops creating jobs
- Producers may fill local, notify local full (throttled)

### 10.2 Forge starved
- Craft provider pauses; notify `ammo.forge_no_input` only when a tower needs ammo or armory buffer low (avoid spam)

### 10.3 Armory empty but tower requests
- Create HaulAmmo if forge has ammo
- If forge has none: notify `ammo.armory_empty` + hint “Cần sản xuất đạn”

### 10.4 Mixed storages
- Selection uses deterministic nearest + priority order (Part 7)

---

## 11) Metrics hooks (for future live ops)
Track:
- avg time local storage full
- % time towers below 25% ammo
- job fail reasons counts
- time-to-first-warehouse/forge/armory
These can be printed to debug logs v0.1, later analytics.

---

## 12) QA Checklist (Part 10)
- [ ] Food/Wood increases only when worker completes harvest and delivers to local
- [ ] Warehouse transporter moves resources to warehouse/hq
- [ ] Builder consumes resources from storage to build sites (no “auto income”)
- [ ] Forge crafts ammo only with inputs; output stored locally
- [ ] Armory runner moves ammo to armory and resupplies towers at <=25%
- [ ] Notifications guide player without spam
- [ ] Season changes adjust speeds and messaging

---

## 13) Next Part (Part 11 đề xuất)
**Combat model minimal**: enemy spawn lanes, tower targeting, damage, ammo consumption per shot, defend wave loop integration with RunClock.

