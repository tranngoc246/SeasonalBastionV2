# SEASONAL BASTION — BACKLOG M1: VERTICAL SLICE PLAYABLE v1.0 (VN)

> Mục đích: Tách milestone M1 trong roadmap thành backlog task-by-task đủ chi tiết để bắt đầu làm việc ngay.
> Mục tiêu M1: Chứng minh Seasonal Bastion “là game gì” trong 1 build chơi được; road/placement + labor + logistics + defend phải thực sự dính vào nhau.
> Tài liệu nền:
> - `SEASONAL_BASTION_GDD_COMPLETE_v1.0_VN.md`
> - `SEASONAL_BASTION_UI_SPEC_v1.0_VN.md`
> - `SEASONAL_BASTION_UX_ONBOARDING_SPEC_v1.0_VN.md`
> - `SEASONAL_BASTION_CONTENT_SCOPE_SPEC_v1.0_VN.md`
> - `SEASONAL_BASTION_VERTICAL_SLICE_ROADMAP_v1.0_VN.md`

---

## 1. Cách đọc backlog này

### Priority labels
- **P0**: chặn milestone, phải có để M1 tồn tại.
- **P1**: cần có để M1 thực sự “vào”.
- **P2**: polish / hỗ trợ readability, chỉ làm khi P0/P1 đã ổn.

### Status labels (gợi ý)
- TODO
- IN PROGRESS
- BLOCKED
- DONE
- CUT FOR M1

### Definition of Done chung cho task M1
Một task chỉ tính DONE nếu:
- feature hoạt động trong build playable
- không phá readability của flow hiện tại
- có cách verify bằng playtest/smoke test
- không dựa trên giả định mơ hồ chưa chốt ở GDD/spec

---

## 2. Exit criteria của M1 (để luôn nhớ đích)

M1 xong khi:
- New Run vào game ổn định
- Placement không gây mù mờ
- Player assign được NPC và thấy settlement đổi hành vi
- Resource flow tăng bằng lao động thật
- Tower có thể fail vì thiếu ammo, và player đọc được điều đó
- Defend phase tạo pressure đủ thật
- Một người mới nhìn vào có thể hiểu fantasy cốt lõi trong 20 phút

---

## 3. Backlog M1 theo cụm

# A. RUN START / CORE LOOP BASELINE

## M1-A1 — New Run flow tối thiểu
**Priority:** P0  
**Mục tiêu:** Từ Main Menu vào được gameplay state hợp lệ.

### Kết quả mong muốn
- Có nút New Run hoạt động.
- New Run load được start map baseline.
- Spawn được toàn bộ start package.

### Việc cần làm
- nối Main Menu → New Run
- reset sạch state cũ nếu restart từ session trước
- đảm bảo game vào đúng gameplay scene/state

### Verify
- từ menu bấm New Run 3–5 lần liên tiếp không lỗi
- world khởi tạo đúng mỗi lần

---

## M1-A2 — Start map baseline hợp lệ
**Priority:** P0  
**Mục tiêu:** Có map 64x64 hoặc tương đương đủ để chứng minh loop.

### Kết quả mong muốn
- map có ground playable
- có road seed baseline
- có vùng farm/lumber hợp lý gần HQ
- có chỗ để player placement mà không kẹt vô lý

### Việc cần làm
- chốt 1 start map handcrafted baseline
- chốt vị trí HQ / houses / production / tower / gates sơ bộ
- verify road graph và buildable space

### Verify
- đi vào map và thử đặt vài building cơ bản
- player không bị choke quá sớm vì layout dở

---

## M1-A3 — Start package spawn đúng
**Priority:** P0  
**Mục tiêu:** Base khởi đầu phải “đã sống”.

### Kết quả mong muốn
- HQ
- 2 House
- Farm/Farmhouse
- Lumber Camp
- 1 Arrow Tower full ammo
- road seed
- 3 NPC start hợp lệ

### Verify
- mỗi New Run spawn đúng set này
- không thiếu storage/assignment/road dependency cần thiết

