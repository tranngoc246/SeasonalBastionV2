# SEASONAL BASTION — IMPLEMENTATION CHECKLIST M1 / WAVE 3 (VN)

> Mục đích: Bóc nhỏ M1 Wave 3 thành checklist implementation cụ thể.
> Wave 3 mục tiêu: dựng xong **economy / logistics minimum loop** để vertical slice có resource loop thật.
> Nguồn bám:
> - `SEASONAL_BASTION_BACKLOG_M1_VERTICAL_SLICE_v1.0_VN.md`
> - `SEASONAL_BASTION_GDD_COMPLETE_v1.0_VN.md`
> - `SEASONAL_BASTION_CONTENT_SCOPE_SPEC_v1.0_VN.md`
> - `SEASONAL_BASTION_UX_ONBOARDING_SPEC_v1.0_VN.md`

---

## 1. Scope của Wave 3

Wave 3 chỉ tập trung vào 5 task sau:
1. `M1-D1` — Harvest loop cơ bản
2. `M1-D2` — Local storage tối thiểu
3. `M1-D3` — Haul basic tối thiểu
4. `M1-D4` — Build/repair dùng resource thật
5. `M1-B4` — Fail reason đủ đọc

### Kết quả cuối của Wave 3
Sau Wave 3, build phải đạt được:
- resource tăng nhờ worker thật, không phải timer rỗng
- producer có local storage state tối thiểu
- có một flow harvest → haul → usable resource
- build hoặc repair thực sự tiêu resource
- thiếu resource hoặc placement fail bắt đầu có lý do đọc được

---

## 2. Thứ tự làm đề xuất

1. Harvest loop cơ bản
2. Local storage tối thiểu
3. Haul basic tối thiểu
4. Build/repair dùng resource thật
5. Fail reason đủ đọc
6. Smoke test wave 3

Không nên làm fail reason text đẹp trước khi economy loop đã có failure thật để giải thích.

---

## 3. Checklist implementation chi tiết

# TASK 1 — M1-D1 Harvest loop cơ bản

## Goal
Resource phải tăng nhờ labor thật, không phải tự cộng vô hình.

## Checklist
- [ ] Chốt 1–2 nguồn resource đầu game cần chạy trong M1:
  - [ ] Farm/Food
  - [ ] Lumber/Wood
- [ ] Xác định worker nào tạo ra harvest output và bằng flow nào.
- [ ] Chốt unit output tối thiểu / cadence tối thiểu cho M1.
- [ ] Đảm bảo worker phải được assign đúng workplace thì mới tạo output.
- [ ] Xác nhận bỏ worker hoặc thiếu worker thì throughput giảm/không chạy.
- [ ] Xác nhận harvest output đi vào state resource thật, không chỉ là animation giả.

## Verify
- [ ] Farm có worker thì food tăng hoặc output xuất hiện đúng flow
- [ ] Lumber có worker thì wood tăng hoặc output xuất hiện đúng flow
- [ ] bỏ worker thì throughput thay đổi rõ ràng

## Deliverable mong muốn
- Người chơi nhìn thấy rõ “có worker thì có tài nguyên, không worker thì không có”.

---

# TASK 2 — M1-D2 Local storage tối thiểu

## Goal
Resource phải có nơi đứng tạm trước khi được dùng hoặc chở đi.

## Checklist
- [ ] Chốt producer nào có local storage trong M1.
- [ ] Chốt state tối thiểu local storage cần theo dõi:
  - [ ] current amount
  - [ ] cap tối thiểu (nếu đã active trong M1)
- [ ] Khi harvest xong, resource vào local storage thay vì teleport ngay vào global pool.
- [ ] Nếu chưa làm đủ cap behavior, vẫn phải có data/state đủ để mở rộng đúng hướng.
- [ ] Xác nhận inspect/debug có thể nhìn thấy resource đang nằm ở producer/local storage.

## Verify
- [ ] harvest xong resource nằm ở producer/local storage trước
- [ ] có thể phân biệt local resource với resource đã usable ở chỗ khác

## Deliverable mong muốn
- Economy loop không còn cảm giác “magic inventory”.

---

# TASK 3 — M1-D3 Haul basic tối thiểu

## Goal
Có logistics thật giữa nơi tạo resource và nơi dùng resource.

