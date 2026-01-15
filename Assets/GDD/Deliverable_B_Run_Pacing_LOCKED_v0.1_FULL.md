# DELIVERABLE B — RUN PACING SHEET (LOCKED v0.1) — TIMING & SPEED UPDATE (B)

> Khóa theo phương án **B**: Dev day dài hơn Defend day để người chơi “cảm nhịp” rõ ràng, đủ thời gian cho quy hoạch–assign–logistics trong mùa ấm, và mùa thủ giữ căng nhưng không lê thê.  
> Bổ sung **Speed Controls**: Dev cho phép 2x/3x, Defend mặc định 1x.

---

## 0) Key Design Goals
1) Người chơi hiểu luật trong 10 phút: **Road/Entry**, **Assign NPC**, **Ammo cho tower**.  
2) Year 1: học “economy + ammo pipeline + defense cơ bản”.  
3) Year 2: mở “specialization + elite/boss + multi-direction threats”.  
4) Không kẹt vì logistics: luôn có “đường thoát” (gợi ý + notification + milestone rõ).

---

## 1) Global Run Parameters (LOCKED defaults)

### 1.1 Calendar (Days per season)
- SpringDays = 6  
- SummerDays = 6  
- AutumnDays = 4  
- WinterDays = 4  

**Tổng ngày / 1 năm:** 20 ngày  
**Tổng ngày / 2 năm:** 40 ngày

### 1.2 Day Length (LOCK — Option B)
- **Dev Seasons (Spring + Summer): `SecondsPerDay_Dev = 180s`**  
- **Defend Seasons (Autumn + Winter): `SecondsPerDay_Defend = 120s`**

**Ước lượng thời lượng 1 run (2 năm):**
- Dev days: 24 ngày × 180s = 4,320s ≈ 72 phút  
- Defend days: 16 ngày × 120s = 1,920s ≈ 32 phút  
- **Tổng:** ≈ 104 phút/run (chưa tính pause/menu)

### 1.3 Speed Controls (LOCK)
- Các mức tốc độ: **Pause / 1x / 2x / 3x**
- **Khi vào Defend (Autumn/Winter): tự động set 1x**
  - Player vẫn có thể Pause (để đặt/assign/quan sát), nhưng **không khuyến khích 2x/3x** trong Defend.
- **Trong Dev (Spring/Summer): cho phép người chơi thường xuyên dùng 2x/3x**
  - Default gợi ý: Dev mở đầu ở 1x, sau khi tutorial xong gợi ý dùng 2x/3x.

> Rationale: Dev cần thời gian cảm nhận logistics và planning; Defend cần rõ ràng, dễ đọc threat, tránh “miss” vì speed cao.

---

## 2) Starting Setup (LOCK)

### 2.1 Start Buildings
- HQ (Workplace: 1 NPC assigned)
- 2× House L1 (housing 4)
- Farmhouse L1 + basic zone (1 NPC assigned)
- Lumber Camp L1 (1 NPC assigned)
- 1× Arrow Tower (**Ammo full at start**)

### 2.2 Start NPCs
- HQ worker: 1 (Build/Repair/HaulBasic)
- Farmer: 1
- Woodcutter: 1  
**Total start:** 3 NPC assigned

### 2.3 Start Storage (tunable)
- HQ: Wood 30, Stone 20, Food 10, Iron 0, Ammo 0
- Arrow Tower: AmmoCurrent = AmmoMax (full)

---

## 3) NPC Growth + Onboarding Auto-fill (LOCK)

### 3.1 NPC Growth (baseline)
- Nếu housing còn chỗ trống và food đủ: spawn NPC theo nhịp từ từ.  
- Default: tối đa **1 NPC spawn / 2 ngày** (tunable)
- NPC spawn ra **Unassigned**
- Notification: **`Dân mới đã đến — Hãy gán vào công trình`**

### 3.2 Auto-fill (LOCK)
- Auto-fill **chỉ ON trong Onboarding Phase**, sau đó OFF toàn cục.
- Onboarding kết thúc khi:
  - người chơi **tự assign** thành công ≥ 1 NPC, hoặc
  - hết **Spring Day 2** (fallback)

> Sau onboarding: NPC mới sinh ra **không tự gán**. Người chơi tự phân bổ workforce.

---

## 4) Unlock Cadence (LOCK)

### Year 1 Unlock Schedule
- **Spring Day 1:** Road, House, Farmhouse, Lumber, Arrow Tower (start)
- **Spring Day 2:** Warehouse L1, Builder Hut L1  
- **Spring Day 3:** Forge L1, Armory L1  
- **Summer Day 1:** Quarry L1  
- **Summer Day 2:** Iron Hut L1  
- **Summer Day 3:** Cannon Tower L1, Frost Tower L1  
- **Autumn Day 1:** Upgrade L2 cho Farm/Lumber/Arrow (nếu đủ)
- **Winter:** Sniper/Fire Tower **chưa mở** (để Year 2)

### Year 2 Unlock Schedule
- **Spring Day 1:** Fire Tower L1
- **Spring Day 2:** Sniper Tower L1
- **Summer Day 1:** Builder Hut L2 (slot 2)
- **Summer Day 2:** Forge L2 / Armory L2 / Warehouse L2
- **Autumn Day 1:** Tower upgrades L3 mở (nếu có)
- **Winter:** boss + elite

---

## 5) Expected Player Milestones (target curve)

