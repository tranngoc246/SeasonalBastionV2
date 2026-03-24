# SEASONAL BASTION — IMPLEMENTATION CHECKLIST M1 / WAVE 5 (VN)

> Mục đích: Bóc nhỏ M1 Wave 5 thành checklist implementation cụ thể.
> Wave 5 mục tiêu: hoàn thiện **readability / first-run support / validation** để vertical slice không chỉ chạy được mà còn đọc được và test được.
> Nguồn bám:
> - `SEASONAL_BASTION_BACKLOG_M1_VERTICAL_SLICE_v1.0_VN.md`
> - `SEASONAL_BASTION_GDD_COMPLETE_v1.0_VN.md`
> - `SEASONAL_BASTION_UI_SPEC_v1.0_VN.md`
> - `SEASONAL_BASTION_UX_ONBOARDING_SPEC_v1.0_VN.md`

---

## 1. Scope của Wave 5

Wave 5 chỉ tập trung vào 6 task sau:
1. `M1-G2` — Notification stack tối thiểu
2. `M1-G3` — Inspect panel tối thiểu hữu dụng
3. `M1-F4` — Defend transition clarity
4. `M1-H1` — First 5-minute guidance pass
5. `M1-I1` — Internal smoke checklist
6. `M1-I2` — First-run playtest questions

### Kết quả cuối của Wave 5
Sau Wave 5, build phải đạt được:
- có notification đủ dùng cho các warning quan trọng
- có inspect panel tối thiểu để đọc state thật
- player nhận ra rõ khi game bước vào defend phase
- 5 phút đầu có guidance vừa đủ, không ngợp
- có checklist nội bộ để test nhanh M1
- có bộ câu hỏi playtest để validate người mới

---

## 2. Thứ tự làm đề xuất

1. Notification stack tối thiểu
2. Inspect panel tối thiểu hữu dụng
3. Defend transition clarity
4. First 5-minute guidance pass
5. Internal smoke checklist
6. First-run playtest questions

Không nên viết playtest questions trước khi readability layer tối thiểu chưa có hình hài thật.

---

## 3. Checklist implementation chi tiết

# TASK 1 — M1-G2 Notification stack tối thiểu

## Goal
Có một lớp thông báo đủ dùng để đưa các warning/hint quan trọng tới người chơi.

## Checklist
- [ ] Chốt vị trí notification stack dưới top HUD.
- [ ] Chốt số lượng visible tối đa cho M1 (theo spec: tối đa 3).
- [ ] Chốt thứ tự newest-first.
- [ ] Chốt style tối thiểu cho các mức:
  - [ ] info
  - [ ] warning
  - [ ] critical
- [ ] Kết nối ít nhất các case sau vào notification:
  - [ ] thiếu resource khi build
  - [ ] tower low ammo
  - [ ] defend phase imminent/bắt đầu
  - [ ] NPC mới xuất hiện (nếu flow này đã có trong M1)
- [ ] Có anti-spam/dedupe tối thiểu cho cùng một warning lặp lại liên tục.
- [ ] Đảm bảo notification không che khu nhìn quan trọng quá mức.

## Verify
- [ ] notification hiện đúng thứ tự và không flood màn hình
- [ ] player nhìn thấy warning quan trọng trong lúc chơi slice

## Deliverable mong muốn
- Một stack thông báo tối thiểu nhưng hữu dụng, đủ làm lớp guidance ngắn cho M1.

---

# TASK 2 — M1-G3 Inspect panel tối thiểu hữu dụng

## Goal
Player có một nơi để đọc “sự thật hiện tại” của selection, thay vì đoán state hệ thống.

## Checklist
- [ ] Chốt vị trí inspect panel trong layout hiện tại.
- [ ] Chốt selection types cần support tối thiểu ở M1:
  - [ ] building
  - [ ] tower
  - [ ] NPC
  - [ ] build site (nếu có trong slice)
- [ ] Với building/workplace, hiển thị được tối thiểu:
  - [ ] tên
  - [ ] level hoặc type
  - [ ] worker count / slot count
  - [ ] blocked state hoặc status cơ bản
- [ ] Với tower, hiển thị được tối thiểu:
  - [ ] HP
  - [ ] ammo current / max
  - [ ] trạng thái low ammo / resupply nếu có
- [ ] Với NPC, hiển thị được tối thiểu:
  - [ ] workplace hiện tại
  - [ ] current state/job ngắn gọn
- [ ] Với build site, hiển thị được tối thiểu:
  - [ ] progress
  - [ ] thiếu resource hay không
- [ ] Nếu có action phù hợp, hiển thị tối thiểu action chính mà không làm panel quá nặng.

## Verify
- [ ] click vào building/tower/NPC là panel đổi đúng
- [ ] inspect giúp giải thích ít nhất 2 bottleneck thật trong M1

## Deliverable mong muốn
- Inspect panel đủ để người chơi đọc state cốt lõi của slice.

---

# TASK 3 — M1-F4 Defend transition clarity

## Goal
Người chơi phải biết rõ khi game bước vào phase khác và áp lực đã đổi.

## Checklist
- [ ] Chốt moment transition sang defend phase.
- [ ] Trên HUD, phase hiện tại phải đổi rõ khi vào defend.
- [ ] Có notification hoặc banner báo defend bắt đầu/sắp bắt đầu.
- [ ] Nếu có speed auto về 1x, phải thể hiện đủ rõ để player không tưởng game bị lag/chậm bất thường.
- [ ] Nếu có màu sắc/visual emphasis cho defend, dùng vừa đủ để tạo cảm giác shift mà không ồn.
- [ ] Transition phải diễn ra nhất quán mỗi lần, không có run bị missing signal.

