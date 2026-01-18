# SEASONAL BASTION
## GAME DESIGN DOCUMENT — MASTER (COMMERCIAL) — LOCKED v2.0 (VN)

> **Trạng thái:** LOCKED  
> **Mục đích:** *Single Source of Truth* cho thiết kế game để có thể **ship & public lên thị trường** (Base Premium) và kế hoạch DLC.  
> **Quy tắc:** Tất cả code / tool / AI logic **phải bám đúng tài liệu này**.  
> Thiếu thông tin → **HỎI, KHÔNG ĐƯỢC TỰ ĐOÁN**.

---

## 0) CHANGELOG & DECISION LOG

### 0.1 Changelog (v2.0)
- Nâng phạm vi từ “Phase 1” lên **thiết kế thương mại hoàn chỉnh**:
  - **Base game:** Premium **run-based roguelite** (run = **2 năm**, WIN sau Winter Year 2).
  - **DLC:** **Endless mode** (vô hạn) + logistics nâng cao + automation (policy/queue/blueprint).
- Cập nhật hệ **NPC & Jobs**:
  - NPC sinh ra ở trạng thái **Unassigned**, chỉ làm **Leisure/Inspect**.
  - Thêm **Workplace Assignment**: gán NPC vào công trình → chỉ làm JobSet của công trình đó.
  - Thêm công trình **Builder Hut** (cổng xây dựng/sửa/nâng cấp).
  - Mặc định **Auto-fill = ON trong onboarding, OFF sau đó**.
- Bổ sung **Notification System** chuẩn hóa (vị trí, số lượng, anti-spam + danh sách thông báo).

### 0.2 Decision Log (LOCKED)
- **R1 Road/Entry Connectivity:** EntryPoint là **world-space midpoint cạnh**; điều kiện “nối road” cho placement/move:
  - EntryCell và 4 ô N/E/S/W phải có ít nhất 1 road cell (**driveway length = 1**).
- **Road Placement:** chỉ đặt theo grid-orthogonal (N/E/S/W), tất cả road phải connected tới HQ entry cluster.
- **Authority:** RoadService/MapOccupancy/Season/Jobs là **source of truth**; Tilemap/prefab chỉ render.
- **Base Run:** run = **2 năm**, WIN sau Winter Year 2, LOSE khi HQ HP=0.
- **Automation nâng cao + ammo/parts chain:** **chỉ trong DLC**, không thuộc base.

---

## 1) TỔNG QUAN SẢN PHẨM

### 1.1 Định vị
**Seasonal Bastion** là game **xây dựng + phòng thủ theo mùa**, kết hợp:
- City Builder (nhẹ, thiên về quy hoạch)
- Tower Defense (theo wave, theo mùa)
- Roguelite Runs (map seed + King rule-changer + mutators)

### 1.2 Mô hình kinh doanh
- **Base:** Premium (bán 1 lần) — game **đầy đủ**, có điểm kết thúc run rõ.
- **DLC (trả phí):** Endless Mode (vô hạn) + late-game systems (logistics/automation/milestones).

### 1.3 USP (điểm bán)
1) **Season Split Loop**: mùa ấm phát triển, mùa lạnh chống wave — nhịp rõ, dễ “căng”.
2) **Road + EntryPoint Puzzle**: đặt công trình đúng luật nối road → quy hoạch thành bài toán thật.
3) **Run Variety hệ thống**: King rule-changer + biome + mutators → replayability cao.

---

## 2) CORE DESIGN PILLARS (ABSOLUTE)
1) Nhịp mùa rõ ràng (Dev vs Defend)  
2) Job-based NPC (ít micromanagement)  
3) Tính ổn định & debug-friendly (authority + determinism)  
4) Quy hoạch/chuẩn bị quyết định thắng thua (không chỉ spam tower)  
5) Nội dung vừa đủ nhưng mỗi thứ có vai trò rõ (ít nhưng sâu)  
6) Mở rộng theo Phase/DLC có kiểm soát (không phá nền)

---

## 3) GAMEPLAY LOOP — BASE RUN (2 NĂM)

### 3.1 Cấu trúc Run
- 1 run = **Year 1 + Year 2** (mỗi năm 4 mùa: Spring/Summer/Autumn/Winter).
- **WIN:** sống qua **Winter Day cuối của Year 2**.
- **LOSE:** HQ HP = 0.

