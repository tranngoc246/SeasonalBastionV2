# SEASONAL BASTION — UI SPEC v1.0 (VN)

> Mục đích: Tài liệu đặc tả UI player-facing cho base game. Tập trung vào cấu trúc màn hình, hierarchy thông tin, panel, HUD, feedback và các nguyên tắc trình bày.
> Phạm vi: Base game / vertical slice / ship scope gần.

---

## 1. Mục tiêu UI

UI của Seasonal Bastion phải giúp người chơi:
- đọc được trạng thái settlement nhanh
- hiểu bottleneck chính ở đâu
- thao tác build / assign / inspect ít friction
- không bị ngợp dù game có nhiều hệ thống liên kết

UI không được làm game “phức tạp hơn cảm giác thật”.

---

## 2. UI Principles

1. **Readability first** — thông tin quan trọng phải nhìn ra nhanh.
2. **Actionable over decorative** — ưu tiên thứ người chơi có thể hành động.
3. **One source, one place** — mỗi loại thông tin nên có nơi chính để xem.
4. **Escalate by importance** — critical thì bật ra HUD/notification, low-priority thì để inspect/panel.
5. **Low click cost** — workflow thường xuyên phải ít thao tác.
6. **Context-sensitive** — panel hiện đúng thứ liên quan tới selection hiện tại.

---

## 3. Screen Map

### 3.1 Main Menu
Thành phần:
- Logo / Title
- New Run
- Continue (nếu có save)
- Settings
- Quit

Tùy chọn khi New Run:
- Tutorial ON/OFF

### 3.2 In-Run Screen
Các vùng chính:
- **Top HUD bar**
- **Build panel**
- **Workforce / assignment panel**
- **Inspect panel**
- **Notification stack**
- **World interaction layer** (ghost, highlight, contextual labels)

### 3.3 End Screens
- Win screen
- Lose screen
- Run summary
- Retry / Back to Menu

---

## 4. Top HUD Bar

### 4.1 Mục tiêu
Cho người chơi thấy các thông tin chiến lược luôn cần nhìn:
- thời gian
- phase
- speed
- tài nguyên lõi

### 4.2 Thành phần bắt buộc
- Year
- Season
- Day
- Current Phase: Build / Defend
- Speed controls: Pause / 1x / 2x / 3x
- Resource counters tối thiểu:
  - Wood
  - Stone
  - Food
  - Iron
  - Ammo (hoặc ammo state tổng quát nếu phù hợp)

### 4.3 Hành vi
- Khi vào Defend, speed auto về 1x nhưng người chơi vẫn có thể đổi lại nếu design cho phép.
- Phase hiện tại phải đủ nổi bật.
- Winter / high danger nên có visual emphasis nhẹ, không quá ồn.

---

## 5. Notification Stack

### 5.1 Vị trí
- Giữa màn hình, mép trên, nằm dưới top HUD.

### 5.2 Quy tắc
- Tối đa 3 notification visible cùng lúc.
- Newest ở trên cùng.
- Có anti-spam / dedupe.

### 5.3 Notification types
- Info
- Warning
- Critical
- Tutorial hint
- System hint

### 5.4 Notification priority cases
Bắt buộc notify rõ cho các case:
- NPC mới xuất hiện
- thiếu food / growth bị chặn
- tower low ammo
- build blocked vì thiếu resource
- workplace thiếu worker
- local storage gần đầy
- defend phase imminent
- HQ đang nguy cấp

### 5.5 Wording rule
Thông báo phải:
- ngắn
- cụ thể
- action-oriented
- không dùng câu mơ hồ kiểu “Có vấn đề xảy ra”

Ví dụ tốt:
- “Arrow Tower thiếu đạn”
- “Builder Hut chưa có worker”
- “Thiếu 10 Wood để hoàn tất công trình”

---

## 6. Build Panel

### 6.1 Mục tiêu
Cho người chơi vào flow placement nhanh, rõ category và cost.

### 6.2 Structure
Nhóm building theo category:
- Core
- Production
- Logistics
- Ammo
- Defense

### 6.3 Mỗi item build hiển thị
- Tên
- Icon
- Cost cơ bản
- Lock/unlock state
- trạng thái unavailable nếu thiếu prerequisite

### 6.4 Build flow
1. Chọn category
2. Chọn building
3. Ghost xuất hiện trong world
4. Xem trạng thái valid/invalid
5. Confirm placement / cancel

### 6.5 UX requirements
- Không giấu cost quá sâu
- Item lock phải cho biết vì sao lock
- Ghost phải hiển thị rõ entry / invalid reason

---

## 7. Workforce / Assignment Panel

### 7.1 Mục tiêu
Biến assignment thành quyết định chiến lược dễ thao tác, không thành spreadsheet nặng nề.

### 7.2 Thành phần
- Danh sách NPC unassigned
- Danh sách workplace có slot trống/đầy
- worker counts mỗi workplace
- quick assign actions