## Checklist
- [ ] Chốt flow haul basic đầu tiên trong M1.
- [ ] Chốt source → destination tối thiểu cho M1:
  - [ ] producer/local storage → HQ hoặc storage node cơ bản
  - [ ] storage → building/site nếu cần cho build/repair
- [ ] Xác nhận có worker/job chịu trách nhiệm haul.
- [ ] Haul phải cập nhật state thật ở source và destination.
- [ ] Nếu chưa hoàn chỉnh mọi case logistics, vẫn phải có ít nhất 1 tuyến flow thật hoạt động.
- [ ] Xác nhận path/workplace requirement không làm haul impossible một cách vô tình.

## Verify
- [ ] ít nhất 1 resource flow harvest → haul → usable destination chạy được
- [ ] source giảm và destination tăng đúng

## Deliverable mong muốn
- Vertical slice bắt đầu có “hậu cần” chứ không chỉ có “sản xuất”.

---

# TASK 4 — M1-D4 Build/repair dùng resource thật

## Goal
Build và repair phải gắn vào resource loop, không được là thao tác free.

## Checklist
- [ ] Chốt 1 building/place order hoặc repair flow dùng làm case test cho M1.
- [ ] Chốt resource cost tối thiểu cho case đó.
- [ ] Khi đủ resource, build/repair tiến hành được.
- [ ] Khi thiếu resource, build/repair bị block thật.
- [ ] Resource bị trừ theo flow thật, không fake.
- [ ] Nếu có delivery từng phần, ít nhất state phải đúng hướng; không cần hoàn hảo toàn bộ ở M1.
- [ ] Build blocked nên đẩy được state/hint để UI/notification có thể đọc sau.

## Verify
- [ ] 1 case build thành công khi đủ resource
- [ ] 1 case build fail/block khi thiếu resource
- [ ] 1 case repair (nếu có trong M1) dùng resource đúng

## Deliverable mong muốn
- Người chơi thấy ngay building/repair là một phần của economy, không phải nút bấm miễn phí.

---

# TASK 5 — M1-B4 Fail reason đủ đọc

## Goal
Khi thứ gì đó fail ở placement hoặc economy/build flow, người chơi đọc được nguyên nhân cơ bản.

## Checklist
- [ ] Chốt danh sách fail reason tối thiểu phải có ở M1:
  - [ ] thiếu resource
  - [ ] placement invalid
  - [ ] no road / invalid entry
  - [ ] blocked / overlap nếu đã active
- [ ] Chọn nơi hiển thị reason trong M1:
  - [ ] notification
  - [ ] build/inspect panel
  - [ ] contextual hint ngắn
- [ ] Viết wording ngắn, không mơ hồ.
- [ ] Đảm bảo fail reason không spam liên tục ngoài tầm kiểm soát.
- [ ] Placement fail và economy fail phải đủ khác ngữ cảnh để player không nhầm.

## Verify
- [ ] player biết vì sao building không đặt được
- [ ] player biết vì sao build/repair chưa chạy được vì thiếu resource

## Deliverable mong muốn
- Failure bắt đầu trở thành bài học chiến lược thay vì cảm giác “game không cho làm”.

---

## 4. Smoke test cho Wave 3

Sau khi xong 5 task trên, chạy checklist nhanh sau:
- [ ] Farm/Lumber có worker thì tạo output thật
- [ ] Resource đi vào local storage hoặc state tương đương đúng hướng
- [ ] Có ít nhất 1 flow haul basic hoạt động
- [ ] Build hoặc repair tiêu resource thật
- [ ] Thiếu resource thì block được và đọc được lý do
- [ ] Placement fail vẫn đọc được lý do cơ bản

---

## 5. Những thứ chưa làm trong Wave 3

Chưa làm ở wave này:
- ammo pipeline thật
- defend pressure thật
- low ammo warning hoàn chỉnh
- inspect panel hoàn chỉnh cho mọi bottleneck
- notification stack hoàn chỉnh
- balancing sâu local caps / throughput

Nếu một task không trực tiếp làm economy/logistics loop thật hơn, nên để sang wave sau.

---

## 6. Wave 3 done means what?

Wave 3 chỉ tính DONE khi build trả lời được câu này:

> “Settlement đã bắt đầu tạo, giữ, chở và tiêu tài nguyên theo cách mà người chơi nhìn ra được chưa?”

Nếu resource vẫn tăng theo kiểu khó hiểu hoặc build vẫn tách rời economy, thì Wave 3 chưa xong.
