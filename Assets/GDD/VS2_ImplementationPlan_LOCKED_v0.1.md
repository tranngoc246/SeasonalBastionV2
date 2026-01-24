# Promttitle: VS2 — VERTICAL SLICE #2 IMPLEMENTATION PLAN — LOCKED SPEC v0.1

Mình gửi mục tiêu day ..., kiểm tra các file cần thiết, triển khai chuẩn, chi tiết, đầy đủ day ... giúp mình, nếu thiếu hoặc cần xác nhận gì thì nhắn mình trước khi code. Gửi các file, vị trí, các chỗ thay đổi hoặc thêm để mình copy vào dự án (không gửi patch, file zip)

# VS2 — VERTICAL SLICE #2 IMPLEMENTATION PLAN — LOCKED SPEC v0.1

> Mục tiêu VS2: từ nền tảng VS1 (Day 1–14), triển khai **“Playable Run Loop Year 1”** theo Deliverable B/C + Part 25–26 contracts:
- StartNewRun spawn map đúng **StartMapConfig 64x64**
- RunClock chạy đúng pacing Option B (Dev/Defend) + speed rules
- Build thật (Site → Deliver vật tư → Work → Complete)
- Ammo pipeline (Forge craft → Armory buffer → Resupply Tower khi low-ammo)
- Combat tối thiểu (Wave → Enemy move/attack → Tower fire consume ammo)
- RunOutcome + Save/Load tối thiểu (đủ để resume run)

Không over-engineer. Ưu tiên **MonoBehaviour**, **deterministic grid logic**, tick order rõ ràng.

**Single Source of Truth**
- PART25 Technical Interfaces Pack
- PART26 Concrete Class Skeletons
- PART27 VS#1 Sprint Plan (nền tảng)
- StartMapConfig_RunStart_64x64_v0.1
- Deliverable B Run Pacing (Option B)
- Deliverable C Balance Tables (cost/recipe/ammo/waves)

---

## 0) Quy ước “Definition of Done” cho VS2

VS2 Done khi:
1) Mở game → StartNewRun → map 64x64 spawn đúng config (roads/buildings/NPCs/initial storage/tower ammo).
2) RunClock chạy đúng:
   - **Dev (Spring+Summer): 180s/day**
   - **Defend (Autumn+Winter): 120s/day**
   - **Vào Defend tự set 1x** (vẫn cho Pause).
3) Place Building tạo **BuildSite** có cost; NPC **deliver vật tư** từ storage; đủ vật tư mới **work**; xong thì commit Building.
4) Forge craft ammo theo recipe; ammo chảy **Forge → Armory**; Tower ammo <= 25% tạo request; Transporter resupply tower.
5) Defend day: WaveDirector spawn enemy theo defs; tower bắn (consume ammo); enemy đánh HQ/building; HQ HP <= 0 → Defeat; survive hết Winter Y1 (hoặc điều kiện Victory tối thiểu theo GDD/Deliverable B) → Victory.
6) Không softlock:
   - claim leak không xảy ra (có debug ReleaseAllClaims)
   - occupancy leak không xảy ra (cancel/complete đều cleanup)
7) Save/Load tối thiểu chạy: có thể lưu run và load lại để tiếp tục (ít nhất: clock + grid/buildings/sites/npcs/storage + tower ammo).

---

## 1) Scope / Out of Scope

### 1.1 Scope (phải có trong VS2)
- RunClock + Calendar + Speed controls (Option B)
- StartNewRun từ StartMapConfig
- DataRegistry load đủ defs tối thiểu cho VS2 (Buildings + NPC + Recipes + Ammo + Enemy + Waves + Rewards placeholder nếu cần)
- Build pipeline thật (Deliver + Work)
- Ammo pipeline + Tower low-ammo request
- Combat minimal + RunOutcome
- Save/Load tối thiểu

### 1.2 Out of Scope (không làm trong VS2)
- Meta progression (unlock tree full), shop, long-term economy
- UI/UX polish (HUD đẹp, tutorial hoàn chỉnh) — chỉ cần debug HUD đủ chơi
- Pathfinding nâng cao (A* tối ưu, multi-agent avoidance phức tạp)
- Full damage model/armor/status effects phức tạp
- Analytics, localization, optimization sâu

---

## 2) Nguyên tắc kỹ thuật (LOCK)

1) **Không đổi contract Part25** trừ khi có “LOCKED update”.
2) Tick order phải ổn định: cùng seed → cùng outcome (mức deterministic “ish” như VS1).
3) Không lạm dụng LINQ/alloc trong Tick.
4) Mọi rule grid phải dựa trên CellPos + Dir4, không dựa physics.
5) Services là single source of truth (không duplicate state ở tool/UI).

---

## 3) Roadmap VS2 theo Day (đề xuất Day 15 → Day 31)