### End of Spring Y1 (Day 6)
- Population: 4–5
- Có Builder Hut L1 (hoặc HQ worker đủ làm builder)
- Farm + Lumber chạy ổn, không tụt food về 0
- Warehouse có thể đã đặt hoặc vẫn dựa HQ hauling (tùy player)
- Forge/Armory đã “thấy” trong build menu và được tutorial gợi ý

### End of Summer Y1 (Day 6)
- Population: 6–8
- Quarry bắt đầu chạy
- Có Forge + Armory hoạt động:
  - 1 smith ở Forge
  - 1 armory worker (haul+resupply)
- Defense: 2–3 Arrow towers (hoặc Arrow + Cannon chuẩn bị)

### End of Winter Y1 (Boss 1)
- Population: 8–10
- 1 Cannon + 2 Arrow (hoặc 3 Arrow + Frost)
- Stone đủ repair/tower
- Boss không “one-push” nếu đạt mốc tối thiểu

### End of Summer Y2
- Population: 12–16
- Full economy: wood/stone/iron/food ổn
- Có Fire hoặc Sniper để xử lý elite/boss
- Builder Hut L2 giúp sửa/nâng nhanh

---

## 6) Workforce Guidance (gợi ý phân bổ theo giai đoạn)

### Early Y1 (3–5 NPC)
- 1 Farmer (Farmhouse)
- 1 Woodcutter (Lumber)
- 1 HQ worker (Build/Repair/HaulBasic)
- NPC mới: gợi ý assign vào Builder Hut hoặc Warehouse

### Mid Y1 (6–8 NPC)
- 1 Builder (Builder Hut)
- 1 Farmer
- 2 HarvestWood
- 1 HaulBasic (Warehouse)
- 1 Smith (Forge)
- 1 Armory (haul ammo + resupply)

### Late Y1 (8–10 NPC)
- +1 HarvestStone (Quarry)
- +1 Reserve builder hoặc extra hauler (tùy thiếu kho cục bộ)

### Year 2 (12+ NPC)
- 2 Builders (Builder Hut L2)
- 2 Farmers
- 2–3 HarvestWood
- 2 HarvestStone
- 1–2 HarvestIron
- 2 HaulBasic
- 1 Smith (Forge L2 nếu muốn)
- 1–2 Armory (resupply load tăng)

---

## 7) Wave Pacing (composition theo day/season)

### 7.1 Year 1 — Autumn (4 days)
- D1: Swarm + Raider (nhẹ, dạy ammo tiêu)
- D2: Raider + Bruiser (dạy cannon/repair)
- D3: Swarm lớn hơn + 1 Archer (dạy placement/focus)
- D4: Raider + 2 Bruiser + Archer (mini-check)

### 7.2 Year 1 — Winter (4 days)
- D1: Bruiser mix + Archer nhiều hơn
- D2: Archer + Swarm (ép đa dạng tower)
- D3: 1 Sapper xuất hiện (dạy repair + choke)
- D4: **Boss 1: Siege Brute** + adds nhỏ

### 7.3 Year 2 — Autumn (4 days)
- Spawn directions +1 (2–3 hướng)
- D1: Elite Raider (1) + Raiders
- D2: Sapper (2) + Bruiser
- D3: Archer pack + Swarm lớn
- D4: Elite Bruiser (1) + mixed

### 7.4 Year 2 — Winter (4 days)
- D1: Elite mix (nhẹ)
- D2: Sapper + Archer combo
- D3: “Pressure wave” (ammo check)
- D4: **Boss 2: Frost Warlord** + adds + aura slow

---

## 8) Ammo Resupply Rules (LOCK recap)
- Tower phát **NeedsAmmo** khi **AmmoCurrent ≤ 25% AmmoMax**
- Armory ưu tiên tower có NeedsAmmo theo thứ tự:
  1) AmmoCurrent thấp hơn
  2) Đang combat
  3) Distance ngắn hơn
  4) TowerId nhỏ hơn

---

## 9) Tutorial & Hint Script (định hướng nhịp + speed)

### 9.1 Tutorial beats (10 phút đầu)
1) Road/Entry rule  
2) Assign NPC (auto-fill sẽ tắt)  
3) Ammo exists + threshold 25%  
4) Forge + Armory hint khi có request đầu tiên  
5) Gợi ý Speed Controls:
   - Dev: “Bạn có thể tăng 2x/3x để tiết kiệm thời gian”
   - Defend: “Về 1x để quan sát chiến đấu”

### 9.2 Notification-driven guidance
- NPC spawn: `Dân mới đã đến` + highlight Assign
- Tower ≤ 25% ammo: `Tháp cần tiếp đạn` + hint xây Forge/Armory + assign smith/armory

---

## 10) Failure Cases & Recovery (anti hard-lock)
1) Không có smith/armory → tower out-of-ammo dần, nhưng cảnh báo từ 25% và hint rõ.  
2) Kho cục bộ đầy → cảnh báo + gợi ý xây Warehouse và gán hauler.  
3) Builder blocked thiếu resource → notification hiển thị resource thiếu + nguồn kho gợi ý.

---

## 11) DoD (Deliverable B)
- Day length theo Option B + speed controls locked.
- Unlock + waves + milestones + workforce guidance rõ ràng.
- Ammo guardrails đủ để tune recipe và tower stats.
- Tutorial/hints đảm bảo người chơi đi đúng hướng.
