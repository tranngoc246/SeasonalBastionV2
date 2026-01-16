# PART 3 — UNLOCK SERVICE + HUD/UI BINDING (EVENT-DRIVEN) — SPEC v0.1

> Mục tiêu: mọi thứ liên quan **hiển thị & mở khóa** (build menu, tower menu, hint) phải:
- Data-driven (UnlockDef + RunClock)
- Event-driven (không poll mỗi frame)
- Dễ mở rộng (thêm content chỉ là thêm defs)

Phần này KHÔNG làm JobSystem. Chỉ tập trung: **unlock logic + UI binding**.

---

## 1) Ranh giới & Responsibilities

### 1.1 UnlockService chịu trách nhiệm
- Quyết định **def nào đã unlocked** tại thời điểm hiện tại (Year/Season/Day)
- Cung cấp danh sách đã unlocked theo category (Buildings/Towers)
- Emit events khi “unlock set changed” (ví dụ sang ngày mới, sang mùa)
- Không tạo UI, không spawn building, không tự build.

### 1.2 HUD/UI chịu trách nhiệm
- HUD: hiển thị Year/Season/Day + speed theo events
- BuildMenu: render list unlocked buildings/towers, grouped categories
- Hint: hiển thị gợi ý khi có unlock quan trọng (optional, trong spec có hook)

---

## 2) Data Input
- `IDataRegistry` cung cấp:
  - `BuildingDef`[] (qua registry enumerations hoặc db reference)
  - `TowerDef`[]
- `RunClock` cung cấp calendar snapshot
- Unlock rules dùng `UnlockDef` in defs

> NOTE: `IDataRegistry` trong Part 1 không có API enumerate all defs.  
> Để unlock service làm việc tối ưu, bạn nên bổ sung **read-only enumerators**.

---

## 3) Registry enumerable additions (read-only)

### 3.1 Extend `IDataRegistry` (optional nhưng recommended)
```csharp
public interface IDataRegistry
{
    // ... existing Get/TryGet ...

    IReadOnlyCollection<BuildingDef> AllBuildings { get; }
    IReadOnlyCollection<TowerDef> AllTowers { get; }
}
```

### 3.2 `DataRegistry` implement
- Expose `AllBuildings` = `_buildings.Values`
- Expose `AllTowers` = `_towers.Values`
- Return as `IReadOnlyCollection<T>` (avoid modification)

---

## 4) Unlock rules

### 4.1 Calendar ordering
Define a comparable “time key”:
- Compare by: Year → SeasonIndex → DayIndex
- SeasonIndex order: Spring=0, Summer=1, Autumn=2, Winter=3

```csharp
public static class CalendarOrder
{
    public static int SeasonIndex(Season s) => s switch
    {
        Season.Spring => 0,
        Season.Summer => 1,
        Season.Autumn => 2,
        Season.Winter => 3,
        _ => 0
    };

    // returns true if a unlock time <= current time
    public static bool IsUnlocked(UnlockDef unlock, int curYear, Season curSeason, int curDayIndex)
    {
        if (unlock.Year < curYear) return true;
        if (unlock.Year > curYear) return false;

        var uS = SeasonIndex(unlock.Season);
        var cS = SeasonIndex(curSeason);

        if (uS < cS) return true;
        if (uS > cS) return false;

        return unlock.DayIndex <= curDayIndex;
    }
}
```

### 4.2 Unlock effective time
- Unlock applied at **DayStarted** event (start of day).
- Nếu muốn unlock ngay khi chuyển mùa: unlock check tại DayStarted của day1 mùa mới (consistent).

---

## 5) UnlockService — API & internal cache

### 5.1 Events
```csharp
public readonly struct UnlockSetChangedEvent
{
    public readonly int Year;
    public readonly Season Season;
    public readonly int DayIndex;

    // payload: newly unlocked IDs (diff-based)
    public readonly string[] NewBuildingIds;
    public readonly string[] NewTowerIds;

    public UnlockSetChangedEvent(int year, Season season, int dayIndex, string[] newB, string[] newT)
    {
        Year = year; Season = season; DayIndex = dayIndex;
        NewBuildingIds = newB; NewTowerIds = newT;
    }
}
```

