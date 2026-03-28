# Task Breakdown — Population Growth + Food Upkeep

## Goal
Thêm hệ population tối giản nhưng usable cho build hiện tại:
- House tạo housing cap
- NPC mới tăng theo growth timer
- Mỗi NPC tiêu **5 Food / ngày**
- Thiếu food chặn growth và tạo starvation pressure
- Hệ phải dễ debug, dễ save/load, hợp với gameplay economy + tower defense hiện tại

---

## Scope

### Pass 1 — Minimal usable loop
- Tính `PopulationCurrent`
- Tính `PopulationCap` từ House đã xây xong
- Trừ food theo ngày: `5 * population`
- Growth theo ngày nếu còn housing slot và đủ food reserve
- Spawn NPC mới ở trạng thái unassigned
- Save/load growth + starvation state
- Có notification cơ bản

### Pass 2 — Pressure / polish
- Starvation penalty theo số ngày thiếu ăn
- Có thể mất dân nếu thiếu ăn kéo dài
- HUD / summary population + food need
- Regression coverage đầy hơn

---

## Rule chốt cho pass này
- `FoodPerNpcPerDay = 5`
- House T1/T2/T3 = `+2 / +4 / +6 housing cap`
- `GrowthDaysPerNpc = 1`
- Chỉ tăng dân nếu `TotalFood >= DailyFoodNeed * 2`
- NPC mới spawn near HQ, `Workplace = 0`, `IsIdle = true`
- Nếu thiếu food:
  - không tăng dân
  - tăng starvation counter
- Pass đầu có thể chưa cần phạt move/work speed nếu muốn giữ scope gọn

---

# FILE 1 — TẠO MỚI `Assets/_Game/Core/Contracts/Economy/PopulationTypes.cs`

## Goal
Tạo DTO / state contract tối thiểu cho population system.

## Cần làm
- [x] Tạo `PopulationState` hoặc struct tương đương
- [x] Field đề xuất:
  - [x] `int PopulationCurrent`
  - [x] `int PopulationCap`
  - [x] `float GrowthProgressDays`
  - [x] `int StarvationDays`
  - [x] `bool StarvedToday`
- [x] Có thể thêm snapshot struct cho UI nếu cần

## Verify
- [x] Contract compile sạch
- [x] Không nhét logic vào DTO

---

# FILE 2 — TẠO MỚI `Assets/_Game/Core/Contracts/Economy/IPopulationService.cs`

## Goal
Expose state + API tối thiểu cho loop/UI/save-load.

## Cần làm
- [x] Tạo interface `IPopulationService`
- [x] API đề xuất:
  - [x] `PopulationState State { get; }`
  - [x] `void Reset()`
  - [x] `void OnDayStarted()`
  - [x] `void RebuildDerivedState()`
- [ ] Nếu cần UI/event:
  - [ ] `event Action<PopulationState> Changed`

## Verify
- [x] Service có surface đủ gọn cho core loop + UI

---

# FILE 3 — TẠO MỚI `Assets/_Game\Population\PopulationService.cs`

## Goal
Implement core logic population + food upkeep.

## Trách nhiệm
- [x] Tính `PopulationCurrent` từ `WorldState.Npcs`
- [x] Tính `PopulationCap` từ House constructed
- [x] Vào mỗi ngày mới:
  - [x] tính `DailyFoodNeed = PopulationCurrent * 5`
  - [x] kiểm tra total food hiện có
  - [x] nếu đủ -> remove food, reset starvation
  - [x] nếu thiếu -> remove hết phần còn lại, tăng starvation
  - [x] nếu đủ điều kiện growth -> tăng `GrowthProgressDays`
  - [x] nếu progress đủ ngưỡng -> spawn 1 NPC mới
- [x] Reset state khi New Run / Retry

## Rule growth
- [x] Chỉ growth nếu:
  - [x] `PopulationCurrent < PopulationCap`
  - [x] `StarvedToday == false`
  - [x] `TotalFood >= DailyFoodNeed * 2`
  - [x] run chưa ended
- [x] `GrowthDaysPerNpc = 1`

## Rule spawn NPC mới
- [x] Spawn `npc_villager_t1`
- [x] Prefer near HQ
- [x] Fallback theo spawn resolver hiện có
- [x] NPC mới:
  - [x] `Workplace = 0`
  - [x] `CurrentJob = 0`
  - [x] `IsIdle = true`

