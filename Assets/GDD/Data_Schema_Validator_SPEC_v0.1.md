# DATA SCHEMA + VALIDATOR — SPEC (PUBLIC-READY) — v0.1

> Mục tiêu: **không hardcode balance**, mọi số liệu sống trong Data Assets; có **Validator** bắt lỗi sớm để tránh “làm lại”.  
> Phạm vi: schema tối ưu cho Seasonal Bastion (grid builder + seasonal defend + ammo pipeline), đủ để chạy 1 run hoàn chỉnh.

---

## 0) Nguyên tắc thiết kế Data (LOCKED cho pipeline)
1) **ID là khóa chính** (string, ổn định): không phụ thuộc tên hiển thị.
2) **Defs bất biến ở runtime** (read-only), runtime chỉ dùng **State**.
3) **Tách rõ Def vs State**:
   - Def: cost, stats, caps, unlock… (data)
   - State: HP hiện tại, ammo hiện tại, tồn kho hiện tại… (runtime)
4) **Validator là gate**: build / playtest không chạy nếu validator có lỗi mức `Error`.

---

## 1) Folder + Naming chuẩn (để public dễ maintain)
### 1.1 Folder
- `Assets/Game/Data/Defs/Buildings/`
- `Assets/Game/Data/Defs/Towers/`
- `Assets/Game/Data/Defs/Enemies/`
- `Assets/Game/Data/Defs/Waves/`
- `Assets/Game/Data/Defs/Run/`
- `Assets/Game/Data/Defs/Resources/`
- `Assets/Game/Data/Db/` (Database assets)
- `Assets/Game/Editor/DataTools/` (Validator + Importer)

### 1.2 Naming convention
- Asset name: `DEF_<Category>_<Id>`  
  Ví dụ: `DEF_Building_Farmhouse_L1`, `DEF_Tower_Arrow_L1`, `DEF_Enemy_Raider`
- ID format (khuyến nghị): `building.farmhouse.l1`, `tower.arrow.l1`, `enemy.raider`
- Không đổi ID sau khi public (save migration cực đau).

---

## 2) Core Types (shared schema)

### 2.1 ResourceType
```csharp
public enum ResourceType
{
    None = 0,
    Wood = 1,
    Stone = 2,
    Iron = 3,
    Food = 4,
    Ammo = 5
}
```

### 2.2 CostDef
```csharp
[System.Serializable]
public struct CostDef
{
    public int Wood;
    public int Stone;
    public int Iron;
    public int Food;
    public int Ammo;

    public bool IsEmpty => Wood==0 && Stone==0 && Iron==0 && Food==0 && Ammo==0;
}
```

### 2.3 StorageCapsDef
```csharp
[System.Serializable]
public struct StorageCapsDef
{
    public int WoodCap;
    public int StoneCap;
    public int IronCap;
    public int FoodCap;
    public int AmmoCap;
}
```

### 2.4 UnlockDef
Unlock gắn vào **run calendar** theo year/season/day.
```csharp
public enum Season { Spring, Summer, Autumn, Winter }

[System.Serializable]
public struct UnlockDef
{
    public int Year;        // 1..TotalYears
    public Season Season;   // Spring/Summer/Autumn/Winter
    public int DayIndex;    // 1..DaysInSeason
}
```

### 2.5 FootprintDef (placement)
```csharp
public enum EntrySide { N, E, S, W }

[System.Serializable]
public struct FootprintDef
{
    public int Width;     // >=1
    public int Height;    // >=1
    public bool RequiresRoadConnection; // true/false
    public int DrivewayLength;         // 0..1 (v0.1)
}
```

---

## 3) ScriptableObject Defs (schema chi tiết)

> Các defs dưới đây là “tối ưu vừa đủ”: đủ data cho public run, không nhồi quá nhiều.

### 3.1 RunCalendarDef
```csharp
[CreateAssetMenu(menuName="Game/Defs/RunCalendarDef")]
public sealed class RunCalendarDef : ScriptableObject
{
    public int TotalYears = 2;

    // days per season
    public int SpringDays = 6;
    public int SummerDays = 6;
    public int AutumnDays = 4;
    public int WinterDays = 4;

    // seconds per day
    public float SecondsPerDayDev = 180f;     // Spring+Summer
    public float SecondsPerDayDefend = 120f;  // Autumn+Winter

    // speed options
    public bool ForceSpeed1xInDefend = true;
    public int[] AllowedSpeeds = new [] { 0, 1, 2, 3 }; // 0=Pause

    // scaling (optional for v0.1)
    public float Year2HpMultiplier = 1.35f;
    public float Year2DmgMultiplier = 1.25f;
    public float Year2CountMultiplier = 1.20f;

    public int DaysInSeason(Season s) => s switch {
        Season.Spring => SpringDays,
        Season.Summer => SummerDays,
        Season.Autumn => AutumnDays,
        Season.Winter => WinterDays,
        _ => 0
    };
}
```

