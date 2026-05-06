# Docs README

> Quy ước chung cho toàn bộ cây `docs/`.
>
> **Rule bắt buộc:** mỗi khi thay đổi code, scope, tiến độ, hoặc quyết định kỹ thuật có liên quan tới một workstream đã có doc, phải cập nhật doc tương ứng trong cùng lượt làm trước khi commit.

---

## 1. Cách tổ chức hiện tại

- `docs/GDD/`
  - product/design/roadmap/backlog chuẩn
- `docs/architecture/`
  - tài liệu kiến trúc kỹ thuật, boundary, refactor plan
- `docs/active/`
  - checklist / smoke matrix / workstream đang sống
- `docs/backlog/`
  - debt/task docs còn giá trị nhưng chưa active ngay
- `docs/archive/`
  - docs lịch sử hoặc đã bị superseded

---

## 2. Rule cập nhật bắt buộc

### Khi nào phải cập nhật doc
Phải cập nhật doc trong cùng lượt làm nếu có một trong các thay đổi sau:
- đổi scope của feature/workstream
- đổi quyết định kiến trúc hoặc dependency boundary
- hoàn thành thêm một phase/checkpoint lớn
- thêm/chuyển/xóa file quan trọng trong một workstream đang theo dõi
- thay đổi smoke checklist / regression expectations
- phát hiện blocker/follow-up mới đáng ghi lại

### Cập nhật tối thiểu cần có
Với workstream đang active, phải cập nhật tối thiểu:
- status/checklist
- progress log nếu file có mục này
- blockers/follow-up nếu có
- path/tên file tham chiếu nếu vừa rename/move

### Rule commit
- Không commit code nếu doc của workstream liên quan đang lệch rõ với code.
- Nếu chỉ làm refactor nội bộ không đổi behavior nhưng file planning/architecture có nhắc trực tiếp, vẫn nên cập nhật ngắn.
- Nếu doc cũ không còn là source-of-truth, phải đánh dấu `Superseded` hoặc move sang `archive/`.

---

## 3. Quy ước đặt tên file

Ưu tiên:
- tiếng Việt không dấu
- kebab-case
- tên nói rõ vai trò file

Ví dụ tốt:
- `checklist-do-on-dinh-seed-opening-economy.md`
- `ma-tran-smoke-opening-economy.md`
- `ke-hoach-don-kien-truc-source.md`
- `audit-no-ky-thuat.md`

Tránh:
- tên quá chung kiểu `notes.md`, `temp.md`, `misc.md`
- giữ file tiếng Anh cũ khi đã có convention mới, trừ bộ GDD legacy cần bảo toàn tên

---

## 4. Rule sống còn

Nếu code và docs lệch nhau, coi như work chưa được ghi nhận xong.
