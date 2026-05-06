# Docs Restructure Proposal

> Mục tiêu: dọn lại cây `docs/` để giảm file trùng vai trò, làm rõ file nào là source-of-truth, file nào là live checklist, file nào chỉ còn giá trị lịch sử.

---

## 1. Vấn đề hiện tại

Cây `docs/` đang trộn lẫn nhiều loại tài liệu:
- product/design docs dài hạn
- architecture notes
- implementation checklists đang sống
- task breakdown tạm thời
- sprint/island-map docs cũ
- audit/technical-debt notes

Hệ quả:
- cùng một workstream có thể có 2-4 file song song
- khó biết file nào còn authoritative
- checklist dễ lệch code thực tế
- doc cũ không chết hẳn nhưng vẫn gây nhiễu

---

## 2. Taxonomy đề xuất

```text
docs/
  architecture/        # kiến trúc, module boundaries, refactor plans
  GDD/                 # product/design/roadmap/backlog chuẩn
  active/              # checklist/workstream đang sống
    stabilization/
    opening-economy/
    endgame/
  backlog/             # task breakdown còn giá trị nhưng chưa active ngay
  archive/             # doc cũ, superseded, historical notes
```

---

## 3. Mapping đề xuất

> Status: **đã áp dụng pass đầu** vào cây `docs/` ngày 2026-05-06. Một số file còn lại có thể tiếp tục move ở pass sau nếu cần.

### Giữ nguyên vị trí
- `docs/architecture/tong-quan-ranh-gioi-module.md`
- `docs/architecture/ke-hoach-tach-file-refactor.md`
- `docs/GDD/**`

### Chuyển sang `docs/active/`
- `docs/active/stabilization/checklist-on-dinh-hoa.md`
- `docs/active/opening-economy/checklist-do-on-dinh-seed-opening-economy.md`
- `docs/active/opening-economy/ma-tran-smoke-opening-economy.md`
- `docs/active/endgame/checklist-luong-endgame.md`

### Chuyển sang `docs/backlog/`
- `docs/backlog/audit-no-ky-thuat.md`
- `docs/archive/phan-ra-task-di-chuyen-npc-theo-road.md`
- `docs/archive/phan-ra-task-population-food-upkeep.md`

### Chuyển sang `docs/archive/`
- `docs/archive/phan-ra-task-on-dinh-seed-opening-economy.md`
  - superseded by live checklist + smoke matrix
- `docs/archive/phan-ra-task-hybrid-resource-zone-generation.md`
  - likely partially superseded by opening-economy hardening batch
- `docs/archive/sprint1-nen-tang-islandmap.md`
- `docs/archive/backlog-islandmap-rebuild.md`
- `docs/archive/chien-luoc-port-v3d.md` (nếu không còn active roadmap owner)

---

## 4. Rule quản lý docs sau khi dọn

### Source-of-truth
- `docs/GDD/**` giữ product/design intent dài hạn
- `docs/architecture/**` giữ structure/refactor intent kỹ thuật

### Live workstream docs
- Mỗi workstream active chỉ nên có:
  - 1 file progress checklist chính
  - 1 file smoke/regression matrix nếu thật sự cần
- Nếu xuất hiện file breakdown cũ, phải đánh dấu rõ:
  - `Superseded`, hoặc
  - move sang `archive/`

### Backlog docs
- Chỉ giữ task breakdown nào còn khả năng được lấy ra làm tiếp
- Nếu backlog đã absorbed vào implementation thực tế, không để nằm lơ lửng ở root `docs/`

### Archive docs
- Không xóa ngay nếu còn giá trị lịch sử
- Thêm banner đầu file:
  - `Archived`
  - `Superseded by ...`
  - `Do not update unless reviving this workstream`

---

## 5. Đề xuất triển khai thực dụng

### Pass 1
- tạo proposal này
- chỉnh các file misleading nhất
- thêm archive markers cho doc đã superseded

### Pass 2
- move file vật lý sang `active/`, `backlog/`, `archive/`
- cập nhật link chéo nếu có

### Pass 3
- dọn encoding tiếng Việt ở các file còn lỗi ký tự
- chuẩn hóa format checklist/status/date

---

## 6. Mục tiêu cuối

Sau khi dọn xong, một người mới vào repo phải trả lời được nhanh:
- game/design intent nằm ở đâu
- architecture intent nằm ở đâu
- workstream nào đang active
- file nào chỉ là historical context
- file nào không nên update nữa