### 3.2 ResourceDef (optional nhưng rất đáng có)
Dùng để: icon, display name, sorting, và validator mapping.
```csharp
[CreateAssetMenu(menuName="Game/Defs/ResourceDef")]
public sealed class ResourceDef : ScriptableObject
{
    public string Id; // e.g., resource.wood
    public ResourceType Type;
    public string DisplayName;
    public int SortOrder;
}
```

### 3.3 BuildingDef
```csharp
public enum BuildingCategory
{
    Core, Housing, Economy, Logistics, Production, Defense, Utility
}

[CreateAssetMenu(menuName="Game/Defs/BuildingDef")]
public sealed class BuildingDef : ScriptableObject
{
    [Header("Identity")]
    public string Id;
    public string DisplayName;
    public BuildingCategory Category;
    public int Tier; // 1..3

    [Header("Build/Upgrade")]
    public CostDef BuildCost;
    public CostDef UpgradeCost;      // if tier>1, optional
    public int MaxHP;

    [Header("Placement")]
    public FootprintDef Footprint;

    [Header("Storage")]
    public StorageCapsDef StorageCaps;

    [Header("Workplace")]
    public WorkplaceDef Workplace;   // null nếu không phải workplace

    [Header("Unlock")]
    public UnlockDef Unlock;

    [Header("Tags")]
    public bool IsHQ;
    public bool IsWarehouse;
    public bool IsForge;
    public bool IsArmory;
    public bool IsTowerLike; // nếu building này là “tower” dạng building (tuỳ kiến trúc)
}
```

### 3.4 WorkplaceDef
Workplace định nghĩa **job archetypes** mà NPC được assign sẽ làm.
```csharp
public enum JobArchetype
{
    None,
    Leisure,
    Inspect,

    // Economy
    HarvestFood,
    HarvestWood,
    HarvestStone,
    HarvestIron,

    // Logistics
    HaulBasic,
    HaulAmmo,
    ResupplyTower,

    // Build
    Build,
    Upgrade,
    Repair,
    Demolish,

    // Production
    CraftAmmo
}

[CreateAssetMenu(menuName="Game/Defs/WorkplaceDef")]
public sealed class WorkplaceDef : ScriptableObject
{
    public string Id;          // workplace.builderhut.l1 ...
    public int Slots = 1;      // số NPC assign tối đa
    public JobArchetype[] Provides; // các job cho workplace
}
```

### 3.5 TowerDef
Tower tách ra khỏi BuildingDef để:
- dễ tune combat
- validate ammo pipeline rõ
```csharp
public enum TargetingPolicy
{
    ClosestToHQ,
    Closest,
    LowestHP,
    HighestHP
}

[System.Serializable]
public struct AmmoDef
{
    public int AmmoMax;
    public int AmmoPerShot;
    public float NeedsAmmoThresholdPct; // default 0.25

    public int ThresholdAmmo => (int)System.MathF.Ceiling(AmmoMax * NeedsAmmoThresholdPct);
}

[CreateAssetMenu(menuName="Game/Defs/TowerDef")]
public sealed class TowerDef : ScriptableObject
{
    [Header("Identity")]
    public string Id;
    public string DisplayName;
    public int Tier;

    [Header("Build/Upgrade")]
    public CostDef BuildCost;
    public CostDef UpgradeCost;
    public int MaxHP;

    [Header("Combat")]
    public float Range;
    public float FireInterval;
    public int Damage;
    public TargetingPolicy Targeting;

    [Header("Ammo")]
    public AmmoDef Ammo;

    [Header("Unlock")]
    public UnlockDef Unlock;
}
```

### 3.6 EnemyDef
```csharp
[System.Flags]
public enum EnemyTag
{
    None = 0,
    Swarm = 1<<0,
    Raider = 1<<1,
    Bruiser = 1<<2,
    Ranged = 1<<3,
    Sapper = 1<<4,
    Elite = 1<<5,
    Boss = 1<<6
}

[CreateAssetMenu(menuName="Game/Defs/EnemyDef")]
public sealed class EnemyDef : ScriptableObject
{
    public string Id;
    public string DisplayName;

    public int HP;
    public float Speed;

    public int DamageToHQ;
    public int DamageToBuildings;

    public EnemyTag Tags;
}
```