### 7.3 Hiển thị mỗi workplace
- Tên workplace
- số worker hiện tại / max slots
- role hoặc loại job chính
- trạng thái thiếu người / idle / blocked nếu có

### 7.4 Hiển thị mỗi NPC
- Tên / ID rút gọn hoặc icon nhận diện
- trạng thái hiện tại
- workplace hiện tại (nếu có)

### 7.5 Actions
- Assign NPC → workplace
- Unassign NPC (nếu design cho phép trong base)
- Quick-fill (nếu còn dùng trong onboarding hoặc debug/QoL)

### 7.6 UX rule
Assignment panel phải giúp trả lời ngay:
- ai đang rảnh?
- chỗ nào thiếu người?
- nếu mình có 1 worker mới thì nên ném vào đâu?

---

## 8. Inspect Panel

### 8.1 Mục tiêu
Inspect panel là nơi đọc “sự thật hiện tại” của selection.

### 8.2 Supported selection
- Building
- Tower
- NPC
- Build site
- Có thể hỗ trợ road/zone ở mức nhẹ nếu cần

### 8.3 Building inspect tối thiểu
- Tên
- loại building
- level
- HP / trạng thái xây dựng
- worker count (nếu là workplace)
- storage / local storage state (nếu liên quan)
- bottleneck hiện tại nếu có
- contextual actions

### 8.4 Tower inspect tối thiểu
- Tên tower / loại
- HP
- Ammo current / max
- trạng thái resupply
- target status cơ bản nếu có

### 8.5 NPC inspect tối thiểu
- workplace hiện tại
- current job
- idle / moving / working
- location / target context cơ bản

### 8.6 Build site inspect tối thiểu
- progress
- delivered / remaining cost
- worker/job state liên quan nếu có
- cancel construction (nếu applicable)

### 8.7 Contextual actions
Ví dụ:
- Cancel Construction
- Repair
- Assign / Unassign
- Priority toggles nhẹ (nếu base scope có)

---

## 9. World-Space UI / Feedback

### 9.1 Các lớp feedback trong world
- Selection highlight
- Placement ghost
- Invalid placement hints
- Attack / danger emphasis
- Ammo / warning icons (nếu cần)
- Context labels rất ngắn cho state quan trọng

### 9.2 Placement feedback
Phải thấy rõ:
- footprint
- entry
- driveway / road connectivity relation
- invalid cell reason

### 9.3 Bottleneck feedback
Trong world nên có chỉ báo nhẹ cho các case:
- tower low ammo
- building blocked
- unassigned worker opportunity
- damaged structure

Nhưng không được biến map thành biển icon.

---

## 10. End-of-Season Summary UI

### 10.1 Mục tiêu
Cho người chơi một nhịp nghỉ và một bài học nhanh.

### 10.2 Nội dung
- resources gained/spent
- buildings built/upgraded
- damage / repair summary
- population trend
- next season threat forecast

### 10.3 UX rule
Summary phải ngắn, đọc được trong <30 giây với người chơi đã quen game.

---

## 11. End-of-Run Summary UI

### 11.1 Mục tiêu
Tạo closure và replay motivation.

### 11.2 Nội dung tối thiểu
- days survived
- peak population
- total resources gathered
- ammo crafted / delivered
- towers built
- buildings lost
- bosses defeated

### 11.3 Actions
- Retry
- Back to Menu

---

## 12. Visual Hierarchy Rules

### 12.1 Primary information
Luôn dễ thấy nhất:
- HQ danger
- current phase
- season/day
- low ammo warnings
- worker shortage / critical blockage nếu player cần xử lý ngay

### 12.2 Secondary information
Đưa vào inspect/panel:
- exact storage values
- exact build cost breakdown
- detailed assignment state
- per-building deeper stats

### 12.3 Color semantics
Khuyến nghị:
- Green: valid / healthy / ready
- Yellow: warning / low / near-full / near-empty
- Red: invalid / critical / danger
- Blue/neutral: information / system state

Không dùng màu lẫn lộn giữa placement invalid và combat danger.

---

## 13. UI Risks to Avoid

1. Quá nhiều icon nổi trên map.
2. Notification spam thay vì inspect clarity.
3. Assignment panel giống bảng Excel hơn là tool ra quyết định.
4. Build panel chôn cost và lock reasons quá sâu.
5. Inspect panel không nói rõ bottleneck hiện tại.
6. HUD hiển thị quá nhiều tài nguyên phụ ngay từ đầu.

---

## 14. Definition of Good UI for Seasonal Bastion

UI được coi là tốt khi người chơi có thể nhanh chóng trả lời:
- Mùa nào / ngày nào / phase nào?
- Base của mình đang yếu ở đâu?
- Tại sao tower không bắn?
- Tại sao building này không chạy?
- Mình có worker rảnh không?
- Mình nên xây hoặc sửa cái gì tiếp theo?
