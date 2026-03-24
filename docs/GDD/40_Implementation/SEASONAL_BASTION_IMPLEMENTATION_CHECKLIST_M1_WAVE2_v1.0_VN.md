# SEASONAL BASTION — IMPLEMENTATION CHECKLIST M1 / WAVE 2 (VN)

> Mục đích: Bóc nhỏ M1 Wave 2 thành checklist implementation cụ thể.
> Wave 2 mục tiêu: dựng xong lớp **placement + workforce** để vertical slice bắt đầu có decision space thật.
> Nguồn bám:
> - `SEASONAL_BASTION_BACKLOG_M1_VERTICAL_SLICE_v1.0_VN.md`
> - `SEASONAL_BASTION_GDD_COMPLETE_v1.0_VN.md`
> - `SEASONAL_BASTION_UI_SPEC_v1.0_VN.md`
> - `SEASONAL_BASTION_UX_ONBOARDING_SPEC_v1.0_VN.md`

---

## 1. Scope của Wave 2

Wave 2 chỉ tập trung vào 6 task sau:
1. `M1-B1` — Build mode + placement ghost hoạt động
2. `M1-B2` — Valid/invalid placement feedback rõ
3. `M1-B3` — Road/entry connectivity rule tối thiểu
4. `M1-C1` — NPC start state hợp lệ
5. `M1-C2` — Unassigned/assigned model hoạt động
6. `M1-C4` — Assignment panel tối thiểu usable

### Kết quả cuối của Wave 2
Sau Wave 2, build phải đạt được:
- player mở build mode và thấy placement ghost rõ ràng
- placement obey road/entry rule cơ bản
- player hiểu một building có đặt được hay không
- NPC start state sạch, workplace start hợp lệ
- player assign được ít nhất 1 NPC bằng panel tối thiểu
- player thấy assignment làm thay đổi hành vi settlement

---

## 2. Thứ tự làm đề xuất

1. Build mode + ghost
2. Valid/invalid placement visuals
3. Road/entry validation rule
4. NPC start state cleanup/verify
5. Unassigned/assigned model
6. Assignment panel tối thiểu
7. Smoke test wave 2

Không nên làm assignment panel trước khi unassigned/assigned model và workplace state đủ thật.

---

## 3. Checklist implementation chi tiết

# TASK 1 — M1-B1 Build mode + placement ghost hoạt động

## Goal
Người chơi vào build mode được và thấy ghost building follow logic placement.

## Checklist
- [ ] Chốt entry point vào build mode (button/menu/category click).
- [ ] Chốt building đầu tiên dùng để test placement trong M1.
- [ ] Khi chọn build item, ghost xuất hiện đúng trong world.
- [ ] Ghost cập nhật theo cell/cursor ổn định.
- [ ] Ghost clear đúng khi cancel build mode.
- [ ] Chuyển selection/build mode không để lại state rác.
- [ ] Nếu có shared highlight/ghost layer, đảm bảo nó không conflict với selection/highlighter khác.
- [ ] Xác nhận camera movement hoặc cursor movement không làm ghost lag/nhảy bất thường.

## Verify
- [ ] vào build mode nhiều lần liên tiếp không lỗi
- [ ] ghost xuất hiện / biến mất đúng lúc
- [ ] đổi giữa ít nhất 2 building test (nếu có) không lỗi state

## Deliverable mong muốn
- Build mode tối thiểu có thể dùng để thử placement thật.

---

# TASK 2 — M1-B2 Valid/invalid placement feedback rõ

## Goal
Player phải đọc được placement hợp lệ hay không ngay bằng mắt.

## Checklist
- [ ] Chốt visual state cho valid ghost.
- [ ] Chốt visual state cho invalid ghost.
- [ ] Footprint được hiển thị rõ.
- [ ] Entry direction / entry point được hiển thị rõ ở mức tối thiểu.
- [ ] Invalid state không chỉ đổi màu mơ hồ mà đủ phân biệt rõ với valid.
- [ ] Xác nhận ghost không gây hiểu nhầm với highlight bình thường của world.
- [ ] Nếu có nhiều lý do fail, vẫn phải có một visual invalid state nhất quán.

## Verify
- [ ] một người khác nhìn vào có thể nói ghost hiện tại là valid hay invalid
- [ ] footprint và entry nhìn ra được mà không phải đoán

## Deliverable mong muốn
- Placement visuals đủ rõ để làm nền cho phần fail reasons và road rule.

---

# TASK 3 — M1-B3 Road / entry connectivity rule tối thiểu

## Goal
Placement phải bám đúng fantasy road/entry của game ở mức cơ bản.

## Checklist
- [ ] Chốt rule tối thiểu cho M1: building chỉ đặt được khi có road/entry hợp lệ.
- [ ] Xác định source of truth cho validation placement.
- [ ] Validate đúng các case cơ bản:
  - [ ] cell/placeable hợp lệ
  - [ ] có road connection hợp lệ
  - [ ] thiếu road connection thì fail
- [ ] Nếu đã có driveway/entry helper, xác nhận đang dùng đúng flow.
- [ ] Nếu chưa có đầy đủ implementation cuối, vẫn phải có logic tạm thời đúng fantasy, không fake hoàn toàn.
- [ ] Đảm bảo placement validation chạy đủ nhanh và không giật ghost rõ rệt.

