# DELIVERABLE C — BALANCE TABLES (LOCKED v0.1)

> Mục tiêu: cung cấp **bộ số liệu v0.1 đủ để implement + chơi được** (vertical slice tới hết Winter Y1, và chạy được tới Winter Y2 theo pacing Deliverable B).  
> Các số dưới đây là **LOCKED cho v0.1** theo nguyên tắc: dễ hiểu, ít biến, tránh hard-lock, vẫn tạo áp lực logistics/ammo.

---

## 0) Assumptions & Conventions (LOCKED)

### 0.1 Time (từ Deliverable B — Option B)
- Dev (Spring/Summer): **180s/day**
- Defend (Autumn/Winter): **120s/day**

### 0.2 Storage / Logistics Model (LOCKED)
- Resource tăng **chỉ khi worker deposit** vào kho (local/HQ/Warehouse/Armory/Forge).
- Warehouse **không chứa Ammo**.
- Ammo flow: **Forge → Armory → Towers**.
- Tower tạo request **NeedsAmmo** khi **AmmoCurrent ≤ 25% AmmoMax**, Armory ưu tiên resupply.

### 0.3 Carry & Action Granularity (LOCKED defaults)
Để ship nhanh, dùng quy ước:
- Mỗi NPC mang **1 loại resource/lần**.
- Mỗi chuyến mang theo `CarryAmount` theo role & tier.
- Các job “sản xuất” theo kiểu **chunk** (đơn vị gói) để giảm chạy đi chạy lại.

---

## 1) Global Tunables (LOCKED v0.1 defaults)

### 1.1 NPC Movement (gợi ý, để tune sau)
- BaseMoveSpeed = 1.0
- RoadSpeedMultiplier = 1.3

### 1.2 Carry Amount (LOCKED)
| Role | CarryAmount L1 | CarryAmount L2 | CarryAmount L3 |
|---|---:|---:|---:|
| Harvester (Wood/Stone/Iron) | 6 | 8 | 10 |
| Farmer (Food) | 6 | 8 | 10 |
| HaulBasic (Warehouse) | 10 | 14 | 18 |
| Builder (fetch materials) | 8 | 12 | 16 |
| Smith (input fetch) | 8 | 12 | 16 |
| Armory (haul/resupply ammo) | 40 ammo | 60 ammo | 80 ammo |

### 1.3 Local Storage Caps (LOCKED)
| Building | Resource | L1 | L2 | L3 |
|---|---|---:|---:|---:|
| Farmhouse | Food | 30 | 60 | 90 |
| Lumber Camp | Wood | 40 | 80 | 120 |
| Quarry | Stone | 40 | 80 | 120 |
| Iron Hut | Iron | 30 | 60 | 90 |
| Forge | Ammo | 50 | 100 | 150 |
| Armory | Ammo | 300 | 600 | 1000 |
| Warehouse | Wood/Stone/Iron/Food | 300 each | 600 each | 1000 each |
| HQ (core only) | Wood/Stone/Iron/Food | 120 each | 180 each | 240 each |

> HQ **không chứa Ammo** trong v0.1 để rõ pipeline.

---

## 2) Economy Rates — Gather/Farm (LOCKED v0.1)

### 2.1 Harvest Loop Model
Mỗi lần harvest tạo ra một “bundle” và worker mang về local storage.
- `HarvestTime` = thời gian đứng tại node/zone để thu
- `HarvestYield` = lượng tạo ra cho 1 lần harvest (bundle)
- Worker mang bundle về local storage (bị giới hạn bởi CarryAmount, nhưng v0.1 set sao cho yield ≤ carry để dễ)

### 2.2 Farmhouse (Food)
| Tier | HarvestTime | Yield / Harvest | Notes |
|---|---:|---:|---|
| L1 | 6s | 6 Food | ổn định đầu game |
| L2 | 6s | 8 Food | tăng theo yield |
| L3 | 6s | 10 Food | |

### 2.3 Lumber Camp (Wood)
| Tier | HarvestTime | Yield / Harvest |
|---|---:|---:|
| L1 | 4s | 6 Wood |
| L2 | 4s | 8 Wood |
| L3 | 4s | 10 Wood |

### 2.4 Quarry (Stone)
| Tier | HarvestTime | Yield / Harvest |
|---|---:|---:|
| L1 | 5s | 6 Stone |
| L2 | 5s | 8 Stone |
| L3 | 5s | 10 Stone |

### 2.5 Iron Hut (Iron)
| Tier | HarvestTime | Yield / Harvest |
|---|---:|---:|
| L1 | 6s | 4 Iron |
| L2 | 6s | 6 Iron |
| L3 | 6s | 8 Iron |

> Iron cố ý “chậm” để ammo là nút thắt chiến lược, nhưng không kẹt vì start tower đã có full ammo.

