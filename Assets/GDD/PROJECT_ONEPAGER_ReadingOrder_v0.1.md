# SEASONAL BASTION — PROJECT ONE-PAGER + READING ORDER (v0.1)

Ngày tạo: 2026-01-16 (Asia/Bangkok)  
Mục tiêu của tài liệu này: **giúp bạn hết “mông lung”** bằng cách gom lại “bức tranh lớn”, các điểm đã khóa, và **thứ tự đọc/ra quyết định** để chuyển sang triển khai.

---

## 1) Game này là gì (10 gạch đầu dòng)

1. **Thể loại:** Premium **run-based roguelite** kết hợp **city builder + tower defense** trên **grid**.
2. Người chơi xây dựng trong pha Build, chuẩn bị để **phòng thủ** trong pha Defend.
3. Gameplay là **deterministic grid simulation**: mọi thứ quan trọng chạy trên cell (ô lưới).
4. **Road** là “xương sống” của quy hoạch; công trình phải **nối road qua Entry/Driveway** mới đặt được.
5. **NPC là lực lượng lao động**: NPC sinh ra **chưa có việc**, người chơi gán NPC vào **công trình** (workplace).
6. Mỗi workplace mở ra **job set** riêng (Farm -> harvest, Builder -> build/repair, Forge -> craft ammo…).
7. **Tài nguyên không tăng theo thời gian**: chỉ tăng khi worker **thu hoạch và mang về** (deliver) đúng luồng.
8. Storage có luật rõ: **Warehouse không chứa Ammo**; Ammo đi theo **Forge → Armory → Tower**.
9. Tower muốn bắn phải có **Ammo**; khi **<25% ammo** thì gửi request, Armory ưu tiên tiếp đạn.
10. Một run kết thúc bằng **outcome + rewards + meta hooks** (để loop roguelite).

---

## 2) “Điểm đã khóa” (đừng bàn lại lúc này)

### 2.1 Map/Road/Placement (LOCKED)
- Road đặt **chỉ N/E/S/W** (grid orthogonal).
- Entry là **điểm world** ở cạnh công trình (không nhất thiết trùng cell).
- Điều kiện nối road: EntryAnchor cách road **≤ 1 cell** (DrivewayLength = 1).
- Khi commit placement: chuyển **chính xác 1** cell driveway gần Entry thành road, chọn **deterministic**.

### 2.2 NPC + Workplace (LOCKED)
- NPC sinh ra **chưa có việc**, chỉ có Leisure/Inspect.
- Người chơi **tự gán** NPC vào workplace; chỉ auto-fill lúc **bắt đầu game**.
- HQ NPC chỉ làm **Build/Repair/HaulBasic** (không harvest).

### 2.3 Economy/Logistics (LOCKED)
- Worker đi harvest theo thời gian & năng suất phụ thuộc cấp công trình; **resource chỉ tăng khi deliver về công trình**.
- Producer có **local storage**; transporter chở về kho.
- Builder khi build/upgrade/repair phải **đi lấy resource** từ kho phù hợp; nếu kho thiếu thì lấy từ nơi chứa tương ứng.

### 2.4 Ammo Pipeline (LOCKED)
- Warehouse **không chứa Ammo**.
- Forge craft ammo từ resource; Armory chứa ammo & dispatch.
- Tower < 25% ammo → gửi request; Armory ưu tiên cấp.

### 2.5 Notifications (LOCKED)
- Banner thông báo ở **giữa, mép trên**, dưới top bar.
- Tối đa **3**, newest-first; vượt quá thì đẩy cái cũ.

---

## 3) Start Run “tối giản nhưng chạy mượt” (đã chốt)

**Start buildings:**
- HQ (1 NPC)
- 2 House (capacity tổng 4)
- Farm/Farmhouse (L1) + zone (1 NPC)
- Lumber Camp + zone (1 NPC)
- 1 Arrow Tower (**full ammo**)

**Onboarding rule:**
- Khi còn capacity -> sau một thời gian sinh thêm NPC và **thông báo**, người chơi tự gán workplace (không auto).

**Start map:**
- 64×64, cross-road seed + spur nhỏ (để placement không kẹt).
- Zone farm/forest gần HQ để nhịp đầu game không “chậm vô cảm”.

---

## 4) Những thứ chưa cần đụng tới ngay (để giảm ngợp)

Bạn **chưa cần** triển khai ngay:
- Combat sâu / nhiều tower types / nhiều enemy types
- Rewards phức tạp
- Procedural map generation
- Art pipeline đầy đủ

Mục tiêu trước mắt là **Vertical Slice #1**: backbone + grid/placement + NPC/jobs + resource flow.

