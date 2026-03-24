# SEASONAL BASTION — GAME DESIGN DOCUMENT COMPLETE v1.0 (VN)

> Trạng thái: Working Master
> Mục đích: Bản GDD tổng hợp hoàn chỉnh để dùng làm tài liệu đọc chính cho thiết kế sản phẩm, gameplay loop, UI, UX, content scope và player experience.
> Nguồn tổng hợp: `SEASONAL_BASTION_GDD_MASTER_LOCKED_v5_VN.md`, `PROJECT_ONEPAGER_ReadingOrder_v0.1.md`, `Run_Complete_Blueprint_v0.1.md` và các tài liệu GDD liên quan trong `Assets/GDD`.

---

## 0. Cách dùng tài liệu này

### 0.1 Ý nghĩa các nhãn quyết định
- **LOCKED**: quyết định cứng, không thay đổi nếu chưa có lý do rất rõ và không có review riêng.
- **TUNABLE**: được phép chỉnh trong balancing/playtest mà không làm đổi bản chất game.
- **OUT OF SCOPE**: chưa làm trong base scope hiện tại; không nên chen vào backlog gần nếu chưa có quyết định mới.

### 0.2 Cách đọc nhanh
- Nếu cần hiểu game ở mức sản phẩm: đọc mục 1 → 6.
- Nếu cần hiểu flow người chơi: đọc mục 7 → 18.
- Nếu cần chốt phạm vi ship: đọc mục 19 → 24.
- Nếu cần giữ định hướng team: xem mục 24 và 25 trước khi thêm feature mới.

### 0.3 Snapshot quyết định hiện tại

#### LOCKED
- Game là premium single-player, run-based roguelite city builder + tower defense trên grid.
- Một run kéo dài 2 năm; win sau Winter Year 2, lose khi HQ HP = 0.
- Seasonal split loop là trục chính của trải nghiệm.
- Road / Entry / Driveway là luật placement thật, không phải flavor.
- NPC dùng workplace-based assignment; NPC mới mặc định là Unassigned.
- HQ worker chỉ là worker đa năng đầu game cho Build / Repair / HaulBasic.
- Warehouse không chứa Ammo.
- Ammo chain chuẩn là Forge → Armory → Tower.
- Game phải ưu tiên readability, failure clarity, và strategy friction hơn interface friction.

#### TUNABLE
- Số ngày mỗi mùa.
- Tốc độ thời gian / speed multipliers.
- Cost build / upgrade / repair.
- NPC spawn cadence, food requirement, housing pacing.
- Storage caps, local caps, ammo request threshold.
- Wave intensity, elite density, boss numbers.
- Mức penalty khi build trong Defend phase.

#### OUT OF SCOPE (base hiện tại)
- Endless mode hoàn chỉnh.
- Automation nâng cao / policy logistics / queue control phức tạp.
- Blueprint macro systems.
- Meta progression sâu.
- Content expansion lớn vượt quá nhu cầu vertical slice / base ship.

