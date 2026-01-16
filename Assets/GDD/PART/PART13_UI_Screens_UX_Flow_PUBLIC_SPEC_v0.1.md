# PART 13 — UI SCREENS & UX FLOW (PUBLIC-READY) — SPEC v0.1

> Mục tiêu: chuẩn hoá luồng UI từ menu → vào run → chơi → rewards → end screen, đủ “ship public”.
- Screen/state machine rõ ràng (không spaghetti)
- UI Toolkit binding theo mô hình Part 3 (UIBinding)
- HUD hiển thị đúng thông tin core (resources, npc assignments, alerts, speed)
- Reward screen (pick 1/3)
- Run summary (victory/defeat)
- Settings + Save/Load UX
- Localization-ready (VN first, dễ thêm EN)

---

## 1) UI Architecture (recommended)

### 1.1 ScreenRouter (single source of truth)
- Một controller duy nhất quản lý screen stack:
  - MainMenu
  - RunSetup
  - InRunHUD
  - RewardSelectionModal
  - PauseMenu
  - RunSummary
  - Settings

```csharp
public enum UIScreen
{
    MainMenu,
    RunSetup,
    InRunHUD,
    RewardModal,
    PauseMenu,
    RunSummary,
    Settings
}

public sealed class ScreenRouter
{
    public UIScreen Current { get; private set; }
    public void Go(UIScreen s) { /* hide/show */ Current = s; }
    public void PushModal(UIScreen modal) { /* stack */ }
    public void PopModal() { /* ... */ }
}
```

> Modal screens: RewardModal, PauseMenu.

### 1.2 UIBinding pattern
- Mỗi screen có binder:
  - `Bind(view, services)`
  - `Unbind()`
- UI reads from “ViewModels” or direct service getters, but avoid direct world mutation from view.

---

## 2) UX Flow (end-to-end)

### 2.1 Main Menu
Buttons:
- New Run
- Continue (if run save exists)
- Meta / Unlock Tree
- Settings
- Quit (PC)

Flow:
- New Run → RunSetup
- Continue → load run save → InRunHUD

### 2.2 Run Setup
- Chọn “Seed” (optional), difficulty preset (optional)
- Show starting kit preview (HQ + initial buildings)
- Start button:
  - create new RunSave
  - auto-fill initial NPC assignments ONLY at run start
  - Go → InRunHUD

### 2.3 In-Run HUD
- Always visible during run (except full-screen modals)
- Top bar: resources + day/season + speed controls
- Left panel: NPC list + assignment + quick actions
- Right panel: build menu/unlocks + selected building details
- Bottom (optional): tips & objectives

### 2.4 Reward Selection (Modal)
Triggered at end of defend day:
- Pause sim
- Show 3 cards
- Player picks 1
- Apply effects
- Close modal; resume sim

### 2.5 Pause Menu (Modal)
- Resume
- Save & Quit to Menu
- Settings
- Abandon Run

### 2.6 Run Summary
On victory/defeat/abort:
- Freeze sim
- Summary: days, waves, kills, score, meta currency gained
- Buttons:
  - Return to Main Menu
  - View Meta Tree (optional)
  - Start New Run

---

## 3) In-Run HUD — exact elements

### 3.1 Top Bar
Left → right:
- Season + Day indicator: `Spring D3` / `Autumn D1`
- Time-of-day bar (optional)
- Speed controls:
  - Build: Pause, 1x, 2x, 3x
  - Defend: 1x default, allow 2x/3x (dev toggle)
- Resource counters:
  - Wood, Food, Stone, Iron, Ammo (ammo only if Armory/Forge exists)
- Notifications stack area below top bar (Part 4)

### 3.2 Resource tooltip
Hover:
- Current / Cap (for storages)
- Net flow last 60s (optional)

### 3.3 NPC Panel (Left)
- NPC count / housing cap
- List items:
  - Name/Id, Role, Workplace assigned, Status icon (Idle/Moving/Working)
  - CurrentJob (short)
- Actions:
  - Assign to Building (opens picker)
  - Unassign
  - Role tag (derived from workplace type)
- Filter:
  - Unassigned first
  - By workplace

