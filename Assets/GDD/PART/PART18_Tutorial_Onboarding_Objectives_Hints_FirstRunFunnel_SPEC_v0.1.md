# PART 18 — TUTORIAL & ONBOARDING DESIGN (OBJECTIVES + HINTS + FIRST-RUN FUNNEL) — SPEC v0.1

> Mục tiêu: người chơi mới hiểu loop trong 5–10 phút và “đi đúng hướng”:
- Teach: placement + road entry + assign NPC + harvest/haul + build pipeline + defend + ammo pipeline + rewards
- Objective system data-driven (không hardcode)
- Hint system dùng notifications (Part 4) + focus camera/select building (Part 13)
- Không làm tutorial quá dài; ưu tiên “guided sandbox”.

---

## 1) Onboarding philosophy (premium roguelite)
- Tutorial chỉ “run đầu tiên” (first-run) hoặc toggle trong Settings
- Không ép đọc, hướng dẫn bằng **mục tiêu nhỏ** + **phản hồi ngay**
- Mỗi bước dạy 1 khái niệm, không dạy 2 thứ cùng lúc
- Nếu người chơi làm sai, hint nêu *vì sao* và *cách sửa* (đúng rules)

---

## 2) Objective system (data-driven)

### 2.1 ObjectiveDef
```csharp
public enum ObjectiveType
{
    PlaceBuilding,
    PlaceRoad,
    AssignNPC,
    ProduceResource,      // reach X stored
    DeliverResource,      // haul to HQ/Warehouse
    StartBuildOrder,      // create build site
    CompleteBuildSite,
    SurviveWave,
    CraftAmmo,
    StoreAmmoInArmory,
    ResupplyTower,
    PickReward
}

public sealed class ObjectiveDef : ScriptableObject
{
    public string Id;
    public int StepIndex;

    public ObjectiveType Type;
    public string TargetId;       // building def id, resource type, etc.
    public int TargetAmount;
    public int TargetCount;

    public string TitleKey;       // localization keys
    public string DescKey;

    public string[] PrereqObjectiveIds;
    public bool IsMandatory;      // some steps optional
}
```

### 2.2 ObjectiveProgress
```csharp
public struct ObjectiveProgress
{
    public string ObjectiveId;
    public int Current;
    public bool Completed;
    public float CompletedAt;
}
```

### 2.3 ObjectiveService
Responsibilities:
- activate next objective when prereqs complete
- listen to game events to update progress
- raise UI updates + hints

API:
```csharp
public sealed class ObjectiveService
{
    public string CurrentObjectiveId { get; }
    public void Tick() { /* optional */ }
    public void OnEvent(GameEvent e) { /* update */ }
}
```

---

## 3) Event hooks (what objective listens to)
Emit lightweight events from core systems:
- `BuildingPlacedEvent(defId, buildingId)`
- `RoadPlacedEvent(countDelta)`
- `NPCAssignedEvent(npcId, workplaceBuildingId)`
- `ResourceAddedEvent(resourceType, amount, destBuildingId)`
- `BuildSiteCreatedEvent(siteId, defId)`
- `BuildSiteCompletedEvent(siteId, defId)`
- `WaveEndedEvent(waveId)`
- `AmmoCraftedEvent(amount)`
- `AmmoStoredEvent(amount, armoryId)`
- `TowerResuppliedEvent(towerId, amount)`
- `RewardPickedEvent(rewardId)`

> Mỗi event nên nhỏ, tránh payload lớn.

---

## 4) Tutorial UI presentation

### 4.1 HUD objective widget (always visible)
- Top-left or left panel:
  - Title
  - 1–2 lines description
  - Progress bar / counter
  - “Show me” button (focus camera/select relevant building)

### 4.2 Hint delivery
- Use NotificationService (Part 4) with special tutorial keys:
  - `tut.step.<id>.hint1`
  - `tut.step.<id>.blocked`
- Hint appears at top, clickable to focus.

### 4.3 Highlight system (minimal)
- Highlight a UI button or building:
  - add `UIHighlightService` that toggles USS class on VisualElement
  - world highlight via outline shader or gizmo ring (dev simple)

---

## 5) First-run funnel (recommended steps)