---

## M1-A4 — Season/day clock tối thiểu
**Priority:** P0  
**Mục tiêu:** Có thời gian trôi và phase chuyển đổi.

### Kết quả mong muốn
- Day tăng đúng
- Season chuyển đúng
- Build/Defend phase chuyển được

### Verify
- chạy nhanh trong debug để xem chuyển phase ổn
- HUD phản ánh đúng state

---

## M1-A5 — Speed control tối thiểu
**Priority:** P1  
**Mục tiêu:** Người chơi điều khiển nhịp game được.

### Kết quả mong muốn
- Pause / 1x / 2x / 3x hoạt động
- vào Defend auto về 1x nếu giữ rule này

### Verify
- đổi speed không làm tick logic sai rõ rệt

---

# B. PLACEMENT / ROAD / ENTRY

## M1-B1 — Build mode + placement ghost hoạt động
**Priority:** P0  
**Mục tiêu:** Có thể vào build mode và thấy ghost building.

### Kết quả mong muốn
- chọn building từ build menu
- ghost follow cursor/cell đúng
- cancel build mode được

### Verify
- chuyển ra/vào build mode không để lại ghost rác

---

## M1-B2 — Valid/invalid placement feedback rõ
**Priority:** P0  
**Mục tiêu:** Người chơi hiểu placement hợp lệ hay không ngay bằng mắt.

### Kết quả mong muốn
- ghost valid/invalid có khác biệt rõ
- footprint đọc được
- entry đọc được

### Verify
- một người khác nhìn vào vẫn hiểu cell nào đang fail

---

## M1-B3 — Road / entry connectivity rule tối thiểu
**Priority:** P0  
**Mục tiêu:** Placement phải bám đúng fantasy quy hoạch.

### Kết quả mong muốn
- building chỉ đặt được khi road/entry hợp lệ
- invalid vì road/entry phải bị chặn thật

### Verify
- test 1 case hợp lệ và 2–3 case fail điển hình

---

## M1-B4 — Fail reason đủ đọc
**Priority:** P1  
**Mục tiêu:** Placement fail không được mù mờ.

### Kết quả mong muốn
- hiển thị lý do fail tối thiểu:
  - blocked
  - no road connection / invalid entry
  - overlap / out of bounds nếu applicable

### Verify
- player có thể nói được vì sao không đặt được

---

## M1-B5 — Commit placement tạo world state đúng
**Priority:** P0  
**Mục tiêu:** Đặt building xong thì world đổi đúng.

### Kết quả mong muốn
- building/site xuất hiện đúng
- occupancy cập nhật đúng
- storage/workplace/runtime state cơ bản được tạo đúng

### Verify
- inspect hoặc debug view cho thấy building thực sự tồn tại và usable

---

# C. NPC / WORKFORCE / ASSIGNMENT

## M1-C1 — NPC start state hợp lệ
**Priority:** P0  
**Mục tiêu:** NPC khởi đầu phải đủ để loop chạy.

### Kết quả mong muốn
- 3 NPC spawn đúng
- workplace initial assignment đúng
- NPC không spawn vào state mồ côi

### Verify
- New Run nhiều lần không lỗi spawn/assignment

---

## M1-C2 — Unassigned/assigned model hoạt động
**Priority:** P0  
**Mục tiêu:** Đây là trụ cột design, phải thấy rõ.

### Kết quả mong muốn
- NPC mới có thể ở trạng thái Unassigned
- NPC assigned chỉ làm việc theo workplace

### Verify
- ít nhất 1 NPC được chuyển từ unassigned → assigned và đổi hành vi rõ ràng

---

## M1-C3 — Workplace slots cơ bản
**Priority:** P0  
**Mục tiêu:** Workplace có capacity và worker count đúng.

### Kết quả mong muốn
- workplace hiển thị số slot
- assign vượt slot bị chặn hoặc handled rõ ràng

### Verify
- assign đầy slot thì hành vi đúng như rule

---

