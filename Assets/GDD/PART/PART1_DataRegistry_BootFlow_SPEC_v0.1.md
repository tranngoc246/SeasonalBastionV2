# PART 1 — DATA REGISTRY RUNTIME + BOOT FLOW (SPEC) — v0.1

> Mục tiêu: sau khi đã có **Data schema + Validator**, bước kế tiếp là tạo **Runtime Registry** (lookup cực nhanh) + **Boot flow** chuẩn để:
- Game **không bao giờ chạy với data lỗi** (Editor gate)
- Runtime không phải scan arrays mỗi lần
- Các hệ khác chỉ phụ thuộc vào `IDataRegistry` (giảm coupling, dễ refactor)

---

## 1) Vai trò & ranh giới
### 1.1 Data schema (đã có)
- `GameDatabase` tham chiếu tất cả defs
- Validator bắt lỗi asset

### 1.2 Data registry (phần này)
- Load `GameDatabase` → build dictionary caches:
  - `Id -> Def` cho Buildings/Towers/Enemies/Waves/Workplaces/Resources
  - `WaveKey -> WaveDef` (WaveKey = Year+Season+DayIndex)
- Cung cấp API lookup chuẩn cho gameplay:
  - `GetBuilding(id)` / `TryGetBuilding(id, out def)`
  - `GetWave(year, season, dayIndex)`
- Không mutate defs, không chứa runtime state.

### 1.3 Boot flow (phần này)
- `GameBootstrapper` chạy 1 lần ở scene entry
- Tải database
- Validate (Editor only strict) / Validate (runtime soft)
- Khởi tạo registry
- Khởi tạo context & run

---

## 2) Interface Contracts (single dependency surface)

### 2.1 `IDataRegistry`
```csharp
public interface IDataRegistry
{
    RunCalendarDef Calendar { get; }

    bool TryGetResource(string id, out ResourceDef def);
    bool TryGetBuilding(string id, out BuildingDef def);
    bool TryGetWorkplace(string id, out WorkplaceDef def);
    bool TryGetTower(string id, out TowerDef def);
    bool TryGetEnemy(string id, out EnemyDef def);

    bool TryGetWave(int year, Season season, int dayIndex, out WaveDef def);

    // Strict getters (throw nếu thiếu) — chỉ dùng khi bạn chắc chắn
    ResourceDef GetResource(string id);
    BuildingDef GetBuilding(string id);
    WorkplaceDef GetWorkplace(string id);
    TowerDef GetTower(string id);
    EnemyDef GetEnemy(string id);
    WaveDef GetWave(int year, Season season, int dayIndex);
}
```

### 2.2 Design rules (LOCKED)
- Gameplay code **không được** truy cập trực tiếp `GameDatabase` arrays.
- Mọi lookup đi qua `IDataRegistry`.
- `GetX()` chỉ dùng cho:
  - content locked (ví dụ start load)
  - assert nội bộ
- `TryGetX()` dùng cho:
  - data-driven unlock (không crash)
  - mod/future content

---

## 3) DataRegistry Implementation

### 3.1 `WaveKey`
```csharp
public readonly struct WaveKey : System.IEquatable<WaveKey>
{
    public readonly int Year;
    public readonly Season Season;
    public readonly int DayIndex;

    public WaveKey(int year, Season season, int dayIndex)
    { Year = year; Season = season; DayIndex = dayIndex; }

    public bool Equals(WaveKey other)
        => Year == other.Year && Season == other.Season && DayIndex == other.DayIndex;

    public override bool Equals(object obj) => obj is WaveKey other && Equals(other);

    public override int GetHashCode()
        => System.HashCode.Combine(Year, (int)Season, DayIndex);

    public override string ToString() => $"Y{Year}-{Season}-D{DayIndex}";
}
```

