# 1 RUN CHƠI HOÀN CHỈNH — IMPLEMENTATION BLUEPRINT (v0.1 LOCKED)

> Mục tiêu: bạn có thể **Play → (2 năm) → Win/Lose → Summary → Restart** một cách trơn tru, đủ cảm giác “premium roguelite run”.  
> Blueprint này **không mở rộng scope ngoài A/B/C**, chỉ “đóng gói” thành một run hoàn chỉnh + các hệ UI/UX tối thiểu để ship được.

---

## 0) Definition of Done — “1 Run Complete”
Một run được coi là hoàn chỉnh khi:

### 0.1 Flow tổng
- **Main Menu** → New Run → (Optional: Tutorial) → Gameplay
- Gameplay: **Year 1 (Dev/Defend) + Year 2 (Dev/Defend)** theo Deliverable B
- **Lose** khi HQ HP = 0 (bất kể mùa)
- **Win** khi hạ **Boss Winter Y2 D4**
- Kết thúc: **Run Summary** (thống kê) → Retry / Back to Menu

### 0.2 Systems cốt lõi phải hoạt động
- Time/Season/Speed: Dev 180s/day, Defend 120s/day; Pause/1x/2x/3x; vào Defend auto 1x
- NPC: spawn theo food/housing; onboarding auto-fill chỉ đầu game; về sau NPC mới → notify → player assign
- Economy: gather theo worker-driven + deposit; local caps; haul basic; builder fetch-before-build
- Ammo: Forge → Armory → Tower; tower request ≤25%; Armory ưu tiên resupply; out-of-ammo stop firing
- Combat: waves theo day + boss Y1/Y2; damage; repair; fail-state rõ

### 0.3 UX tối thiểu để chơi “đã”
- HUD: Year/Season/Day + speed + pause
- Notifications stack (max 3, newest on top)
- Tutorial/hints dẫn đường tối thiểu (đặc biệt: Assign NPC, Ammo pipeline, Speed)
- Run Summary: 6–10 chỉ số cơ bản

---

## 1) Run Content Scope (v0.1)
### 1.1 Buildings/Towers bắt buộc (theo A/B/C)
- HQ, House, Road
- Farmhouse, Lumber Camp, Quarry, Iron Hut
- Warehouse, Builder Hut
- Forge, Armory
- Towers: Arrow, Cannon, Frost, Fire, Sniper

### 1.2 Enemies/Waves
- Enemies: Swarmling/Raider/Bruiser/Archer/Sapper + Elite variants Year 2
- Bosses: Siege Brute (Y1 Winter D4), Frost Warlord (Y2 Winter D4)
- Waves: đủ cho Autumn/Winter Y1 theo Deliverable C; Year 2 dùng scaling + elites insert (v0.1)

---

## 2) Run Flow Spec (menu → run → end)
### 2.1 Main Menu (tối thiểu)
- Buttons: **New Run**, **Settings**, **Quit**
- “New Run” → chọn: **Tutorial ON/OFF** (mặc định ON)

### 2.2 In-Run UI
- Top bar:
  - Year / Season / Day
  - Speed buttons: Pause, 1x, 2x, 3x
  - (Optional) Icon: Defend mode (để nhớ 1x)
- Left/Right panel (tối thiểu):
  - Build menu (categories)
  - Workforce/Assign panel (list NPC unassigned + workplaces)
- Notifications:
  - vị trí giữa, mép trên cùng (dưới topbar), max 3, newest on top

### 2.3 End Screens
- Lose screen: “HQ destroyed” + summary + Retry/Exit
- Win screen: “Winter Y2 cleared” + summary + Retry/Exit

---

## 3) “Must Ship” Data-Driven Pipeline (để không hardcode)
### 3.1 Data assets tối thiểu
- RunCalendarDef (days/season, seconds/day Dev/Defend)
- BuildingDefs (cost/HP/storage caps/unlock tier)
- TowerDefs (stats/ammo)
- EnemyDefs (stats/scaling)
- WaveDefs (by Year/Season/Day)

### 3.2 Validator (Editor tool)
- Missing defs, negative values, unlock day out-of-range
- AmmoMax > 0; craft recipe sane
- Wave exists cho mọi ngày Defend

---

## 4) Production Plan — để ra “1 run complete” (8–10 sessions)
> Đây là “đường thẳng” ít rẽ nhánh nhất.

### Session A — Core Loop Framework
- RunClock + Season change events
- Speed controller + auto 1x in Defend
- HUD hiển thị calendar + speed  
**DoD:** chạy được 2 năm (dummy) không crash

