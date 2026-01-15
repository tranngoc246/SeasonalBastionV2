# SEASONAL BASTION — DELIVERABLE A (CONTENT BIBLE)  
## LOCKED v0.1 (FULL) — Base Game Premium Roguelite

> Tài liệu này là **Content Bible** (đủ chi tiết để triển khai).  
> Trạng thái: **LOCKED v0.1** (các default numbers có thể tune về sau nhưng **rule/behavior là khóa**).  
> Ngôn ngữ: **Tiếng Việt**.

---

## 0) Quy ước & Luật nền (GLOBAL LOCKS)

### 0.1 Resource Set (Base)
- **Wood (Gỗ)**
- **Stone (Đá)**
- **Iron (Sắt)**
- **Food (Lương thực)**
- **Ammo (Đạn dược)**

### 0.2 Storage Model (worker-driven) — LOCK
- **Tài nguyên chỉ tăng** khi worker **mang về và deposit** vào kho (HQ / Warehouse / kho cục bộ của công trình).
- **Không có** cơ chế “tự tăng theo thời gian”.
- Mỗi công trình có thể có **kho cục bộ** (local storage) với capacity giới hạn.
- **Warehouse** và **HQ** có kho đa loại (multi-resource).  
- **Warehouse KHÔNG chứa Ammo**. Ammo chỉ ở **Forge / Armory / Tower**.

### 0.3 Carry Model — LOCK (khuyến nghị ship)
- Mỗi NPC mỗi chuyến **mang 1 loại resource**.
- **CarryAmount** phụ thuộc workplace level (L1/L2/L3).  
- Không spawn item entity thật (crate); chỉ là **số lượng + animation**.

### 0.4 Workplace Assignment — LOCK
- NPC sinh ra ở trạng thái **Unassigned**.
- **Unassigned NPC chỉ làm:** `Leisure`, `Inspect`.
- Người chơi **Assign/Unassign** NPC vào một **Workplace**.
- NPC đã assign chỉ nhận job thuộc **JobSet** của Workplace.

### 0.5 Auto-fill chỉ trong Onboarding — LOCK
- **Auto-fill ON chỉ trong giai đoạn Onboarding** (đầu game).  
- Kết thúc Onboarding → **Auto-fill OFF toàn cục**.  
- Từ đó về sau, NPC mới sinh ra → **thông báo** và người chơi **tự assign**.

**Kết thúc Onboarding (default):**
- Khi người chơi assign thành công 1 NPC (bước tutorial “Assign” hoàn thành), **hoặc**
- Hết **Spring Day 2** (fallback).

### 0.6 Fetch-before-work (mọi tiêu thụ tài nguyên) — LOCK
Áp dụng cho: **Build / Upgrade / Repair / ForgeAmmo**.
1) Worker phải **fetch đủ input** trước khi bắt đầu work tick.
2) Nguồn fetch ưu tiên:
   - (1) **Warehouse** (nếu chứa loại đó và đủ)  
   - (2) **HQ** (nếu đủ)  
   - (3) **Local storages** phù hợp (Lumber/Quarry/Iron/Farmhouse/Forge/Armory)  
3) Tie-break: path distance ngắn hơn → nếu bằng nhau → ID nhỏ hơn.
4) Không nguồn nào đủ → job `Blocked: InsufficientResource`.

### 0.7 Ammo Logistics — LOCK
- **Towers cần Ammo để bắn**. Ammo = 0 → `OutOfAmmo` và ngừng bắn.
- **Forge** sản xuất Ammo từ (Iron + Wood).  
- **Armory** là kho Ammo trung tâm + cấp đạn cho towers.
- **Armory worker** (khác Warehouse worker):
  - `HaulAmmo`: Forge → Armory
  - `ResupplyTower`: Armory → Tower

### 0.8 Resupply Request Threshold (25%) — LOCK
- Tower phát `NeedsAmmo` request khi **AmmoCurrent <= 25% AmmoMax**.
- Armory ưu tiên cấp đạn theo thứ tự:
  1) Tower có `NeedsAmmo` active  
  2) AmmoCurrent thấp hơn  
  3) Đang combat (enemy trong range hoặc vừa bắn trong X giây)  
  4) Path distance ngắn hơn  
  5) towerId nhỏ hơn