---

## 5) Reading Order (90 phút) — chỉ đọc đúng cái cần để bắt đầu

> Mục tiêu sau 90 phút: bạn hiểu “game là gì”, các rule cứng, và biết ngay bước tiếp theo cần làm gì.

### 5.1 15 phút — Bức tranh lớn
1) **`SEASONAL_BASTION_GDD_MASTER_LOCKED_v5_VN.md`**  
   - Đọc: tổng quan loop, hệ thống NPC, economy, ammo, placement.

### 5.2 20 phút — Content & luật game
2) **`Deliverable_A_Content_Bible_LOCKED_v0.1_FULL.md`**  
   - Đọc: taxonomy công trình/NPC/jobs/storage + rule “Warehouse no Ammo”, HQ roles.
3) **`Deliverable_B_Run_Pacing_LOCKED_v0.1_FULL.md`**  
   - Đọc: nhịp run, Build/Defend, speed control.

### 5.3 10 phút — 1 run hoàn chỉnh trông như thế nào
4) **`Run_Complete_Blueprint_v0.1.md`**  
   - Đọc: flow New Run → onboarding → build → defend → outcome.

### 5.4 25 phút — 4 SPEC quan trọng nhất cho VS#1
5) **`PART5_GridMap_Placement_RoadEntryDriveway_SPEC_v0.1.md`**  
6) **`PART6_EntityStores_RuntimeState_SPEC_v0.1.md`**  
7) **`PART7_ResourceFlow_StorageRules_SPEC_v0.1.md`**  
8) **`PART4_Notifications_UIStack_SpamControl_SPEC_v0.1.md`**

### 5.5 20 phút — “Triển khai theo hợp đồng”
9) **`PART25_Technical_InterfacesPack_Services_Events_DTOs_LOCKED_SPEC_v0.1.md`**  
10) **`PART27_VerticalSlice1_FromZeroProject_Init_SprintPlan_LOCKED_SPEC_v0.1.md`**  
11) (Tuỳ) **`StartMapConfig_RunStart_64x64_v0.1.json.md`** để hiểu start map.

---

## 6) “Decision Checklist” — trước khi code bạn chỉ cần chốt 7 câu

Bạn chỉ cần trả lời (cho chính bạn) 7 câu này là đủ để bắt đầu VS#1:

1) **Unity 2D pipeline:** Built-in 2D hay URP 2D?  
2) **Movement trong VS#1:** teleport (nhanh) hay mover-lite cell step?  
3) **UI debug:** IMGUI hay UI Toolkit?  
4) **Map start:** giữ start map handcrafted v0.1 (khuyến nghị) đúng không?  
5) **Resource model:** zone-based (khuyến nghị) đúng không?  
6) **Determinism:** iteration order luôn sort theo id/cell, không dùng LINQ runtime?  
7) **Scope VS#1:** chỉ backbone+placement+harvest+haulbasic (không buildsite/combat) đúng không?

> Nếu câu nào chưa chắc: cứ chọn phương án “đơn giản nhất” cho VS#1, vì mục tiêu là **ra bản chạy được**.

---

## 7) “Bước tiếp theo” (không phải code ngay)

### 7.1 Bước tiếp theo tối ưu (30–45 phút)
- Bạn mở các file ở Reading Order mục 5 theo đúng thứ tự.
- Gạch ra 1 trang note:
  - 5 rule cứng nhất (road/entry/driveway; worker deliver; ammo; HQ role; notifications)
  - 3 thứ sẽ làm trong VS#1

### 7.2 Khi bạn đã đọc xong 8–10 file trọng yếu
Bạn quay lại đây và nói:
- “Mình đã đọc xong, mình chọn Built-in/URP, teleport/mover-lite, IMGUI/UITK”
=> mình sẽ chuyển bạn sang **Session 1 (Day 1)** theo Part 27, dạng checklist + file list.

---

## 8) Phụ lục: “Nếu thấy mông lung” thì kiểm tra 3 lỗi thường gặp

1) **Ngợp tài liệu** → chỉ đọc đúng 10 file ở Reading Order.  
2) **Chưa có mốc hoàn thành** → đặt mục tiêu nhỏ: “VS#1 chạy, đặt được building, resource deliver thấy tăng”.  
3) **Lẫn lộn design vs implementation** → design đã khóa trong Deliverables; implementation bám Part 25–27.

---

**Kết luận:** Bạn không thiếu gì cả — hiện bạn đang ở giai đoạn “đóng gói và chọn lộ trình”. Hãy dùng Reading Order để lấy lại “điểm neo”, rồi chuyển sang VS#1.