## Mục lục
- [1. Product Overview](#1-product-overview)
- [2. Core Design Pillars](#2-core-design-pillars)
- [3. Run Structure](#3-run-structure)
- [4. Core Gameplay Loop](#4-core-gameplay-loop)
- [5. Map, Grid, and Spatial Rules](#5-map-grid-and-spatial-rules)
- [6. Road, Entry, and Placement](#6-road-entry-and-placement)
- [7. Starting Package and New Run](#7-starting-package-and-new-run)
- [8. Buildings and Roles](#8-buildings-and-roles)
- [9. NPC, Population, and Workplace System](#9-npc-population-and-workplace-system)
- [10. Job System](#10-job-system)
- [11. Resources, Economy, and Storage](#11-resources-economy-and-storage)
- [12. Ammo Pipeline](#12-ammo-pipeline)
- [13. Combat and Defense](#13-combat-and-defense)
- [14. Time, Phases, and Speed Control](#14-time-phases-and-speed-control)
- [15. UI Overview](#15-ui-overview)
- [16. UX Principles](#16-ux-principles)
- [17. Notifications and Guidance](#17-notifications-and-guidance)
- [18. Tutorial and Onboarding](#18-tutorial-and-onboarding)
- [19. Save / Load](#19-save--load)
- [20. End-of-Season and End-of-Run Summary](#20-end-of-season-and-end-of-run-summary)
- [21. Difficulty and Pacing](#21-difficulty-and-pacing)
- [22. Replayability and Meta Layer](#22-replayability-and-meta-layer)
- [23. Content Scope for Base Version](#23-content-scope-for-base-version)
- [24. Design Risks to Watch](#24-design-risks-to-watch)
- [25. Definition of a Great Build](#25-definition-of-a-great-build)
- [26. One-Paragraph Product Summary](#26-one-paragraph-product-summary)

---

## 1. Product Overview

### 1.1 Tên game
**Seasonal Bastion**

### 1.2 Thể loại
- Premium single-player
- Run-based roguelite
- City builder chiến lược
- Tower defense theo mùa
- Grid-based deterministic simulation

### 1.3 Core Fantasy
Người chơi xây dựng và vận hành một settlement phòng thủ qua các mùa, dùng quy hoạch, lao động và hậu cần để sống sót qua các đợt tấn công ngày càng khắc nghiệt.

### 1.4 Player Promise
Người chơi phải luôn cảm thấy:
- Mình đang xây một hệ thống sống, không chỉ đặt nhà.
- Mỗi mùa phát triển là chuẩn bị cho một mùa kiểm tra.
- Thắng thua đến từ planning, staffing và logistics.
- Tower mạnh chưa đủ; vận hành mới quyết định sống còn.

### 1.5 Business Model
- **Base game:** Premium, đầy đủ loop từ New Run đến Win/Lose.
- **DLC tương lai:** Endless mode, automation/logistics nâng cao, late-game systems.

### 1.6 Unique Selling Points
1. **Season Split Loop**: phát triển mùa ấm, trả giá mùa lạnh.
2. **Road + Entry Puzzle**: placement là bài toán quy hoạch thật.
3. **Workplace-based NPC system**: NPC là labor strategy, không phải trang trí.
4. **Ammo logistics**: combat và economy gắn vào nhau bằng pipeline thật.
5. **Run-based replayability**: mỗi run là một bài toán chiến lược hoàn chỉnh.

---

## 2. Core Design Pillars

1. **Seasonal pacing rõ ràng** — build trong Dev phase, chịu hậu quả trong Defend phase.
2. **Quy hoạch là gameplay** — road, entry, spacing và layout phải có ý nghĩa.
3. **Labor là tài nguyên chiến lược** — assignment và workplace tạo decision space thật.
4. **Hậu cần quan trọng ngang hỏa lực** — ammo và logistics là phần lõi, không phải phụ trợ.
5. **Ít hệ nhưng liên kết chặt** — mỗi subsystem phải nâng giá trị cho subsystem khác.
6. **Khó vì quyết định, không khó vì UI** — complexity nằm ở chiến lược, không nằm ở việc đoán luật.

---

## 3. Run Structure

### 3.1 Win/Lose
- **Win:** sống sót qua Winter Year 2.
- **Lose:** HQ HP = 0.

### 3.2 Run Length
Một run gồm **2 năm**:
- Spring
- Summer
- Autumn
- Winter

### 3.3 Seasonal Intent
- **Spring:** hồi nhịp, mở rộng nền kinh tế.
- **Summer:** tối ưu, greed, chuẩn bị hạ tầng và ammo.
- **Autumn:** áp lực bắt đầu, readiness bị kiểm tra.
- **Winter:** đỉnh điểm combat, logistics và resilience.

### 3.4 End-of-Season Cadence
Sau mỗi mùa có bảng tóm tắt để giúp người chơi:
- hiểu tiến độ
- đọc bottleneck
- nhìn threat mùa tới
- chuẩn bị lại kế hoạch

---

## 4. Core Gameplay Loop

### 4.1 Macro Loop
1. Thu thập tài nguyên
2. Vận chuyển và lưu kho
3. Xây / nâng cấp / sửa chữa
4. Mở rộng workforce và infrastructure
5. Sản xuất ammo
6. Phân phối ammo tới defense
7. Chịu wave / boss pressure
8. Đọc hậu quả qua summary
9. Bắt đầu mùa mới với bài toán mới

### 4.2 Micro Loop
Trong lúc chơi, người chơi liên tục:
- đọc state settlement
- gán NPC
- đặt road/building
- giải bottleneck
- chuẩn bị cho defend phase
- phản ứng với warning và thiếu hụt

---

## 5. Map, Grid, and Spatial Rules

### 5.1 Map Style
- Grid-based map, logic theo cell.
- Kích thước baseline: **64x64**.
- Layered world: Ground / Water / Road / Resources / Buildings / Zones / Spawn Gates.

### 5.2 Overlap Rules
- Water chặn mọi thứ.
- Building không overlap road/resource/obstacle/zone.
- Road không overlap building hoặc blockers.
- Resource / obstacle / zone không overlap nhau.

### 5.3 Settlement Readability
Layout của người chơi phải đọc được bằng mắt:
- road spine ở đâu
- khu logistics ở đâu
- khu sản xuất ở đâu
- khu phòng thủ ở đâu
- điểm nghẽn ở đâu

### 5.4 Difficulty Gradient
- Gần HQ: an toàn hơn, dễ đọc hơn.
- Xa HQ: reward cao hơn nhưng logistics tốn hơn và rủi ro cao hơn.

---

## 6. Road, Entry, and Placement

### 6.1 Road Role
Road là backbone của settlement:
- hỗ trợ connectivity
- quyết định placement hợp lệ
- tạo flow logistics
- tăng giá trị quy hoạch

### 6.2 Locked Road Rules
- Road đặt theo 4 hướng orthogonal.
- Công trình phải kết nối road qua **Entry / Driveway** hợp lệ.
- Entry/Driveway phải được xử lý deterministic.

### 6.3 Placement Design Goals
Placement phải là một bài toán thú vị, không phải thao tác cơ học.
Người chơi phải nghĩ về:
- hướng entry
- connectivity
- future expansion
- travel distance
- defensive layout

### 6.4 Placement UX Requirements
- Ghost valid/invalid rõ ràng
- Hiển thị lý do fail cụ thể
- Preview entry/driveway rõ
- Không để người chơi phải đoán tại sao không đặt được

---

## 7. Starting Package and New Run

### 7.1 Starting Settlement
Khởi đầu gồm:
- HQ
- 2 House
- Farm/Farmhouse + farm zone
- Lumber Camp + resource zone
- 1 Arrow Tower full ammo
- road seed tối thiểu nối các công trình chính

### 7.2 Starting NPCs
- 3 NPC ban đầu:
  - 1 assigned HQ
  - 1 assigned Farm
  - 1 assigned Lumber Camp
- Housing còn dư để sớm sinh NPC thứ 4, dùng như tutorial moment cho assignment.

### 7.3 New Run Goals
Người chơi vào game phải:
- thấy settlement đã “sống”
- hiểu được assignment cơ bản
- sớm thấy pressure của growth và defense
- không bị overload ngay từ phút đầu

---

## 8. Buildings and Roles

### 8.1 Core Buildings
- HQ
- House
- Road

### 8.2 Resource / Production Buildings
- Farm / Farmhouse
- Lumber Camp
- Quarry
- Iron Hut / mine equivalent

### 8.3 Infrastructure / Logistics Buildings
- Warehouse
- Builder Hut

### 8.4 Ammo Chain Buildings
- Forge
- Armory

### 8.5 Defense Buildings
- Arrow Tower
- Cannon Tower
- Frost Tower
- Fire Tower
- Sniper Tower

### 8.6 Building Design Rule
Mỗi building phải có vai trò chiến lược rõ ràng:
- mở workplace
- mở job set
- tạo hoặc giải bottleneck
- thay đổi nhịp run
- tác động rõ lên planning

---

## 9. NPC, Population, and Workplace System

### 9.1 NPC Philosophy
NPC là labor unit chiến lược, không phải combat pawn trực tiếp.

### 9.2 NPC States
- Unassigned
- Assigned
- Idle at workplace
- Moving
- Working
- Leisure / Inspect
- Waiting / blocked nếu cần

### 9.3 Assignment Rules
- NPC mới sinh ra là **Unassigned**.
- Người chơi gán NPC vào workplace.
- NPC chỉ làm job thuộc job set của workplace đó.
- Auto-fill chỉ dùng như hỗ trợ onboarding đầu game, không thay thế quyết định người chơi lâu dài.

### 9.4 Workplace Design
Mỗi workplace có:
- worker slots
- allowed job types
- assigned workers
- potential work radius / targets
- trạng thái thiếu hoặc dư nhân lực

### 9.5 HQ Special Role
HQ là workplace đặc biệt cho early game:
- có storage cơ bản
- worker HQ làm được Build / Repair / HaulBasic
- không harvest và không làm ammo logistics chuyên biệt

### 9.6 Population Growth
NPC mới chỉ sinh khi:
- còn housing capacity
- đủ food
- thỏa cooldown / spawn interval

Mục tiêu:
- tạo nhịp growth tự nhiên
- tạo ra các decision assignment có giá trị
- tránh flood người chơi bằng quá nhiều nhân lực mới

---

## 10. Job System

### 10.1 Role of Jobs
Job system là thứ biến settlement từ tĩnh sang động.

### 10.2 Main Job Groups
- Harvest
- HaulBasic
- BuildDeliver
- BuildWork
- RepairWork
- CraftAmmo
- ResupplyTower
- Leisure / Inspect / fallback jobs

### 10.3 Job Lifecycle
1. Job được tạo
2. Job được assign đúng NPC
3. NPC di chuyển tới target
4. NPC thực hiện công việc
5. State world được cập nhật
6. Claim / runtime refs được cleanup sạch
7. NPC trở về idle hoặc tìm việc mới

### 10.4 Design Rule
- Không duplicate jobs vô nghĩa
- Không giữ stale assignment
- Cleanup phải tuyệt đối sạch
- Workplace filtering phải đúng
- Mỗi job nên nhìn vào là hiểu mục đích

---

## 11. Resources, Economy, and Storage

### 11.1 Main Resources
- Wood
- Stone
- Food
- Iron
- Ammo

### 11.2 Economy Philosophy
Tài nguyên chỉ tăng khi:
- đúng worker làm đúng việc
- resource được harvest thật
- resource được deliver về nơi hợp lệ

### 11.3 Resource Flow
1. Resource node / zone tồn tại
2. Worker harvest
3. Resource vào local storage
4. Haul jobs mang tài nguyên tới nơi cần
5. Resource được dùng cho build / repair / craft / sustain

### 11.4 Storage Rules
- HQ có storage cơ bản đầu game
- Warehouse chứa tài nguyên thường
- Producer có local storage riêng
- Armory chứa ammo
- Tower có ammo pool riêng
- **Warehouse không chứa Ammo**

### 11.5 Design Intent
Resource system phải khiến người chơi nghĩ về:
- khoảng cách
- throughput
- bottleneck
- staffing
- infrastructure timing

---

## 12. Ammo Pipeline

### 12.1 Ammo Chain
**Forge → Armory → Tower**

### 12.2 Rules
- Forge craft ammo từ resource phù hợp
- Armory lưu ammo và dispatch
- Tower request ammo khi xuống dưới ngưỡng
- Tower hết ammo thì ngừng bắn

### 12.3 Design Purpose
Ammo pipeline dùng để:
- nối combat với economy
- tạo chiều sâu logistics
- buộc người chơi chuẩn bị chứ không chỉ spam DPS

### 12.4 UX Requirements
Player phải đọc được:
- tower nào thiếu ammo
- armory còn ammo không
- forge có đang craft không
- request/resupply có đang bị nghẽn không

---

## 13. Combat and Defense

### 13.1 Combat Role
Combat là bài kiểm tra của settlement, không phải mini-game tách rời.

### 13.2 Core Combat Elements
- Enemy waves
- Spawn gates / lanes
- HQ target pressure
- Tower targeting / firing
- Damage / HP / repair
- Ammo consumption
- Boss encounters

### 13.3 Year Curve
- Year 1: học hệ thống, wave readable hơn
- Year 2: pressure cao hơn, elite/boss nặng hơn, logistics bị thử thách thật

### 13.4 Boss Design Role
Boss phải kiểm tra settlement holistically:
- sustained DPS
- ammo logistics
- repair ability
- resilience và planning

---

## 14. Time, Phases, and Speed Control

### 14.1 Core Time Rules
- Run chia theo ngày và mùa
- Simulation mang tính deterministic
- Mỗi phase có nhịp khác nhau

### 14.2 Player Controls
- Pause
- 1x
- 2x
- 3x
- Defend phase tự đưa về 1x để người chơi kịp đọc tình huống

### 14.3 UX Goals
Người chơi luôn biết:
- đang ở Year/Season/Day nào
- phase hiện tại là Build hay Defend
- tốc độ game hiện tại là bao nhiêu

---

## 15. UI Overview

### 15.1 Main Menu
- New Run
- Continue (nếu có save)
- Settings
- Quit

### 15.2 In-Run HUD
Top bar hiển thị:
- Year / Season / Day
- current phase
- speed controls
- pause state
- tài nguyên chính

### 15.3 Build Menu
- nhóm building theo category
- hiển thị cost và lock state
- quick preview trước placement

### 15.4 Workforce / Assignment Panel
- danh sách NPC unassigned
- danh sách workplace và slots
- trạng thái staffing thiếu/đủ
- thao tác assign rõ ràng, ít click

### 15.5 Inspect Panel
Khi chọn building / NPC / tower:
- thông tin hiện tại
- trạng thái hoạt động
- bottleneck hiện tại
- actions phù hợp (repair, cancel construction, assign, v.v.)

### 15.6 Notifications
- vị trí giữa phía trên, dưới topbar
- max 3
- newest-first
- dedupe / anti-spam

### 15.7 Summary Screens
- end-of-season summary
- win screen
- lose screen
- end-of-run summary

---

## 16. UX Principles

### 16.1 Strategy Friction, Not Interface Friction
Người chơi nên dừng vì đang suy nghĩ chiến lược, không phải vì không hiểu UI.

### 16.2 All Failure Must Be Readable
Khi có lỗi hoặc blockage, game phải cho biết:
- chuyện gì đang sai
- nghẽn ở đâu
- người chơi nên nhìn vào subsystem nào

### 16.3 Readability Over Raw Complexity
Game được phép sâu, nhưng phải đọc được bằng mắt và bằng panel.

---

## 17. Notifications and Guidance

### 17.1 Notification Types
- Info
- Warning
- Critical
- Tutorial hint
- System hint

### 17.2 Priority Cases
Game nên notify rõ ở các case:
- NPC mới xuất hiện
- thiếu food
- tower low ammo
- build blocked vì thiếu resource
- workplace thiếu worker
- local storage sắp đầy
- defend phase sắp bắt đầu

### 17.3 Design Goal
Notification phải thúc đẩy hành động đúng lúc, không phải chỉ spam thông tin.

---

## 18. Tutorial and Onboarding

### 18.1 Goal
Dạy người chơi đủ để chơi được, không dạy hết tất cả hệ thống ngay lập tức.

### 18.2 Suggested Onboarding Order
1. Giới thiệu HQ và settlement hiện tại
2. Giới thiệu NPC assignment
3. Giới thiệu road / placement
4. Giới thiệu harvest và resource flow
5. Giới thiệu defend pressure
6. Giới thiệu ammo pipeline khi người chơi thật sự cần

### 18.3 Golden Rule
Chỉ dạy cái người chơi sẽ dùng trong vài phút tiếp theo.

---

## 19. Save / Load

### 19.1 Save Goals
Save/load phải:
- đáng tin
- không giữ stale runtime state
- không duplicate order/job
- rebuild đúng runtime-derived data cần thiết

### 19.2 What Must Persist
- world state
- roads
- buildings / towers / NPCs / sites
- tiến độ run
- relevant economy state

### 19.3 What Must Be Sanitized/Rebuilt
- stale current job refs
- transient claims
- runtime cache
- active build order tracking
- runstart-derived metadata nếu cần

---

## 20. End-of-Season and End-of-Run Summary

### 20.1 Season Summary
Hiển thị tối thiểu:
- resources gained/spent
- buildings built/upgraded
- damage / repair
- population trend
- threat forecast

### 20.2 Run Summary
Hiển thị tối thiểu:
- days survived
- peak population
- resources gathered
- ammo crafted/delivered
- towers built
- buildings lost
- bosses defeated
- speed usage / core strategic stats nếu cần

### 20.3 Design Purpose
Summary phải tạo ra:
- closure
- reflection
- lesson for next run
- replay motivation

---

## 21. Difficulty and Pacing

### 21.1 Early Game
- dễ đọc
- đủ thành công ban đầu
- dạy hệ thống đúng nhịp

### 21.2 Mid Game
- bottleneck thật xuất hiện
- staffing và logistics bắt đầu quan trọng mạnh
- player phải chọn infrastructure priorities

### 21.3 Late Game
- system mastery bị kiểm tra
- greed bị trừng phạt
- settlement resilience quyết định kết quả

### 21.4 Good Difficulty
Khó vì:
- planning sai
- staffing sai
- layout sai
- logistics sai

Không khó vì:
- feedback mù
- UI kém
- rules khó hiểu

---

## 22. Replayability and Meta Layer

### 22.1 Replay Drivers
- map seed / biome variation
- king rule-changer
- mutator nhẹ
- different settlement decisions each run

### 22.2 Design Rule
Replayability không được làm game khó đọc hơn quá mức.
Người chơi phải vẫn hiểu vì sao mình thắng/thua.

---

## 23. Content Scope for Base Version

### 23.1 Must-Ship Core
- 1 full run complete từ menu đến win/lose
- start map baseline tốt
- đủ building cốt lõi
- đủ towers có role riêng
- đủ waves / enemies / bosses cho 2 năm
- assignment UI tối thiểu tốt
- save/load đáng tin
- notifications đủ dùng
- season/run summary

### 23.2 Nice-to-Have
- biome variety rộng hơn
- mutator variety cao hơn
- visual flavor / polish nhiều hơn
- meta layer sâu hơn

### 23.3 Out of Scope for Base
- advanced automation
- endless mode sâu
- logistics policies nâng cao
- blueprint macro systems
- content explosion chưa cần thiết

---

## 24. Design Risks to Watch

1. **Over-friction** — quá nhiều luật trước khi người chơi thấy payoff.
2. **Under-reward** — settlement chạy đúng nhưng không đem lại cảm giác thành quả.
3. **Logistics opacity** — người chơi không hiểu nghẽn ở đâu.
4. **Combat disconnect** — combat vui riêng, economy vui riêng, không ăn nhau.
5. **Weak onboarding** — 10–20 phút đầu không đủ rõ hoặc quá tải.

## 24.1 Quy tắc quản lý thay đổi thiết kế

### Chỉ sửa trực tiếp vào file này khi:
- thay đổi làm ảnh hưởng tới core fantasy hoặc product direction
- thay đổi làm ảnh hưởng tới nhiều subsystem cùng lúc
- cần cập nhật lại single source of truth cho team

### Không cần sửa trực tiếp vào file này khi:
- chỉ đang tuning numbers
- chỉ đang sửa UI wording nhỏ
- chỉ đang playtest và thử một biến thể tạm thời
- chỉ đang xử lý implementation detail không đổi player-facing design

### Khi một quyết định đổi trạng thái
- Từ **TUNABLE → LOCKED**: chỉ khóa sau khi đã playtest đủ và team đồng thuận.
- Từ **OUT OF SCOPE → IN SCOPE**: phải ghi rõ vì sao và cắt cái gì khác nếu scope tăng.
- Từ **LOCKED → mở lại**: cần lý do rõ hơn mức “có vẻ chưa ổn”.

---

## 25. Definition of a Great Build

Một build tốt của Seasonal Bastion phải khiến người chơi nói được:
- “Mình hiểu vì sao base mình đang ổn hoặc sắp chết.”
- “Mỗi building mình đặt đều có lý do.”
- “Mỗi mùa tới đều khiến mình lo nhưng vẫn háo hức.”
- “Mình thua là do planning, không phải game ăn gian.”
- “Lần sau mình sẽ build khác.”

---

## 26. One-Paragraph Product Summary

**Seasonal Bastion là một game premium run-based kết hợp city builder, logistics sim và tower defense trên grid, nơi người chơi xây dựng một pháo đài sống qua các mùa, dùng assignment lao động, quy hoạch road/entry và chuỗi hậu cần tài nguyên–ammo để chuẩn bị cho các đợt tấn công ngày càng khắc nghiệt, với mỗi mùa là một vòng lặp chuẩn bị–trả giá rõ ràng và mỗi run là một bài học chiến lược hoàn chỉnh.**
