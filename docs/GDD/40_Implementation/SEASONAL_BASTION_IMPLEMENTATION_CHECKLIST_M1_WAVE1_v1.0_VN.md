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

> Cập nhật trạng thái thực tế đến 2026-03-25:
> - Wave 1 backbone hiện đã ở mức **done-ish / usable** cho vertical slice nền.
> - Nhiều mục dưới đây đã được code + smoke + regression khóa, nhưng vẫn còn một ít việc polish UX/UI trước khi coi là “đóng đẹp”.
> - Quy ước trạng thái dùng trong checklist này:
>   - `[x]` = đã làm xong ở mức chấp nhận được cho Wave 1
>   - `[~]` = đã có nền chạy được / done-ish nhưng còn polish hoặc chưa khóa hết bằng test end-to-end
>   - `[ ]` = chưa làm hoặc chưa đủ tin cậy để tick

# TASK 1 — M1-A1 New Run flow tối thiểu

## Goal
Từ Main Menu bấm **New Run** là vào gameplay state hợp lệ.

## Checklist
- [x] Xác nhận scene/menu flow hiện tại đang dùng gì làm entry point.
- [x] Xác nhận New Run hiện đang stub, thiếu, hay có flow cũ cần thay thế.
- [x] Chốt 1 đường đi duy nhất cho New Run trong M1 (tránh nhiều đường tạm song song).
- [x] Tạo hoặc hoàn thiện logic reset state trước khi start run mới.
- [x] Đảm bảo world/services/runtime state được init sạch khi New Run bắt đầu.
- [x] Nếu có scene loading, đảm bảo scene gameplay vào đúng state expected.
- [x] Nếu không có scene riêng, đảm bảo bootstrap/reset trong cùng scene không để lại state cũ.
- [x] Gắn New Run button vào flow thật.
- [x] Xác nhận từ menu có thể vào run nhiều lần liên tiếp mà không bị state leak rõ ràng.
- [~] UX overwrite save khi bấm `New Run` hiện vẫn đang theo hướng wipe save ngay; chấp nhận tạm cho dev/test nhưng chưa phải flow ship đẹp.

## Verify
- [x] Bấm New Run từ trạng thái app mới mở → vào game đúng.
- [x] Back to Menu → New Run lần 2 vẫn sạch.
- [x] Retry/new run liên tiếp không giữ NPC/building/resource cũ.

## Deliverable mong muốn
- Có một đường chạy New Run ổn định và dùng được cho toàn bộ milestone M1.

---

# TASK 2 — M1-A2 Start map baseline hợp lệ

## Goal
Có một map baseline đủ tốt để chứng minh loop, không cần procedural hay variety.

## Checklist
- [x] Chốt dùng 1 start map handcrafted cho M1.
- [x] Xác nhận map size baseline (64x64 hoặc tương đương) đã phù hợp với camera + readability.
- [x] Chốt khu HQ placement.
- [x] Chốt road seed baseline.
- [x] Chốt khu farm/forest/lumber gần HQ.
- [x] Chốt một khoảng buildable space đủ để người chơi thử expansion sau này.
- [x] Chốt hoặc đánh dấu sơ bộ lane/gate/defend entry area nếu đã cần cho M1.
- [x] Kiểm tra map không có choke vô lý khiến placement fail quá sớm.
- [~] Kiểm tra camera framing ban đầu có cho thấy phần quan trọng của start settlement.

## Verify
- [x] Vào run thấy base khởi đầu đọc được ngay.
- [x] Có đủ không gian để làm placement test sau này.
- [x] Không có blocker vô tình chặn các công trình start.

## Deliverable mong muốn
- 1 map baseline duy nhất cho M1, dùng nhất quán trong test và iteration.

---

# TASK 3 — M1-A3 Start package spawn đúng

## Goal
Khi vào run, settlement khởi đầu phải “đã sống” đúng thiết kế.

## Checklist
- [x] Spawn HQ đúng vị trí.
- [x] Spawn 2 House đúng vị trí.
- [x] Spawn Farm/Farmhouse đúng vị trí.
- [x] Spawn Lumber Camp đúng vị trí.
- [x] Spawn 1 Arrow Tower đúng vị trí.
- [x] Seed road đúng theo layout đã chốt.
- [x] Chốt ammo start cho Arrow Tower = full.
- [x] Spawn đúng 3 NPC start.
- [x] Gắn workplace start hợp lệ:
  - [x] 1 NPC HQ
  - [x] 1 NPC Farm
  - [x] 1 NPC Lumber