### 3.2 Thời gian & Tick (Deterministic)
- Thời gian chạy theo **in-game day tick** (tunable).
- Tham số mặc định:
  - SecondsPerDay = 60
  - SpringDays = 6, SummerDays = 6, AutumnDays = 4, WinterDays = 4

### 3.3 Quy tắc mùa
- **Dev (Spring/Summer):** xây / di chuyển / nâng cấp / phá / sửa được.
- **Defend (Autumn/Winter):** base vẫn cho phép build nhưng có penalty (tunable) để giảm “kẹt người mới”.
  - (DLC Endless sẽ “pause upgrade” nghiêm ngặt.)

### 3.4 Post-season Summary (bắt buộc)
Sau mỗi mùa hiển thị bảng tóm tắt:
- Resource gained/spent
- Buildings built/upgraded
- Towers destroyed/repair count
- Population trend
- Threat forecast mùa tới

---

## 4) MAP & WORLD SYSTEM (LOCKED)

### 4.1 Layered, Data-Driven
Bản đồ được xây theo layer; logic và render tách bạch:
- Ground (walkable)
- Water (block all)
- Road (centerline logic + render 3-cell)
- Resources / Obstacles
- Buildings (prefab)
- Farm Zone (special)

### 4.2 Overlap Rules (Hard Constraints)
- Không stacking (mỗi cell 1 “loại chính” theo quy tắc)
- Water block tất cả
- Road không overlap building/resource/obstacle/farm zone
- Building không overlap road/resource/obstacle/farm zone
- Resource/obstacle/farm zone không overlap nhau

**Implementation note:** MapOccupancy/MapLayers phải truy vấn O(1).

### 4.3 Starting Settlement (START PACKAGE — BASE PREMIUM)
Mục tiêu: cho người chơi **vào game là chạy được ngay**, hiểu “assignment + mùa + phòng thủ”, nhưng vẫn còn chỗ để mở rộng hệ thống logistics về sau.

**Spawn khởi đầu (LOCKED):**
- **HQ** (có chức năng *storage* cơ bản)  
- **02 Nhà dân (House L1)**  
  - Mỗi House L1 chứa **2 NPC** ⇒ tổng housing = **4**
- **Farm/Farmhouse (L1) + farm zone cơ bản**
- **Lumber Camp (L1)**
- **01 Arrow Tower** (*khởi đầu với đầy đạn trong tháp*)

**NPC khởi đầu (LOCKED):**
- Tổng NPC ban đầu: **3**
  - 1 NPC **assigned HQ** (Workplace đặc biệt)
  - 1 NPC **assigned Farm**
  - 1 NPC **assigned Lumber Camp**
- 01 slot housing còn trống ⇒ sau một khoảng thời gian (và đủ Food) sẽ **spawn thêm 1 NPC Unassigned** để tutorial hướng dẫn phân công.

**Road khởi đầu:** tạo tối thiểu 1–2 đoạn road nối các công trình chính để người chơi hiểu rule Entry.

> Ghi chú: Builder Hut / Warehouse / Forge / Armory **không có sẵn** ở start, người chơi sẽ được hướng dẫn xây dần theo nhu cầu (thiếu builder / thiếu storage / thiếu ammo…).

### 4.4 Safe Zone & Difficulty Gradient
- Có “safe zone” gần HQ với density tài nguyên dễ hơn
- Ra xa: obstacle nhiều hơn, node hiếm hơn, threat cao hơn (tunable)

### 4.5 Determinism & Save Semantics
- Run generate base map **1 lần** theo seed
- Save gồm:
  - BaseMapData (seed + generated)
  - PlayerDelta (roads/buildings, nodes depleted, upgrades…)
- Reset run: xóa PlayerDelta, giữ base map seed.

### 4.6 Seasonal Skins
- Chỉ đổi visual theo mùa, **không đổi logic** (base).

---

## 5) NPC & POPULATION SYSTEM (CẬP NHẬT ASSIGNMENT)

### 5.1 NPC Generation (Population Growth — ASSIGNMENT READY)
NPC sinh ra theo nhịp “tự nhiên” để người chơi có thêm workforce, nhưng **NPC mới luôn là Unassigned**.

