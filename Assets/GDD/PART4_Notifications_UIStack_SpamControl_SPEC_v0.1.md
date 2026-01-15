# PART 4 — NOTIFICATION SERVICE + UI STACK + SPAM CONTROL (SPEC) — v0.1

> Mục tiêu: hệ thống **thông báo** là “xương sống UX” để ship public: player luôn biết **thiếu gì / kẹt gì / cần làm gì**.  
> Yêu cầu: hiển thị **giữa, mép trên cùng màn hình**, dưới top bar, **tối đa 3**, **mới nhất lên trước**, vượt quá thì **đẩy cái cũ ra**.  
> Đồng thời phải có **spam control** (cooldown + coalesce) để không “flood” khi game chạy nhanh (2x/3x).

---

## 1) Ranh giới & Responsibilities

### 1.1 NotificationService chịu trách nhiệm
- API duy nhất để push thông báo từ gameplay
- Quản lý queue/stack hiển thị (max 3)
- Thời gian sống (TTL), fade out (UI xử lý)
- Spam control:
  - Cooldown theo key
  - Coalesce (gộp) khi lặp lại cùng loại
  - Escalation (tăng severity nếu lặp nhiều)

### 1.2 Notification UI (UI Toolkit)
- Render danh sách visible notifications
- Animation/fade (optional)
- Không chứa logic cooldown

---

## 2) Thông số UX (LOCKED)
- Anchor: center-top, dưới top bar, margin top khoảng 8–16px
- Max visible: `3`
- Newest on top
- Overflow: remove oldest visible
- Default TTL: `4.0s` (Info), `6.0s` (Warning), `8.0s` (Error)
- Manual dismiss (optional v0.1): click to dismiss

---

## 3) Notification Types

### 3.1 Severity
```csharp
public enum NotifSeverity { Info, Warning, Error }
```

### 3.2 Category (optional, giúp filter)
```csharp
public enum NotifCategory
{
    General,
    Economy,
    Build,
    Combat,
    Ammo,
    Workforce,
    Storage,
    Pathing
}
```

### 3.3 Notification payload
```csharp
public readonly struct Notification
{
    public readonly string Key;            // stable id for spam control
    public readonly NotifSeverity Severity;
    public readonly NotifCategory Category;

    public readonly string Title;
    public readonly string Body;

    public readonly float CreatedTime;     // unscaled time
    public readonly float TTL;             // seconds

    public readonly int Count;             // coalesce count (xN)
    public readonly UnityEngine.Object Context; // optional ping source (Editor)

    public Notification(
        string key,
        NotifSeverity severity,
        NotifCategory category,
        string title,
        string body,
        float createdTime,
        float ttl,
        int count,
        UnityEngine.Object context)
    {
        Key = key;
        Severity = severity;
        Category = category;
        Title = title;
        Body = body;
        CreatedTime = createdTime;
        TTL = ttl;
        Count = count;
        Context = context;
    }

    public Notification WithCount(int newCount, float newCreatedTime)
        => new Notification(Key, Severity, Category, Title, Body, newCreatedTime, TTL, newCount, Context);

    public bool IsExpired(float now) => (now - CreatedTime) >= TTL;
}
```

---

## 4) NotificationService — API Design

### 4.1 Public API
```csharp
public interface INotificationService
{
    IReadOnlyList<Notification> Visible { get; }

    void Push(
        string key,
        string title,
        string body,
        NotifSeverity severity = NotifSeverity.Info,
        NotifCategory category = NotifCategory.General,
        float? ttlOverride = null,
        float? cooldownOverride = null,
        bool coalesce = true,
        UnityEngine.Object context = null);

    void Dismiss(string key);   // dismiss by key (if present)
    void ClearAll();
    void Tick(float unscaledDt); // expire updates
}
```

### 4.2 Default policy table
- TTL by severity:
  - Info: 4s
  - Warning: 6s
  - Error: 8s
- Cooldown default:
  - Info: 2s
  - Warning: 3s
  - Error: 0s (error always show)
- Coalesce:
  - enabled by default for Info/Warning
  - disabled by default for Error (but can enable)

---

## 5) Spam control strategy (cực quan trọng)

### 5.1 Cooldown per key
- Nếu `now - lastShown[key] < cooldown` => drop hoặc coalesce (tùy mode)
- Với coalesce: không tạo item mới, chỉ tăng Count của item gần nhất cùng key.

### 5.2 Coalesce rules
- Nếu visible stack đang có notification cùng `Key`:
  - Update Count + refresh CreatedTime (để nó sống thêm)
  - Option: update body (ví dụ “thiếu gỗ (x3)”)
- Nếu không có trong visible:
  - Nếu cooldown chưa hết: ignore (hoặc store pending count)
  - Nếu cooldown hết: push mới