## M1-C4 — Assignment panel tối thiểu usable
**Priority:** P0  
**Mục tiêu:** Người chơi phải assign được mà không vật lộn.

### Kết quả mong muốn
- thấy danh sách NPC unassigned
- thấy workplace có slot trống
- assign bằng flow ngắn gọn

### Verify
- người mới có thể assign 1 NPC trong <30 giây sau khi được chỉ ra panel

---

## M1-C5 — Basic growth trigger hoặc tutorial NPC mới
**Priority:** P1  
**Mục tiêu:** Tạo ra “assignment moment” có chủ đích.

### Kết quả mong muốn
- có ít nhất 1 thời điểm trong slice người chơi thấy NPC mới / worker mới cần assign

### Verify
- playtest cho thấy người chơi thật sự chạm vào assignment, không chỉ nhìn nó tồn tại

---

# D. ECONOMY / LOGISTICS MINIMUM LOOP

## M1-D1 — Harvest loop cơ bản
**Priority:** P0  
**Mục tiêu:** Resource phải tăng nhờ worker làm việc thật.

### Kết quả mong muốn
- Farm/Farmhouse và Lumber Camp tạo ra resource thông qua worker
- resource không tự cộng kiểu passive timer không gắn labor

### Verify
- bỏ worker thì throughput thay đổi rõ

---

## M1-D2 — Local storage tối thiểu
**Priority:** P0  
**Mục tiêu:** Resource có chỗ “đứng” trước khi đi tiếp.

### Kết quả mong muốn
- producer có local storage state đủ để đọc
- local cap basic hoạt động hoặc ít nhất được mô phỏng đúng hướng

### Verify
- inspect/debug cho thấy resource không teleport vô nghĩa vào global pool

---

## M1-D3 — Haul basic tối thiểu
**Priority:** P0  
**Mục tiêu:** Chứng minh logistics là thật.

### Kết quả mong muốn
- resource đi từ producer/local storage → nơi cần thiết cơ bản
- có worker/job chịu trách nhiệm phần đó

### Verify
- ít nhất 1 flow harvest → haul → usable resource nhìn thấy được

---

## M1-D4 — Build/repair dùng resource thật
**Priority:** P0  
**Mục tiêu:** Build phải ăn vào economy loop.

### Kết quả mong muốn
- xây/sửa cần resource thật
- thiếu resource thì blocked thật

### Verify
- có 1 case build fail vì thiếu resource và người chơi đọc được lý do

---

## M1-D5 — Bottleneck readability tối thiểu cho economy
**Priority:** P1  
**Mục tiêu:** Người chơi không được thấy economy fail mà không hiểu tại sao.

### Kết quả mong muốn
- ít nhất các bottleneck này đọc được:
  - thiếu worker
  - thiếu resource
  - local cap gần đầy / logistics chưa theo kịp (nếu đã có)

---

# E. AMMO PIPELINE MINIMUM LOOP

## M1-E1 — Forge craft ammo tối thiểu
**Priority:** P0  
**Mục tiêu:** Có nguồn tạo ammo thật.

### Kết quả mong muốn
- forge nhận input phù hợp
- forge tạo ammo output cơ bản

### Verify
- ammo không xuất hiện “miễn phí” trong tower

---

## M1-E2 — Armory lưu và dispatch ammo
**Priority:** P0  
**Mục tiêu:** Có node logistics trung gian đúng fantasy.

### Kết quả mong muốn
- armory có state ammo riêng
- ammo flow qua armory trước khi tới tower

### Verify
- bỏ armory hoặc nghẽn armory thì resupply không chạy đúng

---

## M1-E3 — Tower low ammo request
**Priority:** P0  
**Mục tiêu:** Tower chủ động tạo nhu cầu hậu cần.

### Kết quả mong muốn
- tower dưới threshold sẽ request ammo
- state request đọc được ít nhất bằng inspect/notification

### Verify
- tower bắn dần, ammo giảm, request phát ra đúng lúc

---

