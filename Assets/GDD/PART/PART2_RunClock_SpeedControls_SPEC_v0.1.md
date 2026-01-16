# PART 2 — RUNCLOCK + SPEED CONTROLS + CALENDAR EVENTS (SPEC) — v0.1

> Mục tiêu: tạo “xương sống thời gian” cho Seasonal Bastion: **Year/Season/Day**, pacing Dev/Defend, và **Speed Controls** (Pause/1x/2x/3x) với rule “vào Defend auto 1x”.  
> Phần này phải: deterministic, data-driven, testable, và là single-source-of-truth cho mọi hệ (UI, unlock, wave start).

---

## 1) Trách nhiệm & ranh giới

### 1.1 RunClock chịu trách nhiệm
- Đếm thời gian trong ngày theo `RunCalendarDef`
- Chuyển ngày / chuyển mùa / chuyển năm
- Phát event chuẩn hoá: `DayStarted`, `DayEnded`, `SeasonChanged`, `YearChanged`
- Tính các property tiện dụng: `IsDevSeason`, `IsDefendSeason`, `DayProgress01`

### 1.2 TimeScaleController chịu trách nhiệm
- Quản lý SpeedIndex (Pause/1x/2x/3x)
- Áp `Time.timeScale` hoặc “SimTimescale” (khuyến nghị)
- Rule: **khi vào Defend → auto set 1x** (nếu `Calendar.ForceSpeed1xInDefend`)

### 1.3 Không thuộc phạm vi
- Không spawn waves trực tiếp (WaveController nghe event)
- Không unlock buildings trực tiếp (UI/UnlockService nghe event)
- Không xử lý save/load (RunController xử lý)

---

## 2) Data inputs (100% từ RunCalendarDef)

Required fields:
- `TotalYears`
- `SpringDays, SummerDays, AutumnDays, WinterDays`
- `SecondsPerDayDev`, `SecondsPerDayDefend`
- `ForceSpeed1xInDefend`
- `AllowedSpeeds` (0,1,2,3)

---

## 3) Event Types (chuẩn hoá, dùng EventBus)

### 3.1 Calendar snapshot struct
```csharp
public readonly struct CalendarSnapshot
{
    public readonly int Year;
    public readonly Season Season;
    public readonly int DayIndex;         // 1..DaysInSeason
    public readonly float DayProgress01;  // 0..1

    public CalendarSnapshot(int year, Season season, int dayIndex, float dayProgress01)
    {
        Year = year;
        Season = season;
        DayIndex = dayIndex;
        DayProgress01 = dayProgress01;
    }

    public override string ToString() => $"Y{Year} {Season} D{DayIndex} ({DayProgress01:0.00})";
}
```

### 3.2 Events
```csharp
public readonly struct DayStartedEvent
{
    public readonly CalendarSnapshot Snapshot;
    public DayStartedEvent(CalendarSnapshot s) { Snapshot = s; }
}

public readonly struct DayEndedEvent
{
    public readonly CalendarSnapshot Snapshot;  // snapshot at end-of-day (progress=1)
    public DayEndedEvent(CalendarSnapshot s) { Snapshot = s; }
}

public readonly struct SeasonChangedEvent
{
    public readonly int Year;
    public readonly Season From;
    public readonly Season To;
    public SeasonChangedEvent(int year, Season from, Season to) { Year = year; From = from; To = to; }
}

public readonly struct YearChangedEvent
{
    public readonly int FromYear;
    public readonly int ToYear;
    public YearChangedEvent(int from, int to) { FromYear = from; ToYear = to; }
}

public readonly struct SpeedChangedEvent
{
    public readonly int SpeedIndex;   // 0..N
    public readonly int SpeedValue;   // 0,1,2,3
    public SpeedChangedEvent(int idx, int val) { SpeedIndex = idx; SpeedValue = val; }
}
```

---

## 4) RunClock — Class Definition

### 4.1 State model
RunClock không giữ “toàn bộ RunState”, chỉ giữ phần calendar + internal counters.