- [~] Xác nhận housing capacity và population start phù hợp với ý đồ onboarding.
- [x] Kiểm tra mọi building start có runtime state hợp lệ ngay sau spawn.
- [x] Hardening thêm spawn NPC: nếu `spawnCell` config không hợp lệ thì runtime relocate sang cell hợp lệ gần đó.

## Verify
- [x] New Run lần nào cũng ra đúng start package.
- [x] Không có NPC mồ côi hoặc workplace null ngoài ý đồ.
- [x] Tower start có ammo thật, không chỉ hiển thị giả.

## Deliverable mong muốn
- Start package ổn định, đủ để chụp screenshot và bắt đầu playtest loop đầu tiên.

---

# TASK 4 — M1-A4 Season/day clock tối thiểu

## Goal
Thời gian và phase chạy được ở mức tối thiểu để tạo backbone cho loop.

## Checklist
- [x] Xác nhận source of truth cho run clock.
- [x] Chốt state tối thiểu cần có trên clock:
  - [x] Year
  - [x] Season
  - [x] Day
  - [x] Phase (Build / Defend)
- [x] Tạo/init giá trị start cho run mới.
- [x] Cho day tiến theo thời gian thật hoặc debug-scaled time.
- [x] Chuyển season đúng khi hết số ngày của mùa.
- [x] Chuyển phase đúng theo season rule hiện tại.
- [x] Expose state đủ để HUD đọc.
- [x] Nếu có event phase/season changed, đảm bảo event được phát đúng.
- [x] Có cách debug/fast-forward để test season transitions nhanh.

## Verify
- [x] Day tăng đúng.
- [x] Season chuyển đúng thứ tự.
- [x] Phase Build/Defend phản ánh đúng mùa theo design hiện tại.
- [x] Không có trường hợp clock reset/nhảy sai khi New Run.
- [x] Continue/load giữ đúng state clock đã save, không bị biến thành New Run ngầm.

## Deliverable mong muốn
- Clock chạy thật, không còn là placeholder text.

---

# TASK 5 — M1-G1 Top HUD tối thiểu

## Goal
Người chơi luôn đọc được thời gian, phase và state lõi ngay từ Wave 1.

## Checklist
- [x] Chốt vị trí top HUD trong gameplay screen.
- [x] Hiển thị được:
  - [x] Year
  - [x] Season
  - [x] Day
  - [x] Current Phase
  - [x] Speed controls (ít nhất placeholder usable nếu speed chưa xong hoàn toàn)
- [~] Chốt resource counters tối thiểu cần hiện trong Wave 1.
- [x] Bind HUD với run clock state thật.
- [x] Nếu resource chưa full loop, dùng state đủ thật để không gây hiểu nhầm.
- [x] Đảm bảo HUD update đúng khi day/season đổi.
- [~] Phase hiện tại phải đủ nổi bật.
- [~] HUD không che khu nhìn quan trọng của start settlement.

## Verify
- [x] Vào run là nhìn thấy thời gian/phase ngay.
- [x] Day/Season đổi thì HUD đổi theo.
- [x] HUD không rung, không stale state, không hiển thị giá trị placeholder sai.
- [~] Continue/load vào run nên rà thêm một vòng smoke UI để xác nhận resource/speed visual state luôn refresh đẹp.

## Deliverable mong muốn
- Một top HUD tối thiểu nhưng thật, đủ để toàn bộ các wave sau bám vào.

---

## 4. Smoke test cho Wave 1

Sau khi xong 5 task trên, chạy checklist nhanh sau:
- [x] App mở → Main Menu → New Run hoạt động
- [x] Vào game thấy đúng start map baseline
- [x] Vào game thấy đúng start package
- [x] Có 3 NPC đúng assignment start
- [x] Day đang chạy
- [x] Season/Phase đang đọc được trên HUD
- [x] Restart/New Run lần 2 không bị state cũ dính lại
- [x] Save từ scene Game hoạt động thật
- [x] Main Menu → Continue restore đúng save thay vì fallback New Run

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

### Trạng thái thực tế 2026-03-25
Wave 1 hiện đã đạt mức **usable vertical-slice backbone**:
- New Run / Save / Continue đều có flow chạy được và đã có regression quan trọng khóa behavior
- start map + start package baseline đã ổn định đủ để playtest vòng đầu
- HUD clock state không còn là placeholder sai rõ ràng

Tuy vậy vẫn còn vài mục nên polish ở wave kế tiếp hoặc pass cleanup ngắn:
- UX confirm khi `New Run` sẽ wipe save hiện có
- resource/speed visual smoke sau `Continue`
- camera/HUD readability pass cuối nếu muốn chốt đẹp hơn