### 5.3 Escalation
- Nếu một key bị coalesce quá nhiều trong khoảng ngắn:
  - nâng severity từ Info -> Warning (optional v0.1)
- Điều này giúp “thiếu tài nguyên” lặp nhiều lần trở nên nổi bật.

---

## 6) NotificationService — Implementation spec

### 6.1 Internal fields
```csharp
public sealed class NotificationService : INotificationService
{
    private readonly EventBus _events;

    private readonly List<Notification> _visible = new(3);
    public IReadOnlyList<Notification> Visible => _visible;

    private readonly Dictionary<string, float> _lastShownAt = new(128);  // key -> time
    private readonly Dictionary<string, int> _burstCount = new(128);     // key -> count in window
    private readonly Dictionary<string, float> _burstStart = new(128);   // key -> window start

    public int MaxVisible = 3;

    // defaults
    public float InfoTTL = 4f, WarningTTL = 6f, ErrorTTL = 8f;
    public float InfoCooldown = 2f, WarningCooldown = 3f, ErrorCooldown = 0f;

    // burst window
    public float BurstWindow = 6f;      // seconds
    public int EscalateAfter = 4;       // count in window to escalate
}
```

### 6.2 Core methods (pseudo-code)
```csharp
public void Push(string key, string title, string body,
                 NotifSeverity severity = NotifSeverity.Info,
                 NotifCategory category = NotifCategory.General,
                 float? ttlOverride = null,
                 float? cooldownOverride = null,
                 bool coalesce = true,
                 UnityEngine.Object context = null)
{
    var now = UnityEngine.Time.unscaledTime;

    // resolve policy
    var ttl = ttlOverride ?? ResolveTTL(severity);
    var cd  = cooldownOverride ?? ResolveCooldown(severity);

    // escalation tracking (optional)
    severity = MaybeEscalate(key, severity, now);

    // 1) coalesce if already visible
    if (coalesce)
    {
        var idx = FindVisibleIndexByKey(key);
        if (idx >= 0)
        {
            var current = _visible[idx];
            _visible[idx] = current.WithCount(current.Count + 1, now);
            _events.Publish(new NotificationChangedEvent()); // UI refresh trigger
            return;
        }
    }

    // 2) cooldown gate
    if (cd > 0f && _lastShownAt.TryGetValue(key, out var last) && (now - last) < cd)
    {
        // too soon: ignore or count burst only
        return;
    }

    _lastShownAt[key] = now;

    // 3) push new notification (newest first)
    var n = new Notification(key, severity, category, title, body, now, ttl, 1, context);
    InsertNewest(n);
    _events.Publish(new NotificationChangedEvent());
}
```

### 6.3 Insert policy (newest first, max 3)
```csharp
private void InsertNewest(Notification n)
{
    _visible.Insert(0, n);
    if (_visible.Count > MaxVisible)
        _visible.RemoveAt(_visible.Count - 1);
}
```

### 6.4 Tick expire
```csharp
public void Tick(float unscaledDt)
{
    var now = UnityEngine.Time.unscaledTime;
    var changed = false;

    for (int i = _visible.Count - 1; i >= 0; i--)
    {
        if (_visible[i].IsExpired(now))
        {
            _visible.RemoveAt(i);
            changed = true;
        }
    }

    if (changed)
        _events.Publish(new NotificationChangedEvent());
}
```

### 6.5 Dismiss
```csharp
public void Dismiss(string key)
{
    var idx = FindVisibleIndexByKey(key);
    if (idx >= 0)
    {
        _visible.RemoveAt(idx);
        _events.Publish(new NotificationChangedEvent());
    }
}
```

### 6.6 Events for UI refresh
```csharp
public readonly struct NotificationChangedEvent { }
```

---

## 7) UI Toolkit — Notification Stack View

### 7.1 UXML structure (concept)
- Root container: `notifRoot` (absolute, top-center)
- Child list: `notifList`
- Each item:
  - Title label
  - Body label
  - Count badge (xN) if Count>1
  - Severity class: `.sev-info`, `.sev-warning`, `.sev-error`

