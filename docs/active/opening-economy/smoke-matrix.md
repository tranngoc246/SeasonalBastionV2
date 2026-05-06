# Opening Economy Smoke Matrix

> Mục tiêu: có một bộ seed-smoke cố định để kiểm tra nhanh opening economy sau mỗi batch liên quan generation / harvest / tuning.
>
> **Rule bắt buộc:** mỗi lần chạy smoke thật, phải cập nhật lại file này trong cùng lượt làm, giống rule của `docs/active/opening-economy/seed-stability-checklist.md`.

---

## 1. Cách dùng file này

### Trạng thái
- `[ ]` chưa smoke
- `[~]` đã smoke một phần / còn nghi ngờ
- `[x]` pass
- `[!]` blocker

### Mức đánh giá
- `Blocker`: opener fail rõ, thiếu coverage quan trọng, hoặc worker pick sai nghiêm trọng
- `Playable but weak`: chơi được nhưng opening méo, chậm, hoặc target pick chưa đẹp
- `Good`: opener ổn, worker behavior hợp lý, không có vấn đề đáng kể

### Checklist mỗi seed
- [ ] có `Wood` starter usable
- [ ] có `Food` starter usable
- [ ] có `Stone` starter usable
- [ ] iron không chen opener quá mức
- [ ] worker pick starter patch hợp lý
- [ ] khi patch cạn có retarget hợp lý
- [ ] overlay/inspect đúng starter/bonus/authored/legacy semantics
- [ ] có hướng expand hợp lý

---

## 2. Seed set đề xuất

> Chưa verify runtime trên máy này, nên danh sách dưới đây là smoke matrix khởi tạo để dùng ngay khi có môi trường chạy phù hợp.

| Seed | Status | Đánh giá | Ghi chú |
|---|---|---|---|
| 101 | [ ] | TBD | baseline low seed |
| 111 | [ ] | TBD | seed khác layout test cũ |
| 222 | [ ] | TBD | pair với 111 để check variance |
| 333 | [ ] | TBD | seed đã dùng cho negative quality test |
| 555 | [ ] | TBD | seed starter guarantee test |
| 777 | [ ] | TBD | seed runtime cache/generated path |
| 999 | [ ] | TBD | seed bounds safety |
| 12345 | [ ] | TBD | seed usable-quality reference |
| 4242 | [ ] | TBD | seed starter/bonus distinction |
| 9001 | [ ] | TBD | stress random-ish reference |
| 16001 | [ ] | TBD | outer ring distribution spot-check |
| 32003 | [ ] | TBD | larger numeric variance spot-check |

---

## 3. Smoke run notes

### Run 2026-05-06
- [ ] chưa chạy runtime smoke thật trong môi trường Unity/.NET phù hợp
- [x] đã có seed set cố định để smoke ở batch kế tiếp
- [x] đã có acceptance criteria rõ cho generation + harvest opening

---

## 4. Acceptable variance

### Chấp nhận được
- layout khác nhau giữa seed
- số lượng bonus patch thay đổi trong range config
- vị trí starter khác nhau nhưng vẫn giữ opening usable

### Không chấp nhận được
- thiếu `Wood`, `Food`, hoặc `Stone` starter usable
- iron chen quá sát HQ làm opener méo rõ
- worker bỏ starter patch để chạy bonus patch vô lý từ sớm
- patch cạn mà worker không retarget sạch
- runtime/debug overlay mất distinction starter/bonus/fallback

---

## 5. Gợi ý thứ tự smoke thực dụng

1. chạy 4 seed nhanh: `111, 222, 555, 12345`
2. nếu ổn, chạy full 12 seed
3. nếu fail, cập nhật ngay:
   - seed nào fail
   - fail ở generation hay harvest
   - blocker hay chỉ weak
4. sau khi sửa, rerun tối thiểu seed fail + 4 seed nhanh

---

## 6. Checklist smoke thật, cực ngắn

### Vòng 1, smoke nhanh
Chạy theo thứ tự:
1. `111`
2. `222`
3. `555`
4. `12345`

Mỗi seed chỉ cần check 5 thứ:
- [ ] đủ `Wood / Food / Stone` starter usable
- [ ] iron không chen opener quá mức
- [ ] worker pick starter patch hợp lý
- [ ] patch cạn thì retarget sạch
- [ ] không thấy overlay/inspect bị sai nghĩa

### Vòng 2, smoke full
Nếu vòng 1 ổn, chạy tiếp:
5. `101`
6. `333`
7. `777`
8. `999`
9. `4242`
10. `9001`
11. `16001`
12. `32003`

### Rule pass/fail rất ngắn
- có 1 lỗi blocker ở bất kỳ seed nào, dừng và log ngay
- nếu chỉ `Playable but weak`, chạy hết vòng rồi gom issue sau
- sau khi sửa gì đó, rerun tối thiểu:
  - seed fail
  - `111`
  - `555`
  - `12345`