### 0.9 HQ JobSet — LOCK
HQ worker **chỉ** làm:
- `Build / Move / Demolish / Upgrade / Repair`
- `HaulBasic` (core resources)
HQ worker **không** làm: Harvest, Farm, ForgeAmmo, HaulAmmo, ResupplyTower.

---

## 1) Starting Setup (Base Tutorial Start) — LOCK

Người chơi bắt đầu với:
- **B-00 HQ**: 1 NPC assigned (HQ Worker)
- **B-01 House L1 x2** (mỗi nhà chứa 2) → tổng housing 4
- **B-03 Farmhouse L1 + zone**: 1 NPC assigned (Farmer)
- **B-04 Lumber Camp L1**: 1 NPC assigned (Woodcutter)
- **T-00 Arrow Tower L1**: **Ammo full** (không cần Armory lúc start)

Tổng NPC start: **3 NPC** (1 HQ + 1 Farm + 1 Lumber).  
Các NPC mới sinh ra sẽ là **Unassigned**, game sẽ hướng dẫn người chơi tự assign.

**Hướng dẫn đầu game (gợi ý, không khóa nội dung UI):**
- Khi housing còn trống (4 slots) → sau một thời gian spawn thêm 1 NPC → hiển thị notification “Dân mới đã đến” + tutorial assign.

---

## 2) Unlock Pacing (Run: 2 năm) — DEFAULT LOCKED

> Đây là “nhịp mở khóa” để ship v0.1. Có thể tune nhưng **mốc logic** nên giữ.

### Year 1
- **Spring (Dev)**: Start set + xây Road + mở Warehouse/Forge/Armory/Builder Hut (khuyến nghị)
- **Summer (Dev)**: mở Quarry, Iron Hut, upgrade tower L2
- **Autumn (Defend)**: wave tăng; yêu cầu ammo supply ổn định
- **Winter (Defend)**: boss Y1

### Year 2
- Mở tower advanced (Cannon/Frost/Fire/Sniper) + enemy elites + boss Y2

**Mốc unlock cụ thể (default):**
- Warehouse / Forge / Armory / Builder Hut: **Y1 Spring Day 2** (hoặc khi hoàn thành tutorial “Assign”)
- Quarry / Iron Hut: **Y1 Summer Day 1**
- Cannon + Frost: **Y1 Summer Day 3**
- Fire + Sniper: **Y2 Spring Day 1**

---

## 3) Notifications Catalog (Base) — LOCKED BEHAVIOR

### 3.1 UI Layout (LOCK)
- Hiển thị **chính giữa**, mép **trên cùng**, ngay **dưới Top Bar**.
- Hiển thị tối đa **3** thông báo.  
  - Quá 3 → đẩy cái **cũ nhất** ra, thêm cái mới **lên trên cùng**.
- Thứ tự: **mới nhất ở trên**.

### 3.2 Anti-spam (LOCK)
- Gộp theo `eventKey` trong 3–10s (tunable).
- Ưu tiên: Critical > Error > Warning > Info.

### 3.3 Danh sách thông báo chính (LOCKED SET)
**Placement/Build/Upgrade**
- Không thể đặt (chồng lấn / nước / entry-road)
- Thiếu tài nguyên (build/upgrade/repair)
- Công trình chờ tài nguyên (builder blocked)

**Assignment/Workforce**
- Dân mới đã đến (NPC mới Unassigned)
- NPC chưa phân công (n quá ngưỡng)
- Thiếu nhân lực tại Workplace (Farm ưu tiên Warning)

**Logistics/Storage**
- Kho cục bộ đầy (Farm/Lumber/Quarry/Iron/Forge)
- Nhà kho gần đầy
- Thiếu người vận chuyển (Warehouse)

**Ammo/Defense**
- Thiếu nguyên liệu rèn (Forge thiếu Iron/Wood)
- Kho vũ khí thiếu đạn (Armory low)
- Tháp cần tiếp đạn (<=25%)
- Tháp hết đạn

**Season/Combat**
- Sắp sang Autumn/Winter
- Wave đến / Boss xuất hiện
- HQ đang bị tấn công
- Tower bị phá

---

# 4) BUILDINGS — FULL SPECS (LOCKED v0.1)

> Format: ID / Tên / Tier / Footprint / Entry / Workplace / Storage / Costs / Upgrades / Tooltip / Key Notes

