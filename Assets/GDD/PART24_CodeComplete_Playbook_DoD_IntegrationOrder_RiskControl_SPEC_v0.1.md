# PART 24 — “CODE ĐƯỢC GAME HOÀN CHỈNH” PLAYBOOK (DEFINITION OF DONE + INTEGRATION ORDER + RISK CONTROL) — SPEC v0.1

> Bạn đang ở giai đoạn “từ SPEC → code chạy hoàn chỉnh”. Phần này không bàn monetization nữa.
Mục tiêu Part 24:
- Định nghĩa **Game hoàn chỉnh** (Definition of Done)
- Chốt **scope v0.1 ship** (không lan man)
- Thứ tự tích hợp hệ thống để **không phải làm lại**
- Checklist kỹ thuật: save, QA, performance, content minimal
- Risk register + cách giảm rủi ro cho solo dev

---

## 1) Definition of Done (DoD) — “Game hoàn chỉnh v0.1”
Game được coi là “hoàn chỉnh v0.1” khi đạt đủ:

### 1.1 Loop chơi trọn 1 run
- New Run → Build Phase (harvest/haul/build) → Defend (waves) → Reward pick → tiếp tục → Victory/Defeat → Summary → back to menu
- Tối thiểu 1 điều kiện Victory (final wave) và 1 điều kiện Defeat (HQ destroyed)

### 1.2 Economy & jobs chạy “đúng logic”
- Không auto-income: tài nguyên chỉ tăng khi worker harvest và transporter deliver
- Build/Upgrade/Repair consume resource theo Part 9 (delivery → work → commit)
- Ammo pipeline chạy đầy đủ:
  - Forge craft ammo từ resource
  - Armory giữ ammo
  - Tower dưới 25% ammo gửi request, Armory ưu tiên resupply
- Warehouse không chứa ammo, HQ chỉ Build/Repair/HaulBasic

### 1.3 UI đủ dùng & không gây bực
- HUD (resources/day/season/speed)
- NPC assign UI
- Build placement UI (ghost + blocked reason)
- Notifications stack (max 3, spam control)
- Reward modal (pick 1/3)
- Summary screen

### 1.4 Save/Load không làm mất dữ liệu
- Continue run hoạt động
- MetaSave persists
- Schema version + graceful handling incompatible save

### 1.5 Stability & performance
- Không crash trong 10 runs liên tiếp (dev seeds)
- No per-frame allocations trong vòng lặp scheduler/combat core (reasonable)
- FX/Audio spam caps hoạt động

---

## 2) Freeze scope cho v0.1 (để code xong)
### 2.1 Content tối thiểu bắt buộc
Buildings:
- HQ, House, Farmhouse, LumberCamp, Warehouse, Forge, Armory, ArrowTower
Enemies:
- 2 types + 1 elite
Waves:
- Autumn/Winter: 6–10 waves total (đủ cảm giác)
Rewards:
- 20–30 rewards (economy/combat/ammo), pick 1/3

### 2.2 Features “NOT in v0.1”
- Multiple tower types, heroes, tech tree sâu
- Complex enemy AI (boss mechanics phức tạp)
- Multiplayer, procedural biomes
- Full controller support

> Lý do: v0.1 cần “loop chạy” trước. Mọi thứ khác là content mở rộng.

---

## 3) Integration order (để không phải làm lại)
Đây là thứ tự code/integrate “an toàn nhất”:

### Phase 1 — Core runtime backbone
1) DataRegistry + Validator + BootFlow (Part 1 + schema)
2) WorldState + Stores + WorldOps (Part 6)
3) RunClock + SpeedControls + events (Part 2)
4) Notifications stack (Part 4)
**Exit criteria**: chạy scene, tick clock, spawn entities bằng dev tools.

### Phase 2 — Placement & map
5) GridMap + road + entry/driveway validation (Part 5)
6) Selection + camera focus (needed for notifications click)
**Exit criteria**: đặt HQ/road/building đúng luật.

### Phase 3 — Economy plumbing (no combat yet)
7) Storage rules + selector + indices (Part 7)
8) JobBoard + ClaimService + Scheduler (Part 8)
9) Providers + Executors:
   - Harvest (producers)
   - HaulBasic (warehouse/hq)
**Exit criteria**: resources flow đúng (local → warehouse/hq), no duplication.

### Phase 4 — Build pipeline (turn economy into construction)
10) BuildOrder + BuildSite + delivery tracking + commit (Part 9)
11) Builder executor: fetch → deliver → work
**Exit criteria**: player đặt build order, NPC xây hoàn thiện công trình.