## Gợi ý helper nội bộ
- [x] `RecountPopulationCurrent()`
- [x] `RecountPopulationCap()`
- [x] `TryConsumeDailyFood(out int need, out int consumed)`
- [x] `CanGrowToday(...)`
- [x] `TrySpawnNewVillager()`
- [x] `TryFindSpawnCellNearHq(...)`

## Verify
- [x] 3 NPC -> tiêu đúng 15 food/day
- [x] Có house -> cap tăng đúng
- [x] Đủ điều kiện -> spawn thêm 1 NPC/ngày
- [x] NPC mới là unassigned

---

# FILE 4 — `Assets/_Game/Core/Loop/TickOrder.cs`

## Goal
Hook population system vào vòng đời ngày mới.

## Cần làm
- [x] Xác định điểm an toàn để detect day rollover / day start
- [x] Gọi `PopulationService.OnDayStarted()` đúng 1 lần mỗi ngày
- [x] Không gọi trong run ended

## Chú ý
- [x] Không tick population upkeep mỗi frame
- [x] Chỉ trigger theo ngày

## Verify
- [x] Không double-consume food trong 1 ngày
- [x] Pause/resume không gây consume lặp

---

# FILE 5 — `Assets/_Game/Core/Boot/GameServices.cs`

## Goal
Thêm population service vào service graph.

## Cần làm
- [x] Thêm field `IPopulationService PopulationService`

## Verify
- [x] Các chỗ khác truy cập được service qua `GameServices`

---

# FILE 6 — `Assets/_Game/Core/Boot/GameServicesFactory.cs`

## Goal
Wire concrete `PopulationService` vào runtime.

## Cần làm
- [x] Instantiate `PopulationService`
- [x] Inject dependencies cần thiết:
  - [x] `WorldState`
  - [ ] `WorldIndex` (nếu cần)
  - [x] `StorageService`
  - [x] `DataRegistry`
  - [x] `GridMap`
  - [x] `RunOutcomeService` nếu muốn chặn growth sau endgame
  - [x] `EventBus` nếu muốn publish event
- [x] Reset lifecycle khớp với New Run / Load

## Verify
- [x] Service tồn tại thật trong gameplay session

---

# FILE 7 — `Assets/_Game/Core/Contracts/Save/SaveDTOs.cs`

## Goal
Thêm state tối thiểu để save/load population progress.

## Cần làm
- [x] Thêm DTO save cho population state
- [x] Persist tối thiểu:
  - [x] `GrowthProgressDays`
  - [x] `StarvationDays`
  - [x] `StarvedToday`
- [x] Không cần persist `PopulationCurrent` nếu derive từ NPC store
- [x] Không cần persist `PopulationCap` nếu derive từ buildings

## Verify
- [x] DTO compile sạch
- [x] Không duplicate data có thể derive

---

# FILE 8 — `Assets/_Game/Save/SaveService.cs`

## Goal
Serialize population state vào save file.

## Cần làm
- [x] Đọc `PopulationService.State`
- [x] Ghi sang save DTO

## Verify
- [x] Save giữa lúc đang có growth progress vẫn giữ đúng state

---

# FILE 9 — `Assets/_Game/Save/SaveLoadApplier.cs`

## Goal
Restore population progress sau load.

## Cần làm
- [x] Restore `GrowthProgressDays`
- [x] Restore `StarvationDays`
- [x] Restore `StarvedToday`
- [x] Sau load gọi `RebuildDerivedState()` để recount current/cap từ world thật

## Verify
- [x] Load không làm sai `PopulationCurrent`
- [x] Load không reset nhầm growth progress

---

# FILE 10 — `Assets/_Game/Core/RunStart/RunStartNpcSpawner.cs`

## Goal
Tái dùng logic spawn cell resolver cho population growth.

## Cần làm
- [ ] Cân nhắc extract helper spawn-cell chung từ `RunStartNpcSpawner`
- [x] Hoặc expose utility nội bộ để `PopulationService` dùng lại logic spawn an toàn
- [x] Ưu tiên cell trống gần HQ trước, fallback hợp lệ nếu kẹt

## Verify
- [x] NPC mới không spawn vào building/site blocked cell
- [x] Không spawn OOB

---

# FILE 11 — `Assets/_Game/Core/Contracts/Data/DefDTOs_Missing.cs`

## Goal
Xác nhận data hiện có đủ để tính housing cap.

## Cần làm
- [x] Giữ `BuildingDef.IsHouse`
- [x] Không cần đổi schema nếu housing cap hardcode theo level trong service
- [ ] Nếu muốn data-driven hơn:
  - [ ] thêm `HousingCapByLevel` hoặc field tương đương ở phase sau

