# PART 19 — ADVANCED UX & ACCESSIBILITY (KEY REMAP, COLORBLIND, SCALABLE UI, TUTORIAL REPLAY) — SPEC v0.1

> Mục tiêu: nâng chất lượng trải nghiệm “public-ready”, giảm review xấu vì UI khó nhìn / thao tác khó:
- Accessibility: colorblind-friendly, text scaling, contrast
- UX: key remap (PC), tooltips, confirmations
- UI scalability: 1080p/1440p/4K, ultra-wide
- Tutorial replay + contextual help
- Controller support: optional (defer)

v0.1 tập trung vào **PC mouse + keyboard**.

---

## 1) UX principles (game này cần gì)
- Người chơi phải “đọc được trạng thái” nhanh:
  - storage full, low ammo, unassigned NPC, stuck build
- Feedback phải nhất quán:
  - notification + focus + highlight
- Tránh thao tác thừa:
  - assign NPC nhanh
  - open building details nhanh
- Không để màu là “tín hiệu duy nhất” (colorblind)

---

## 2) Settings additions (Settings.json)

### 2.1 UI scale
- `UIScale` (0.8–1.4), default 1.0
- `UIFontSize` preset: Small/Normal/Large
Implementation:
- root VisualElement scale or USS variable

### 2.2 Accessibility
- `HighContrastMode` bool
- `ColorblindMode` enum: Off / Deuteranopia / Protanopia / Tritanopia
- `ReduceMotion` bool (turn off fancy animations)
- `NotificationSound` bool + volume

### 2.3 Gameplay QoL
- `AutoPauseOnReward` true
- `ConfirmDemolish` true
- `ConfirmAbandonRun` true
- `ShowTutorial` toggle + `AllowTutorialReplay`

### 2.4 Input
- `Keybinds` dictionary (action → key)

---

## 3) Key remap (PC) — minimal but solid

### 3.1 Actions
Define limited set:
- Camera: PanUp/Down/Left/Right, ZoomIn/Out
- Tools: PlaceRoad, PlaceBuilding, CancelTool
- UI: TogglePause, ToggleDevPanel (dev), OpenBuildMenu, OpenNPCPanel
- Speed: Speed1, Speed2, Speed3, Pause

### 3.2 Model
```csharp
public sealed class Keybinds
{
    public System.Collections.Generic.Dictionary<string, UnityEngine.KeyCode> Map;
}
```

### 3.3 UI flow
- Settings → Controls
- List actions with current key
- Click action → “Press a key…”
- Conflict resolution:
  - prompt “Key này đã dùng cho X, bạn muốn đổi không?”

### 3.4 Persistence
- Save to settings.json
- On load apply to Input layer:
  - If using New Input System: map via runtime rebinding (recommended)
  - If you prefer not to touch InputActions asset: store overrides at runtime only (ok)

---

## 4) Tooltips & contextual help

### 4.1 Global tooltip system
- One TooltipService that can show tooltip near cursor
- Works for:
  - resources (show cap, flow)
  - building stats (hp, storage)
  - reward effects

### 4.2 Context help overlay
- Press `H` to show “Help overlay”
- Shows:
  - common hotkeys
  - what to do next (objective summary)
  - current season tips

---

## 5) Colorblind & iconography

### 5.1 Avoid color-only signals
- Notifications use icon + text:
  - Warning icon for low ammo
  - Box icon for storage full
  - Person icon for unassigned NPC

### 5.2 Tower ammo indicator
- Use segmented bar + numeric:
  - “12/50”
- Low ammo state:
  - add pattern/stripe (USS background) not only color

### 5.3 Producer local storage full
- Show small “FULL” label or exclamation icon over building

### 5.4 Colorblind palettes
- v0.1: implement via USS variables:
  - `--accent-color`
  - `--warning-color`
  - `--error-color`
Switch based on setting.

---

## 6) Scalable UI (1080p → 4K)

### 6.1 Layout rules
- Use flex layout, avoid fixed pixel widths
- Min/max width constraints:
  - NPC panel min 260, max 420
- Use `rem`-style scaling with USS vars if possible

### 6.2 Ultra-wide
- Keep panels pinned left/right
- Center gameplay remains visible
- Avoid stretching top bar elements too far:
  - center cluster for season/time; resources right.

### 6.3 Safe areas
- Provide margin from screen edges (8–16px scaled)

---

## 7) Motion reduction
- If `ReduceMotion`:
  - disable screen transitions
  - disable notification slide animations
  - keep instant fades

---

## 8) Confirmation dialogs (prevent misclick pain)

### 8.1 Demolish
- confirm dialog with building name + refund note (if any)
- “Don’t show again” checkbox (respects setting)

### 8.2 Abandon run
- confirm with warning: “Bạn sẽ mất run hiện tại (trừ meta progress)”

### 8.3 Reset meta
- hard confirm (type “RESET” optional)

---

## 9) Tutorial replay & contextual hints

### 9.1 Replay button
- Main menu: “Replay Tutorial”
- In run: Settings → Tutorial → “Restart tutorial objectives”
- Should not break run logic:
  - objective service resets, hints start again
  - do not re-autofill NPCs

### 9.2 Contextual hints
- If player repeatedly fails placement because no road:
  - show hint with image overlay of entry cell (optional)
- If ammo shortage occurs:
  - show hint chain: build forge → build armory → assign smith/runner

---

## 10) Accessibility QA checklist

### 10.1 Colorblind
- [ ] All critical states have icon/text
- [ ] Colorblind mode toggles palette without breaking contrast

### 10.2 Scale
- [ ] UI scale 0.8–1.4 works without overlapping
- [ ] 4K readable with large font

### 10.3 Input
- [ ] Rebinding persists and conflicts handled
- [ ] Default keys documented in help overlay

### 10.4 Motion
- [ ] Reduce motion disables animations

---

## 11) Implementation plan (fast)
1) Settings additions + persistence
2) UI scale via USS variables
3) TooltipService + resource/building tooltips
4) Confirm dialogs
5) Icons for notifications + ammo/local full markers
6) Key rebind UI + runtime overrides
7) Help overlay + tutorial replay controls

---

## 12) Next Part (Part 20 đề xuất)
**Audio & Feedback design**: sfx cues for notifications, tower shots, build complete, wave start/end, UI clicks; mix rules.

