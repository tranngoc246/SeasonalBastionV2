# Promttitle: VS3 — SHIPABLE BASE RUN IMPLEMENTATION PLAN — LOCKED SPEC v0.1

Mình gửi mục tiêu day 35, kiểm tra các file cần thiết, triển khai chuẩn, chi tiết, đầy đủ day 35 giúp mình, nếu thiếu hoặc cần xác nhận gì thì tạm dừng, nhắn mình trước khi code (cần check các file đã cập nhật trong đoạn chat, kiểm tra kỹ các file asmdef để tránh lỗi vòng lặp, phần debug cần rõ ràng). Gửi các file, vị trí, các chỗ thay đổi hoặc thêm để mình copy vào dự án (không gửi patch, file zip)

# VS3 — SHIPABLE BASE RUN IMPLEMENTATION PLAN — LOCKED SPEC v0.1

> Mục tiêu VS3: từ trạng thái VS2 (Playable Run Loop Year 1), hoàn thiện **“Shipable Base Run (Year 1 → Year 2)”** theo **GDD v5** + Deliverable B/C + Part 25–26 contracts:
- RunClock + Calendar chạy đủ **2 năm** (Year 1 → Year 2) theo GDD
- RunOutcome: **Defeat** (HQ HP <= 0) / **Victory** (survive hết Winter Year 2)
- Combat/Waves ổn định (Autumn/Winter) + Boss tối thiểu + anti-softlock
- Economy loop đủ chơi (Producer → Local → Haul → Central Storage) + ammo pipeline khép kín
- Unlock cadence tối thiểu (gating build list theo mốc time/season)
- UX tối thiểu để “ship”: Season Summary, basic tutorial hints, notification anti-spam
- Validator + hardening (start config + runtime invariants) + Save/Load regression

Không over-engineer. Ưu tiên **MonoBehaviour**, **deterministic grid logic**, tick order rõ ràng.

**Single Source of Truth**
- SEASONAL_BASTION_GDD_MASTER_LOCKED_v5_VN
- Deliverable B Run Pacing (Option được chọn trong repo hiện tại)
- Deliverable C Balance Tables (production/cost/recipe/ammo/waves)
- PART25 Technical Interfaces Pack
- PART26 Concrete Class Skeletons
- VS2_ImplementationPlan_LOCKED_v0.1 (baseline đã làm)

---

## 0) Quy ước “Definition of Done” cho VS3

VS3 Done khi:
1) StartNewRun → spawn map đúng StartMapConfig 64x64 (roads/buildings/NPCs/tower ammo) và **Validator pass**.
2) RunClock chạy đúng calendar theo GDD:
   - Spring 6, Summer 6, Autumn 4, Winter 4
   - Chạy được **Year 1 → Year 2**
3) Combat/Waves:
   - Autumn + Winter có wave; Tower bắn tiêu ammo; Enemy đánh HQ/building; không softlock (enemy stuck/wave never ends).
4) Ammo pipeline khép kín:
   - Forge craft ammo (recipe) → HaulAmmo về Armory → ResupplyTower khi low-ammo.
5) Economy loop đủ chơi:
   - Farm/Lumber/Quarry/Iron (tối thiểu subset theo data) sinh resource → local storage cap → HaulBasic về kho trung tâm.
6) RunOutcome:
   - Defeat: HQ HP <= 0 (ngay lập tức)
   - Victory: survive hết Winter Year 2 (trigger đúng 1 lần)
7) Save/Load tối thiểu:
   - Lưu/Load không làm lệch clock snapshot; không crash; run tiếp tục được.
   - (Enemy/wave state: chọn rõ Option Reset hoặc Serialize; phải nhất quán)
8) UX tối thiểu:
   - Season Summary (end of season)
   - Tutorial hints theo triggers
   - Notification anti-spam (cooldown + grouping + max visible)
9) Không có lỗi ngầm:
   - tick order ổn định, không enumerate Dictionary.Keys trong gameplay tick
   - không alloc lớn mỗi frame trong systems chính

---

## 1) Scope / Out of Scope

### 1.1 Scope (phải có trong VS3)
- Calendar đủ 2 năm + Victory theo GDD
- Combat stability pass (enemy stuck, wave resolve, boss minimal)
- Economy pass (producer + haul + storage caps + starvation-safe tối thiểu nếu GDD yêu cầu)
- Unlock gating tối thiểu (build list)
- Season Summary + tutorial hints + notification anti-spam
- Validator + hardening + save/load regression suite

