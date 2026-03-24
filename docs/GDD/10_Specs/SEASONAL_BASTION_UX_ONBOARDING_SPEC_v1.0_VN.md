# SEASONAL BASTION — UX / ONBOARDING SPEC v1.0 (VN)

> Mục đích: Đặc tả trải nghiệm người chơi mới, readability, tutorial flow, failure clarity và pacing của 10–30 phút đầu.
> Phạm vi: Base game / vertical slice / first-run experience.

---

## 1. Mục tiêu UX tổng thể

Seasonal Bastion là game system-heavy. Vì vậy UX phải đạt 3 mục tiêu:
- người chơi **hiểu được** game trước khi bị game thử thách
- người chơi **đọc được nguyên nhân** của thành công/thất bại
- người chơi **muốn thử lại** sau mỗi sai lầm thay vì bực và bỏ game

---

## 2. UX Principles

1. **Teach by need** — chỉ dạy thứ người chơi sắp dùng.
2. **Failure must be legible** — thua phải hiểu vì sao.
3. **Every friction needs payoff** — luật khó phải đem lại cảm giác chiến lược rõ ràng.
4. **The system should feel alive early** — 10 phút đầu phải có nhịp sống.
5. **One lesson at a time** — không chồng 3 tutorial cùng lúc.

---

## 3. First-Run Experience Goals

Trong run đầu, người chơi phải lần lượt hiểu được:
1. Đây là base của mình.
2. NPC cần assignment.
3. Building placement cần road/entry hợp lệ.
4. Resource không tự tăng; worker phải làm thật.
5. Mùa defend sẽ đến và chuẩn bị là bắt buộc.
6. Tower cần ammo pipeline, không tự mạnh mãi.

---

## 4. 5 Phút Đầu

### 4.1 Cảm giác mục tiêu
Người chơi phải thấy:
- game đang chạy rồi, không phải map trống
- base khởi đầu có logic
- mình có việc để làm ngay
- game không đòi hiểu mọi thứ cùng lúc

### 4.2 Điều phải hiển thị rõ
- HQ là trung tâm
- có một vài công trình đang hoạt động
- có dân / NPC ban đầu
- có top bar thời gian
- build / assignment là hai tương tác lớn nhất ban đầu

### 4.3 Điều không nên làm
- bắt người chơi đọc quá nhiều text tutorial một lúc
- ép mở mọi panel ngay đầu game
- giới thiệu ammo logistics quá sớm nếu người chơi còn chưa hiểu worker assignment

---

## 5. 15 Phút Đầu

### 5.1 Bài học bắt buộc người chơi phải trải qua
- assign ít nhất 1 NPC
- đặt hoặc xem placement của ít nhất 1 building hợp lệ
- thấy resource thực sự tăng nhờ worker
- gặp ít nhất 1 bottleneck nhỏ và hiểu nguyên nhân
- được báo hiệu defend phase là mối đe dọa thật

### 5.2 First bottleneck nên là gì
Nên là bottleneck nhẹ, dễ học:
- thiếu worker ở workplace
- build blocked vì thiếu resource nhỏ
- local storage gần đầy

Không nên là bottleneck quá sâu ngay đầu:
- ammo chain vỡ toàn bộ
- multi-system collapse khó đọc

---

## 6. Onboarding Sequence

### Step 1 — Giới thiệu settlement khởi đầu
Người chơi được thấy:
- HQ
- housing
- basic production
- road seed
- 1 tower phòng thủ cơ bản

Thông điệp: “Base này đã sống, và bạn đang tiếp quản nó.”

### Step 2 — Giới thiệu assignment
Khi NPC mới xuất hiện hoặc khi panel workforce mở lần đầu:
- giải thích NPC unassigned
- giải thích workplace slots
- yêu cầu assign thử 1 NPC

Thông điệp: “Dân không tự biết làm gì; bạn quyết định vai trò của họ.”

### Step 3 — Giới thiệu placement / road / entry
Khi người chơi mở build mode lần đầu:
- hiển thị entry / invalid reason rõ
- cho một task placement đơn giản

Thông điệp: “Quy hoạch đúng là luật thật của game.”

### Step 4 — Giới thiệu resource flow
Sau khi có worker làm việc một lúc:
- chỉ ra resource được harvest và đưa về đâu
- cho thấy resource không tự tăng theo thời gian

Thông điệp: “Sản xuất = lao động + vận chuyển, không phải timer tự cộng.”