**Điều kiện sinh NPC (LOCKED):**
- Có **Housing slot trống** (tổng capacity > population)
- Có **Food** ≥ `NPCSpawnCostFood`
- Không vượt `MaxSpawnRate` (giới hạn nhịp sinh để tránh bùng nổ)

**Tham số (tunable, default gợi ý):**
- `NPCSpawnCostFood = 5`
- `SpawnCheckIntervalDays = 2` (mỗi 2 ngày kiểm tra 1 lần)
- `MaxSpawnPerCheck = 1`
- `SpawnCooldownDays = 2` (sau khi sinh xong, chờ 2 ngày)

**Hành vi khi sinh (LOCKED):**
- NPC spawn ra ở gần HQ, trạng thái **Unassigned**
- Hệ thống phát notification: `NPC mới xuất hiện` + gợi ý Assign

**Thiếu Food:**
- Không sinh NPC mới
- Có thể phát warning: `Thiếu lương thực` (cooldown dài)

> Start package đã tạo sẵn housing = 4 và population = 3, vì vậy người chơi sẽ sớm thấy NPC thứ 4 xuất hiện để học Assign.


### 5.2 Trạng thái NPC (LOCKED)
- NPC sinh ra là **Unassigned**
- Unassigned NPC chỉ làm: **Leisure / Inspect**
- Người chơi gán NPC vào Workplace → NPC chỉ làm JobSet của Workplace đó.

### 5.3 Workplace (công trình có slot lao động)
Mỗi Workplace có:
- WorkSlots
- AllowedJobTypes (JobSet)
- WorkTargets/WorkRadius (nếu cần)
- AutoFillEnabled (**DEFAULT = ON**)
- AssignedWorkers

**Rule:** Mọi job “sản xuất/xây dựng/vận chuyển” đều phải chạy bởi NPC đã assign đúng Workplace.

### 5.4 Fallback khi Workplace không có việc
- NPC assigned sẽ **idle quanh Workplace**, không tự quay về Leisure/Inspect.
- (Option AllowOffDutyLeisure: mặc định OFF)

### 5.5 Auto-fill (DEFAULT = ON)
- Workplace tự lấp slot từ pool Unassigned.
- Ưu tiên mặc định (có thể chỉnh sau): Farm → Harvest → Builder → Warehouse


### 5.5 HQ Workplace (ĐẶC BIỆT — START FRIENDLY)
HQ là Workplace đặc biệt để người chơi **khởi đầu dễ**, không bị “kẹt” khi chưa xây đủ hệ thống.

**HQ Storage (LOCKED):**
- HQ có kho chứa đa tài nguyên (small capacity) dùng như “kho tạm” đầu game.
- HQ có thể nhận tài nguyên được vận chuyển về (deposit).

**HQ JobSet (LOCKED):**
NPC assigned HQ có thể làm **mọi việc TRỪ thu thập tài nguyên tại node**:
- Build / Move / Demolish / Upgrade / Repair *(builder tasks)*
- HaulCore (vận chuyển tài nguyên cơ bản giữa các kho/công trình về HQ - HQ chỉ Build/Repair/HaulBasic, không đụng ammo)
- EmergencyRepair (ưu tiên HQ/tower)

**Giới hạn để giữ bản sắc hệ thống:**
- HQ worker **không** làm HarvestNode (đi chặt cây/đào đá/mỏ sắt).
- HQ worker **không** thực hiện chuỗi ammo logistics chuyên biệt (ForgeAmmo / ArmoryResupply). Chuỗi này yêu cầu Forge/Armory đúng jobset.

> Khi người chơi đã ổn định, Builder Hut + Warehouse sẽ thay HQ worker làm phần lớn công việc thường nhật, HQ worker trở thành “đa nhiệm cứu nguy”.


---

## 6) INTERACTION SYSTEM

### 6.1 Stand Cell Authority
NPC không đứng lên target cell; dùng Stand Cell Resolver để tìm vị trí đứng hợp lệ xung quanh.

### 6.2 Idle / Leisure / Inspect
- Leisure/Inspect dùng claim + cooldown + variety để tránh dồn cục/spam.

---


## 7) RESOURCE SYSTEM & LOGISTICS (BASE — WORKER-DRIVEN + STORAGE)