### 3.4 Building Inspect Panel (Right)
When select building:
- Title + Tier
- HP bar
- Storage view (type + amount/cap)
- Workplace slots + assigned NPCs
- Buttons depending on building:
  - Upgrade (if unlocked)
  - Repair (if damaged)
  - Move (if allowed)
  - Demolish
- For Forge/Armory:
  - Ammo pipeline status (forge ammo, armory ammo, pending requests count)

### 3.5 Build Menu
- Categorized by unlock groups:
  - Core (Warehouse)
  - Production (Farm, Lumber, ...)
  - Defense (Towers)
  - Ammo (Forge, Armory)
- Locked items show requirement:
  - “Unlock at day X” or “Spend shards”

---

## 4) Reward Screen UI (Modal)

### 4.1 Layout
- Title: “Phần thưởng ngày X”
- 3 Reward Cards (equal width)
Each card:
- Rarity color (style only)
- Title
- Description
- Effects list bullet
- Category icon (optional)
- Pick button

### 4.2 Reroll (optional v0.1)
- Allow reroll 1 time per run if you want.
- If not, omit.

---

## 5) Run Summary UI

### 5.1 Panels
- Outcome banner (Victory/Defeat)
- Stats:
  - Days survived
  - Waves cleared
  - Enemies killed
  - HQ damage taken
  - Ammo shortage time (optional)
- Score + meta currency gained
- Rewards recap (picked rewards list)
- Unlocks gained

---

## 6) Settings UI

Settings categories:
- Graphics (fullscreen, resolution)
- Audio (master/music/sfx)
- Controls (keybinds optional)
- Gameplay:
  - Show tips
  - Speed control enable in Defend (dev)
- Language (VN first)

Persist in `settings.json`.

---

## 7) Save/Load UX

### 7.1 Auto save
- Auto-save at:
  - end of day
  - after reward pick
  - when entering defend
- Save indicator small icon top-right.

### 7.2 Continue run
- Main menu shows: “Continue — Spring D2”
- If save incompatible (schemaVersion mismatch):
  - show message and option to discard.

---

## 8) Notifications integration (Part 4)
- Notification stack is part of HUD, below top bar.
- Clicking notification:
  - focuses related building (select it) if payload has BuildingId/TowerId.

Define payload:
```csharp
public struct NotificationPayload
{
    public BuildingId Building;
    public TowerId Tower;
    public string Extra;
}
```

---

## 9) Input & interactions

### 9.1 Build placement
- Select build item → enters BuildTool mode
- Ghost preview shows:
  - footprint
  - entry cell highlight
  - blocked reason tooltip (Part 5)
- Confirm:
  - if ok: create BuildOrder + BuildSite (Part 9)
  - else: push notification blocked

### 9.2 Assign NPC
- Select building → “Assign NPC”
- Picker modal lists eligible NPCs:
  - Unassigned first
  - Role compatibility
- Assign triggers:
  - update workplace slots
  - optionally show toast “Đã gán NPC”

---

## 10) UI Toolkit implementation notes (practical)

### 10.1 Folder structure
- `Assets/UI/USS/`
- `Assets/UI/UXML/`
- `Assets/UI/Screens/Binders/`
- `Assets/UI/Widgets/`

### 10.2 Binders
Each binder holds references to:
- VisualElements
- service interfaces

Example binder signature:
```csharp
public interface IScreenBinder
{
    void Bind(VisualElement root, GameServices services);
    void Unbind();
}
```

### 10.3 Update cadence
- Avoid per-frame UI rebuild.
- Use event-driven updates:
  - On resource change
  - On selection change
  - On day/season change
- For counters: update every 0.25–0.5s max.

---

## 11) Localization-ready
- All UI strings from `LocalizationTable`:
  - key → VN string
- Reward titles/descriptions stored in RewardDef, but can be keys too.

---

## 12) QA Checklist (Part 13)
- [ ] Can start new run and enter HUD with correct starting kit
- [ ] Can assign NPCs and see workplace/job updates
- [ ] Build placement shows blocked reasons correctly
- [ ] Reward modal pauses sim and applies reward
- [ ] Defeat/victory shows run summary and grants meta currency
- [ ] Save/continue works and restores UI state
- [ ] Notifications clickable focus building/tower

---

## 13) Next Part (Part 14 đề xuất)
**Implementation Plan (Milestones + sprint tasks)**: theo thứ tự code để có vertical slice playable trong 2–4 tuần.