---

## 3) Logistics Jobs — HaulBasic / Armory Haul+Resupply (LOCKED v0.1)

### 3.1 Warehouse HaulBasic
- Mục tiêu: giảm tình trạng **local storage đầy** và gom resource về 1 chỗ.
- Hauler chọn nguồn theo ưu tiên:
  1) Local storage **đang đầy ≥ 80%**
  2) Local storage **gần đầy ≥ 60%**
  3) Local storage có resource bất kỳ
- Chuyến đi:
  - Pickup tối đa `CarryAmount (HaulBasic)` hoặc đến khi nguồn hết
  - Deposit vào Warehouse

### 3.2 Armory HaulAmmo
- Điều kiện chạy:
  - Nếu Forge ammo storage ≥ 20% cap hoặc Armory ammo < 80% cap → ưu tiên haul
- Pickup: từ Forge (Ammo), deposit Armory

### 3.3 Armory ResupplyTower
- Trigger: tower `NeedsAmmo` active khi ≤ 25% ammo max
- Pickup: từ Armory
- Delivery: đến tower
- Delivery amount:
  - **Min**: 30 ammo
  - **Max**: min(ArmoryCarryAmount, AmmoMax - AmmoCurrent)
  - Nếu Armory thiếu, giao được bao nhiêu giao

---

## 4) Builder — Build/Upgrade/Repair Costs & Timings (LOCKED v0.1)

### 4.1 Build/Upgrade model
- Builder thực hiện theo **work chunks**:
  - Mỗi chunk = `BuildWorkChunkTime` giây “làm việc tại công trình”
  - Mỗi chunk tiêu thụ một phần materials (đã fetch trước)
- v0.1 dùng số đơn giản: build = 1–3 chunks tùy công trình, upgrade tăng chunks.

**BuildWorkChunkTime = 6s**  
**RepairWorkChunkTime = 4s**

### 4.2 Repair model
- Repair tiêu resource theo % HP đã mất:
  - RepairCost = 30% * BuildCost * (HPMissing / MaxHP) (làm tròn lên theo bundle)
- RepairWork: mỗi 4s hồi 15% HP (tunable)

### 4.3 Costs — Buildings (LOCKED)
> Các cost này giả định người chơi có Lumber+Farm start, và unlock Quarry/Iron sau Summer.

#### Core
| Building | Build Cost | Build Chunks | Base HP |
|---|---|---:|---:|
| House L1 | 20 Wood | 2 | 150 |
| House L2 | 30 Wood + 10 Stone | 3 | 220 |
| House L3 | 40 Wood + 20 Stone | 4 | 300 |
| Road (per tile) | 1 Wood | — | — |

#### Economy
| Building | L1 Build | Chunks | L2 Upgrade | Chunks | L3 Upgrade | Chunks | Base HP |
|---|---|---:|---|---:|---|---:|---:|
| Farmhouse | 25 Wood | 2 | 30 Wood + 10 Stone | 3 | 40 Wood + 20 Stone | 4 | 240 |
| Lumber Camp | 20 Wood | 2 | 25 Wood + 10 Stone | 3 | 35 Wood + 20 Stone | 4 | 220 |
| Quarry | 25 Wood + 20 Stone | 3 | 30 Wood + 30 Stone | 4 | 40 Wood + 40 Stone | 5 | 260 |
| Iron Hut | 30 Wood + 20 Stone | 3 | 35 Wood + 30 Stone | 4 | 45 Wood + 40 Stone | 5 | 260 |

#### Logistics / Utility
| Building | L1 Build | Chunks | L2 Upgrade | Chunks | L3 Upgrade | Chunks | Base HP |
|---|---|---:|---|---:|---|---:|---:|
| Warehouse | 30 Wood + 20 Stone | 3 | 40 Wood + 40 Stone | 4 | 60 Wood + 60 Stone | 5 | 300 |
| Forge | 25 Wood + 20 Stone + 10 Iron | 3 | 35 Wood + 30 Stone + 20 Iron | 4 | 50 Wood + 40 Stone + 30 Iron | 5 | 300 |
| Armory | 25 Wood + 30 Stone + 10 Iron | 3 | 35 Wood + 45 Stone + 20 Iron | 4 | 50 Wood + 60 Stone + 30 Iron | 5 | 340 |
| Builder Hut | 30 Wood + 10 Stone | 3 | 40 Wood + 20 Stone | 4 | 55 Wood + 35 Stone | 5 | 280 |
| Bonfire/Heater | 20 Wood + 10 Stone | 2 | 30 Wood + 20 Stone | 3 | 40 Wood + 30 Stone | 4 | 240 |

---

## 5) Forge — Ammo Recipe & Production (LOCKED v0.1)

### 5.1 Recipe (LOCKED)
- **Input:** 2 Iron + 1 Wood  
- **Output:** 10 Ammo  
- **CraftTime:** 6s (work at Forge)