### 3.2 `DataRegistry` class
```csharp
public sealed class DataRegistry : IDataRegistry
{
    public RunCalendarDef Calendar { get; private set; }

    private readonly Dictionary<string, ResourceDef> _resources = new(64);
    private readonly Dictionary<string, BuildingDef> _buildings = new(256);
    private readonly Dictionary<string, WorkplaceDef> _workplaces = new(128);
    private readonly Dictionary<string, TowerDef> _towers = new(128);
    private readonly Dictionary<string, EnemyDef> _enemies = new(256);
    private readonly Dictionary<WaveKey, WaveDef> _waves = new(256);

    public static DataRegistry BuildFrom(GameDatabase db)
    {
        var r = new DataRegistry();
        r.Load(db);
        return r;
    }

    public void Load(GameDatabase db)
    {
        Calendar = db.Calendar;

        _resources.Clear();
        _buildings.Clear();
        _workplaces.Clear();
        _towers.Clear();
        _enemies.Clear();
        _waves.Clear();

        IndexById(db.Resources, _resources);
        IndexById(db.Buildings, _buildings);
        IndexById(db.Workplaces, _workplaces);
        IndexById(db.Towers, _towers);
        IndexById(db.Enemies, _enemies);

        IndexWaves(db.Waves, _waves);
    }

    private static void IndexById<T>(T[] list, Dictionary<string, T> dict) where T : UnityEngine.Object
    {
        if (list == null) return;
        foreach (var item in list)
        {
            if (item == null) continue;
            var id = GetId(item);
            if (string.IsNullOrWhiteSpace(id)) continue;
            dict[id] = item;
        }
    }

    private static string GetId(UnityEngine.Object obj)
    {
        return obj switch
        {
            ResourceDef d => d.Id,
            BuildingDef d => d.Id,
            WorkplaceDef d => d.Id,
            TowerDef d => d.Id,
            EnemyDef d => d.Id,
            WaveDef d => d.Id,
            _ => null
        };
    }

    private static void IndexWaves(WaveDef[] waves, Dictionary<WaveKey, WaveDef> dict)
    {
        if (waves == null) return;
        foreach (var w in waves)
        {
            if (w == null) continue;
            dict[new WaveKey(w.Year, w.Season, w.DayIndex)] = w;
        }
    }

    // TryGet
    public bool TryGetResource(string id, out ResourceDef def) => _resources.TryGetValue(id, out def);
    public bool TryGetBuilding(string id, out BuildingDef def) => _buildings.TryGetValue(id, out def);
    public bool TryGetWorkplace(string id, out WorkplaceDef def) => _workplaces.TryGetValue(id, out def);
    public bool TryGetTower(string id, out TowerDef def) => _towers.TryGetValue(id, out def);
    public bool TryGetEnemy(string id, out EnemyDef def) => _enemies.TryGetValue(id, out def);
    public bool TryGetWave(int year, Season season, int dayIndex, out WaveDef def)
        => _waves.TryGetValue(new WaveKey(year, season, dayIndex), out def);

    // Get (throw)
    public ResourceDef GetResource(string id) => GetOrThrow(_resources, id, nameof(ResourceDef));
    public BuildingDef GetBuilding(string id) => GetOrThrow(_buildings, id, nameof(BuildingDef));
    public WorkplaceDef GetWorkplace(string id) => GetOrThrow(_workplaces, id, nameof(WorkplaceDef));
    public TowerDef GetTower(string id) => GetOrThrow(_towers, id, nameof(TowerDef));
    public EnemyDef GetEnemy(string id) => GetOrThrow(_enemies, id, nameof(EnemyDef));
    public WaveDef GetWave(int year, Season season, int dayIndex)
    {
        if (_waves.TryGetValue(new WaveKey(year, season, dayIndex), out var w)) return w;
        throw new System.Collections.Generic.KeyNotFoundException($"Missing WaveDef: Y{year} {season} D{dayIndex}");
    }

    private static T GetOrThrow<T>(Dictionary<string, T> dict, string id, string typeName)
    {
        if (id == null) throw new System.ArgumentNullException(nameof(id));
        if (dict.TryGetValue(id, out var v)) return v;
        throw new System.Collections.Generic.KeyNotFoundException($"Missing {typeName} id='{id}'");
    }
}
```

> Ghi chú: `IndexById` ở trên “đơn giản”. Trên thực tế bạn nên rely vào Validator để đảm bảo không có trùng ID. Nếu trùng ID, dict sẽ overwrite — và đó là lý do Validator phải gate.

---

## 4) Boot Flow (Public-ready)

### 4.1 `GameBootstrapper` (MonoBehaviour)
**Trách nhiệm**
- Locate `GameDatabase` (ref trong inspector)
- Validate data
- Build registry
- Create GameContext
- Start new run / load run