> **Thay đổi quan trọng (LOCKED):** Resource **phụ thuộc vào NPC vận hành**. Không còn cơ chế “tăng resource tự động theo thời gian”.
> - Worker **thu thập theo thời gian** tại điểm tài nguyên.
> - Resource **chỉ tăng** khi Worker **mang về và nạp vào kho** (storage) của công trình.
> - Các hành động **tiêu thụ resource** (build/upgrade/repair/forge...) yêu cầu NPC **đi lấy resource** từ kho phù hợp.

### 7.1 Resource Types (Base)
- Core: **Wood / Stone / Food / Iron**
- Combat: **Ammo** (đạn dược) — bắt buộc để tháp bắn (xem Combat + Armory/Forge)

### 7.2 Storage Model (LOCKED)
Mỗi công trình có thể có **kho cục bộ** (local storage) cho 1 hoặc nhiều loại resource.
- **Công trình tài nguyên (Lumber/Quarry/Iron Hut/Farm)** có kho cục bộ cho resource tương ứng (sức chứa nhỏ).
- **Warehouse (Nhà kho)** có thể chứa **nhiều loại core resource** với sức chứa **lớn hơn**.
- **Armory (Kho vũ khí)** chứa **Ammo** (và chỉ Ammo trong base để rõ vai trò).

**Lưu ý triển khai:** tổng resource của người chơi có thể vẫn là “global view”, nhưng **source of truth** là **tổng tất cả lượng đang nằm trong các kho** (warehouse + kho cục bộ + armory). UI top bar hiển thị tổng.

### 7.3 Gathering — Worker-Driven (LOCKED)
- Worker (assigned đúng workplace) đi tới **resource point** (node / farm zone).
- Thực hiện **gather theo thời gian**:
  - `WorkTime` và `YieldPerWork` phụ thuộc **cấp độ công trình tương ứng** (Tier/Level).
- Sau khi thu được một “mẻ” (batch), Worker **mang về** kho cục bộ của workplace (hoặc kho gần nhất nếu kho cục bộ đầy, xem 7.6).
- Resource **chỉ được cộng** khi Worker **deposit thành công** vào kho.

### 7.4 Hauling Core Resources — Warehouse Transporters (LOCKED)
- NPC được gán vào **Warehouse/Storage** (JobSet: HaulBasic) sẽ:
  1) lấy core resource từ **kho cục bộ** của các công trình tài nguyên (Lumber/Quarry/Iron Hut/Farm)
  2) mang về **Warehouse** để tập trung dự trữ
- **Kho cục bộ** giúp công trình vẫn chạy khi đường xa, nhưng sẽ đầy nếu không có hauling → tạo áp lực logistics tự nhiên.

> Quy tắc: **Warehouse Transporters chỉ vận chuyển core resource** (Wood/Stone/Food/Iron). **Không vận chuyển Ammo**.

### 7.5 Consumption — “Fetch then Spend” (LOCKED)
Mọi hành động tiêu thụ resource đều theo nguyên tắc:
1) Tạo “task/job” (Build/Upgrade/Repair/Forge...)
2) NPC thực thi job phải **fetch đủ resource** cần thiết (có thể theo nhiều chuyến)
3) Chỉ khi đã fetch (đặt vào “job reserve” hoặc mang tới site) thì mới **bắt đầu work timer**

Áp dụng cho:
- **Build / Upgrade / Repair** (Builder Hut workers)
- **Forge Ammo** (Forge workers)
- Các tiêu thụ khác về sau

### 7.6 Storage Fallback & Priority (LOCKED)
Khi **fetch** hoặc **deposit**, ưu tiên kho theo loại job:

**Deposit (giao nộp):**
- Worker của công trình tài nguyên: ưu tiên **kho cục bộ** của workplace → nếu đầy, deposit vào **Warehouse gần nhất** (nếu có đường/đi được).

**Fetch (lấy về tiêu thụ):**
- Builder/Forge/Armory haulers ưu tiên:
  1) **Kho gần nhất** có đủ resource (distance theo grid path cost)
  2) Nếu không đủ: lấy phần còn thiếu từ kho khác (multi-source)

