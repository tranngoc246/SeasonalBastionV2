# Archive Candidates

> Danh sách này không xóa file nào. Mục tiêu là chốt file nào nên archive để giảm nhiễu trong `docs/`.
>
> Status 2026-05-06: pass đầu đã move một số file sang `docs/archive/`.

---

## Nhóm A - nên archive sớm

### `docs/archive/task-breakdown-opening-economy-seed-stability.md`
**Lý do**
- đã bị superseded bởi:
  - `docs/active/opening-economy/seed-stability-checklist.md`
  - `docs/active/opening-economy/smoke-matrix.md`
- nếu giữ ở root dễ làm team update nhầm file cũ

**Khuyến nghị**
- đã move sang `docs/archive/`
- giữ banner `Superseded`

---

### `docs/archive/task-breakdown-hybrid-resource-zone-generation.md`
**Lý do**
- nhiều phần intent đã bị hấp thụ vào batch hybrid/opening economy gần đây
- dễ chồng chéo với checklist mới

**Khuyến nghị**
- đã move sang `docs/archive/`
- giữ như historical context

---

## Nhóm B - archive nếu roadmap không còn active

### `docs/archive/Sprint1_IslandMap_Foundation.md`
### `docs/archive/Backlog_IslandMap_Rebuild.md`
**Lý do**
- có vẻ là một nhánh workstream riêng
- nếu island-map không còn là active track, nên bỏ khỏi root docs

**Khuyến nghị**
- move sang `docs/archive/` nếu chưa có owner active
- nếu vẫn active, nên move sang `docs/backlog/`

---

### `docs/v3d-port-strategy.md`
**Lý do**
- cần xác nhận còn active hay chỉ là note chiến lược cũ

**Khuyến nghị**
- nếu không có owner hiện tại, archive
- nếu còn roadmap thật, chuyển về `docs/backlog/`

---

## Nhóm C - giữ nhưng cần chỉnh

### `docs/active/endgame/endgame-flow-checklist.md`
**Giữ vì**
- vẫn là live implementation/history doc có giá trị

**Nhưng cần chỉnh**
- đồng bộ lại status với code hiện tại
- thêm note phần nào đã done, phần nào chỉ còn polish

---

### `docs/backlog/technical-debt-audit.md`
**Giữ vì**
- vẫn có giá trị như debt inventory

**Nhưng cần chỉnh**
- đổi vai trò rõ hơn thành technical debt backlog
- thêm owner/status/priority/date nếu muốn dùng thật

---

## Nguyên tắc chung

- Không xóa hẳn ngay nếu file còn giúp hiểu lịch sử quyết định.
- Ưu tiên `archive` hơn `delete`.
- Chỉ xóa khi:
  - nội dung trùng hoàn toàn,
  - đã có file mới thay thế rõ ràng,
  - và không còn giá trị lịch sử/debug context.
