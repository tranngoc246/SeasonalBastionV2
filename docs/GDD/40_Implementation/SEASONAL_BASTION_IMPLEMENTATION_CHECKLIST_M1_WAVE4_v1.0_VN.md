# SEASONAL BASTION — IMPLEMENTATION CHECKLIST M1 / WAVE 4 (VN)

> Mục đích: Bóc nhỏ M1 Wave 4 thành checklist implementation cụ thể.
> Wave 4 mục tiêu: dựng xong **ammo pipeline tối thiểu + defend pressure cơ bản** để vertical slice thật sự có thử thách và hậu quả.
> Nguồn bám:
> - `SEASONAL_BASTION_BACKLOG_M1_VERTICAL_SLICE_v1.0_VN.md`
> - `SEASONAL_BASTION_GDD_COMPLETE_v1.0_VN.md`
> - `SEASONAL_BASTION_CONTENT_SCOPE_SPEC_v1.0_VN.md`
> - `SEASONAL_BASTION_UI_SPEC_v1.0_VN.md`
> - `SEASONAL_BASTION_UX_ONBOARDING_SPEC_v1.0_VN.md`

---

## 1. Scope của Wave 4

Wave 4 chỉ tập trung vào 6 task sau:
1. `M1-E1` — Forge craft ammo tối thiểu
2. `M1-E2` — Armory lưu và dispatch ammo
3. `M1-E3` — Tower low ammo request
4. `M1-E4` — Out-of-ammo stop firing
5. `M1-F1` — 1 defend phase playable
6. `M1-F3` — HQ damage / lose state cơ bản

### Kết quả cuối của Wave 4
Sau Wave 4, build phải đạt được:
- ammo không còn là số giả, mà là resource loop thật
- tower cần ammo để hoạt động
- tower có thể hết ammo và điều đó tạo hậu quả thật
- có ít nhất 1 defend phase playable
- enemy gây pressure đủ để test settlement
- HQ có thể nhận damage và lose state hoạt động

---

## 2. Thứ tự làm đề xuất

1. Forge craft ammo tối thiểu
2. Armory lưu và dispatch ammo
3. Tower low ammo request
4. Out-of-ammo stop firing
5. 1 defend phase playable
6. HQ damage / lose state cơ bản
7. Smoke test wave 4

Không nên làm defend phase trước khi ammo pipeline tối thiểu đã có ý nghĩa, nếu không pressure sẽ không test đúng fantasy của game.

---

## 3. Checklist implementation chi tiết

# TASK 1 — M1-E1 Forge craft ammo tối thiểu

## Goal
Có nguồn tạo ammo thật cho vertical slice.

## Checklist
- [ ] Chốt input resource tối thiểu để craft ammo trong M1.
- [ ] Chốt workplace/worker requirement cho Forge ở mức tối thiểu.
- [ ] Chốt output ammo unit/cadence cơ bản.
- [ ] Đảm bảo ammo được tạo ra như một state thật, không chỉ là chỉ số giả trên tower.
- [ ] Nếu chưa làm hết recipe complexity, vẫn phải có flow craft tối thiểu đúng fantasy.
- [ ] Xác nhận không có đường nào bypass Forge để ammo xuất hiện miễn phí ngoài start ammo của tower đầu game.

## Verify
- [ ] Forge có thể craft ammo khi đủ điều kiện
- [ ] thiếu input thì không craft
- [ ] ammo output được ghi nhận ở node/state đúng

## Deliverable mong muốn
- Vertical slice có một nguồn tạo ammo thật và đọc được.

---

# TASK 2 — M1-E2 Armory lưu và dispatch ammo

## Goal
Ammo phải đi qua Armory trước khi tới tower, đúng fantasy logistics.

## Checklist
- [ ] Chốt state ammo riêng cho Armory.
- [ ] Chốt flow nhận ammo từ Forge hoặc nguồn craft.
- [ ] Chốt flow dispatch ammo từ Armory tới Tower.
- [ ] Xác nhận Warehouse không tham gia chứa ammo trong M1.
- [ ] Nếu logistics path chưa đầy đủ cho mọi case, ít nhất phải có 1 tuyến ammo flow đúng hoạt động.
- [ ] Armory state phải inspect/debug được tối thiểu.

## Verify
- [ ] ammo xuất hiện ở Armory trước khi đến Tower
- [ ] nếu Armory trống thì tower không được resupply đúng logic

## Deliverable mong muốn
- Ammo chain Forge → Armory → Tower được chứng minh ở mức tối thiểu.

---

# TASK 3 — M1-E3 Tower low ammo request

## Goal
Tower phải chủ động tạo nhu cầu hậu cần khi ammo xuống thấp.