### Session B — Start Setup + Save-less Restart
- New Run spawn đúng: HQ + 2 Houses + Farm + Lumber + Arrow full ammo + 3 NPC assigned
- Restart run = reset scene/state sạch  
**DoD:** bấm Retry quay lại đúng start

### Session C — Workforce + Onboarding
- Auto-fill ON tới Spring Day 2 hoặc first manual assign
- NPC spawn cadence + notifications
- Assignment UI tối thiểu (unassigned → chọn workplace)  
**DoD:** onboarding tắt đúng, về sau không auto-assign

### Session D — Economy Y1 (Farm/Lumber) + HaulBasic
- Harvest + deposit + caps + notifications
- HQ hauling basic + Warehouse hauling  
**DoD:** không “tự tăng theo thời gian”, kho cục bộ không hard-lock

### Session E — Builder Fetch-before-build + Repair
- Build/upgrade/repair với fetch rule
- Blocked reason + notification “thiếu tài nguyên”  
**DoD:** xây được Warehouse/Forge/Armory trong Y1 Dev

### Session F — Ammo Pipeline
- Forge craft + smith loop
- Armory haul ammo + resupply priority
- Tower request ≤25% + out-of-ammo stop firing  
**DoD:** survive Autumn Y1 nếu player làm đúng

### Session G — Combat + Waves Y1 (Autumn/Winter) + Boss Y1
- Enemy spawn + path + damage
- W1–W4 + Boss Siege Brute  
**DoD:** clear Winter Y1 (khi chơi đúng)

### Session H — Extend to Year 2 Content
- Unlock Fire/Sniper
- Year 2 scaling + elites
- Boss Frost Warlord  
**DoD:** clear Winter Y2 (khi chơi đúng)

### Session I — Run Summary + Polishing UX
- Summary metrics, end screen polish
- Tutorial hint triggers + speed hint + ammo hint
- Debug overlay toggle (ship in dev build)  
**DoD:** 1 run hoàn chỉnh từ menu tới end

---

## 5) Run Summary Spec (tối thiểu nhưng “premium”)
Hiển thị 6–10 chỉ số:
- Total days survived
- Peak population
- Total resources gathered (wood/stone/iron/food)
- Total ammo crafted
- Total ammo delivered to towers
- Towers built by type
- Buildings lost/ destroyed
- Bosses defeated (Y1/Y2)
- Time spent at each speed (1x/2x/3x)

---

## 6) Balance Guardrails (để run không kẹt)
- Luôn có “early ammo runway”: Arrow tower start full ammo
- Iron bottleneck vừa đủ: Iron L1 chậm nhưng không zero
- Notifications phải xuất hiện “đúng lúc”:
  - Tower ≤25% ammo (warning) xuất hiện trước khi hết đạn đủ lâu để player phản ứng
  - Local storage near full để player học haul/warehouse
  - Builder blocked thiếu resource để player hiểu fetch rule

---

## 7) QA Checklist — trước khi gọi là “complete run”
### 7.1 Smoke tests
- New Run → chơi tới hết Spring Y1 không lỗi
- Vào Autumn: speed auto về 1x
- Tower request ≤25% tạo đúng; Armory ưu tiên đúng
- Lose: HQ=0 luôn ra màn lose
- Win: Boss Y2 chết ra win screen

### 7.2 Regression traps
- Auto-fill chỉ onboarding, về sau tuyệt đối không tự assign
- Warehouse không chứa Ammo (test storage routing)
- HQ worker jobset chỉ Build/Repair/HaulBasic (test job filtering)

---

## 8) Những thứ “đừng làm” trong v0.1 (để không vỡ scope)
- Meta progression, perk tree, relics hệ roguelite (để v0.2+)
- Morale/happiness hệ phức tạp
- Advanced AI director
- Weather micro-effects (ngoài seasonal pacing)
- Save/Load run giữa chừng (chỉ restart full run)

---

## 9) Kết luận — cách bạn bắt đầu ngay
Nếu mục tiêu là “1 run hoàn chỉnh” nhanh nhất:
1) Implement theo thứ tự **A → I** ở mục 4 (đừng nhảy)  
2) Data-driven từ đầu (mục 3)  
3) Mỗi session kết thúc bằng **playtest 1–2 ngày** trong run thật (không test isolated)

---

### (Optional) Nếu bạn muốn mình viết tiếp
Mình có thể viết ngay:
- **Session Brief** dạng chuẩn Copilot cho từng Session A–I (mỗi session: mục tiêu, file list, DoD, edge cases, prompt cho Copilot).
