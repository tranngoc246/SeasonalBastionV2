DLC: ENDLESS MODE — Automation + Logistics + Infinite Goals (GDD SPEC)
0) Mục tiêu DLC (giá trị mua DLC)
DLC mở ra trải nghiệm “căn cứ tự vận hành vô hạn”:
Không có màn hình WIN. Người chơi chơi 1 save lâu dài, mùa lặp mãi.
Độ khó tăng theo “Year”, nhưng không chỉ tăng chỉ số: tăng hành vi địch + áp lực logistics.
Người chơi chuyển từ “đặt tay từng việc” sang “thiết kế hệ thống + policy + blueprint + queue”, giống cảm giác automation/management game.
USP DLC (3 ý để bán):
Ammo + Repair chain biến defense thành bài toán logistics thật sự.
Automation Policies + Upgrade Queue: căn cứ tự chạy theo chiến lược bạn đặt.
Endless Goals: milestones + score + megaprojects tạo mục tiêu vô hạn.
1) Endless Save Loop (cấu trúc vô hạn)
1.1 Season Loop
Mỗi year gồm 4 mùa: Spring/Summer (Dev), Autumn/Winter (Defend).
Year tăng vô hạn: Y1, Y2, Y3…
1.2 Trạng thái season (state machine)
PreSeason → DevSeasonActive → TransitionToDefend → DefendSeasonActive → PostSeasonResults → NextYear
1.3 Quy tắc “Pause Upgrade trong Defend”
Trong Autumn/Winter:
CẤM: xây mới, upgrade, relocate (trừ “emergency rebuild” nếu bạn chọn bật).
CHO PHÉP: Repair, Refuel/Reload Ammo, Rebuild (tùy policy), Combat jobs.
Mục tiêu: ép người chơi chuẩn bị trong Dev, và Defend là kiểm tra hệ thống.
2) Logistics DLC: Ammo + Repair Chain (late-game pressure)
2.1 Tài nguyên mới/được kích hoạt trong DLC
Ammo (đạn) — dùng cho tower
Parts/Tools (tùy bạn muốn 1 hay 2 resource; khuyến nghị 1 resource “Parts” cho đơn giản)
Repair Kits (không bắt buộc; có thể coi Repair dùng Parts trực tiếp)
Khuyến nghị tối giản: Ammo + Parts. Repair tiêu Parts, Ammo tiêu Ammo.
2.2 Tiêu thụ
Tower bắn → tiêu Ammo
Tower/HQ/Wall bị đánh → cần Repair (tiêu Parts) để hồi HP (hoặc hồi theo tick khi có repair job)
2.3 Sản xuất
Workshop / Armory: sản xuất Ammo từ Iron + Wood (hoặc Iron + Parts)
Repair Station: biến Iron/Stone thành Parts (nếu cần)
Các tỉ lệ là tunable table (mục 8)
2.4 Vận chuyển (visual logistics)
Vẫn giữ nguyên triết lý base: resource số là global.
Nhưng DLC cho “ảo giác logistics” mạnh hơn:
NPC “deliver ammo crate” tới tower (visual)
NPC “carry parts” tới công trình repair (visual)
Logic thật sự vẫn là Job + reservation (không phải vật phẩm vật lý), tránh nổ scope.
3) Automation System (Policy-based, Queue-driven)
3.1 Nguyên tắc
Automation không tự quyết mục tiêu, chỉ thực thi:
Upgrade Queue do người chơi xếp
Blueprint build plan do người chơi vẽ
Các policy toggle (auto repair/refuel/rebuild)
Automation luôn bị giới hạn bởi:
Builder slots / workforce
Reserve thresholds
Season budget
Season gating (pause upgrade trong Defend)
4) Upgrade Queue System (đã chốt: 1-A)
4.1 Khái niệm
Người chơi có một danh sách Upgrade Queue (hàng đợi).
Mỗi item = (TargetBuildingId, DesiredLevel hoặc UpgradeType).
Item chỉ được “dispatch thành job” nếu thỏa:
đang ở Dev season (Spring/Summer)
đủ tài nguyên sau khi trừ reserve
còn budget mùa
còn builder capacity
4.2 UI/UX bắt buộc
Panel: Queue List (drag reorder)
Mỗi item hiển thị:
target, level, cost, trạng thái (Waiting / Reserved / InProgress / Blocked)
lý do blocked (thiếu resource, hết budget, builder bận, bị pause do Defend, road invalid…)
Có nút:
“Pause Queue”
“Clear Completed”
“Skip item”
“Pin critical” (đưa lên đầu)
4.3 Đảm bảo không snowball
Queue không được “tự nhân bản” (ví dụ auto-add upgrade). Người chơi là người thêm item.
5) Budget & Reserve Control (đã chốt: 2-A)
5.1 Reserve Thresholds (ngưỡng dự trữ)
Người chơi đặt “luôn giữ lại” tối thiểu:
FoodReserveMin
AmmoReserveMin
PartsReserveMin
Wood/Stone/IronReserveMin
Rule: Queue/Blueprint không được tiêu dưới ngưỡng.
5.2 Season Budget (ngân sách theo mùa)
Người chơi đặt:
DevSeasonBudgetPercent cho từng nhóm: Economy / Defense / Utility
Hoặc đơn giản: MaxResourceSpendPerDevSeason theo resource
Rule: khi dispatch job, hệ thống “đánh dấu” phần tiêu vào budget; hết budget → job bị Blocked.
5.3 Combat Priority Lock
Trong Defend season:
Budget tự chuyển sang “Defense Ops only”:
Repair budget
Ammo refill budget
6) Automation Policies (toggle — dễ hiểu, shipable)
6.1 Policy toggles
AutoRepair: tự tạo repair jobs cho công trình quan trọng khi HP < threshold
AutoRefuelAmmo: tự tạo refill jobs khi Ammo tower < threshold
AutoRebuild: tự rebuild tower bị phá nếu có blueprint/slot
AutoResupplyPriority: ưu tiên resupply tower theo “Defense Tier”
6.2 Thresholds (tunable)
RepairThresholdHPPercent (vd 40%)
AmmoThresholdPercent (vd 30%)
CriticalBuildings list (HQ, main towers…)
6.3 Người chơi điều khiển cấp độ can thiệp
“Only Critical” / “All Defensive” / “All Buildings”
Default khuyến nghị: Only Critical (tránh spam job)
7) Builder Progression (đúng ý bạn: căn cứ càng lâu càng tự động)
7.1 Builder Levels (tối giản 1–5)
Builder có Level 1–5.
Level tăng dựa trên BuildXP tích lũy (tổng thời gian xây/upgrade/repair).
Level ảnh hưởng:
BuildSpeedMultiplier
Unlock “Advanced Upgrade” (vd level 3+ mới được upgrade tower branch)
MaxConcurrentTasks (optional, khuyến nghị chỉ tăng speed để đơn giản)
7.2 Tương tác với Automation
Queue dispatch vẫn theo builder capacity.
Builder level giúp hệ thống “tự vận hành” mượt hơn theo thời gian, nhưng không phá game vì vẫn bị budget/reserve/season gating.
8) Endless Goals: Milestones + Score + Megaprojects (đã chốt: 3-A)
8.1 Milestones theo năm
Ví dụ:
Survive Year 1
Survive 3 Winters liên tiếp không mất tower
Reach Population 50/100/200
Maintain Ammo surplus X trong 1 year
8.2 Bastion Rating (Score)
Score tổng hợp từ:
SurvivalYears
DefenseEfficiency (damage taken, towers lost)
EconomicStability (resource variance, starvation events)
LogisticsHealth (ammo stockouts count, repair backlog)
Expansion (map control, road network efficiency)
8.3 Megaprojects
Các công trình “ngốn” tài nguyên lớn, tạo mục tiêu sandbox:
Great Wall Ring
Grand Arsenal (giảm ammo cost)
Winterproof Network (giảm debuff mùa đông)
Beacon Towers (mở vision / early warning)
Megaproject không bắt buộc để chơi, nhưng là “lý do tồn tại” của endless.
9) Difficulty Scaling trong Endless (không chỉ tăng chỉ số)
9.1 Scaling theo YearIndex
Mỗi year mở thêm:
1 behavior mới hoặc biến thể: shield, ranged, sapper, siege…
thêm hướng spawn
“raid events” hiếm: elite wave
9.2 Season Modifiers (Endless-only, nhẹ)
Winter: giảm move speed, giảm tower range nhẹ
Autumn: fog giảm vision
(Chỉ nếu bạn muốn, không bắt buộc)
10) Quy tắc an toàn để không phá core fun
Automation không tự chọn nâng cái gì — chỉ làm theo queue/blueprint/policy.
Pause Upgrade trong Defend — giữ nhịp game.
Reserve + Budget — chống snowball và chống “auto tiêu sạch”.
Mọi thứ là Job — thống nhất hệ thống, debug được.
Deterministic scaling — wave spawn theo year/season/day seed.
11) Tuning Tables (placeholder để bạn cân bằng sau)
Bạn có thể đưa các bảng sau vào data (JSON/ScriptableObject):
Ammo production rate / cost
Parts production rate / cost
Repair cost curve theo level
Tower ammo consumption per shot
Policy thresholds default
Budget default per difficulty
12) Definition of Done (DLC)
Người chơi tạo endless save → mùa chạy vô hạn, year tăng.
Có upgrade queue, reorder, thấy lý do blocked rõ.
Reserve thresholds hoạt động: không bao giờ tiêu dưới ngưỡng.
Dev season: queue chạy tự động; Defend season: upgrade pause, repair/ammo vẫn chạy.
AutoRepair/AutoRefuel hoạt động ổn định, không spam vô hạn.
Builder progression tạo khác biệt rõ sau vài year (nhanh hơn, unlock advanced).
Milestones/Score/Megaprojects tạo mục tiêu dài hạn.
Save/Load ổn (quan trọng nhất cho endless).