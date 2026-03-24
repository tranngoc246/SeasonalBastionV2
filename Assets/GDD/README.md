# Assets/GDD

Thư mục này giữ **working set GDD hiện tại** của Seasonal Bastion.

## Cấu trúc thư mục

### `00_Master`
- `SEASONAL_BASTION_GDD_COMPLETE_v1.0_VN.md`
- Single source of truth ở mức product/design tổng quan.

### `10_Specs`
- `SEASONAL_BASTION_UI_SPEC_v1.0_VN.md`
- `SEASONAL_BASTION_UX_ONBOARDING_SPEC_v1.0_VN.md`
- `SEASONAL_BASTION_CONTENT_SCOPE_SPEC_v1.0_VN.md`

### `20_Roadmap`
- `SEASONAL_BASTION_VERTICAL_SLICE_ROADMAP_v1.0_VN.md`

### `30_Backlog`
- `SEASONAL_BASTION_BACKLOG_M1_VERTICAL_SLICE_v1.0_VN.md`
- `SEASONAL_BASTION_BACKLOG_M2_YEAR1_COMPLETE_v1.0_VN.md`
- `SEASONAL_BASTION_BACKLOG_M3_BASE_RUN_COMPLETE_v1.0_VN.md`

### `40_Implementation`
- `SEASONAL_BASTION_IMPLEMENTATION_CHECKLIST_M1_WAVE1_v1.0_VN.md`
- `SEASONAL_BASTION_IMPLEMENTATION_CHECKLIST_M1_WAVE2_v1.0_VN.md`
- `SEASONAL_BASTION_IMPLEMENTATION_CHECKLIST_M1_WAVE3_v1.0_VN.md`
- `SEASONAL_BASTION_IMPLEMENTATION_CHECKLIST_M1_WAVE4_v1.0_VN.md`
- `SEASONAL_BASTION_IMPLEMENTATION_CHECKLIST_M1_WAVE5_v1.0_VN.md`

## File nên đọc theo thứ tự
1. `00_Master/SEASONAL_BASTION_GDD_COMPLETE_v1.0_VN.md`
2. `10_Specs/*`
3. `20_Roadmap/SEASONAL_BASTION_VERTICAL_SLICE_ROADMAP_v1.0_VN.md`
4. `30_Backlog/*`
5. `40_Implementation/*`

## Cách dùng nhanh
- Muốn hiểu game là gì → đọc **00_Master**
- Muốn làm UI → đọc **10_Specs/UI Spec**
- Muốn làm onboarding / readability → đọc **10_Specs/UX Onboarding Spec**
- Muốn kiểm soát scope → đọc **10_Specs/Content Scope Spec**
- Muốn biết nên làm gì tiếp → đọc **20_Roadmap**
- Muốn bắt tay vào task cụ thể theo milestone → đọc **30_Backlog**
- Muốn bắt tay vào implementation checklist chi tiết của M1 → đọc **40_Implementation**

## Quy ước
- Chỉ sửa file trong `00_Master` khi thay đổi ảnh hưởng product direction hoặc nhiều subsystem cùng lúc.
- Các thay đổi về flow triển khai nên phản ánh ở `20_Roadmap`, `30_Backlog`, hoặc `40_Implementation` tùy mức độ.
- Tránh tạo thêm tài liệu lớn mới nếu chưa thực sự cần; ưu tiên cập nhật bộ working set hiện tại.