## B-00 — HQ (Headquarters)
- **Category:** Core
- **Tier:** Start
- **Footprint:** 3x3
- **Entry:** 1 cạnh, rotate được
- **Workplace Slots:** 1 (start) | L2: 2 (optional, default OFF) | L3: 2
- **Auto-fill:** ON trong onboarding, OFF sau đó
- **JobSet (LOCK):** Build/Move/Demolish/Upgrade/Repair + HaulBasic
- **Storage:** Multi (core only, **không Ammo**)  
  - Cap default: Wood 120, Stone 120, Iron 80, Food 120
- **Cost:** start free
- **Upgrade (optional):**
  - HQ L2: +cap +10%, +HP +10%
  - HQ L3: +cap +10%, +HP +10%
- **Tooltip:**  
  - Title: `Nhà chính (HQ)`  
  - Body: `Trung tâm căn cứ. Nếu bị phá hủy bạn sẽ thua. Có thể chứa tài nguyên cơ bản và thực hiện xây/sửa cơ bản.`
- **Notes:** HQ worker là “đội đa dụng” đầu game, nhưng không thay thế các workplace chuyên môn.

---

## B-01 — House L1/L2/L3
- **Category:** Core
- **Tier:** Start (L1), Y1 mid (L2), Y2 (L3)
- **Footprint:** 2x2
- **Workplace:** None
- **Housing:** L1 +2 | L2 +3 | L3 +4
- **Cost:**
  - L1: 20W 10S
  - Upgrade to L2: 25W 20S
  - Upgrade to L3: 30W 30S 5I
- **Tooltip:** `Tăng chỗ ở. Khi còn chỗ trống và đủ Food, dân mới sẽ xuất hiện.`
- **Notes:** Dân mới spawn ra **Unassigned** → notify + tutorial assign.

---

## B-02 — Road
- **Category:** Core
- **Tier:** Start
- **Footprint:** 1 cell (orthogonal)
- **Cost:** 1W mỗi cell (default)
- **Rule:** Placement N/E/S/W only, no overlap.
- **Tooltip:** `Kết nối công trình. Entry của công trình phải gần đường (1 ô).`

---

## B-03 — Farmhouse L1/L2/L3 + Farm Zone
- **Category:** Economy
- **Tier:** Start
- **Footprint:** 2x2 + zone (tile layer riêng)
- **Workplace Slots:** L1 1 | L2 2 | L3 3
- **JobSet:** Farm
- **Local Storage:** Food  
  - Cap: L1 30 | L2 60 | L3 90
- **Production (default):**
  - L1: Harvest 6 Food / 6s
  - L2: Harvest 8 Food / 6s
  - L3: Harvest 10 Food / 6s
- **CarryAmount (Food per trip):**
  - L1 6 | L2 8 | L3 10
- **Costs:**
  - Build L1: 30W 10S
  - Upgrade L2: 40W 20S
  - Upgrade L3: 50W 30S 5I
- **Tooltip:** `Tạo Food khi có nông dân. Food chỉ tăng khi được mang về kho.`
- **Notes:** Nếu local storage đầy → Warehouse hauler nên lấy về (core logistics).

---

## B-04 — Lumber Camp L1/L2/L3
- **Category:** Economy
- **Tier:** Start
- **Footprint:** 2x2
- **Workplace Slots:** L1 1 | L2 2 | L3 3
- **JobSet:** HarvestWood
- **Local Storage:** Wood  
  - Cap: L1 40 | L2 80 | L3 120
- **Harvest (default):**
  - L1: +3 Wood / 3s
  - L2: +4 Wood / 3s
  - L3: +5 Wood / 3s
- **CarryAmount:** 3/4/5
- **Costs:**
  - Build L1: 25W 5S
  - Upgrade L2: 35W 15S
  - Upgrade L3: 45W 25S 5I
- **Tooltip:** `Thu thập gỗ từ cây. Gỗ chỉ tăng khi mang về trại.`

---

## B-05 — Quarry L1/L2/L3
- **Category:** Economy
- **Tier:** Y1 mid
- **Footprint:** 2x2
- **Workplace Slots:** 1/2/3
- **JobSet:** HarvestStone
- **Local Storage:** Stone cap 40/80/120
- **Harvest:** +3/+4/+5 Stone / 3s
- **CarryAmount:** 3/4/5
- **Costs:**
  - Build L1: 25W 15S
  - Upgrade L2: 35W 30S
  - Upgrade L3: 45W 45S 5I
- **Tooltip:** `Thu đá từ mỏ. Đá chỉ tăng khi mang về.`

---

