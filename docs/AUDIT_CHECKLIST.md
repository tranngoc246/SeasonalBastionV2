🧠 SeasonalBastionV2 – Audit Checklist
🔴 P0 — BẮT BUỘC LÀM NGAY (Critical)
1. Save System – Migration
 Implement migration logic trong SaveMigrator.cs
 Thêm versioning rõ ràng cho schema save
 Test load save cũ sau khi thay đổi data structure
2. World Lifecycle / WorldOps
 Hoàn thiện CreateBuilding() (init state đầy đủ từ def)
 Hoàn thiện DestroyBuilding() (publish event đầy đủ)
 Đồng bộ WorldState với EventBus
 Đảm bảo các hệ khác (UI, Pathfinding, Job…) không giữ reference cũ
3. Building Destruction Flow
 Thiết kế lifecycle rõ: Alive → Destroyed → Removed
 Cleanup toàn bộ:
 Grid occupancy
 World index
 Job liên quan
 Tránh trạng thái “đã phá nhưng vẫn tồn tại logic”
4. Performance – Pathfinder (HOT PATH)
 Loại bỏ new NpcPathfinder() trong executor
 Dùng shared instance từ GameServices
 Hoặc tạo PathService riêng
 Kiểm tra GC allocation trong job loop
5. Critical Tests
 Test Save/Load round-trip (full world)
 Test destroy building → cleanup đầy đủ
 Test job cancel / fail / reload
 Test deterministic với cùng seed
🟠 P1 — NÊN LÀM NGAY SAU P0
6. Refactor GameServices (God Object)
 Chia nhỏ:
 CoreServices
 WorldServices
 EconomyServices
 CombatServices
 Giảm dependency trực tiếp toàn bộ GameServices
 Inject interface thay vì container lớn
7. Refactor Large Classes
 Tách AmmoService.cs
 Tách EnemySystem.cs
 Tách SaveService.cs
 Tách DebugHUDHub.cs
 Tách PlacementInputController.cs
8. UI Binding Stability
 Loại bỏ FindObjectOfType() trong runtime
 Inject dependency từ bootstrap
 Kiểm soát lifecycle UI rõ ràng
9. Save System Refactor
 Tách:
 SaveWriter
 SaveReader
 StateMapper
 Kiểm soát mapping DTO ↔ State rõ ràng
 Thêm test cho corrupted save
10. Regression Test Refactor
 Tách file Regression_P0P1_Tests.cs
 Nhóm theo feature:
 Economy
 Combat
 Build
 Save/Load
🟢 P2 — CẢI TIẾN DÀI HẠN
11. Event Lifecycle Standardization
 Chuẩn hóa event:
 OnCreated
 OnUpdated
 OnDestroyed
 Đảm bảo tất cả system subscribe đúng lifecycle
12. Domain Separation
 Tách rõ:
 Economy logic
 Combat logic
 World logic
 Tránh cross-dependency không cần thiết
13. Debug System Optimization
 Gom log qua Logger wrapper
 Phân level:
 Info
 Warning
 Error
 Verbose
 Tách debug code khỏi production
14. Integration / PlayMode Tests
 Test full gameplay loop:
 Start Run → Build → Combat → Save → Load
 Test long-run (30 phút không lỗi)
 Test stress NPC/job system
15. Logging & Monitoring
 Giảm spam Debug.Log
 Thêm tracking lỗi runtime quan trọng
 Log theo context (jobId, entityId…)