## M1-E4 — Out-of-ammo stop firing
**Priority:** P0  
**Mục tiêu:** Chứng minh logistics ảnh hưởng trực tiếp tới combat.

### Kết quả mong muốn
- tower hết ammo thì dừng bắn thật
- đây là nguyên nhân player có thể đọc được

### Verify
- cho tower cạn ammo và quan sát fail state trong combat nhỏ

---

## M1-E5 — Low ammo readability
**Priority:** P1  
**Mục tiêu:** Người chơi hiểu tháp yếu vì hậu cần, không phải vì bug.

### Kết quả mong muốn
- low ammo warning ở notification hoặc inspect đủ rõ
- inspect tower cho thấy ammo current/max

---

# F. DEFEND PRESSURE / COMBAT MINIMUM

## M1-F1 — 1 defend phase playable
**Priority:** P0  
**Mục tiêu:** Seasonal pressure phải thành gameplay thật.

### Kết quả mong muốn
- phase defend bắt đầu rõ ràng
- enemy spawn được
- settlement bị thử thách thật

### Verify
- player cảm thấy có áp lực khác rõ so với build phase

---

## M1-F2 — 1–2 enemy types readable
**Priority:** P0  
**Mục tiêu:** Đủ threat nhưng không hỗn loạn.

### Kết quả mong muốn
- có basic enemy roster đủ để thấy tower/ammo có ý nghĩa
- enemy behavior readable, không cần variety quá sớm

---

## M1-F3 — HQ damage / lose state cơ bản
**Priority:** P0  
**Mục tiêu:** Slice phải có consequence thật.

### Kết quả mong muốn
- HQ nhận damage thật
- HQ = 0 thì lose state kích hoạt

### Verify
- có thể cố tình fail defend để thấy lose flow

---

## M1-F4 — Defend transition clarity
**Priority:** P1  
**Mục tiêu:** Người chơi phải biết mình bước vào phase khác.

### Kết quả mong muốn
- season/phase transition đủ rõ
- speed/notification/HUD phản ánh defend state

---

# G. UI / UX MINIMUM FOR M1

## M1-G1 — Top HUD tối thiểu
**Priority:** P0  
**Mục tiêu:** Người chơi luôn biết thời gian và phase.

### Bắt buộc hiển thị
- Year
- Season
- Day
- Build/Defend
- speed controls
- resource counters lõi tối thiểu

---

## M1-G2 — Notification stack tối thiểu
**Priority:** P1  
**Mục tiêu:** Có hệ dẫn hướng ngắn gọn cho các warning quan trọng.

### Bắt buộc ít nhất cho M1
- NPC mới xuất hiện
- thiếu resource khi build
- tower low ammo
- defend phase imminent

---

## M1-G3 — Inspect panel tối thiểu hữu dụng
**Priority:** P1  
**Mục tiêu:** Player có chỗ đọc “sự thật hiện tại”.

### Cần support ít nhất
- building
- tower
- NPC
- build site (nếu có trong slice)

### Cần nhìn được ít nhất
- state hiện tại
- worker count (nếu workplace)
- ammo state (nếu tower)
- progress / blocked state (nếu build site)

---

## M1-G4 — End-of-session state cơ bản
**Priority:** P1  
**Mục tiêu:** Có closure nhỏ cho slice.

### Kết quả mong muốn
- lose state có màn / panel / flow tối thiểu
- retry hoặc restart khả dụng
- pseudo-summary nếu chưa làm summary đầy đủ

---

# H. ONBOARDING / FIRST-RUN SUPPORT

## M1-H1 — First 5-minute guidance pass
**Priority:** P1  
**Mục tiêu:** Người chơi mới không bị ngợp ngay đầu.

### Cần dạy đủ
- đây là base của bạn
- đây là assignment
- đây là placement
- đây là threat sắp tới

---

## M1-H2 — Onboarding trigger cho assignment
**Priority:** P1  
**Mục tiêu:** Player thật sự chạm vào mechanic này.