### 5.2 Smith loop
1) Fetch đủ input cho **1 craft** (2 Iron + 1 Wood) từ Warehouse / local storages.
2) Craft 6s
3) Deposit 10 Ammo vào Forge ammo storage

### 5.3 Target throughput sanity check
- 1 smith lý thuyết: 10 ammo / 6s ≈ 100 ammo/min (chưa tính fetch)
- Thực tế: 40–70 ammo/min (tùy distance)
- Mục tiêu: Y1 mid cung cấp đủ cho 2–3 towers mà không chain OutOfAmmo.

---

## 6) Towers — Stats & Costs (LOCKED v0.1)

### 6.1 Shared tower rules (LOCKED)
- Towers require Ammo to shoot.
- `NeedsAmmo` request when `AmmoCurrent ≤ 25% AmmoMax`.
- If `AmmoCurrent = 0`: stop shooting.

### 6.2 Costs + Base Stats
#### Arrow Tower
| Tier | Build Cost | Chunks | HP | Range | ROF | Damage | AmmoMax | Ammo/Shot |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| L1 | 25 Wood + 10 Stone | 2 | 260 | 5 | 1.0s | 6 | 90 | 1 |
| L2 | +20 Wood + 20 Stone | 3 | 320 | 6 | 1.0s | 7 | 110 | 1 |
| L3 | +30 Wood + 30 Stone + 10 Iron | 4 | 380 | 6 | 0.9s | 8 | 130 | 1 |

#### Cannon Tower (unlocks Summer Y1 Day 3)
| Tier | Build Cost | Chunks | HP | Range | ROF | Damage | AmmoMax | Ammo/Shot | Notes |
|---|---|---:|---:|---:|---:|---:|---:|---:|---|
| L1 | 20 Wood + 40 Stone + 10 Iron | 3 | 320 | 4 | 1.6s | 18 | 80 | 2 | anti-bruiser |
| L2 | +20 Wood + 40 Stone + 15 Iron | 4 | 380 | 4 | 1.5s | 22 | 95 | 2 | |
| L3 | +30 Wood + 50 Stone + 20 Iron | 5 | 440 | 5 | 1.5s | 26 | 110 | 2 | |

#### Frost Tower (unlocks Summer Y1 Day 3)
| Tier | Build Cost | Chunks | HP | Range | ROF | Damage | Slow | AmmoMax | Ammo/Shot |
|---|---|---:|---:|---:|---:|---:|---|---:|---:|
| L1 | 25 Wood + 35 Stone + 10 Iron | 3 | 300 | 5 | 1.3s | 4 | 30% for 2s | 90 | 1 |
| L2 | +20 Wood + 40 Stone + 15 Iron | 4 | 360 | 6 | 1.3s | 5 | 35% for 2s | 110 | 1 |
| L3 | +30 Wood + 50 Stone + 20 Iron | 5 | 420 | 6 | 1.2s | 6 | 40% for 2s | 130 | 1 |

#### Fire Tower (unlocks Y2 Spring Day 1)
| Tier | Build Cost | Chunks | HP | Range | ROF | Damage | AoE | DOT | AmmoMax | Ammo/Shot |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| L1 | 30 Wood + 45 Stone + 20 Iron | 4 | 340 | 5 | 1.5s | 8 | small | 3 dmg/2s | 90 | 2 |
| L2 | +25 Wood + 55 Stone + 25 Iron | 5 | 400 | 5 | 1.4s | 10 | small | 4 dmg/2s | 110 | 2 |
| L3 | +35 Wood + 70 Stone + 35 Iron | 6 | 460 | 6 | 1.4s | 12 | med | 5 dmg/2s | 130 | 2 |

#### Sniper Tower (unlocks Y2 Spring Day 2)
| Tier | Build Cost | Chunks | HP | Range | ROF | Damage | AmmoMax | Ammo/Shot | Notes |
|---|---|---:|---:|---:|---:|---:|---:|---:|---|
| L1 | 25 Wood + 40 Stone + 30 Iron | 4 | 300 | 8 | 2.2s | 40 | 70 | 3 | anti-elite/boss |
| L2 | +25 Wood + 55 Stone + 35 Iron | 5 | 360 | 8 | 2.1s | 48 | 85 | 3 | |
| L3 | +35 Wood + 70 Stone + 45 Iron | 6 | 420 | 9 | 2.0s | 56 | 100 | 3 | |

---

## 7) Enemies — Stats (LOCKED v0.1)

### 7.1 Baseline scaling by year
- Year 2 enemies get:
  - HP × 1.35
  - Damage × 1.25
  - Count +20% (waves)