> Gợi ý: mỗi “Day” là 1 session triển khai + test + log.  
> Nếu repo đã có sẵn một phần, vẫn giữ cấu trúc Day để audit dễ.

---

### Day 15 — RunClockService (Option B) + Events
**Mục tiêu**
- Implement RunClock tick theo Deliverable B Option B.
- Phát event DayStart/DayEnd/SeasonChanged/YearChanged/SpeedChanged.

**Tasks**
- Implement:
  - SecondsPerDay_Dev = 180
  - SecondsPerDay_Defend = 120
  - Season day counts (theo Deliverable B / GDD)
- Speed controls: Pause/1x/2x/3x; vào Defend auto set 1x.
- Debug HUD hiển thị: Year/Season/Day + remaining seconds.

**Acceptance**
- Để game chạy 2–3 ngày liên tục: day rollover ổn, speed đổi đúng rule.

---

### Day 16 — StartNewRun spawn theo StartMapConfig (64x64)
**Mục tiêu**
- GameLoop.StartNewRun() tạo world đúng config.

**Tasks**
- JSON loader cho StartMapConfig:
  - map size 64x64, buildable rect
  - roads list
  - start buildings placements
  - start NPC spawn + initial roles (manual ok)
  - initial storage amounts
  - tower initial ammo
- Clear/reset world & grid clean trước khi spawn.

**Acceptance**
- Enter Play: map spawn đúng; không có building/site về (0,0); debug view thể hiện đúng.

---

### Day 17 — DataRegistry mở rộng defs cho VS2
**Mục tiêu**
- Load thêm defs ngoài Buildings để phục vụ VS2.

**Tasks**
- Add loaders:
  - NPC defs (role/capacity)
  - Recipes (ammo craft)
  - Enemy defs (hp, speed, damage)
  - Wave defs (spawn schedule)
  - Rewards placeholder (nếu RunOutcome cần)
- Validate schema tối thiểu (Data_Schema_Validator_SPEC nếu đang dùng).

**Acceptance**
- Log “Loaded X defs” từng nhóm; không crash khi missing optional fields.

---

### Day 18 — BuildSite state + cost tracking
**Mục tiêu**
- Site tạo ra có cost và track delivered/remaining.

**Tasks**
- Định nghĩa cost source:
  - Ưu tiên Deliverable C / Buildings.json (khóa 1 nguồn).
- BuildSiteState:
  - RemainingCosts, DeliveredSoFar
  - IsReadyToWork khi remaining = 0
- Debug HUD show: site cost progress.

**Acceptance**
- Place building tạo site có progress đúng.

---

### Day 19 — JobProvider cho DeliveryToSite + WorkOnSite
**Mục tiêu**
- JobBoard có thể sinh job delivery và job work cho build site.

**Tasks**
- Provider rule:
  - Nếu site còn thiếu → tạo DeliveryToSite jobs theo từng resource chunk.
  - Nếu đủ → tạo WorkOnSite job (builder role).
- Throttle: mỗi site tối đa N pending delivery jobs (tránh spam).

**Acceptance**
- Khi thiếu vật tư: thấy job delivery; đủ vật tư mới xuất hiện job work.

---

### Day 20 — Executors: Pickup/Deliver/Work cho Build
**Mục tiêu**
- NPC builder thực sự đi lấy vật tư, giao site, rồi xây.

**Tasks**
- Delivery executor:
  - Choose source storage deterministically
  - Pickup → reserve/consume → move → deliver (rollback nếu fail)
- Work executor:
  - Chỉ work khi site IsReadyToWork
  - WorkDuration theo def (hoặc tạm fixed từ Deliverable C)

**Acceptance**
- Đặt building mới: NPC deliver xong mới build; hoàn tất commit building.

---

### Day 21 — Cancel build + cleanup + refund policy
**Mục tiêu**
- Hủy site không leak occupancy/claims và có refund hợp lý.

**Tasks**
- Cancel rules:
  - Release claims
  - Clear occupancy & references
  - Refund delivered to nearest storage (hoặc HQ) theo rule đơn giản
- Tool/UI: expose cancel for debug.

**Acceptance**
- Cancel giữa chừng không gây kẹt, không “ghost site”.

---

### Day 22 — Damage/Repair tối thiểu để hỗ trợ Combat
**Mục tiêu**
- Building/HQ có HP, bị damage, có repair job cơ bản.

**Tasks**
- Add HP to building states (nếu chưa có).
- Repair job provider + executor (consume basic resource hoặc time-only minimal).

**Acceptance**
- Khi building bị damage: có đường repair, không crash.

---

### Day 23 — Forge craft ammo (Recipe + Craft job)
**Mục tiêu**
- Producer ammo chạy theo recipe (consume inputs, output ammo).