### 1.2 Out of Scope (không làm trong VS3)
- Meta progression dài hạn, shop, tech tree full
- UI/Art polish (chỉ cần functional HUD + debug)
- Pathfinding tối ưu sâu (A* heavy) — chỉ giữ thuật toán đủ ổn định
- Status effects/armor phức tạp, nhiều loại enemy nâng cao
- Analytics, localization, achievements

---

## 2) Nguyên tắc kỹ thuật (LOCK)

1) Không đổi contract Part25/Part26 trừ khi có “LOCKED update”.
2) Deterministic-ish: cùng seed/layout → outcome ổn định ở mức VS.
3) Không LINQ/alloc trong Tick (combat, jobs, clock).
4) Grid rules dựa trên CellPos + Dir4, không physics.
5) Một nguồn chân lý cho state: services/stores, không duplicate ở UI/tools.
6) Save/Load: versioned DTO, backward-compatible tối thiểu (default fields).

---

## 3) Roadmap VS3 theo Day (đề xuất Day 32 → Day 45)

> Mỗi Day = 1 session triển khai + test + log vào VS2_DEV_LOG (hoặc tạo VS3_DEV_LOG nếu bạn muốn tách).

---

### Day 32 — RunClock Year2 + Victory condition theo GDD
**Mục tiêu**
- Clock chạy Year 1 → Year 2; Victory cuối Winter Year2.

**Tasks**
- RunClock:
  - rollover year index + events YearChanged
  - đảm bảo season/day counts theo GDD
- RunOutcomeService:
  - Defeat: HQ HP <= 0
  - Victory: End of Winter Year2
- Debug HUD: hiển thị Year/Season/Day rõ.

**Acceptance**
- Fast-forward (speed) chạy tới Winter Y2 end → Victory đúng 1 lần.

---

### Day 33 — Save/Load regression + clock snapshot (Year2-ready)
**Mục tiêu**
- Save/Load không làm lệch yearIndex/dayTimer; load tiếp tục đúng.

**Tasks**
- SaveService:
  - đảm bảo serialize đầy đủ clock snapshot (yearIndex/season/dayIndex/dayTimer/timeScale)
  - verify apply order sau load (grid → world → indices → clock)
- Regression script: 6 điểm save/load (mỗi season 1 điểm) + mid-wave 1 điểm.

**Acceptance**
- Load lại đúng season/dayTimer; wave (nếu reset) reset đúng; không crash.

---

### Day 34 — Combat stability pass (enemy stuck + wave resolve)
**Mục tiêu**
- Không còn case wave không kết thúc vì enemy kẹt / path fail.

**Tasks**
- EnemyMover fallback:
  - nếu path fail N lần → step theo dirToHQ hoặc BFS radius nhỏ
- WaveDirector resolve rule:
  - wave kết thúc khi spawn done + aliveCount==0
  - timeout safety (log warn + force resolve) để tránh softlock.
- Debug: counter alive/spawned.

**Acceptance**
- Chạy Winter 1 ngày: wave luôn kết thúc (không treo).

---

### Day 35 — Boss minimal + reward hook (placeholder)
**Mục tiêu**
- Có boss tối thiểu trong Winter (theo pacing), reward placeholder khi end season.

**Tasks**
- BossDef: HP lớn hơn, damage cao hơn (data-driven)
- WaveDirector: boss spawn ở wave cuối ngày/season
- Reward hook: emit event EndSeasonRewardRequested (placeholder).

**Acceptance**
- Boss xuất hiện đúng mốc; giết boss không crash; end season trigger reward event.

---

### Day 36 — Producer loop chuẩn (subset theo data) + local storage cap
**Mục tiêu**
- Farm/Lumber (tối thiểu) sinh resource theo balance; local cap; notify full.

**Tasks**
- Producer tick/work:
  - yield/time theo Balance tables
  - local storage per building
- Notification key: producer.local.full (anti-spam friendly)

**Acceptance**
- Chạy 2–3 ngày: local tăng rồi full, dừng đúng.

---

### Day 37 — HaulBasic (Producer → Central Storage) + claim hardening
**Mục tiêu**
- NPC vận chuyển làm giảm local, tăng central; không deadlock.

**Tasks**
- JobProvider: ưu tiên nguồn local gần full, tie-break deterministic
- Executor:
  - claim slot/amount
  - nếu dest full → fallback “return source” hoặc “drop back” (chọn 1 và nhất quán)
- Debug: show active haul jobs count.

**Acceptance**
- Không có 2 NPC bốc cùng stack; không loop vô hạn khi kho đầy.

---

### Day 38 — Ammo pipeline polish (Forge→Armory→Resupply) + anti-spam requests
**Mục tiêu**
- Ammo loop chạy êm trong combat dài (Year2).