### 7.2 Controller
```csharp
public sealed class NotificationPanelController
{
    private VisualElement _list;
    private INotificationService _notifs;
    private EventBus _events;

    public void Bind(VisualElement root, INotificationService notifs, EventBus events)
    {
        _list = root.Q<VisualElement>("notifList");
        _notifs = notifs;
        _events = events;

        _events.Subscribe<NotificationChangedEvent>(_ => Render());
        Render();
    }

    private void Render()
    {
        _list.Clear();

        var visible = _notifs.Visible;
        for (int i = 0; i < visible.Count; i++)
        {
            var n = visible[i];

            var item = new VisualElement();
            item.AddToClassList("notif-item");
            item.AddToClassList(n.Severity == NotifSeverity.Info ? "sev-info"
                : n.Severity == NotifSeverity.Warning ? "sev-warning" : "sev-error");

            var title = new Label(n.Title);
            title.AddToClassList("notif-title");

            var body = new Label(n.Body);
            body.AddToClassList("notif-body");

            item.Add(title);
            item.Add(body);

            if (n.Count > 1)
            {
                var badge = new Label($"x{n.Count}");
                badge.AddToClassList("notif-badge");
                item.Add(badge);
            }

            item.RegisterCallback<UnityEngine.UIElements.ClickEvent>(_ => _notifs.Dismiss(n.Key));
            _list.Add(item);
        }
    }
}
```

### 7.3 Positioning (USS)
- Root:
  - `position: absolute;`
  - `top: <topbarHeight + margin>;`
  - `left: 50%; transform: translateX(-50%);`
- Max width: 520–680px
- Item spacing: 8–10px
- Soft shadow, rounded corners

---

## 8) Notification Catalog (chuẩn hoá keys)

> Dùng key ổn định để spam control hoạt động.  
> Mỗi message có Title + Body gợi ý hành động.

### 8.1 Workforce
- `npc.unassigned` — *NPC mới chưa được gán việc*  
- `workplace.full` — *Công trình đã đủ người*  
- `workplace.none` — *Không có công trình nhận việc phù hợp*

### 8.2 Economy/Storage
- `storage.local_full.<buildingId>` — *Kho cục bộ đầy*  
- `storage.warehouse_full` — *Warehouse gần đầy*  
- `resource.insufficient.<type>` — *Không đủ tài nguyên*  
- `resource.no_source.<type>` — *Không có nguồn tài nguyên*

### 8.3 Build/Repair
- `build.blocked.missing_resource` — *Thiếu tài nguyên để xây/nâng cấp*  
- `build.blocked.no_road` — *Công trình cần nối road*  
- `repair.blocked.missing_resource` — *Thiếu tài nguyên sửa chữa*

### 8.4 Ammo/Combat
- `ammo.tower_low.<towerId>` — *Tháp sắp hết đạn (<=25%)*  
- `ammo.tower_empty.<towerId>` — *Tháp hết đạn*  
- `ammo.armory_empty` — *Kho vũ khí hết đạn*  
- `ammo.forge_no_input` — *Lò rèn thiếu nguyên liệu*  
- `combat.wave_started` — *Bắt đầu phòng thủ*  
- `combat.boss_incoming` — *Boss xuất hiện!*

### 8.5 Pathing
- `path.blocked` — *NPC không tìm thấy đường*  
- `path.reservation_fail` — *Ô bị chiếm / không thể đặt*

> Với keys có `<buildingId>`/`<towerId>`, cooldown nên cao hơn (3–6s) để tránh spam.

---

## 9) Policy table (gợi ý cooldown/ttl)
| Severity | TTL | Cooldown |
|---|---:|---:|
| Info | 4s | 2s |
| Warning | 6s | 3s |
| Error | 8s | 0s |

Override examples:
- `ammo.tower_empty.*`: severity=Error, ttl=10s
- `path.blocked`: cooldown=5s

---

## 10) Integration points (ai gọi notification?)

### 10.1 Systems publish notifications (examples)
- HarvestSystem:
  - local full => `storage.local_full.<id>`
- BuildSystem:
  - missing cost => `build.blocked.missing_resource`
- TowerSystem:
  - threshold reached => `ammo.tower_low.<id>`
  - ammo=0 => `ammo.tower_empty.<id>`
- Assignment UI:
  - npc spawn => `npc.unassigned` (coalesce)
- Pathfinding:
  - no path => `path.blocked` (cooldown)

### 10.2 Where Tick happens
- `NotificationService.Tick(unscaledDt)` chạy trong `SimLoop.Update()` bằng `unscaledDt` (không bị pause).

---

## 11) Tests (EditMode)
- `Push_NewestFirst_Max3()`
- `Cooldown_DropsSpam()`
- `Coalesce_IncrementsCount_RefreshesTTL()`
- `Tick_Expires_Removes()`
- `Dismiss_RemovesByKey()`

---

## 12) Done Checklist (Part 4)
- [ ] Max 3 visible, newest on top
- [ ] Overflow removes oldest
- [ ] Cooldown + coalesce hoạt động
- [ ] UI panel render đúng vị trí + click dismiss
- [ ] Systems có thể push bằng key chuẩn
- [ ] No spam khi 3x speed

---

## 13) Next Part (Part 5 đề xuất)
**Map/Grid Placement + Road Connectivity rules** (validate placement + driveway) — vì build UX phụ thuộc mạnh vào rule road/entry.