```csharp
public sealed class RunClock
{
    private readonly RunCalendarDef _cal;
    private readonly EventBus _events;

    private int _year;          // 1..TotalYears
    private Season _season;
    private int _dayIndex;      // 1..DaysInSeason
    private float _dayElapsed;  // seconds

    private float _dayLength;   // seconds

    public RunClock(RunCalendarDef cal, EventBus events)
    {
        _cal = cal;
        _events = events;
    }

    public int Year => _year;
    public Season Season => _season;
    public int DayIndex => _dayIndex;

    public float DayLength => _dayLength;
    public float DayElapsed => _dayElapsed;

    public bool IsDevSeason => _season == Season.Spring || _season == Season.Summer;
    public bool IsDefendSeason => _season == Season.Autumn || _season == Season.Winter;

    public float DayProgress01 => _dayLength <= 0f ? 0f : UnityEngine.Mathf.Clamp01(_dayElapsed / _dayLength);

    public CalendarSnapshot Snapshot => new CalendarSnapshot(_year, _season, _dayIndex, DayProgress01);

    // init / control
    public void InitNewRun(int startYear = 1, Season startSeason = Season.Spring, int startDayIndex = 1)
    {
        _year = startYear;
        _season = startSeason;
        _dayIndex = startDayIndex;
        _dayElapsed = 0f;
        _dayLength = ResolveDayLength(_season);

        _events.Publish(new DayStartedEvent(Snapshot));
    }

    public void Advance(float dt)
    {
        _dayElapsed += dt;
        if (_dayElapsed < _dayLength) return;

        // clamp and end day
        _dayElapsed = _dayLength;
        _events.Publish(new DayEndedEvent(Snapshot));

        AdvanceCalendar();
    }

    private void AdvanceCalendar()
    {
        _dayElapsed = 0f;

        var daysInSeason = _cal.DaysInSeason(_season);
        var nextDay = _dayIndex + 1;

        if (nextDay <= daysInSeason)
        {
            _dayIndex = nextDay;
            _dayLength = ResolveDayLength(_season);
            _events.Publish(new DayStartedEvent(Snapshot));
            return;
        }

        // season change
        var fromSeason = _season;
        var toSeason = NextSeason(_season);

        // year change if Winter -> Spring
        if (fromSeason == Season.Winter && toSeason == Season.Spring)
        {
            var fromYear = _year;
            var toYear = _year + 1;
            _year = toYear;

            _events.Publish(new YearChangedEvent(fromYear, toYear));

            // if exceed total years -> run end evaluator should handle
        }

        _season = toSeason;
        _dayIndex = 1;
        _dayLength = ResolveDayLength(_season);

        _events.Publish(new SeasonChangedEvent(_year, fromSeason, toSeason));
        _events.Publish(new DayStartedEvent(Snapshot));
    }

    private float ResolveDayLength(Season s)
    {
        var isDev = s == Season.Spring || s == Season.Summer;
        return isDev ? _cal.SecondsPerDayDev : _cal.SecondsPerDayDefend;
    }

    private static Season NextSeason(Season s) => s switch
    {
        Season.Spring => Season.Summer,
        Season.Summer => Season.Autumn,
        Season.Autumn => Season.Winter,
        Season.Winter => Season.Spring,
        _ => Season.Spring
    };
}
```

---

## 5) TimeScaleController — Class Definition (2 modes)

Bạn có 2 lựa chọn:

### Option A (đơn giản): dùng `Time.timeScale`
- Pros: nhanh, ít code
- Cons: ảnh hưởng physics/animation/timing chung; cần cẩn thận UI

### Option B (khuyến nghị): “SimTimeScale” riêng
- Game loop dùng `scaledDt = dt * SimScale`
- UI/anim có thể dùng unscaled time
- Giữ deterministic hơn

#### 5.1 Interface
```csharp
public interface ITimeScale
{
    int SpeedIndex { get; }
    int SpeedValue { get; }   // 0,1,2,3
    float SimScale { get; }   // 0f,1f,2f,3f

    void SetSpeedIndex(int index);
    void SetSpeedValue(int value);
}
```