### Kết quả mong muốn
- khi có NPC mới hoặc khi workforce panel mở lần đầu, game có prompt/hint phù hợp

---

## M1-H3 — Onboarding trigger cho ammo/logistics
**Priority:** P2  
**Mục tiêu:** Chỉ dạy khi người chơi sắp cần.

### Kết quả mong muốn
- low ammo hoặc first defend là thời điểm giới thiệu ammo pipeline

---

# I. VALIDATION / PLAYTEST / QA

## M1-I1 — Internal smoke checklist cho M1
**Priority:** P0  
**Mục tiêu:** Có checklist test nhanh mỗi build.

### Smoke cases bắt buộc
- New Run vào được map
- placement hợp lệ/không hợp lệ đều đọc được
- assign NPC làm đổi hành vi
- harvest tăng tài nguyên thật
- tower có thể cạn ammo
- defend phase có threat
- lose state chạy được

---

## M1-I2 — First-run playtest questions
**Priority:** P1  
**Mục tiêu:** Validate slice bằng mắt người mới.

### Câu hỏi nên dùng
- Bạn có hiểu mình phải làm gì trong 10 phút đầu không?
- Bạn có hiểu vì sao building có lúc không đặt được không?
- Bạn có hiểu vì sao tower không bắn không?
- Assignment có dễ hiểu không?
- Bạn có muốn chơi tiếp không?

---

## M1-I3 — Cut list nếu M1 bị phình scope
**Priority:** P0  
**Mục tiêu:** Biết rõ cái gì cắt trước.

### Cắt trước nếu quá tải
- extra enemy variety
- extra tower variety ngoài mức cần thiết
- deep summary polish
- fancy onboarding presentation
- map variety
- visual flavor không ảnh hưởng readability

### Không được cắt cho M1
- New Run
- placement clarity
- assignment usable
- harvest/haul/build loop cơ bản
- ammo failure visibility
- defend pressure cơ bản

---

## 4. Thứ tự đề xuất để làm M1

### Wave 1 — Backbone
1. M1-A1 New Run flow tối thiểu
2. M1-A2 Start map baseline hợp lệ
3. M1-A3 Start package spawn đúng
4. M1-A4 Season/day clock tối thiểu
5. M1-G1 Top HUD tối thiểu

### Wave 2 — Placement + workforce
6. M1-B1 Build mode + placement ghost
7. M1-B2 Valid/invalid placement feedback rõ
8. M1-B3 Road/entry connectivity rule tối thiểu
9. M1-C1 NPC start state hợp lệ
10. M1-C2 Unassigned/assigned model hoạt động
11. M1-C4 Assignment panel tối thiểu usable

### Wave 3 — Economy loop
12. M1-D1 Harvest loop cơ bản
13. M1-D2 Local storage tối thiểu
14. M1-D3 Haul basic tối thiểu
15. M1-D4 Build/repair dùng resource thật
16. M1-B4 Fail reason đủ đọc

### Wave 4 — Ammo + defend
17. M1-E1 Forge craft ammo tối thiểu
18. M1-E2 Armory lưu và dispatch ammo
19. M1-E3 Tower low ammo request
20. M1-E4 Out-of-ammo stop firing
21. M1-F1 1 defend phase playable
22. M1-F3 HQ damage / lose state cơ bản

### Wave 5 — Readability pass
23. M1-G2 Notification stack tối thiểu
24. M1-G3 Inspect panel tối thiểu hữu dụng
25. M1-F4 Defend transition clarity
26. M1-H1 First 5-minute guidance pass
27. M1-I1 Internal smoke checklist
28. M1-I2 First-run playtest questions

---

## 5. M1 done means what?

M1 chỉ tính DONE khi build vertical slice trả lời được 3 câu:
1. Game này có “vào” không?
2. Core fantasy road + labor + logistics + defend có dính nhau không?
3. Người mới có hiểu trong 20 phút đầu không?

Nếu chưa trả lời được 3 câu đó, M1 chưa xong dù code đã nhiều.
