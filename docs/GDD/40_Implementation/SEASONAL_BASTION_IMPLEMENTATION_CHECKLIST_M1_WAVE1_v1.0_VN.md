# SEASONAL BASTION — IMPLEMENTATION CHECKLIST M1 / WAVE 1 (VN)

> Mục đích: Bóc nhỏ M1 Wave 1 thành checklist implementation đủ cụ thể để bắt đầu làm việc ngay.
> Wave 1 mục tiêu: dựng backbone cho vertical slice.
> Nguồn bám:
> - `SEASONAL_BASTION_BACKLOG_M1_VERTICAL_SLICE_v1.0_VN.md`
> - `SEASONAL_BASTION_GDD_COMPLETE_v1.0_VN.md`
> - `SEASONAL_BASTION_UI_SPEC_v1.0_VN.md`

---

## 1. Scope của Wave 1

Wave 1 chỉ tập trung vào 5 task backbone sau:
1. `M1-A1` — New Run flow tối thiểu
2. `M1-A2` — Start map baseline hợp lệ
3. `M1-A3` — Start package spawn đúng
4. `M1-A4` — Season/day clock tối thiểu
5. `M1-G1` — Top HUD tối thiểu

### Kết quả cuối của Wave 1
Sau Wave 1, build phải đạt được:
- từ menu vào được run mới
- map baseline spawn đúng
- start package có mặt và nhìn ra được
- thời gian / mùa / phase bắt đầu chạy
- HUD phản ánh được thời gian và state lõi

---

## 2. Thứ tự làm đề xuất

1. New Run flow
2. Start map baseline
3. Start package spawn
4. Season/day clock
5. Top HUD
6. Smoke test wave 1

Không nên làm HUD trước khi run flow và clock có state thật.

---

## 3. Checklist implementation chi tiết

# TASK 1 — M1-A1 New Run flow tối thiểu

## Goal
Từ Main Menu bấm **New Run** là vào gameplay state hợp lệ.

## Checklist
- [ ] Xác nhận scene/menu flow hiện tại đang dùng gì làm entry point.
- [ ] Xác nhận New Run hiện đang stub, thiếu, hay có flow cũ cần thay thế.
- [ ] Chốt 1 đường đi duy nhất cho New Run trong M1 (tránh nhiều đường tạm song song).
- [ ] Tạo hoặc hoàn thiện logic reset state trước khi start run mới.
- [ ] Đảm bảo world/services/runtime state được init sạch khi New Run bắt đầu.
- [ ] Nếu có scene loading, đảm bảo scene gameplay vào đúng state expected.
- [ ] Nếu không có scene riêng, đảm bảo bootstrap/reset trong cùng scene không để lại state cũ.
- [ ] Gắn New Run button vào flow thật.
- [ ] Xác nhận từ menu có thể vào run nhiều lần liên tiếp mà không bị state leak rõ ràng.

## Verify
- [ ] Bấm New Run từ trạng thái app mới mở → vào game đúng.
- [ ] Back to Menu → New Run lần 2 vẫn sạch.
- [ ] Retry/new run liên tiếp không giữ NPC/building/resource cũ.

## Deliverable mong muốn
- Có một đường chạy New Run ổn định và dùng được cho toàn bộ milestone M1.

---

# TASK 2 — M1-A2 Start map baseline hợp lệ

## Goal
Có một map baseline đủ tốt để chứng minh loop, không cần procedural hay variety.

## Checklist
- [ ] Chốt dùng 1 start map handcrafted cho M1.
- [ ] Xác nhận map size baseline (64x64 hoặc tương đương) đã phù hợp với camera + readability.
- [ ] Chốt khu HQ placement.
- [ ] Chốt road seed baseline.
- [ ] Chốt khu farm/forest/lumber gần HQ.
- [ ] Chốt một khoảng buildable space đủ để người chơi thử expansion sau này.
- [ ] Chốt hoặc đánh dấu sơ bộ lane/gate/defend entry area nếu đã cần cho M1.
- [ ] Kiểm tra map không có choke vô lý khiến placement fail quá sớm.
- [ ] Kiểm tra camera framing ban đầu có cho thấy phần quan trọng của start settlement.

## Verify
- [ ] Vào run thấy base khởi đầu đọc được ngay.
- [ ] Có đủ không gian để làm placement test sau này.
- [ ] Không có blocker vô tình chặn các công trình start.

## Deliverable mong muốn
- 1 map baseline duy nhất cho M1, dùng nhất quán trong test và iteration.

---

# TASK 3 — M1-A3 Start package spawn đúng

## Goal
Khi vào run, settlement khởi đầu phải “đã sống” đúng thiết kế.