## Checklist
- [ ] Chốt ammo threshold tối thiểu cho M1.
- [ ] Khi ammo dưới threshold, tower tạo request/resupply need.
- [ ] Request phải đi vào state mà system khác đọc được.
- [ ] Tránh tạo request spam vô hạn mỗi tick nếu state chưa đổi.
- [ ] Nếu có nhiều tower trong M1, bảo đảm ít nhất tower test đầu tiên có flow đúng.

## Verify
- [ ] tower bắn dần, ammo xuống thấp, request được tạo đúng lúc
- [ ] request không spam vô hạn ngoài ý muốn

## Deliverable mong muốn
- Tower bắt đầu trở thành consumer thật của logistics, không phải object bắn vô điều kiện.

---

# TASK 4 — M1-E4 Out-of-ammo stop firing

## Goal
Chứng minh logistics tác động trực tiếp đến combat.

## Checklist
- [ ] Khi tower còn ammo, tower bắn được đúng như flow hiện tại.
- [ ] Khi tower hết ammo, tower dừng bắn thật.
- [ ] Dừng bắn phải là state đọc được, không phải bug mơ hồ.
- [ ] Nếu có reload/resupply state, tối thiểu phải phản ánh đúng logic low/no ammo.
- [ ] Xác nhận out-of-ammo state có tác động thực lên defend outcome.

## Verify
- [ ] cho tower hết ammo và xác nhận không còn bắn
- [ ] resupply thành công thì tower quay lại bắn được

## Deliverable mong muốn
- “Tower mạnh chưa đủ; phải có ammo” trở thành gameplay thật.

---

# TASK 5 — M1-F1 1 defend phase playable

## Goal
Vertical slice phải có ít nhất một defend phase tạo pressure thật.

## Checklist
- [ ] Chốt thời điểm hoặc trigger bước vào defend phase trong M1.
- [ ] Chốt 1 wave set tối thiểu cho defend phase đầu tiên.
- [ ] Spawn được 1–2 enemy types readable.
- [ ] Enemy đi đúng flow/path về phía target/HQ.
- [ ] Tower/combat/pressure tương tác đủ để người chơi thấy hậu quả của preparation.
- [ ] Defend phase phải đủ dài để test pressure, nhưng không quá dài gây loãng vertical slice.

## Verify
- [ ] player nhìn ra rõ “đây là phase phòng thủ”
- [ ] threat đủ thật để phân biệt với build phase

## Deliverable mong muốn
- Seasonal pressure lần đầu xuất hiện thành gameplay thật.

---

# TASK 6 — M1-F3 HQ damage / lose state cơ bản

## Goal
Vertical slice phải có consequence thật khi defense fail.

## Checklist
- [ ] HQ có HP/state đủ để nhận damage.
- [ ] Enemy hoặc defend failure route có thể tác động lên HQ.
- [ ] Khi HQ HP về 0, lose state kích hoạt.
- [ ] Lose state có flow tối thiểu usable:
  - [ ] hiển thị đã thua
  - [ ] có đường retry / restart / back (ít nhất một trong các lựa chọn đó)
- [ ] Xác nhận lose state không để simulation tiếp tục chạy bừa nếu không chủ đích.

## Verify
- [ ] cố tình fail defend và thấy lose state đúng
- [ ] lose state không bị mơ hồ kiểu “game cứ chạy nhưng mình đã chết”

## Deliverable mong muốn
- Vertical slice có stakes thật, không phải sandbox an toàn vô hạn.

---

## 4. Smoke test cho Wave 4

Sau khi xong 6 task trên, chạy checklist nhanh sau:
- [ ] Forge craft được ammo
- [ ] Ammo đi qua Armory
- [ ] Tower xuống low ammo thì có request đúng
- [ ] Tower hết ammo thì dừng bắn thật
- [ ] Có ít nhất 1 defend phase playable
- [ ] Enemy tạo pressure đủ để test defense
- [ ] HQ có thể chết và lose state chạy được

---

## 5. Những thứ chưa làm trong Wave 4

Chưa làm ở wave này:
- low ammo warning polish hoàn chỉnh
- inspect panel hoàn chỉnh cho ammo/debug
- defend transition clarity polish
- notification stack đầy đủ
- end-of-session pseudo-summary đẹp hơn
- balancing sâu enemy/tower/ammo curve

Nếu một task không trực tiếp giúp chứng minh ammo logistics hoặc defend pressure, nên để sang Wave 5.

---

## 6. Wave 4 done means what?

Wave 4 chỉ tính DONE khi build trả lời được câu này:

> “Settlement có thể bị trừng phạt thật vì logistics và defense chưa chuẩn bị đủ chưa?”

Nếu tower vẫn mạnh vô điều kiện, hoặc defend chưa tạo consequence thật, thì Wave 4 chưa xong.