### Step 5 — Giới thiệu defend pressure
Trước defend phase đầu tiên:
- cảnh báo season transition
- giải thích rằng chuẩn bị hiện tại ảnh hưởng tới khả năng sống sót

Thông điệp: “Mùa chiến đấu là bài kiểm tra cho mọi quyết định vừa rồi.”

### Step 6 — Giới thiệu ammo pipeline
Chỉ nên đưa mạnh khi người chơi sắp cần hoặc đã bắt đầu thấy low ammo warning.

Thông điệp: “Tower cần hậu cần, không chỉ cần tồn tại.”

---

## 7. Tutorial Delivery Format

### 7.1 Nên dùng
- short banner hints
- contextual prompts
- task-based introduction nhẹ
- panel emphasis / highlight / focus

### 7.2 Không nên dùng
- hộp text dài chặn gameplay liên tục
- pop-up liên hoàn không tương tác được với world
- giải thích abstract quá nhiều trước khi người chơi chạm vào system

---

## 8. Readability Requirements

### 8.1 Người chơi phải đọc được nhanh
- đang ở phase nào
- bottleneck chính hiện tại là gì
- building nào đang blocked
- tower nào đang thiếu ammo
- worker nào đang thiếu assignment hoặc workplace nào thiếu worker

### 8.2 Readability hierarchy
1. HQ danger / defend pressure
2. resource and labor bottlenecks cần hành động ngay
3. placement validity
4. logistics warnings
5. low-priority informational state

---

## 9. Failure Clarity

### 9.1 Khi build fail
Game phải nói được lý do cụ thể:
- thiếu resource
- placement invalid
- road/entry invalid
- prerequisite chưa có

### 9.2 Khi defense fail
Người chơi phải đọc được một hoặc vài nguyên nhân chính:
- thiếu ammo
- thiếu repair / builder capacity
- layout yếu
- mở rộng greed quá mức
- thiếu worker ở chỗ then chốt

### 9.3 Khi economy chững
Player phải nhìn ra:
- thiếu worker
- local cap đầy
- thiếu haul
- thiếu storage
- thiếu road/connectivity nếu có liên quan

---

## 10. Reward Cadence

### 10.1 Small rewards
- building vừa xong
- assignment đúng làm settlement chạy mượt hơn
- resource bắt đầu vào đều hơn
- low-ammo issue được giải quyết

### 10.2 Medium rewards
- sống sót qua Autumn đầu tiên
- thấy settlement bắt đầu “tự chạy” hơn
- summary cho thấy mình thật sự tiến bộ

### 10.3 Big rewards
- vượt qua Winter Y1
- clear Year 2
- một run hoàn chỉnh với cảm giác “mình thật sự học được cách vận hành game”

---

## 11. Friction Budget

Game này có nhiều luật. UX phải quản lý số lượng luật được đưa ra mỗi giai đoạn.

### 11.1 Fritction được chấp nhận
- planning kỹ hơn trước khi đặt building
- suy nghĩ assignment
- đọc bottleneck logistics

### 11.2 Friction không được chấp nhận
- không hiểu vì sao placement fail
- không hiểu vì sao tower không bắn
- không biết mình nên mở panel nào để xử lý vấn đề
- tutorial nói quá nhiều nhưng không giúp hành động

---

## 12. Onboarding Success Criteria

Một onboarding tốt khi người chơi mới có thể nói:
- “Mình hiểu vì sao phải assign NPC.”
- “Mình hiểu placement không phải muốn đặt đâu cũng được.”
- “Mình hiểu tài nguyên phải do worker mang về.”
- “Mình hiểu mùa defend sẽ phạt nếu mình chuẩn bị kém.”
- “Mình chưa biết hết game, nhưng mình muốn tiếp tục.”

---

## 13. First-Run QA Questions

Dùng cho playtest:
- Bạn có hiểu mình phải làm gì trong 10 phút đầu không?
- Bạn có hiểu vì sao building có lúc không đặt được không?
- Bạn có thấy assignment là việc đáng làm, hay chỉ là việc hành chính?
- Bạn có hiểu vì sao tower thiếu ammo không?
- Bạn thua vì quyết định sai hay vì không hiểu game đang muốn gì?
- Sau run đầu, bạn có muốn chơi lại không? Vì sao?

---

## 14. Definition of Good UX for Seasonal Bastion

UX được coi là tốt khi:
- người chơi hiểu 70–80% loop cốt lõi trong run đầu
- thua nhưng vẫn muốn thử lại
- có thể chỉ ra nguyên nhân thua chính
- thấy luật game “có chiều sâu” chứ không chỉ “rối”