```csharp
public sealed class GameBootstrapper : UnityEngine.MonoBehaviour
{
    [Header("Database")]
    [SerializeField] private GameDatabase _db;

    [Header("Start Mode")]
    [SerializeField] private bool _autoStartNewRun = true;
    [SerializeField] private bool _tutorialDefaultOn = true;

    private GameContext _ctx;

    private void Awake()
    {
        if (_db == null)
        {
            UnityEngine.Debug.LogError("GameDatabase missing on GameBootstrapper.");
            enabled = false;
            return;
        }

#if UNITY_EDITOR
        var issues = GameDataValidator.Validate(_db);
        var hasError = issues.Exists(i => i.Severity == ValidationSeverity.Error);
        if (hasError)
        {
            // In Editor: block play to prevent debugging wrong data
            foreach (var i in issues)
                UnityEngine.Debug.LogError($"[DATA:{i.Code}] {i.Message}", i.Context);

            UnityEditor.EditorApplication.isPlaying = false;
            return;
        }
#else
        // Runtime: still validate, but fail gracefully (show dialog / safe scene)
        var issues = GameDataValidator.Validate(_db);
        if (issues.Exists(i => i.Severity == ValidationSeverity.Error))
        {
            foreach (var i in issues)
                UnityEngine.Debug.LogError($"[DATA:{i.Code}] {i.Message}", i.Context);
            // TODO: show "Data corrupted" screen or fallback
            enabled = false;
            return;
        }
#endif

        var registry = DataRegistry.BuildFrom(_db);

        _ctx = new GameContext(registry);
        _ctx.Boot();

        if (_autoStartNewRun)
        {
            _ctx.Run.StartNew(new RunStartParams
            {
                Seed = System.Environment.TickCount,
                TutorialOn = _tutorialDefaultOn
            });
        }
    }

    private void OnDestroy()
    {
        _ctx?.Shutdown();
    }
}
```

### 4.2 `GameContext` (khuyến nghị dạng “pure C#”, bootstrapper giữ lifetime)
```csharp
public sealed class GameContext
{
    public IDataRegistry Data { get; }
    public EventBus Events { get; } = new EventBus();

    public RunController Run { get; private set; }
    public SimLoop Sim { get; private set; }
    public NotificationService Notifications { get; private set; }
    public SaveService Saves { get; private set; }

    public GameContext(IDataRegistry data) { Data = data; }

    public void Boot()
    {
        Notifications = new NotificationService(Events);
        Saves = new SaveService();

        Run = new RunController(Data, Events, Notifications);
        Sim = new SimLoop(Run, Data, Events, Notifications);
    }

    public void Shutdown()
    {
        // unsubscribe, dispose if needed
    }
}
```

---

## 5) “Editor Gate” chuẩn (để khỏi debug sai data)

### 5.1 Khi nào block play?
- `Validate(db)` có `Error` => block.
- Warnings cho phép chạy, nhưng in log.

### 5.2 Lưu report
- Lưu report json/txt ra `Library/` hoặc `Temp/` khi validate để dễ gửi bug.

---

## 6) Runtime Lookup Patterns (chuẩn hóa usage)
### 6.1 Strict usage (khi chắc chắn)
- Start setup spawn: `var hqDef = Data.GetBuilding("building.hq.l1");`
- Tower build: `var towerDef = Data.GetTower(selectedId);`

### 6.2 Soft usage (khi data-driven)
- Unlock list:
  - iterate defs -> check Unlock <= current day
  - nếu thiếu def => skip + warning

### 6.3 Wave fetch
- `TryGetWave(year, season, dayIndex, out def)`:
  - nếu fail => treat as content error (should not happen if validator coverage pass)

---

## 7) Performance Notes
- Dictionary lookups O(1), không allocation.
- `WaveKey` là struct nhỏ, hash stable.
- Build cache 1 lần ở boot.

---

## 8) Edge Cases & Future-proof (không làm lại về sau)
- Save migration: ID không đổi => registry vẫn hoạt động.
- DLC/content pack: có thể load nhiều database và merge (v1.1+).  
  => thiết kế `DataRegistry.Load(db)` tách bạch để sau này `LoadMany(dbs)`.

---

## 9) Done Checklist (Part 1)
- [ ] GameDatabase set trong bootstrapper
- [ ] Validate Data block play khi Error
- [ ] DataRegistry build dictionary caches
- [ ] `GetX/TryGetX` API chuẩn
- [ ] WaveKey mapping hoạt động
- [ ] Scene chạy vào gameplay không scan arrays

---

## 10) Next Part (Part 2 đề xuất)
**RunClock + Speed** đọc 100% từ `RunCalendarDef`, emit events sạch cho UI/combat/waves.