### 3.7 WaveDef
```csharp
[System.Serializable]
public struct SpawnEntry
{
    public string EnemyId;
    public int Count;
}

[System.Serializable]
public struct SpawnGroupDef
{
    public float Delay;            // delay trước nhóm này
    public float Interval;         // spawn interval trong group
    public SpawnEntry[] Enemies;   // các loại enemy trong group
}

[CreateAssetMenu(menuName="Game/Defs/WaveDef")]
public sealed class WaveDef : ScriptableObject
{
    public string Id;       // wave.y1.autumn.d1 ...
    public int Year;
    public Season Season;
    public int DayIndex;    // 1..DaysInSeason

    public bool IsBossWave;
    public string BossEnemyId; // optional

    public SpawnGroupDef[] Groups;
}
```

---

## 4) Data Database (single entry point)
Bạn nên có “database asset” tham chiếu tất cả defs để load nhanh, validate nhanh.

### 4.1 GameDatabase
```csharp
[CreateAssetMenu(menuName="Game/Db/GameDatabase")]
public sealed class GameDatabase : ScriptableObject
{
    public RunCalendarDef Calendar;

    public ResourceDef[] Resources;
    public BuildingDef[] Buildings;
    public WorkplaceDef[] Workplaces;
    public TowerDef[] Towers;
    public EnemyDef[] Enemies;
    public WaveDef[] Waves;
}
```

---

## 5) Validator — Spec chi tiết (Editor Tool)

### 5.1 Severity
- `Info`: gợi ý
- `Warning`: có thể chạy, nhưng nên sửa
- `Error`: không cho play/build

### 5.2 Output format
Mỗi issue gồm:
- `Severity`
- `Code` (stable string)
- `Message`
- `Object` (UnityEngine.Object reference)
- `Path` (optional)

```csharp
public enum ValidationSeverity { Info, Warning, Error }

public readonly struct ValidationIssue
{
    public readonly ValidationSeverity Severity;
    public readonly string Code;
    public readonly string Message;
    public readonly UnityEngine.Object Context;

    public ValidationIssue(ValidationSeverity s, string code, string msg, UnityEngine.Object ctx)
    {
        Severity = s; Code = code; Message = msg; Context = ctx;
    }
}
```

### 5.3 Validator entry point
```csharp
public static class GameDataValidator
{
    public static List<ValidationIssue> Validate(GameDatabase db)
    {
        var issues = new List<ValidationIssue>(256);

        ValidateDatabase(db, issues);
        ValidateCalendar(db.Calendar, issues);

        ValidateResources(db, issues);
        ValidateWorkplaces(db, issues);
        ValidateBuildings(db, issues);
        ValidateTowers(db, issues);
        ValidateEnemies(db, issues);
        ValidateWaves(db, issues);

        ValidateCrossRefs(db, issues);
        ValidateWaveCoverage(db, issues);

        return issues;
    }
}
```

---

## 6) Validation Rules — chi tiết theo nhóm

### 6.1 Database level
**ERROR**
- `DB_NULL_CALENDAR`: Calendar null
- `DB_DUPLICATE_ID`: bất kỳ def nào trùng ID

**WARNING**
- `DB_EMPTY_LIST`: list rỗng (ví dụ chưa có waves) — tùy giai đoạn dev

### 6.2 Calendar
**ERROR**
- `CAL_TOTAL_YEARS_INVALID`: TotalYears < 1
- `CAL_DAYS_INVALID`: bất kỳ season days <= 0
- `CAL_SECONDS_INVALID`: SecondsPerDayDev/Defend <= 10 (quá thấp) hoặc <=0
- `CAL_SPEED_OPTIONS_INVALID`: AllowedSpeeds thiếu 1x hoặc thiếu pause (nếu bạn muốn pause)

**INFO**
- `CAL_LONG_RUN`: nếu tổng thời gian 1 run > 150 phút (gợi ý)

### 6.3 Generic Def Rules (apply to all)
**ERROR**
- `DEF_ID_EMPTY`
- `DEF_ID_NOT_UNIQUE`
- `DEF_DISPLAY_EMPTY`
- `DEF_TIER_INVALID` (tier <1)
- `DEF_COST_NEGATIVE`
- `DEF_MAXHP_INVALID` (<=0)