### 7.7 “Được không?” — Tác động hệ thống & kiểm soát scope
- Hệ này **hợp** với kiến trúc hiện tại (Workplace + JobSet + reservation + pathing), vì mọi thứ vẫn là **Job** có lifecycle rõ.
- Nhưng scope tăng: cần thêm (a) storage per building (b) carry/deliver states (c) fetch-before-work.
- Để shipable: base chỉ cần **1 “carry slot” / NPC**, và resource dạng “stack” theo loại (không item entity), tránh nổ complexity.


---

## 8) BUILDING SYSTEM (LOCKED + WORKPLACE)

### 8.1 Placement Rules
Building là prefab:
- Footprint theo grid cell
- Validate:
  - within bounds
  - ground hợp lệ (không water)
  - không overlap road/building/resource/obstacle/farm zone

### 8.2 EntryPoint + Road Connectivity (LOCKED)
- Mỗi building có **EntryPoint world-space** (midpoint cạnh).
- Road-connect condition: EntryPoint phải “gần road” với driveway length = 1.
- Quy tắc đo (LOCKED, tránh bug):
  - EntryCell = WorldToCell(EntryPoint)
  - Pass nếu có road ở một trong: EntryCell, N, E, S, W
  - Road cell gần nhất được dùng làm “driveway” (visual)

### 8.3 Rotation
- 4 hướng N/E/S/W bằng swap prefab.
- Footprint giữ nguyên; EntryPoint thay theo hướng.


### 8.4 Workplace Slots & JobSets (Base — UPDATED)

**NPC Assignment (LOCKED):**
- NPC spawn ra là **Unassigned** → chỉ làm **Leisure/Inspect**.
- NPC chỉ làm được các job thuộc **JobSet** của công trình mà nó được gán (Workplace).
- Mặc định **Auto-fill = ON** cho Workplace (lấp slot từ Unassigned).

**Workplace → JobSet mapping (Base):**
- **HQ (Workplace đặc biệt):** làm **mọi việc trừ Harvest**  
  - JobSet: Build / Move / Demolish / Upgrade / Repair / HaulBasic / HaulAmmo / ForgeAmmo / Farm (OPTIONAL)  
  - *LOCK:* HQ **không làm HarvestNode** (không tự đi chặt cây/đập đá/mỏ sắt).  
  - Gợi ý triển khai: cho HQ làm “điều phối” (builder + logistics + smith), giúp start game dễ hơn.
- **Builder Hut:** Build / Move / Demolish / Upgrade / Repair (xem mục 10)
- **Farm/Farmhouse:** Farm (thu Food từ farm zone) + deposit vào kho cục bộ
- **Lumber Camp / Quarry / Iron Hut:** HarvestNode (trong radius) + deposit vào kho cục bộ
- **Warehouse/Storage:** **HaulBasic** (vận chuyển core resources từ kho cục bộ về Warehouse)
- **Forge (Lò rèn):** **ForgeAmmo** (sản xuất Ammo từ resource) — xem mục 10A
- **Armory (Kho vũ khí):** **HaulAmmo** (lấy Ammo từ Forge → Armory; và resupply Ammo cho Towers) — xem mục 10B

**Auto-fill priority (default đề xuất):**
Farm → Harvest (Lumber/Quarry/Iron) → Builder → Warehouse → Forge → Armory → HQ


---

## 9) ROAD SYSTEM (LOCKED)
- RoadService là authority; tilemap chỉ render.
- Road placement:
  - chỉ grid-orthogonal
  - phải connected network (no islands)
- Render road 3-cell wide (visual), nhưng logic lưu centerline set/graph.

---

## 10) BUILDER HUT (NEW — CONSTRUCTION SPECIALIST)

### 10.1 Mục đích
Builder Hut là Workplace “chuyên xây dựng”, giúp tăng tính chiến lược phân bổ lao động.
- Khi có Builder Hut: phần lớn build/move/demolish/upgrade/repair sẽ do builder đảm nhiệm.
- Khi **chưa có Builder Hut (đầu game)**: NPC assigned **HQ** vẫn có thể thực hiện các công việc xây dựng để người chơi không bị kẹt.

### 10.2 Slots theo level (DEFAULT)
- L1 = 1 slot
- L2 = 2 slots
- L3 = 3 slots

### 10.3 Rule thi hành (LOCKED)
- Các công việc xây dựng (build/move/demolish/upgrade/repair) có thể được thực hiện bởi:
  - NPC assigned **Builder Hut**, hoặc
  - NPC assigned **HQ** (workplace đặc biệt)