## B-06 — Iron Hut L1/L2/L3
- **Category:** Economy
- **Tier:** Y1 mid
- **Footprint:** 2x2
- **Workplace Slots:** 1/2/2 (default cap 2 để tránh snowball)
- **JobSet:** HarvestIron
- **Local Storage:** Iron cap 30/60/90
- **Harvest:** +2/+3/+4 Iron / 4s
- **CarryAmount:** 2/3/4
- **Costs:**
  - Build L1: 30W 20S
  - Upgrade L2: 40W 40S
  - Upgrade L3: 50W 60S 10I
- **Tooltip:** `Thu sắt. Sắt chỉ tăng khi mang về.`

---

## B-07 — Warehouse L1/L2/L3 (Nhà kho)
- **Category:** Logistics
- **Tier:** Y1 early
- **Footprint:** 2x2
- **Workplace Slots:** 1/2/3
- **JobSet:** HaulBasic (Wood/Stone/Iron/Food)
- **Storage:** Multi (core only; **NO Ammo**)  
  - Cap per resource: L1 300 | L2 600 | L3 1000
- **Costs:**
  - Build L1: 30W 20S
  - Upgrade L2: 50W 40S
  - Upgrade L3: 70W 60S 10I
- **Tooltip:** `Tập trung tài nguyên. Người vận chuyển sẽ mang tài nguyên từ các công trình về đây.`
- **Notes:** Warehouse hauler **không** chở Ammo.

---

## B-08 — Forge L1/L2/L3 (Lò rèn)
- **Category:** Logistics (Ammo production)
- **Tier:** Y1 early
- **Footprint:** 2x2
- **Workplace Slots:** 1/2/2 (default)
- **JobSet:** ForgeAmmo (Smith)
- **Input Recipe (default):**
  - 2 Iron + 1 Wood → 10 Ammo
- **Craft Time (default):**
  - L1: 8s / batch
  - L2: 7s / batch
  - L3: 6s / batch
- **Local Storage:** Ammo cap 50/100/150
- **Costs:**
  - Build L1: 35W 25S 5I
  - Upgrade L2: 50W 40S 10I
  - Upgrade L3: 70W 60S 20I
- **Tooltip:** `Sản xuất đạn dược từ tài nguyên. Đạn chỉ tăng khi chế tạo và đưa vào kho.`
- **Notes:** Forge local ammo đầy → Armory hauler cần lấy đi.

---

## B-09 — Armory L1/L2/L3 (Kho vũ khí)
- **Category:** Logistics (Ammo distribution)
- **Tier:** Y1 early
- **Footprint:** 2x2
- **Workplace Slots:** 1/2/3
- **JobSet:** HaulAmmo + ResupplyTower
- **Storage:** Ammo only  
  - Cap: L1 300 | L2 600 | L3 1000
- **Resupply Threshold:** 25% AmmoMax (LOCK)
- **Resupply Amount per trip (default):**
  - L1: 20 Ammo
  - L2: 30 Ammo
  - L3: 40 Ammo
- **Costs:**
  - Build L1: 40W 30S 10I
  - Upgrade L2: 60W 50S 20I
  - Upgrade L3: 90W 80S 35I
- **Tooltip:** `Chứa đạn và cấp đạn cho tháp. Người vận chuyển vũ khí sẽ lấy đạn từ lò rèn và tiếp đạn cho tháp.`

---

## B-10 — Builder Hut L1/L2/L3 (Lều thợ xây)
- **Category:** Utility
- **Tier:** Y1 early
- **Footprint:** 2x2
- **Workplace Slots:** **L1 1 | L2 2 | L3 3** (LOCK)
- **JobSet:** Build/Move/Demolish/Upgrade/Repair
- **Costs:**
  - Build L1: 25W 15S
  - Upgrade L2: 40W 30S
  - Upgrade L3: 60W 45S 10I
- **Tooltip:** `Thợ xây thực hiện xây dựng, nâng cấp và sửa chữa. Mọi hành động sẽ cần thợ xây và tài nguyên được mang tới.`
- **Notes:** Builder fetch resources theo rule 0.6.

---

## B-11 — Bonfire/Heater L1/L2/L3
- **Category:** Utility (Winter mitigation)
- **Tier:** Y1 mid
- **Footprint:** 2x2
- **Workplace:** none
- **Effect (default):** trong radius 6/7/8, giảm Winter penalty 10%/15%/20% (tunable)
- **Costs:**
  - Build L1: 20W 20S
  - Upgrade L2: 30W 35S 5I
  - Upgrade L3: 45W 55S 10I