> Dựa trên starting kit bạn chốt:
- HQ (1 NPC)
- 2 Houses (capacity 4)
- Farmhouse + zone (1 NPC)
- LumberCamp (1 NPC)
- 1 ArrowTower (full ammo)
Auto-fill only at run start.

### Step 0 — Welcome (non-blocking)
- Title: “Chào mừng đến Seasonal Bastion”
- Explain 1 sentence: “Xây – vận hành NPC – thủ thành theo mùa”
- Button: “Bắt đầu”

### Step 1 — Place Road (teach entry rule)
Objective: Place 5 road tiles near HQ.
- If player tries place building without road: notify `build.blocked.no_road` + tutorial hint.

### Step 2 — Place Warehouse (teach build pipeline & hauling)
Objective: Start build order Warehouse.
- Guide: open Build menu → Core → Warehouse → place near road.
- Hint if resources insufficient: explain harvest/haul.

### Step 3 — Assign transporter to Warehouse
Objective: Assign 1 NPC to Warehouse.
- A new NPC spawns (capacity available) shortly after (or immediately for tutorial)
- Show hint: “NPC mới sinh ra chưa có việc – hãy gán vào Warehouse để vận chuyển.”

### Step 4 — Observe hauling loop
Objective: Deliver 20 Wood to Warehouse (or HQ).
- Progress increases on ResourceAddedEvent at warehouse/hq.
- Hint if stuck: producer local full, warehouse needs transporter.

### Step 5 — Enter Defend preview (teach defend & speed)
Objective: Survive 1 wave.
- At Autumn start or tutorial triggers a mini-wave.
- Force speed 1x, show speed buttons.

### Step 6 — Introduce ammo pipeline (unlock Forge/Armory)
Objective: Start build order Forge.
- UnlockService grants Forge/Armory for tutorial or via objective completion.

### Step 7 — Assign Smith to Forge
Objective: Assign 1 NPC to Forge.

### Step 8 — Craft ammo
Objective: Craft 20 ammo.
- Progress: AmmoCraftedEvent.

### Step 9 — Build Armory + assign Armory runner
Objective: Place Armory, assign 1 NPC.

### Step 10 — Resupply tower
Objective: Tower ammo falls below 25% (scripted drain) → resupply occurs.
- Progress: TowerResuppliedEvent.

### Step 11 — Reward pick
Objective: Pick 1 reward after day end.
- Show reward modal; teach “pick 1 of 3”.

### Step 12 — Tutorial ends
- Message: “Bạn đã sẵn sàng. Từ giờ bạn tự do tối ưu căn cứ.”
- Option: “Tắt hướng dẫn trong các run sau”.

---

## 6) Anti-frustration rules (critical)

### 6.1 Detect deadlocks and help
If objective progress not moving for X seconds:
- show hint:
  - “Kiểm tra NPC có được gán đúng công trình chưa”
  - “Kho đầy / local đầy”
- Provide “Show me” focus to the relevant UI.

### 6.2 Avoid punishing first-run
- First run:
  - lower enemy HP
  - start with full ammo tower
  - spawn fewer enemies
- Do not kill run too early; aim for “learning”.

---

## 7) Tutorial toggles & persistence

### 7.1 Settings
- `ShowTutorial` bool in settings.json
- Default true for first launch, false after completion.

### 7.2 MetaSave flag
- `HasCompletedTutorial`

---

## 8) Implementation plan (fast)

### 8.1 Milestone order
1) ObjectiveService + defs
2) Objective HUD widget
3) Hints as notifications + focus click
4) Highlight service (optional)
5) First-run script: spawn 1 extra NPC + mini-wave

### 8.2 Acceptance criteria
- New player can complete tutorial in 10 minutes
- No step requires reading long text
- Clickable hints always lead to correct place

---

## 9) QA Checklist (Part 18)
- [ ] Each objective can be completed without softlock
- [ ] Hints trigger on common mistakes (no road, no assignment, storage full)
- [ ] “Show me” focuses correct UI/building
- [ ] Tutorial can be skipped/disabled
- [ ] First-run difficulty forgiving

---

## 10) Next Part (Part 19 đề xuất)
**Advanced UX & Accessibility**: key remap, colorblind friendly indicators, scalable UI, tutorial replay, controller support (optional).