- Nếu **không có worker** ở cả Builder Hut lẫn HQ:
  - thao tác build/upgrade/repair có thể được “đặt kế hoạch”
  - nhưng trạng thái sẽ là **Pending (Need Builder)**, không có tiến độ.

---


## 10A) FORGE (LÒ RÈN) — AMMO PRODUCTION (BASE)

### 10A.1 Vai trò
- Sản xuất **Ammo** từ các core resource tương ứng (tunable recipe).
- Forge là workplace; NPC assigned gọi là **Thợ rèn** (Smith).

### 10A.2 Input/Output & Storage
- Input mặc định (đề xuất): **Iron + Wood → Ammo**
- Forge có:
  - **Input buffer** (Iron/Wood) sức chứa nhỏ
  - **Output buffer** (Ammo) sức chứa nhỏ (để Armory đến lấy)

### 10A.3 Job: ForgeAmmo (Smith)
Workflow (LOCKED):
1) Smith kiểm tra recipe còn thiếu input.
2) Smith **đi lấy input** từ **kho gần nhất** (ưu tiên Warehouse; nếu thiếu thì lấy từ kho cục bộ công trình tài nguyên).
3) Smith mang về Forge, **deposit vào input buffer**.
4) Khi đủ input cho 1 batch → chạy **work timer**.
5) Hoàn thành → **Ammo xuất hiện trong output buffer** của Forge (chưa vào Armory).

**Nếu thiếu input / không tìm được đường / kho trống:** job fail-fast + notification phù hợp.

---

## 10B) ARMORY (KHO VŨ KHÍ) — AMMO STORAGE + RESUPPLY (BASE)

### 10B.1 Vai trò
- Lưu trữ **Ammo** tập trung.
- Cấp ammo cho **Defense Towers**. Tháp **không có ammo → không bắn**.

### 10B.2 Storage
- Armory chứa **Ammo** (base chỉ Ammo để rõ vai trò).
- Có capacity lớn hơn Forge output buffer.

### 10B.3 Jobs: HaulAmmo (Armory Runners)
NPC assigned Armory thực hiện 2 nhóm việc (LOCKED priority):
1) **Refill Towers**: nếu có tower ammo < threshold → mang ammo từ Armory đến tower.
2) **Fetch from Forge**: nếu Armory ammo thấp hoặc còn tower cần ammo → lấy ammo từ Forge output buffer về Armory.

**Rule phân biệt với Warehouse haulers:**
- Warehouse haulers chỉ vận chuyển core resources.
- Armory runners chỉ vận chuyển Ammo (Forge ↔ Armory ↔ Towers).

### 10B.4 Tower Ammo Model (Base)
- Mỗi tower có:
  - `AmmoMax`
  - `AmmoCurrent`
  - `AmmoPerShot` (mặc định 1)
- Khi `AmmoCurrent == 0` → tower **ngừng bắn**, hiển thị icon “Out of Ammo”.
- Armory runner có thể refill theo gói (batch), ví dụ +10 ammo/chuyến (tunable).

---

## 11) KING SYSTEM (META — PER RUN)
- Mỗi run chọn 1 King (random 3 lựa chọn), King là passive rule-changer.
- Base target: 6 kings (chi tiết ở Content Bible).

---

## 12) COMBAT SYSTEM (BASE — SHIP)

### 12.1 Mục tiêu
- Dễ hiểu, phản hồi rõ, mỗi tower có role
- Enemy behaviors tạo bài toán positioning/planning (không chỉ tăng HP)

### 12.1A Ammo Requirement (BASE — LOCKED)
- Tất cả **Defense Towers** cần **Ammo** để bắn.
- Nếu `AmmoCurrent == 0` → tower **ngừng bắn** + hiện trạng thái **Out of Ammo**.
- Ammo được sản xuất tại **Forge** và lưu trữ/điều phối bởi **Armory** (xem mục 10A/10B).
- Resupply được thực hiện bởi NPC assigned **Armory (HaulAmmo)**.


### 12.2 Enemy Targeting & Pathing
- Primary target: HQ
- Road có traversal cost thấp hơn để tạo “định tuyến” (chống exploit bằng rule/caps tunable)