## Khuyến nghị pass đầu
- [x] Hardcode cap house theo level trong service để scope gọn

## Verify
- [x] Không phá loader/data hiện tại

---

# FILE 12 — `Assets/_Game/Defs/Buildings/Buildings.json`

## Goal
Chưa bắt buộc sửa ở pass đầu, nhưng cần review để chắc House đã được tag đúng.

## Cần làm
- [x] Xác nhận `bld_house_t1/t2/t3` có `isHouse = true`
- [ ] Nếu phase sau muốn data-driven housing cap thì bổ sung field tương ứng

## Verify
- [ ] House được detect đúng trong runtime

---

# FILE 13 — UI summary / HUD presenter file(s)

## Goal
Hiển thị population + food pressure cho player.

## Cần làm tối thiểu
- [x] Hiển thị `Population: X / Y`
- [x] Hiển thị `Food need/day: Z`
- [x] Nếu starving thì hiện status/hint

## File cần rà
- [x] `HudPresenter`
- [x] overlay/hud runtime file liên quan

## Verify
- [x] Player đọc được tình trạng dân số/lương thực mà không cần debug tool

---

# FILE 14 — Notification / hint wiring

## Goal
Có feedback player-facing khi dân mới tới hoặc thiếu food.

## Cần làm
- [x] Notification: `Có NPC mới!`
- [x] Notification: `Thiếu lương thực`
- [ ] Nếu có starvation dài ngày: `Dân đang rời làng`

## Verify
- [x] Notification không spam mỗi frame
- [x] Cooldown/dedupe hợp lý

---

# FILE 15 — Regression tests

## Goal
Khóa behavior population/food upkeep cốt lõi.

## File đề xuất
- [x] `Assets/_Game/Tests/EditMode/Economy/PopulationServiceTests.cs`
  hoặc thêm có chọn lọc vào regression suite hiện có

## Cần có test
- [x] `PopulationCurrent` đếm đúng theo `WorldState.Npcs`
- [x] `PopulationCap` tính đúng từ House constructed
- [x] 3 NPC tiêu đúng `15 Food/day`
- [x] đủ food + còn cap -> growth progress tăng đúng
- [x] đủ growth progress -> spawn 1 NPC mới unassigned
- [x] thiếu food -> không growth, starvation tăng
- [x] save/load restore growth + starvation đúng
- [x] house chưa xây xong không tăng cap
- [x] upgrade house làm cap tăng đúng

## Verify
- [x] Test pass ổn định
- [x] Không phụ thuộc runtime scene

---

# Manual Smoke Checklist

## Population / Housing
- [ ] Start run -> population ban đầu đúng với số NPC spawn từ config
- [ ] Xây xong House T1 -> population cap tăng +2
- [ ] Upgrade House -> cap tăng đúng theo tier

## Food upkeep
- [ ] Sang ngày mới -> food bị trừ đúng `5 * số NPC`
- [ ] Nếu food không đủ -> starvation tăng, growth bị chặn

## Growth
- [ ] Khi còn housing slot + đủ food reserve -> spawn thêm 1 NPC sau 1 ngày
- [ ] NPC mới xuất hiện ở gần HQ và đang unassigned

## Save / Load
- [ ] Save giữa lúc growth progress đang dở
- [ ] Load lại -> progress/starvation vẫn đúng

---

# Suggested Commit Breakdown

## Commit 1
- `population: add population service contracts and runtime state`

## Commit 2
- `population: implement daily food upkeep and housing cap`

## Commit 3
- `population: add villager growth and spawn flow`

## Commit 4
- `save: persist population growth and starvation state`

## Commit 5
- `ui: show population and food need summary`

## Commit 6
- `tests: add population and food upkeep coverage`

---

# Definition of Done

## Pass 1 DONE khi
- [x] House tạo housing cap đúng
- [x] Mỗi NPC tiêu đúng `5 Food / ngày`
- [x] Growth chỉ xảy ra khi còn cap và đủ food reserve
- [x] NPC mới spawn đúng flow và unassigned
- [x] Save/load giữ được progress + starvation
- [x] Có feedback tối thiểu cho player
- [x] Regression tests cốt lõi đã có

## Pass 2 DONE khi
- [ ] Starvation pressure có gameplay consequence rõ ràng
- [ ] UI/HUD readable hơn
- [ ] Balance bắt đầu usable qua nhiều ngày/wave