#### 5.2 Implementation (SimScale)
```csharp
public sealed class TimeScaleController : ITimeScale
{
    private readonly RunCalendarDef _cal;
    private readonly EventBus _events;

    private int _speedIndex;
    private int _speedValue;

    public int SpeedIndex => _speedIndex;
    public int SpeedValue => _speedValue;
    public float SimScale => _speedValue; // 0..3

    public TimeScaleController(RunCalendarDef cal, EventBus events)
    {
        _cal = cal;
        _events = events;

        // default 1x
        SetSpeedValue(1);
    }

    public void SetSpeedIndex(int index)
    {
        index = UnityEngine.Mathf.Clamp(index, 0, _cal.AllowedSpeeds.Length - 1);
        SetSpeedValue(_cal.AllowedSpeeds[index]);
        _speedIndex = index;
    }

    public void SetSpeedValue(int value)
    {
        if (!IsAllowed(value))
            value = 1;

        _speedValue = value;
        _events.Publish(new SpeedChangedEvent(_speedIndex, _speedValue));
    }

    public void OnSeasonChanged(SeasonChangedEvent e)
    {
        if (!_cal.ForceSpeed1xInDefend) return;

        var isDefend = e.To == Season.Autumn || e.To == Season.Winter;
        if (isDefend) SetSpeedValue(1);
    }

    private bool IsAllowed(int v)
    {
        var arr = _cal.AllowedSpeeds;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] == v) return true;
        return false;
    }
}
```

---

## 6) Integration vào SimLoop (scaled dt)

### 6.1 SimLoop uses SimScale
```csharp
public sealed class SimLoop
{
    private readonly RunClock _clock;
    private readonly TimeScaleController _timeScale;

    private float _clockAcc, _simAcc, _combatAcc, _moveAcc;

    public void Update(float unscaledDt)
    {
        var dt = unscaledDt * _timeScale.SimScale;
        if (dt <= 0f) return;

        // accumulate lanes (hz)
        _clockAcc += dt;
        _simAcc += dt;
        _combatAcc += dt;
        _moveAcc += dt;

        // ... tick loops
        // clock tick might be per-frame or 1Hz; your choice:
        _clock.Advance(dt);

        // For lane-based tick:
        // while (_simAcc >= (1f/SimHz)) { TickSim(); _simAcc -= (1f/SimHz); }
    }
}
```

> NOTE: Nếu bạn muốn clock chính xác theo seconds/day (không phụ thuộc hz), cứ gọi `_clock.Advance(dt)` mỗi update.

---

## 7) End-of-Run handling (calendar overflow)

RunClock chỉ phát event; quyết định kết thúc run nên để `RunEndEvaluator`.

### 7.1 Rule
- Nếu `Year > Calendar.TotalYears` sau khi Winter->Spring => EndRun(Win) (nếu boss dead) hoặc EndRun(Win by calendar) tuỳ design
- Với Seasonal Bastion: **Win** khi Boss Winter Y2 D4 chết, không phụ thuộc qua Spring.

### 7.2 `RunEndEvaluator`
```csharp
public sealed class RunEndEvaluator
{
    private readonly RunCalendarDef _cal;

    public bool IsRunOverByCalendar(int year) => year > _cal.TotalYears;
}
```

---

## 8) QA / Edge Cases Checklist

### 8.1 Calendar correctness
- DayProgress01 luôn 0..1
- DayStarted phát đúng 1 lần mỗi ngày
- DayEnded phát đúng 1 lần mỗi ngày
- SeasonChanged phát trước DayStarted của ngày mới

### 8.2 Speed correctness
- Pause => SimScale=0 => clock không advance
- Defend start => speed auto 1x (nếu ForceSpeed1xInDefend)
- AllowedSpeeds không chứa giá trị set => fallback 1x

### 8.3 Re-entrancy
- Listener của DayEnded không được gọi AdvanceCalendar lại (no recursion)
- Event handlers không mutate RunClock trực tiếp (rule code review)

---

## 9) Tests (EditMode, không cần PlayMode)
- `RunClock_Advances_Day_Correctly()`
- `RunClock_Season_Changes_After_Last_Day()`
- `RunClock_Year_Changes_After_Winter()`
- `TimeScale_Force1x_InDefend()`
- `AllowedSpeeds_Fallback_To1x()`

---

## 10) Done Checklist (Part 2)
- [ ] RunClock init new run emits DayStarted
- [ ] Day length resolved from calendar
- [ ] Day/Season/Year events order đúng
- [ ] Speed controls publish SpeedChangedEvent
- [ ] Defend auto set 1x works
- [ ] SimLoop uses SimScale (pause stops sim)

---

## 11) Next Part (Part 3 đề xuất)
**UnlockService + HUD binding**: build menu hiển thị đúng theo unlock, và HUD cập nhật event-driven (không poll).