## Verify
- [ ] player nhận ra defend đã bắt đầu mà không cần được nhắc ngoài game
- [ ] player mô tả được rằng phase này khác build phase

## Deliverable mong muốn
- Seasonal split loop bắt đầu đọc được về mặt cảm xúc và UX, không chỉ tồn tại trong logic.

---

# TASK 4 — M1-H1 First 5-minute guidance pass

## Goal
5 phút đầu của vertical slice đủ dẫn người chơi mà không ngợp.

## Checklist
- [ ] Xác định 3–4 bài học tối thiểu phải truyền trong 5 phút đầu:
  - [ ] đây là base của bạn
  - [ ] đây là assignment
  - [ ] đây là placement/road rule
  - [ ] defend sẽ đến
- [ ] Chốt format guidance dùng cho M1:
  - [ ] short notification
  - [ ] contextual hint
  - [ ] panel emphasis nhẹ
- [ ] Không dạy ammo pipeline quá sớm nếu player chưa chạm tới pressure đó.
- [ ] Không hiện nhiều tutorial text cùng lúc.
- [ ] Có ít nhất 1 prompt rõ ràng dẫn player tới assignment hoặc build action đầu tiên.
- [ ] Guidance phải ngắn, action-oriented, không viết như lore dump.

## Verify
- [ ] một người mới có thể nói mình nên làm gì trong vài phút đầu
- [ ] player không bị đập vào 3 popup dài liên tục

## Deliverable mong muốn
- First-run support đủ để player không bị mất phương hướng ngay khi vào slice.

---

# TASK 5 — M1-I1 Internal smoke checklist

## Goal
Có checklist nội bộ để test nhanh mỗi build M1.

## Checklist
- [ ] Chốt một checklist ngắn có thể chạy trong vài phút.
- [ ] Checklist tối thiểu phải cover:
  - [ ] New Run vào được
  - [ ] start package đúng
  - [ ] placement valid/invalid đọc được
  - [ ] assignment usable
  - [ ] harvest/haul/build loop chạy
  - [ ] tower có thể low ammo / hết ammo
  - [ ] defend phase vào được
  - [ ] HQ có thể chết / lose state chạy
- [ ] Lưu checklist ở nơi team dễ dùng chung.
- [ ] Mỗi bug lớn phát hiện trong M1 nên cân nhắc bổ sung một dòng smoke/regression tương ứng.

## Verify
- [ ] có thể chạy checklist từ đầu tới cuối mà không cần nhớ bằng đầu

## Deliverable mong muốn
- Một công cụ giữ M1 không regress quá nhanh khi iteration.

---

# TASK 6 — M1-I2 First-run playtest questions

## Goal
Có bộ câu hỏi gọn để validate vertical slice với người mới.

## Checklist
- [ ] Chốt 5–8 câu hỏi ngắn, không dẫn dắt.
- [ ] Tập trung vào 3 trục:
  - [ ] hiểu game là gì
  - [ ] hiểu mình thua/chậm ở đâu
  - [ ] có muốn chơi tiếp không
- [ ] Giữ ngôn ngữ dễ hiểu, tránh jargon nội bộ.
- [ ] Chuẩn bị chỗ ghi chú lại điểm người chơi bối rối nhất.
- [ ] Chốt câu hỏi bắt buộc tối thiểu:
  - [ ] bạn có hiểu mình phải làm gì trong 10 phút đầu không?
  - [ ] bạn có hiểu vì sao building có lúc không đặt được không?
  - [ ] bạn có hiểu vì sao tower không bắn / thiếu ammo không?
  - [ ] assignment có dễ hiểu không?
  - [ ] bạn có muốn chơi tiếp không?

## Verify
- [ ] có thể dùng bộ câu hỏi này ngay cho 1 buổi test người mới

## Deliverable mong muốn
- Một bộ câu hỏi đủ tốt để M1 được kiểm chứng bằng người thật, không chỉ bằng cảm giác của dev.

---

## 4. Smoke test cho Wave 5

Sau khi xong 6 task trên, chạy checklist nhanh sau:
- [ ] warning quan trọng có notification đủ dùng
- [ ] inspect panel giải thích được state tối thiểu của building/tower/NPC
- [ ] defend transition được nhận ra rõ ràng
- [ ] first 5-minute flow không gây ngợp
- [ ] có smoke checklist nội bộ để chạy lại M1
- [ ] có bộ câu hỏi playtest cho người mới

---

## 5. Những thứ chưa làm trong Wave 5

Chưa làm ở wave này:
- summary polish đẹp
- tutorial production value cao
- inspect panel sâu cho mọi edge case
- notification taxonomy hoàn chỉnh beyond M1 needs
- balancing sâu dựa trên nhiều vòng playtest

Nếu một task không trực tiếp làm M1 dễ đọc hoặc dễ validate hơn, nên để sau.

---

## 6. Wave 5 done means what?

Wave 5 chỉ tính DONE khi build trả lời được câu này:

> “Vertical slice này không chỉ chạy được, mà người mới còn có thể hiểu và đánh giá nó được chưa?”

Nếu slice vẫn chỉ có dev mới hiểu, thì Wave 5 chưa xong.