### 7.2 Enemy sheets (Year 1 base)
| Enemy | HP | Speed | Damage to HQ | Damage to Buildings | Notes |
|---|---:|---:|---:|---:|---|
| Swarmling | 18 | 1.25 | 2 | 1 | swarm pressure, ammo drain |
| Raider | 45 | 1.0 | 5 | 3 | baseline melee |
| Bruiser | 120 | 0.8 | 12 | 7 | tank |
| Archer | 35 | 0.95 | 4 | 2 | ranged; prefers towers in range |
| Sapper | 60 | 1.05 | 6 | 14 | ưu tiên phá towers |

### 7.3 Bosses (Year 1/2)
| Boss | Year | HP | Speed | Special | Notes |
|---|---:|---:|---:|---|---|
| Siege Brute | Y1 Winter D4 | 1200 | 0.75 | 20% dmg reduction vs Arrow | checks cannon/slow |
| Frost Warlord | Y2 Winter D4 | 1700 | 0.8 | Aura: slow towers ROF -10% within radius; summons adds | requires Fire/Sniper |

---

## 8) Waves — Counts (LOCKED v0.1)
> Đây là “starter numbers” để vertical slice sống được khi player đi đúng hướng.

### 8.1 Year 1 — Autumn (4 days)
| Day | Composition | Total |
|---|---|---:|
| A1 | 10 Swarmling + 6 Raider | 16 |
| A2 | 8 Raider + 3 Bruiser | 11 |
| A3 | 16 Swarmling + 6 Raider + 2 Archer | 24 |
| A4 | 10 Raider + 4 Bruiser + 3 Archer | 17 |

### 8.2 Year 1 — Winter (4 days)
| Day | Composition | Total |
|---|---|---:|
| W1 | 10 Raider + 5 Bruiser + 4 Archer | 19 |
| W2 | 22 Swarmling + 6 Archer | 28 |
| W3 | 8 Raider + 4 Bruiser + 2 Sapper + 3 Archer | 17 |
| W4 | Boss: Siege Brute + 12 Swarmling + 6 Raider | 19 |

### 8.3 Year 2 — Autumn/Winter (outline)
- Apply scaling rules (HP/Dmg/Count).
- Add elites:
  - Elite Raider: HP 80 (× scaling), Damage +20%
  - Elite Bruiser: HP 180 (× scaling), mild regen 1%/s out of combat

> v0.1: Year 2 counts = Year 1 counts × 1.2 (round), + elites inserted.

---

## 9) Food & Population (LOCKED v0.1)

### 9.1 Food consumption
- FoodConsumePerNPCPerDay = **2**
- If Food insufficient:
  - Spawn stops
  - v0.1: không thêm morale penalty (scope gọn)

### 9.2 NPC spawn cadence
- Max 1 NPC / 2 days (tunable)
- Requires:
  - Housing available
  - Food ≥ 6 in storage (buffer rule to avoid oscillation)

---

## 10) Notifications — thresholds (LOCKED)
- Local storage near full: ≥ 80% cap → `Kho cục bộ gần đầy`
- Local storage full: = 100% → `Kho cục bộ đầy`
- Warehouse near full: ≥ 85% any tracked resource cap → warning
- Forge ammo full: ≥ 90% → info
- Armory ammo low: ≤ 20% cap → warning
- Tower needs ammo: ≤ 25% ammo max → `Tháp cần tiếp đạn`
- Tower out of ammo: = 0 → `Tháp hết đạn` (severity depends on combat state)

---

## 11) Validation Rules (dev-checklist)
- Không resource âm, không cap vượt.
- Forge craft chỉ chạy khi input đủ.
- Builder job chỉ chạy khi materials đã fetched.
- Tower không bắn khi ammo=0.
- Armory luôn ưu tiên requests trước.

---

## 12) Known Balance Risks (v0.1) & how to tune
1) **Ammo thiếu quá sớm**  
   - Tăng Forge output (10→12) hoặc tăng AmmoMax Arrow/Cannon.
2) **Iron bottleneck quá mạnh**  
   - Tăng Iron yield L1 (4→5) hoặc giảm recipe iron (2→1) nhưng tăng craft time.
3) **Logistics quá “đi bộ”**  
   - Tăng Road multiplier (1.3→1.4) hoặc tăng CarryAmount haulers.
4) **Waves quá dễ**  
   - Tăng 10–15% count ở Autumn/Winter hoặc thêm 1 Sapper ở W3.

---

## 13) Implementation Order (vertical slice)
1) Farm/Lumber harvest + deposit + local caps  
2) HQ hauling basic + notifications (local full)  
3) Builder fetch-before-build + build chunks  
4) Forge recipe + smith loop + forge ammo cap  
5) Armory haul + resupply with 25% request priority  
6) Arrow tower ammo consumption + out-of-ammo state  
7) Autumn Y1 waves A1–A4  
8) Winter Y1 boss W4