## Verify
- [ ] 1 case valid sát road pass
- [ ] 1 case thiếu road fail
- [ ] 1 case overlap/block fail (nếu rule đó đã active ở M1)

## Deliverable mong muốn
- Placement đã là gameplay thật, không còn là thao tác đặt tùy tiện.

---

# TASK 4 — M1-C1 NPC start state hợp lệ

## Goal
NPC khởi đầu phải ở trạng thái runtime sạch và đúng vai trò thiết kế.

## Checklist
- [ ] Xác nhận toàn bộ 3 NPC start được spawn đúng.
- [ ] Mỗi NPC có state tối thiểu hợp lệ:
  - [ ] id
  - [ ] position/cell
  - [ ] workplace đúng hoặc unassigned đúng
  - [ ] idle/current job state không mồ côi
- [ ] Kiểm tra workplace references của 3 NPC start là hợp lệ.
- [ ] Kiểm tra không có NPC bắt đầu với stale current job / invalid assignment.
- [ ] Nếu NPC start đang được auto-fill ở init, xác nhận logic đó không phá đúng role start package.

## Verify
- [ ] vào New Run nhiều lần, 3 NPC start vẫn sạch
- [ ] inspect/debug state không thấy NPC null-workplace ngoài ý đồ

## Deliverable mong muốn
- NPC start state đủ tin cậy để xây tiếp workforce logic.

---

# TASK 5 — M1-C2 Unassigned/assigned model hoạt động

## Goal
Player nhìn thấy được sự khác biệt giữa NPC chưa gán và NPC đã gán.

## Checklist
- [ ] Chốt state model tối thiểu cho NPC unassigned.
- [ ] Chốt state model tối thiểu cho NPC assigned.
- [ ] Xác nhận NPC assigned chỉ làm job thuộc workplace của mình.
- [ ] Xác nhận NPC unassigned không bị hút vào job production ngoài ý đồ.
- [ ] Nếu đang có auto-fill/onboarding assist, đảm bảo không phá concept “player assigns labor”.
- [ ] Chọn ít nhất 1 observable behavior khác nhau giữa unassigned và assigned để người chơi nhìn ra.

## Verify
- [ ] có ít nhất 1 NPC chuyển từ unassigned → assigned thành công
- [ ] sau khi assign, hành vi hoặc workplace state đổi đủ rõ để thấy kết quả

## Deliverable mong muốn
- Workforce system bắt đầu thể hiện được ý tưởng cốt lõi của game.

---

# TASK 6 — M1-C4 Assignment panel tối thiểu usable

## Goal
Player phải gán được worker mà không cần debug tool hoặc flow rối.

## Checklist
- [ ] Chốt vị trí panel workforce/assignment trong layout hiện tại.
- [ ] Hiển thị được danh sách NPC unassigned.
- [ ] Hiển thị được workplace có slot trống.
- [ ] Hiển thị được worker count / slot count tối thiểu cho workplace.
- [ ] Có hành động assign từ NPC → workplace hoặc workplace → NPC đủ ngắn gọn.
- [ ] Assignment action cập nhật UI ngay sau khi thành công.
- [ ] Slot đầy hoặc assignment invalid phải bị chặn/hint đủ đọc.
- [ ] Panel không cần đẹp hoàn chỉnh, nhưng phải usable thật.

## Verify
- [ ] người chơi thử có thể assign 1 NPC mà không cần giải thích dài
- [ ] UI cập nhật đúng sau assign
- [ ] assign xong thì workplace/NPC state đều phản ánh đúng

## Deliverable mong muốn
- Assignment panel tối thiểu đủ để dùng trong M1 playtest.

---

## 4. Smoke test cho Wave 2

Sau khi xong 6 task trên, chạy checklist nhanh sau:
- [ ] vào run và mở build mode được
- [ ] ghost valid/invalid đọc được bằng mắt
- [ ] sát road thì placement pass, thiếu road thì fail
- [ ] 3 NPC start ở trạng thái sạch
- [ ] có ít nhất 1 NPC unassigned hoặc một thời điểm assign test rõ ràng
- [ ] assignment panel mở được và assign được
- [ ] sau assign, hành vi hoặc state settlement đổi đúng hướng

---

## 5. Những thứ chưa làm trong Wave 2

Chưa làm ở wave này:
- fail reason text chi tiết cho placement
- growth trigger/NPC mới hoàn chỉnh
- harvest/haul loop đầy đủ
- ammo pipeline thật
- defend pressure thật
- inspect panel hoàn chỉnh
- notification stack hoàn chỉnh

Nếu một task không trực tiếp làm placement hoặc workforce usable hơn, nên để sang wave sau.

---

## 6. Wave 2 done means what?

Wave 2 chỉ tính DONE khi build trả lời được câu này:

> “Người chơi đã bắt đầu thật sự ra quyết định về quy hoạch và lao động chưa?”

Nếu player vẫn chưa cảm nhận được rằng:
- road/entry là luật thật
- assignment là việc mình chủ động làm

thì Wave 2 chưa xong.