### 5.2 Class definition
```csharp
public sealed class UnlockService
{
    private readonly IDataRegistry _data;
    private readonly EventBus _events;

    // unlocked sets
    private readonly HashSet<string> _unlockedBuildings = new(256);
    private readonly HashSet<string> _unlockedTowers = new(128);

    // cached groups (optional)
    private readonly Dictionary<BuildingCategory, List<BuildingDef>> _buildingByCat = new();

    public UnlockService(IDataRegistry data, EventBus events)
    {
        _data = data;
        _events = events;

        _events.Subscribe<DayStartedEvent>(OnDayStarted);
    }

    public bool IsBuildingUnlocked(string buildingId) => _unlockedBuildings.Contains(buildingId);
    public bool IsTowerUnlocked(string towerId) => _unlockedTowers.Contains(towerId);

    public IReadOnlyCollection<string> UnlockedBuildingIds => _unlockedBuildings;
    public IReadOnlyCollection<string> UnlockedTowerIds => _unlockedTowers;

    public IReadOnlyList<BuildingDef> GetUnlockedBuildings(BuildingCategory cat)
    {
        if (!_buildingByCat.TryGetValue(cat, out var list)) return System.Array.Empty<BuildingDef>();
        return list;
    }

    public void RebuildAll(int year, Season season, int dayIndex)
    {
        _unlockedBuildings.Clear();
        _unlockedTowers.Clear();
        _buildingByCat.Clear();

        foreach (var b in _data.AllBuildings)
        {
            if (b == null) continue;
            if (CalendarOrder.IsUnlocked(b.Unlock, year, season, dayIndex))
                AddBuilding(b);
        }

        foreach (var t in _data.AllTowers)
        {
            if (t == null) continue;
            if (CalendarOrder.IsUnlocked(t.Unlock, year, season, dayIndex))
                _unlockedTowers.Add(t.Id);
        }
    }

    private void AddBuilding(BuildingDef b)
    {
        _unlockedBuildings.Add(b.Id);

        if (!_buildingByCat.TryGetValue(b.Category, out var list))
            _buildingByCat[b.Category] = list = new List<BuildingDef>(16);

        list.Add(b);
    }

    private void OnDayStarted(DayStartedEvent e)
    {
        var s = e.Snapshot;

        // diff-based update: find newly unlocked
        var newB = new List<string>(16);
        var newT = new List<string>(16);

        foreach (var b in _data.AllBuildings)
        {
            if (b == null) continue;
            if (_unlockedBuildings.Contains(b.Id)) continue;

            if (CalendarOrder.IsUnlocked(b.Unlock, s.Year, s.Season, s.DayIndex))
            {
                AddBuilding(b);
                newB.Add(b.Id);
            }
        }

        foreach (var t in _data.AllTowers)
        {
            if (t == null) continue;
            if (_unlockedTowers.Contains(t.Id)) continue;

            if (CalendarOrder.IsUnlocked(t.Unlock, s.Year, s.Season, s.DayIndex))
            {
                _unlockedTowers.Add(t.Id);
                newT.Add(t.Id);
            }
        }

        if (newB.Count > 0 || newT.Count > 0)
        {
            _events.Publish(new UnlockSetChangedEvent(s.Year, s.Season, s.DayIndex, newB.ToArray(), newT.ToArray()));
        }
    }
}
```

### 5.3 Performance note
- DayStarted gọi 1 lần/ngày => scan toàn defs không tốn đáng kể.
- Nếu về sau content rất lớn, có thể pre-index unlock timeline (v1.2+), nhưng v0.1 không cần.

---

## 6) Boot integration (where to create UnlockService)
- UnlockService được tạo trong `GameContext.Boot()`, subscribe events.
- Sau khi `RunClock.InitNewRun()` phát DayStarted, UnlockService sẽ tự build diff.
- Tuy nhiên để có list ngay ở frame 1, bạn có thể gọi `RebuildAll()` ngay sau init:

```csharp
// in RunController.StartNew()
_clock.InitNewRun();
_unlocks.RebuildAll(_clock.Year, _clock.Season, _clock.DayIndex);
```

> Nếu `RebuildAll()` gọi trước DayStarted, UI sẽ có data sớm.  
> Nếu gọi sau, UI vẫn nhận diff và refresh được.