### 12.3 Waves theo mùa
- Autumn: nhiều wave nhỏ (pressure)
- Winter: ít hơn nhưng nặng + boss
- Year 2 tăng thêm elite + thêm hướng spawn (tunable)

---

## 13) PROGRESSION & META (BASE)
- Meta currency sau run dùng để **mở lựa chọn**:
  - Kings, towers/buildings, mutators, cosmetics
- Không làm meta “power vĩnh viễn quá mạnh” để tránh phá balance.

---


## 14) UI SYSTEM — NOTIFICATIONS (LOCKED)

### 14.1 Mục tiêu
- Phản hồi nhanh, rõ ràng các lỗi/thiếu hụt/điểm gãy của hệ thống (tài nguyên, assignment, ammo, combat).
- Tránh spam: phải có **gộp (stack)** + **cooldown** + **ưu tiên**.

### 14.2 Layout & Behavior (LOCKED)
- Vị trí: **giữa màn hình theo chiều ngang**, **mép trên**, **dưới Top Bar**.
- Hiển thị tối đa **3** notifications cùng lúc.
- Thứ tự: **mới nhất ở trên**.
- Nếu >3: **đẩy cái cũ nhất**, thêm cái mới nhất lên **trên cùng**.
- Click (optional khuyến nghị): **focus camera** tới đối tượng liên quan (building/tower/node).

### 14.3 Cấu trúc 1 notification
- `Title` (2–6 từ)
- `Body` (1–2 dòng, nêu nguyên nhân + gợi ý hành động)
- `Severity`: Info / Warning / Error / Critical
- `eventKey` để gộp/cooldown
- `sourceRef` (optional: buildingId, npcId, cellPos, waveId)
- `count` (nếu gộp)

### 14.4 Anti-spam rules (LOCKED)
- **Gộp** thông báo cùng `eventKey` trong cửa sổ 5–10s: hiển thị `xN` thay vì spam.
- **Cooldown** theo `eventKey` (tunable, mặc định 10s với warning/error).
- Ưu tiên: Critical > Error > Warning > Info  
  - Nếu đang đủ 3 slots mà có Critical đến: luôn hiển thị, drop cái thấp hơn/cũ nhất.
- Placement/build failure: chỉ notify khi người chơi **xác nhận** (confirm) hoặc thao tác thật sự thất bại.

### 14.5 Notification Catalog (Base)
> Quy tắc viết: Title ngắn, Body có “gợi ý làm gì”. Không dùng thuật ngữ kỹ thuật (reservation/stand cell).

#### A) Placement / Build / Upgrade / Repair
1) **Không thể đặt**
- Title: `Không thể đặt`
- Body (tuỳ lý do): `Chồng lấn / Nước / Cấm đặt / Entry phải gần road (1 ô).`
- Severity: Warning

2) **Thiếu tài nguyên**
- Title: `Thiếu tài nguyên`
- Body: `Cần thêm {Wood}/{Stone}/{Iron}/{Food}…`
- Severity: Warning

3) **Cần thợ xây**
- Title: `Thiếu thợ xây`
- Body: `Cần NPC ở HQ hoặc Builder Hut để xây/nâng cấp/sửa.`
- Severity: Warning
- Trigger: có công việc xây dựng nhưng không có worker ở HQ/Builder Hut.

4) **Đang chờ tài nguyên**
- Title: `Đang chờ tài nguyên`
- Body: `Thợ xây chưa lấy đủ vật liệu. Hãy đảm bảo kho còn tài nguyên.`
- Severity: Info/Warning (tunable)

5) **Kho đích đầy**
- Title: `Kho đã đầy`
- Body: `{StorageName} không còn chỗ chứa {Resource}.`
- Severity: Warning

#### B) Assignment / Workforce
6) **NPC mới**
- Title: `NPC mới xuất hiện`
- Body: `Hãy gán công việc cho NPC mới (Assign vào công trình).`
- Severity: Info

7) **NPC chưa phân công**
- Title: `NPC chưa phân công`
- Body: `{n} NPC đang rảnh. Hãy assign vào công trình để vận hành.`
- Severity: Info (cooldown dài 30–60s)

8) **Thiếu nhân lực**
- Title: `Thiếu nhân lực`
- Body: `{Workplace} đang thiếu {x} lao động.`
- Severity: Info/Warning (Farm/Forge/Armory nên là Warning)