- **Tooltip:** `Giảm ảnh hưởng mùa đông trong phạm vi.`

---

# 5) DEFENSE TOWERS — FULL SPECS (LOCKED v0.1)

> Tất cả tower: footprint 2x2, không workplace, có Ammo internal.  
> Tower chỉ bắn khi AmmoCurrent > 0.

## T-00 — Arrow Tower L1/L2/L3
- **Tier:** Start (L1), Y1 Summer (L2), Y2 (L3)
- **Role:** generalist
- **AmmoMax:** 60/80/100
- **AmmoPerShot:** 1
- **RequestThreshold:** 25% AmmoMax (LOCK)
- **Range:** 5/5/6
- **ROF:** 1.0s / 0.9s / 0.85s
- **Damage:** 6 / 7 / 8
- **Costs:**
  - Build L1: 20W 20S
  - Upgrade L2: 25W 35S 5I
  - Upgrade L3: 30W 50S 10I
- **Tooltip:** `Tháp cơ bản. Cần đạn để bắn.`

## T-01 — Cannon Tower L1/L2/L3
- **Tier:** Y1 Summer Day 3+
- **Role:** anti-bruiser/armor
- **AmmoMax:** 50/70/90
- **AmmoPerShot:** 2
- **Range:** 4/4/5
- **ROF:** 1.6s / 1.5s / 1.4s
- **Damage:** 18 / 22 / 26
- **Costs:**
  - Build L1: 20W 40S 5I
  - Upgrade L2: 30W 60S 10I
  - Upgrade L3: 40W 80S 20I
- **Tooltip:** `Sát thương lớn, bắn chậm. Tốn nhiều đạn.`

## T-02 — Frost Tower L1/L2/L3
- **Tier:** Y1 Summer Day 3+
- **Role:** control
- **AmmoMax:** 60/80/100
- **AmmoPerShot:** 1
- **Range:** 5/5/6
- **ROF:** 1.2s / 1.1s / 1.0s
- **Damage:** 3 / 4 / 5
- **Slow:** 30%/35%/40% trong 2.0s
- **Costs:**
  - Build L1: 30W 30S 5I
  - Upgrade L2: 40W 50S 10I
  - Upgrade L3: 55W 70S 20I
- **Tooltip:** `Làm chậm kẻ địch. Cần đạn để bắn.`

## T-03 — Fire Tower L1/L2/L3
- **Tier:** Y2 Spring+
- **Role:** anti-swarm AoE
- **AmmoMax:** 50/70/90
- **AmmoPerShot:** 2
- **Range:** 4/4/5
- **ROF:** 1.4s / 1.3s / 1.2s
- **Damage:** 8 / 10 / 12 (AoE nhỏ)
- **Burn:** 2 dmg/s trong 2s (L3: 3s)
- **Costs:**
  - Build L1: 40W 40S 10I
  - Upgrade L2: 55W 65S 20I
  - Upgrade L3: 70W 90S 35I
- **Tooltip:** `Đốt đám đông. Tốn nhiều đạn.`

## T-04 — Sniper Tower L1/L2/L3
- **Tier:** Y2 Spring+
- **Role:** anti-elite/boss
- **AmmoMax:** 40/55/70
- **AmmoPerShot:** 3
- **Range:** 8/9/10
- **ROF:** 2.2s / 2.0s / 1.8s
- **Damage:** 40 / 50 / 60
- **Costs:**
  - Build L1: 30W 50S 15I
  - Upgrade L2: 45W 75S 25I
  - Upgrade L3: 60W 110S 40I
- **Tooltip:** `Bắn xa, diệt mục tiêu mạnh. Rất tốn đạn.`

---

# 6) ENEMIES — FULL SPECS (LOCKED v0.1)

> Các chỉ số là default, tune được. Hành vi là **LOCK**.

## E-00 Swarmling
- **Role:** swarm, tiêu hao ammo
- **HP:** 10 (Y1) | 14 (Y2)
- **Speed:** 1.35
- **Damage:** 2 / hit
- **AttackInterval:** 1.1s
- **Behavior (LOCK):** chạy thẳng theo path tới HQ; số lượng lớn.

## E-01 Raider
- **Role:** baseline melee
- **HP:** 25 | 32
- **Speed:** 1.0
- **Damage:** 5
- **AttackInterval:** 1.2s
- **Behavior:** ưu tiên HQ, có thể đánh tower nếu blocking đường.