### Phase 5 — Ammo pipeline (economy -> combat readiness)
12) RecipeDef + Forge craft (Part 10)
13) Armory haul ammo + resupply jobs (Part 10)
14) Tower ammo monitor requests (Part 10)
**Exit criteria**: tower low ammo được resupply tự động, warehouse never holds ammo.

### Phase 6 — Combat vertical slice
15) EnemyDef + lanes + WaveDirector (Part 11)
16) Enemy move/attack + HQ damage
17) Tower targeting/firing + ammo consumption
**Exit criteria**: defend wave chạy, tower bắn, HQ có thể chết.

### Phase 7 — Roguelite wrap
18) Rewards offer + modal + apply effects (Part 12)
19) RunOutcome + Summary + MetaSave (Part 12/13)
20) MainMenu/RunSetup/Continue/Settings basics (Part 13/19)
**Exit criteria**: full run loop end-to-end.

### Phase 8 — Polish for shipping
21) Tutorial/objectives funnel (Part 18)
22) Audio events (Part 20) + FX minimal (Part 21)
23) Debug tooling (Part 15) + Packaging checklist (Part 16)
**Exit criteria**: v0.1 feels coherent, stable.

---

## 4) “Stop doing rework” rules (cực quan trọng)
- **Không** viết UI quá đẹp trước khi logic chạy.
- **Không** tối ưu premature: chỉ fix hotspots có profiler chứng minh.
- **Không** thêm building/enemy mới khi validator + pipeline chưa ổn.
- Mỗi PR chỉ 1 feature path + 1–3 tests.

---

## 5) Acceptance tests (must-have) — per milestone
### 5.1 Economy test scenario
- Start run with HQ+farm+lumber+warehouse
- 2 workers harvest, 1 transporter hauls
- After 3 minutes: warehouse wood/food tăng, producer local không bị full lâu

### 5.2 Build pipeline scenario
- Build Forge requires wood/stone
- Builder fetches from warehouse
- Site progress increases and completes
- Resource counts giảm đúng

### 5.3 Ammo scenario
- Craft 50 ammo in forge
- Armory hauls 50 ammo
- Drain tower ammo to <25% (dev button)
- Resupply happens, request cleared

### 5.4 Combat scenario
- Spawn wave 10 enemies
- Tower shoots and consumes ammo
- Some enemies reach HQ and deal damage
- Defeat triggers if HQ HP = 0

### 5.5 Reward scenario
- End defend day triggers reward modal
- Pick reward updates modifiers immediately (e.g., +tower dmg)
- Next wave reflects new stats

### 5.6 Save/Load scenario
- Save mid-build and load:
  - build site restored
  - job queues restored or re-generated deterministically
- Save mid-defend:
  - wave schedule + enemies restored

---

## 6) Risk register (solo dev reality) + mitigation
### Risk A: Job deadlocks / claims leaks
Mitigation:
- Part 15 Job inspector + “release all claims”
- Unit tests for claim lifecycle
- Stale job cleanup policy strict

### Risk B: Save/Load complexity
Mitigation:
- Save minimal + reconstruct derived indices on load
- Store RNG seed + offered rewards in save
- Versioned migrator, backups

### Risk C: Combat performance
Mitigation:
- Cap active enemies early (e.g., 60)
- Use pooled views + no alloc loops
- Throttle expensive scans (tower target search grid buckets later)

### Risk D: Scope creep
Mitigation:
- Freeze v0.1 list (section 2)
- Add “Future v0.2 backlog” file; do not implement now

---

## 7) What you should code next (practical next step)
Dựa trên chuỗi parts bạn đang có, “game hoàn chỉnh” sẽ nhanh nhất nếu bạn:
1) **Lock Vertical Slice**: đảm bảo Milestone F–I trong Part 14 chạy end-to-end (không polish)
2) **Add Defend**: Part 11
3) **Add Rewards+RunEnd**: Part 12
4) **UI router + minimal screens**: Part 13
5) **Save/Load**: Part 16 policy + minimal implementation

> Nếu bạn đang ở mức chưa có “Defend chạy”, thì Part 11 + Part 12 là 2 bước lớn nhất để gọi là “hoàn chỉnh”.

---

## 8) Next Part (Part 25 đề xuất)
**Technical “interfaces pack”**: gom lại toàn bộ service interfaces + event definitions chuẩn (một chỗ) để code không lệch giữa parts.