9) **Không có việc**
- Title: `Đang chờ việc`
- Body: `{Workplace} hiện không có mục tiêu phù hợp.`
- Severity: Info (gộp + cooldown mạnh)

#### C) Core Resources (worker-driven + storage)
10) **Không tìm thấy tài nguyên**
- Title: `Kho đang trống`
- Body: `Không tìm thấy {Resource} ở kho gần nhất.`
- Severity: Warning

11) **Không có người vận chuyển**
- Title: `Thiếu vận chuyển`
- Body: `Không có NPC vận chuyển tài nguyên về kho.`
- Severity: Info/Warning (tuỳ thiết kế Warehouse/HQ hauling)

12) **HQ gần đầy**
- Title: `HQ gần đầy`
- Body: `Hãy xây Nhà kho để chứa nhiều loại tài nguyên hơn.`
- Severity: Info/Warning

#### D) Ammo System (Forge + Armory + Towers)
13) **Tháp thiếu đạn**
- Title: `Tháp hết đạn`
- Body: `{TowerName} cần đạn. Hãy đảm bảo có Forge/Armory và worker vận chuyển.`
- Severity: Warning

14) **Đạn sắp cạn**
- Title: `Đạn sắp cạn`
- Body: `Hãy xây Lò rèn và Kho vũ khí để sản xuất & tiếp đạn.`
- Severity: Info/Warning (trigger theo % ammo)

15) **Thiếu nguyên liệu làm đạn**
- Title: `Thiếu nguyên liệu`
- Body: `Lò rèn thiếu {Input}. Kiểm tra khai thác và kho.`
- Severity: Warning

#### E) Season / Waves / Combat
16) **Sắp sang mùa phòng thủ**
- Title: `Sắp sang {Season}`
- Body: `Còn {1} ngày. Chuẩn bị tháp và đường.`
- Severity: Info

17) **Đợt tấn công!**
- Title: `Đợt tấn công!`
- Body: `Kẻ địch đến từ {hướng}.`
- Severity: Warning

18) **Boss xuất hiện!**
- Title: `Boss xuất hiện!`
- Body: `{BossName} đang tiến đến HQ.`
- Severity: Critical

19) **HQ đang bị tấn công**
- Title: `HQ bị tấn công`
- Body: `HP còn {x}%.`
- Severity: Critical (cooldown 10s)

20) **Tháp bị phá**
- Title: `Tháp bị phá`
- Body: `{TowerName} đã bị phá hủy.`
- Severity: Error

### 14.6 Early Guidance (START FRIENDLY — khuyến nghị)
Các prompt hướng dẫn đầu game dùng chung hệ notifications (Info), chỉ xuất hiện 1 lần hoặc có cooldown dài:
- `Bạn có 1 slot dân trống → sẽ có NPC mới sớm.`
- `NPC mới xuất hiện → mở panel Assign gán vào công trình.`
- `Nếu người chơi đặt công trình nhưng thiếu thợ xây → gợi ý xây Builder Hut.`
- `Nếu tháp bắn nhiều và ammo giảm (tower-start ammo) → gợi ý xây Forge + Armory.`


## 15) OUT OF SCOPE (BASE) — ĐỂ DLC / UPDATE
- Physical item logistics (ammo crates thật, inventory per-item)
- Automation nâng cao (upgrade queue / blueprint / policy system) → **DLC Endless**
- Pause upgrade nghiêm ngặt trong Defend → **DLC Endless**
- Hero active combat / skill tree phức tạp (để expansion sau nếu muốn)
- Multiplayer

---

## 16) DESIGN CONSTRAINTS (ABSOLUTE)
- Không micromanagement theo kiểu kéo thả NPC liên tục
- Authority services, không để render quyết định logic
- Deterministic càng nhiều càng tốt (seed/waves)
- Fail-fast + debug visibility
- Mở rộng theo phase/DLC có kiểm soát, không phá nền

---

## 17) CHANGE POLICY
- Mọi thay đổi phải:
  1) cập nhật Changelog
  2) thêm Decision Log nếu là luật/constraint
  3) ghi rõ tác động code + save compatibility
- Nếu chưa lock → ghi OPEN, không tự suy diễn trong code.