**Tasks**
- Resupply requests:
  - dedupe per towerId
  - priority: 0 ammo > low ammo
  - cooldown
- HaulAmmo:
  - chunk size + capacity
- Guard: tower full when deliver → return ammo.

**Acceptance**
- 3 towers bắn liên tục 2 ngày: resupply không spam, tower không đứng yên vì thiếu ammo lâu.

---

### Day 39 — Unlock gating tối thiểu (build list theo mốc time/season)
**Mục tiêu**
- Không cho người chơi build mọi thứ từ đầu; unlock theo nhịp GDD/pacing.

**Tasks**
- UnlockService:
  - schedule data-driven (year/season/day)
  - expose IsUnlocked(defId)
- Build UI/Debug list:
  - chỉ hiện defs unlocked
- Save/Load: persist unlocked state hoặc recompute từ clock (khuyến nghị recompute).

**Acceptance**
- Đầu game: chỉ thấy set công trình start; tới mốc unlock: thấy thêm.

---

### Day 40 — Season Summary UI + metrics (shipable UX)
**Mục tiêu**
- Cuối mỗi season show summary (metrics tối thiểu).

**Tasks**
- Metrics collect:
  - resources gained/spent
  - enemies killed
  - buildings built
  - ammo used
- UI panel: show 4–6 dòng, dismiss để tiếp tục.

**Acceptance**
- End Spring: summary xuất hiện 1 lần; dismiss ok; không spam.

---

### Day 41 — Tutorial hints + notification anti-spam
**Mục tiêu**
- Người chơi không kẹt; thông báo không spam.

**Tasks**
- NotificationService:
  - group by key + cooldown
  - max visible
- Hint triggers:
  - unassigned NPC
  - producer full
  - out of ammo
  - wave incoming

**Acceptance**
- 10 phút đầu: hint đúng lúc, không dồn hàng chục thông báo.

---

### Day 42 — Validator: StartMap + runtime invariants
**Mục tiêu**
- Bắt lỗi ngầm từ data/config trước khi play.

**Tasks**
- RunStartValidator:
  - road connectivity
  - building gap >= 1 cell (8-neighborhood)
  - driveway rule (nếu placement rule yêu cầu)
  - spawn gates in-bounds & connected
- Debug HUD: pass/fail list.

**Acceptance**
- Config sai: show lỗi rõ, không crash, không silent fail.

---

### Day 43 — Balance tuning pass (Year1 → Year2 survivable)
**Mục tiêu**
- Chơi đủ 2 năm có thể win nếu chơi đúng, thua nếu bỏ bê defense.

**Tasks**
- Tune:
  - wave counts/HP scaling
  - tower damage/fireRate/ammo consumption
  - production rates + hauling capacity
- Debug shortcuts: spawn wave, give resources (debug-only).

**Acceptance**
- 1 run manual: có thể survive Winter Y2 với strategy hợp lý.

---

### Day 44 — Performance & cleanup pass (no alloc spikes)
**Mục tiêu**
- Không tụt FPS do GC spikes.

**Tasks**
- Remove LINQ trong combat/job loops
- Cache lists, reuse buffers
- Reduce FindObjectOfType in runtime services (debug-only ok)

**Acceptance**
- Profiler: GC alloc thấp trong play; không spike mỗi wave/tick.

---

### Day 45 — Final regression suite + release checklist
**Mục tiêu**
- “Shipable base run” ổn định.

**Tasks**
- Regression checklist:
  - StartNewRun ×3
  - Save/Load at 8 checkpoints
  - Defeat path (HQ dead)
  - Victory path (end Winter Y2)
  - No softlock after 30 phút
- Write dev log summary + known issues.

**Acceptance**
- Không crash; không softlock; victory/defeat đúng; save/load ổn.

---

## 4) Deliverables VS3 (files bắt buộc)

- `VS3_ImplementationPlan_LOCKED_v0.1.md` (file này)
- `VS3_DEV_LOG.md` (khuyến nghị tách khỏi VS2 để audit)
- Validation report (debug HUD hoặc log) khi start new run
- Test checklist Day 45 (markdown)

---

## 5) Risk list (để tránh kẹt)

1) Wave resolve phụ thuộc enemy path → luôn có timeout safety.
2) Save/Load combat state quá phức tạp → chọn 1: reset wave hoặc serialize tối thiểu.
3) Job spam (haul/resupply) → throttle + dedupe + cooldown.
4) Data drift (json) → validator phải chạy ở boot/run start.
5) Performance: scan targets/enemies mỗi frame → cache & deterministic order.

---

## 6) Quick “Next After VS3”
- VS4: UI polish, more enemy types, deeper tech/unlocks, meta progression.