## Checklist
- [ ] Spawn HQ đúng vị trí.
- [ ] Spawn 2 House đúng vị trí.
- [ ] Spawn Farm/Farmhouse đúng vị trí.
- [ ] Spawn Lumber Camp đúng vị trí.
- [ ] Spawn 1 Arrow Tower đúng vị trí.
- [ ] Seed road đúng theo layout đã chốt.
- [ ] Chốt ammo start cho Arrow Tower = full.
- [ ] Spawn đúng 3 NPC start.
- [ ] Gắn workplace start hợp lệ:
  - [ ] 1 NPC HQ
  - [ ] 1 NPC Farm
  - [ ] 1 NPC Lumber
- [ ] Xác nhận housing capacity và population start phù hợp với ý đồ onboarding.
- [ ] Kiểm tra mọi building start có runtime state hợp lệ ngay sau spawn.

## Verify
- [ ] New Run lần nào cũng ra đúng start package.
- [ ] Không có NPC mồ côi hoặc workplace null ngoài ý đồ.
- [ ] Tower start có ammo thật, không chỉ hiển thị giả.

## Deliverable mong muốn
- Start package ổn định, đủ để chụp screenshot và bắt đầu playtest loop đầu tiên.

---

# TASK 4 — M1-A4 Season/day clock tối thiểu

## Goal
Thời gian và phase chạy được ở mức tối thiểu để tạo backbone cho loop.

## Checklist
- [ ] Xác nhận source of truth cho run clock.
- [ ] Chốt state tối thiểu cần có trên clock:
  - [ ] Year
  - [ ] Season
  - [ ] Day
  - [ ] Phase (Build / Defend)
- [ ] Tạo/init giá trị start cho run mới.
- [ ] Cho day tiến theo thời gian thật hoặc debug-scaled time.
- [ ] Chuyển season đúng khi hết số ngày của mùa.
- [ ] Chuyển phase đúng theo season rule hiện tại.
- [ ] Expose state đủ để HUD đọc.
- [ ] Nếu có event phase/season changed, đảm bảo event được phát đúng.
- [ ] Có cách debug/fast-forward để test season transitions nhanh.

## Verify
- [ ] Day tăng đúng.
- [ ] Season chuyển đúng thứ tự.
- [ ] Phase Build/Defend phản ánh đúng mùa theo design hiện tại.
- [ ] Không có trường hợp clock reset/nhảy sai khi New Run.

## Deliverable mong muốn
- Clock chạy thật, không còn là placeholder text.

---

# TASK 5 — M1-G1 Top HUD tối thiểu

## Goal
Người chơi luôn đọc được thời gian, phase và state lõi ngay từ Wave 1.

## Checklist
- [ ] Chốt vị trí top HUD trong gameplay screen.
- [ ] Hiển thị được:
  - [ ] Year
  - [ ] Season
  - [ ] Day
  - [ ] Current Phase
  - [ ] Speed controls (ít nhất placeholder usable nếu speed chưa xong hoàn toàn)
- [ ] Chốt resource counters tối thiểu cần hiện trong Wave 1.
- [ ] Bind HUD với run clock state thật.
- [ ] Nếu resource chưa full loop, dùng state đủ thật để không gây hiểu nhầm.
- [ ] Đảm bảo HUD update đúng khi day/season đổi.
- [ ] Phase hiện tại phải đủ nổi bật.
- [ ] HUD không che khu nhìn quan trọng của start settlement.

## Verify
- [ ] Vào run là nhìn thấy thời gian/phase ngay.
- [ ] Day/Season đổi thì HUD đổi theo.
- [ ] HUD không rung, không stale state, không hiển thị giá trị placeholder sai.

## Deliverable mong muốn
- Một top HUD tối thiểu nhưng thật, đủ để toàn bộ các wave sau bám vào.

---

## 4. Smoke test cho Wave 1

Sau khi xong 5 task trên, chạy checklist nhanh sau:
- [ ] App mở → Main Menu → New Run hoạt động
- [ ] Vào game thấy đúng start map baseline
- [ ] Vào game thấy đúng start package
- [ ] Có 3 NPC đúng assignment start
- [ ] Day đang chạy
- [ ] Season/Phase đang đọc được trên HUD
- [ ] Restart/New Run lần 2 không bị state cũ dính lại

---

## 5. Những thứ chưa làm trong Wave 1

Chưa làm ở wave này:
- placement valid/invalid hoàn chỉnh
- assignment panel usable
- harvest / haul loop
- ammo pipeline thật
- defend phase pressure thật
- inspect panel / notifications đầy đủ

Nếu một task mới không trực tiếp giúp 5 backbone task ở trên hoàn thành, nên để sang Wave 2+.

---

## 6. Wave 1 done means what?

Wave 1 chỉ tính DONE khi build trả lời được câu này:

> “Mình đã có một run khởi đầu thật sự tồn tại chưa, hay vẫn chỉ là các phần rời rạc?”

Nếu vẫn chưa vào được New Run → map → start package → clock → HUD một cách sạch và lặp lại được, Wave 1 chưa xong.