---

## 7) HUD binding (UI Toolkit) — event-driven

### 7.1 HUDController (simplified spec)
```csharp
public sealed class HUDController
{
    private Label _lblCalendar;
    private Button _btnPause, _btn1x, _btn2x, _btn3x;

    private EventBus _events;
    private ITimeScale _time;

    public void Bind(VisualElement root, EventBus events, ITimeScale time)
    {
        _events = events;
        _time = time;

        _lblCalendar = root.Q<Label>("lblCalendar");
        _btnPause = root.Q<Button>("btnPause");
        _btn1x = root.Q<Button>("btn1x");
        _btn2x = root.Q<Button>("btn2x");
        _btn3x = root.Q<Button>("btn3x");

        _btnPause.clicked += () => _time.SetSpeedValue(0);
        _btn1x.clicked += () => _time.SetSpeedValue(1);
        _btn2x.clicked += () => _time.SetSpeedValue(2);
        _btn3x.clicked += () => _time.SetSpeedValue(3);

        _events.Subscribe<DayStartedEvent>(OnDayStarted);
        _events.Subscribe<SeasonChangedEvent>(OnSeasonChanged);
        _events.Subscribe<SpeedChangedEvent>(OnSpeedChanged);
    }

    private void OnDayStarted(DayStartedEvent e) => RenderCalendar(e.Snapshot);
    private void OnSeasonChanged(SeasonChangedEvent e)
    {
        // render using current snapshot from RunClock if you keep a reference
    }
    private void OnSpeedChanged(SpeedChangedEvent e)
    {
        // update selected state of buttons (css class)
    }

    private void RenderCalendar(CalendarSnapshot s)
    {
        _lblCalendar.text = $"Y{s.Year} • {s.Season} • Day {s.DayIndex}";
    }
}
```

---

## 8) BuildMenu binding — unlocked list refresh

### 8.1 BuildMenuController responsibilities
- Render categories and items
- On UnlockSetChangedEvent: refresh only affected sections

### 8.2 API & events
```csharp
public sealed class BuildMenuController
{
    private UnlockService _unlocks;
    private EventBus _events;

    public void Bind(UnlockService unlocks, EventBus events)
    {
        _unlocks = unlocks;
        _events = events;

        _events.Subscribe<UnlockSetChangedEvent>(OnUnlockChanged);

        RenderAll(); // initial
    }

    private void RenderAll()
    {
        // iterate categories -> unlocks.GetUnlockedBuildings(cat)
        // towers -> unlocks.UnlockedTowerIds
    }

    private void OnUnlockChanged(UnlockSetChangedEvent e)
    {
        // Option 1: RenderAll() (simple & safe)
        // Option 2: partial refresh by categories from new ids (micro-opt)
        RenderAll();
    }
}
```

> v0.1: RenderAll() đủ nhanh vì UI list nhỏ.

---

## 9) Hint hook (optional, nhưng public cực hữu ích)
Unlock event là chỗ đẹp để tạo “gợi ý”:
- “Đã mở khóa Warehouse — hãy xây để tránh kho cục bộ đầy”
- “Đã mở khóa Forge/Armory — hãy thiết lập chuỗi đạn”

### 9.1 Hint rule (simple table)
```csharp
public sealed class HintRule
{
    public string TriggerUnlockId;  // building/tower id
    public string Title;
    public string Body;
}
```

### 9.2 HintService
- Subscribe UnlockSetChangedEvent
- For each new unlocked ID -> push notification/hint once

---

## 10) QA Checklist (Part 3)
- [ ] Build menu chỉ hiển thị items unlocked
- [ ] Chuyển sang ngày mới => unlock mới xuất hiện đúng
- [ ] Unlock không “mất” khi reload UI
- [ ] Sorting ổn định (SortOrder hoặc DisplayName)
- [ ] Không có poll mỗi frame

---

## 11) Tests (EditMode)
- `CalendarOrder_IsUnlocked_Works()`
- `UnlockService_Diff_NewUnlocks_OnDayStarted()`
- `UnlockService_GroupByCategory_Stable()`

---

## 12) Next Part (Part 4 đề xuất)
**NotificationService + Spam Control + UI Stack** (max 3, newest on top, cooldown keys) để public UX “đã”.