**Tasks**
- Recipe evaluation:
  - input resources
  - output ammo (resource type = Ammo)
  - craft time
- Forge provider tạo CraftAmmo job khi:
  - có chỗ chứa local
  - input available

**Acceptance**
- Forge tăng ammo theo thời gian khi đủ input.

---

### Day 24 — Armory buffer + HaulAmmo (Forge → Armory)
**Mục tiêu**
- Ammo logistics chạy vào kho trung tâm.

**Tasks**
- Armory target buffer rule.
- HaulAmmo job + executor (transporters).

**Acceptance**
- Ammo chuyển từ Forge sang Armory theo chunk.

---

### Day 25 — Tower low-ammo monitor + request queue
**Mục tiêu**
- Tower tự tạo request khi ammo thấp.

**Tasks**
- Threshold: <= 25% max ammo
- Cooldown per tower, priority: 0 ammo > low ammo.
- Notifications/HUD: hiển thị low ammo (không spam).

**Acceptance**
- Tower bắn giảm ammo → tạo request đúng 1 lần/cooldown.

---

### Day 26 — ResupplyTower job + executor
**Mục tiêu**
- Transporter resupply tower từ Armory.

**Tasks**
- Provider chọn tower theo priority + distance tie-break.
- Executor: pickup ammo → move → deliver to tower.

**Acceptance**
- Tower low ammo được nạp lại; ammo Armory giảm tương ứng.

---

### Day 27 — Combat map lanes/spawn gates (từ StartMapConfig)
**Mục tiêu**
- Có định nghĩa lane/spawn cell để spawn wave.

**Tasks**
- Parse spawn points/gates từ config (hoặc static list trong StartMapConfig v0.1).
- Runtime lane table: laneId → start cell → direction/target HQ.

**Acceptance**
- Có thể debug spawn 1 enemy theo lane, enemy có target.

---

### Day 28 — WaveDirector schedule + Defend day trigger
**Mục tiêu**
- Vào Defend day thì wave chạy theo defs.

**Tasks**
- Wave schedule: spawn groups by interval
- Events: WaveStarted/WaveEnded
- Pause handling: wave timing theo clock speed.

**Acceptance**
- Vào Autumn/Winter: wave bắt đầu; hết wave: emit end.

---

### Day 29 — Enemy: move + attack HQ/buildings
**Mục tiêu**
- Enemy di chuyển theo grid, tới target và gây damage.

**Tasks**
- Enemy state: HP, speed, attack interval, damage.
- Movement:
  - ưu tiên path đơn giản 4-dir tới HQ (BFS minimal nếu cần)
- Attack:
  - damage HQ/building
  - death cleanup

**Acceptance**
- Enemy tới HQ và giảm HP; HQ 0 → Defeat.

---

### Day 30 — TowerCombatSystem: target + fire + consume ammo
**Mục tiêu**
- Tower bắn enemy trong range, tiêu ammo.

**Tasks**
- Target selection deterministic:
  - nearest in range, tie by EnemyId
- Fire interval + damage
- Consume ammo; nếu ammo 0 thì không bắn.

**Acceptance**
- Tower giết enemy; ammo giảm; low-ammo request hoạt động.

---

### Day 31 — RunOutcome + Save/Load tối thiểu
**Mục tiêu**
- Chốt run cycle + persistence tối thiểu.

**Tasks**
- RunOutcome rules:
  - Defeat: HQ HP <= 0
  - Victory: survive hết Winter Y1 (hoặc condition theo GDD nếu đã lock)
- SaveService:
  - serialize clock snapshot
  - buildings/sites + storage + NPC + tower ammo
  - (enemy/wave state optional – có thể reset wave khi load nếu muốn đơn giản)

**Acceptance**
- Save → quit → load: world phục hồi; clock tiếp tục; không crash.

---

## 4) Deliverables VS2 (files bắt buộc)

- `VS2_DEV_LOG.md` (Day 15–31) — log theo format VS1
- `StartMapConfig` loader + validation
- Update `DefsCatalog` + defs json cho Recipes/Enemies/Waves nếu chưa có
- Minimal debug HUD:
  - Clock (Year/Season/Day, speed)
  - Build sites progress
  - Ammo (Armory + tower warnings)
  - Combat (wave state, HQ HP)

---

## 5) Risk list (để tránh kẹt)

1) **Over-scope pathfinding combat** → giữ BFS đơn giản, optimize sau.
2) **Save/Load quá sâu** → chỉ serialize “must-have”, enemy/wave có thể reset.
3) **Job spam** (delivery/resupply) → throttle per site/tower.
4) **Claim leak** → luôn cleanup theo finally; có debug ReleaseAllClaims.

---

## 6) Quick “Next After VS2”
- VS3: UI/UX, unlock/reward loop, more buildings, balancing, performance pass.