## E-02 Bruiser
- **Role:** tank
- **HP:** 80 | 110
- **Speed:** 0.75
- **Damage:** 10
- **AttackInterval:** 1.6s
- **Behavior:** đi HQ; chịu đòn tốt.

## E-03 Archer (Ranged)
- **Role:** chip damage tower
- **HP:** 20 | 26
- **Speed:** 0.95
- **Range:** 4.5
- **Damage:** 4
- **AttackInterval:** 1.3s
- **Behavior (LOCK):**
  - Nếu có tower trong range: dừng và bắn tower (ưu tiên tower gần nhất)
  - Không có: tiếp tục đi HQ

## E-04 Sapper
- **Role:** phá tuyến phòng thủ
- **HP:** 35 | 45
- **Speed:** 1.05
- **Damage:** 14 (vs building)
- **AttackInterval:** 1.5s
- **Behavior (LOCK):**
  - Nếu gặp tower trong “sapper radius”: ưu tiên phá tower trước
  - Nếu không: đi HQ

## Elite Variants (Year 2)
### E-05 Elite Raider
- HP 45, Armor nhẹ (giảm 1 dmg mỗi hit, tune)
- Spawn: Y2 Autumn/Winter

### E-06 Elite Bruiser
- HP 150, Regen 1 HP/s khi không bị bắn 2s
- Spawn: Y2 Winter

## Bosses
### E-B1 Siege Brute (Y1 Winter Day 4)
- HP 600
- Speed 0.6
- Damage 25
- Behavior: đi HQ, “test tuyến”

### E-B2 Frost Warlord (Y2 Winter Day 4)
- HP 900
- Speed 0.7
- Damage 20
- Aura: slow 15% trong radius
- Adds: spawn Swarmling theo nhịp (tune)

---

# 7) KINGS — FULL SPECS (LOCKED v0.1)

> King = đổi luật + tradeoff rõ, hiển thị tooltip.

## K-00 Roadwright
- Effect: Road cost -20%
- Tradeoff: Tower build cost +10% Stone
- Tooltip: `Đường rẻ hơn, nhưng phòng thủ tốn đá hơn.`

## K-01 Harvester
- Effect: Harvest yield +15%
- Tradeoff: Local storage capacity -10%
- Tooltip: `Thu nhiều hơn, nhưng kho cục bộ nhỏ hơn.`

## K-02 Warden
- Effect: Towers trong radius HQ +15% dmg
- Tradeoff: Towers ngoài radius HQ -10% dmg
- Tooltip: `Phòng thủ mạnh gần trung tâm, yếu hơn khi mở rộng.`

## K-03 Settler
- Effect: House housing +1
- Tradeoff: Food consumption +10%
- Tooltip: `Dân đông hơn, nhưng tốn lương thực hơn.`

## K-04 Mason
- Effect: Building HP +15%
- Tradeoff: Repair cost +10% Stone
- Tooltip: `Công trình bền hơn, nhưng sửa tốn đá hơn.`

## K-05 Scout
- Effect: Forecast wave trước 1 ngày + hiển thị hướng spawn
- Tradeoff: Spawn directions +1
- Tooltip: `Biết trước nguy hiểm, nhưng bị tấn công từ nhiều hướng hơn.`

---

# 8) Appendices — Quick Reference Tables

## 8.1 Workplace JobSets (LOCK)
- HQ: Build/Move/Demolish/Upgrade/Repair + HaulBasic
- Farmhouse: Farm
- Lumber: HarvestWood
- Quarry: HarvestStone
- Iron Hut: HarvestIron
- Warehouse: HaulBasic
- Forge: ForgeAmmo
- Armory: HaulAmmo + ResupplyTower
- Builder Hut: Build/Move/Demolish/Upgrade/Repair

## 8.2 Ammo Flow (LOCK)
Forge crafts Ammo → Forge local ammo → Armory worker hauls → Armory storage → Resupply Tower (<=25%).

## 8.3 Start Checklist (LOCK)
Start: HQ(1 worker) + 2 Houses + Farm(1) + Lumber(1) + Arrow Tower(full ammo).

---

## 9) TODO for v0.2 (không khóa)
- Tower priority UI (player ưu tiên resupply)
- More biomes / more kings
- Wall/Trap system (nếu muốn)