### 6.4 Buildings
**ERROR**
- `BLD_FOOTPRINT_INVALID`: width/height <=0
- `BLD_UNLOCK_OUT_OF_RANGE`: year/dayIndex vượt calendar
- `BLD_HQ_MULTIPLE`: IsHQ true > 1 (trừ khi intentionally)
- `BLD_STORAGE_NEGATIVE` cap <0
- `BLD_AMMO_CAP_IN_WAREHOUSE`: nếu IsWarehouse và AmmoCap>0 => Error

**WARNING**
- `BLD_STORAGE_ALL_ZERO`: building có storage nhưng cap=0 hết (có thể quên)
- `BLD_ROAD_RULE_INVALID`: RequiresRoadConnection true nhưng DrivewayLength ngoài [0..1]

### 6.5 Workplaces
**ERROR**
- `WKP_ID_EMPTY`
- `WKP_SLOTS_INVALID` (<=0)
- `WKP_PROVIDES_EMPTY` (length=0)
- `WKP_JOB_ARCHETYPE_NONE` (chứa None)

**WARNING**
- `WKP_DUPLICATE_PROVIDES` (trùng job archetype trong list)

### 6.6 Towers
**ERROR**
- `TWR_AMMO_INVALID`: AmmoMax <=0 hoặc AmmoPerShot<=0 hoặc AmmoPerShot>AmmoMax
- `TWR_NEEDS_AMMO_PCT_INVALID`: <=0 hoặc >=1
- `TWR_FIRE_INTERVAL_INVALID`: <=0
- `TWR_RANGE_INVALID`: <=0
- `TWR_UNLOCK_OUT_OF_RANGE`

**WARNING**
- `TWR_AMMO_THRESHOLD_TOO_LOW`: threshold ammo < AmmoPerShot*3

### 6.7 Enemies
**ERROR**
- `ENY_HP_INVALID` (<=0)
- `ENY_SPEED_INVALID` (<=0)
- `ENY_DAMAGE_INVALID` (any <0)

**WARNING**
- `ENY_TAGS_NONE` (quên tag)

### 6.8 Waves
**ERROR**
- `WAV_UNLOCK_OUT_OF_RANGE`
- `WAV_GROUPS_EMPTY`
- `WAV_ENTRY_ENEMY_NOT_FOUND`
- `WAV_ENTRY_COUNT_INVALID` (<=0)
- `WAV_BOSS_WAVE_NO_BOSS_ID`: IsBossWave true nhưng BossEnemyId empty
- `WAV_GROUP_INTERVAL_INVALID` (<0) hoặc quá nhỏ <0.05
- `WAV_GROUP_DELAY_NEGATIVE`

**WARNING**
- `WAV_VERY_HIGH_COUNT`: tổng count > 200

---

## 7) Cross-reference Rules (quan trọng để khỏi làm lại)

### 7.1 Unlock cadence sanity
**ERROR**
- Tower unlock trước Forge/Armory (ammo chain)  
Code: `XREF_AMMO_CHAIN_UNLOCK_ORDER`

### 7.2 Ammo pipeline hard rules
**ERROR**
- Warehouse AmmoCap > 0 => `XREF_WAREHOUSE_CONTAINS_AMMO`
- Không có ArmoryDef => `XREF_NO_ARMORY_DEF`
- Không có ForgeDef => `XREF_NO_FORGE_DEF`

### 7.3 Workplace link
**ERROR**
- BuildingDef.Workplace không nằm trong db => `XREF_WORKPLACE_NOT_IN_DB`

---

## 8) Wave Coverage (cực quan trọng)
Validator phải đảm bảo:
- Mọi ngày của mùa **Defend** (Autumn + Winter) cho Year 1..TotalYears đều có wave.

**ERROR**
- `COV_MISSING_WAVE`: thiếu wave cho (Year, Season, DayIndex)
- `COV_DUPLICATE_WAVE`: có >1 wave cho cùng key

---

## 9) Editor UX (Menu + Report)
- `Tools/Game/Validate Data`
- `Tools/Game/Validate Data (Select DB)`
- Report window:
  - Filter severity
  - Click issue => ping asset
  - Export to txt/json

---

## 10) Minimal Implementation Notes (để code nhanh, ít làm lại)
1) `GameDatabase` nên là 1 asset duy nhất.
2) Boot gọi validator; nếu có `Error` => stop play (Editor only).
3) CSV import (optional) chỉ Editor (generate/update SO), runtime chỉ load SO.

---

## 11) Checklist hoàn thành phần này
- [ ] Có GameDatabase asset reference đầy đủ
- [ ] Validate Data chạy < 1s
- [ ] Click issue ping đúng asset
- [ ] Wave coverage check pass
- [ ] Ammo chain unlock order pass
- [ ] Warehouse ammo rule pass
