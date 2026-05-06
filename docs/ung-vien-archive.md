# Archive Candidates

> Danh sách này không xóa file nào. Mục tiêu là chốt file nào nên archive để giảm nhiễu trong `docs/`.
>
> Status 2026-05-06: pass đầu đã move một số file sang `docs/archive/`.

---

## Nhóm A - nên archive sớm

### `docs/archive/phan-ra-task-on-dinh-seed-opening-economy.md`
**Lý do**
- đã bị superseded bởi:
  - `docs/active/opening-economy/checklist-do-on-dinh-seed-opening-economy.md`
  - `docs/active/opening-economy/ma-tran-smoke-opening-economy.md`
- nếu giữ ở root dễ làm team update nhầm file cũ

**Khuyến nghị**
- đã move sang `docs/archive/`
- giữ banner `Superseded`

---

### `docs/archive/phan-ra-task-hybrid-resource-zone-generation.md`
**Lý do**
- nhiều phần intent đã bị hấp thụ vào batch hybrid/opening economy gần đây
- dễ chồng chéo với checklist mới

**Khuyến nghị**
- đã move sang `docs/archive/`
- giữ như historical context

---

## Nhóm B - archive nếu roadmap không còn active

### `docs/archive/sprint1-nen-tang-islandmap.md`
### `docs/archive/backlog-islandmap-rebuild.md`
**Lý do**
- có vẻ là một nhánh workstream riêng
- nếu island-map không còn là active track, nên bỏ khỏi root docs

**Khuyến nghị**
- move sang `docs/archive/` nếu chưa có owner active
- nếu vẫn active, nên move sang `docs/backlog/`

---

### `docs/archive/chien-luoc-port-v3d.md`
**Lý do**
- là note chiến lược cũ, không nên tiếp tục nằm ở root docs nếu không có active owner rõ ràng

**Khuyến nghị**
- đã move sang `docs/archive/`
- nếu sau này roadmap 3D sống lại, cân nhắc restore/move sang `docs/backlog/`

---

## Nhóm C - giữ nhưng cần chỉnh

### `docs/active/endgame/checklist-luong-endgame.md`
**Giữ vì**
- vẫn là live implementation/history doc có giá trị

**Nhưng cần chỉnh**
- đồng bộ lại status với code hiện tại
- thêm note phần nào đã done, phần nào chỉ còn polish

---

### `docs/backlog/audit-no-ky-thuat.md`
**Giữ vì**
- vẫn có giá trị như debt inventory

**Nhưng cần chỉnh**
- đổi vai trò rõ hơn thành technical debt backlog
- thêm owner/status/priority/date nếu muốn dùng thật

---

### `docs/archive/phan-ra-task-di-chuyen-npc-theo-road.md`
**Lý do**
- là task breakdown lịch sử, không còn là live tracker

**Khuyến nghị**
- đã move sang `docs/archive/`
- giữ như historical context cho movement/pathfinding decisions

---

### `docs/archive/phan-ra-task-population-food-upkeep.md`
**Lý do**
- là task breakdown lịch sử, nhiều phần đã được hấp thụ vào implementation hiện tại của `PopulationService`

**Khuyến nghị**
- đã move sang `docs/archive/`
- giữ như historical context cho population/economy decisions

---

## Nguyên tắc chung

- Không xóa hẳn ngay nếu file còn giúp hiểu lịch sử quyết định.
- Ưu tiên `archive` hơn `delete`.
- Chỉ xóa khi:
  - nội dung trùng hoàn toàn,
  - đã có file mới thay thế rõ ràng,
  - và không còn giá trị lịch sử/debug context.
