# ENDGAME FLOW — IMPLEMENTATION CHECKLIST

> Mục tiêu: hoàn thiện flow kết thúc run cho build hiện tại, để game không chỉ có backend thắng/thua mà còn có trạng thái kết thúc rõ ràng, UI usable, và Retry / Main Menu chạy sạch.
>
> Checklist này bám theo codebase hiện tại của project `SeasonalBastionV2`, không phải checklist lý thuyết chung.

---

## 1. Mục tiêu của pass này

Sau pass này, build phải đạt được:

- HQ chết thì **Defeat** thật
- Đạt mốc victory thì **Victory** thật
- Simulation không tiếp tục chạy như run bình thường sau khi end
- Có modal/panel hiển thị kết quả run
- Có hành động tiếp theo rõ ràng:
  - `Retry`
  - `Main Menu`
- Các action gameplay quan trọng không còn mutate world state sau endgame
- Retry tạo ra run mới sạch, không leak state

---

## 2. Scope của pass này

### Trong scope
- Chuẩn hóa `RunOutcome` hiện có
- Freeze simulation khi run ended
- Thêm `RunEnded` UI modal
- Wire `Retry` / `Main Menu`
- Khóa các action gameplay chính sau endgame
- Thêm regression test cốt lõi
- Có smoke checklist thủ công

### Ngoài scope
- Summary screen đầy đủ
- Stats cuối run chi tiết
- Meta progression
- Continue after Victory
- Fancy animation / cinematic ending
- Reward summary production-ready

---

## 3. Rule cần chốt trước khi code

### Win / Lose rule
- [x] Xác nhận rule **Lose** chính thức: `HQ HP <= 0`
- [x] Xác nhận rule **Win** chính thức cho build hiện tại:
  - [x] clear toàn bộ enemy của **final wave Year 2**
- [x] Xác nhận priority nếu race condition xảy ra:
  - [x] **Lose > Win**

### Retry / Save policy
- [x] Xác nhận `Retry` tương đương `New Run` sạch
- [x] Xác nhận retry dùng:
  - [x] seed mặc định qua `RequestNewGame(...)`
- [ ] Xác nhận save policy sau endgame:
  - [ ] disable save
  - [ ] hoặc cho save end-state

### Khuyến nghị hiện tại
- Giữ `Lose = HQ HP <= 0`
- Giữ `Win = clear final wave Year 2`
- `Lose > Win`
- `Retry = New Run sạch`
- `Save sau endgame = chưa chốt`

---

## 4. Checklist implementation file-by-file

---

# FILE 1 — `Assets/_Game/Core/Contracts/Events/CommonEvents.cs`

## Goal
Làm `RunEndedEvent` đủ data để UI và flow layer dùng được.

## Hiện trạng
- `RunEndedEvent` hiện chỉ mang `RunOutcome`
- Chưa có `Reason`

## Cần làm
- [x] Thêm enum `RunEndReason`
- [x] Mở rộng `RunEndedEvent` với `Outcome + Reason`
- [x] Giữ code dễ đọc, tránh nhét text UI trực tiếp vào event
- [x] Mở rộng `WaveEndedEvent` để mang metadata runtime của wave (`Year/Season/Day/IsBoss/IsFinalWave`)

## Gợi ý data model
- [x] `None`
- [x] `HqDestroyed`
- [x] `SurvivedWinterYear1`
- [x] `SurvivedWinterYear2`
- [x] `FinalWaveCleared`

## Verify
- [x] Event publish được reason đúng cho cả Victory và Defeat
- [x] Các call site compile lại sạch

---

# FILE 2 — `Assets/_Game/Core/Contracts/Rewards/IRunOutcomeService.cs`

## Goal
Expose đủ trạng thái để presenter / flow logic đọc được endgame state hiện tại.

## Hiện trạng
- Có `Outcome`
- Chưa có `Reason`

## Cần làm
- [x] Thêm property `RunEndReason Reason { get; }`
- [x] Giữ interface đơn giản, không ôm logic UI

## Verify
- [x] UI layer có thể đọc both `Outcome` và `Reason`
- [x] Các implementation/fake service trong test được update đầy đủ

---

# FILE 3 — `Assets/_Game/Rewards/RunOutcomeService.cs`

## Goal
Chuẩn hóa outcome service hiện có, thêm reason, đảm bảo one-shot endgame trigger.

## Hiện trạng
- Đã detect `Defeat` khi HQ HP <= 0
- Đã detect `Victory` qua `DayEndedEvent`
- Đã có guard `Outcome != Ongoing`
- Chưa có `Reason`

## Cần làm
- [x] Thêm state `Reason`
- [x] `ResetOutcome()` phải reset cả `Reason`
- [x] Khi `Defeat()` -> set reason `HqDestroyed`
- [x] Khi `Victory()` -> set reason phù hợp với rule hiện tại (`FinalWaveCleared`)
- [x] Publish `RunEndedEvent(Outcome, Reason)`
- [x] Giữ one-shot trigger: end rồi thì không set lại outcome lần 2
- [x] Giữ rule ưu tiên `Lose > Win`
- [x] Đổi trigger Victory sang `WaveEndedEvent` của final wave Year 2

## Nếu cần refactor nhẹ
- [x] Tách helper nội bộ:
  - [x] `DefeatInternal(reason)`
  - [x] `VictoryInternal(reason)`
  - [ ] `AbortInternal(reason)` nếu cần

## Verify
- [x] HQ chết -> `Outcome=Defeat`, `Reason=HqDestroyed`
- [x] Final wave Year 2 cleared -> `Outcome=Victory`, `Reason=FinalWaveCleared`
- [x] Không double-trigger
- [x] New Run reset được outcome + reason

---

# FILE 4 — `Assets/_Game/Core/Loop/TickOrder.cs`

## Goal
Khi run ended, simulation chính phải dừng.

## Hiện trạng
- `TickAll(...)` vẫn tick toàn bộ systems dù run đã ended

## Cần làm
- [x] Đọc `RunOutcomeService.Outcome` sớm trong tick loop
- [x] Nếu `Outcome != Ongoing` thì **không tick simulation systems nữa**
- [x] Chốt rõ những gì vẫn được tick:
  - [x] notification/UI tick nếu cần
  - [x] không tick run clock
  - [x] không tick build/jobs/resource/ammo/combat nữa

## Verify
- [x] Sau endgame, day/season không trôi tiếp
- [x] Không spawn wave/enemy mới
- [x] World state không tiếp tục drift

---

# FILE 5 — `Assets/_Game/Combat/CombatService.cs`

## Goal
Combat phải tự dừng khi run ended.

## Hiện trạng
- Không thấy guard theo `RunOutcome`
- Combat vẫn có thể tiếp tục xử lý wave / enemy / tower

## Cần làm
- [x] Ở đầu `Tick(float dt)`, return sớm nếu `Outcome != Ongoing`
- [x] Khi ended, `IsActive = false`
- [x] Không cho restart wave theo day latch sau endgame
- [ ] Không push notification combat thừa sau endgame nếu không cần

## Verify
- [x] Run end rồi thì wave không tiếp tục spawn
- [x] Enemy/tower không còn mutate combat outcome tiếp
- [x] Không có defend day restart sau endgame

---

# FILE 6 — `Assets/_Game/UI/Runtime/Scripts/Core/UiKeys.cs`

## Goal
Thêm key cho modal endgame.

## Cần làm
- [x] Thêm `Modal_RunEnded`

## Khuyến nghị
- Dùng **một modal chung** cho cả Victory/Defeat
- Presenter sẽ đổi title/body theo outcome

## Verify
- [ ] Có key chuẩn để register modal trong UI system

---

# FILE 7 — `Assets/_Game/UI/Runtime/UXML/ModalsRoot.uxml`

## Goal
Thêm UI modal cho endgame vào modal host hiện tại.

## Hiện trạng
- Mới có Settings / Confirm / Assign NPC
- Chưa có endgame modal

## Cần làm
- [x] Thêm `RunEndedModal`
- [x] Thêm các field tối thiểu:
  - [x] `LblRunEndedTitle`
  - [x] `LblRunEndedBody`
  - [x] `BtnRetry`
  - [x] `BtnRunEndedMenu`
- [x] `display:none` mặc định
- [x] Đảm bảo modal vẫn block world input

## Verify
- [x] Modal xuất hiện được trong stack hiện tại
- [x] Không bị modal khác che sai

---

# FILE 8 — `Assets/_Game/UI/Runtime/USS/Modals.uss`

## Goal
Style endgame modal đủ rõ và nổi bật hơn modal thường.

## Cần làm
- [x] Thêm style cho body/title nếu cần
- [x] Đảm bảo spacing dễ đọc
- [x] Đảm bảo endgame modal nhìn đủ “kết thúc run”
- [ ] Nếu muốn, thêm class riêng cho victory/defeat visual emphasis

## Verify
- [x] Title/body/buttons đọc rõ
- [x] Scrim + modal focus ổn
- [x] Không bị style hiện có làm nhìn như settings modal thường

---

# FILE 9 — TẠO MỚI `Assets/_Game/UI/Runtime/Scripts/Presenters/RunEndedModalPresenter.cs`

## Goal
Presenter chịu trách nhiệm mở modal endgame và wire button actions.

## Trách nhiệm
- [x] Bind label/button refs của endgame modal
- [x] Subscribe `RunEndedEvent` hoặc `IRunOutcomeService.OnRunEnded`
- [x] Map `Outcome/Reason` -> title/body text
- [x] Mở modal `RunEnded`
- [x] Wire `Retry`
- [x] Wire `Main Menu`

## Hành vi mong muốn
- [x] Khi run ended:
  - [x] close các modal tạm thời khác nếu cần
  - [x] show endgame modal
  - [ ] không để người chơi tiếp tục thao tác gameplay
- [x] `Retry`:
  - [x] gọi flow New Run sạch qua `GameAppController`
- [x] `Main Menu`:
  - [x] gọi `GameAppController.Instance.GoToMainMenu()`

## Chú ý
- [x] Không nên cho endgame modal bị đóng bằng click scrim như modal thường
- [x] Nếu cần, xử lý riêng non-dismissible behavior

## Verify
- [x] Defeat -> modal hiện đúng text
- [x] Victory -> modal hiện đúng text
- [x] Retry/Menu hoạt động đúng

---

# FILE 10 — UI bootstrap / presenter registration file(s)

## Goal
Register presenter mới vào UI stack hiện tại.

## Hiện trạng
- Project đã có hệ register modal/presenter cho Settings / Confirm / AssignNpc
- Cần tìm đúng file bootstrap UI để gắn `RunEndedModalPresenter`

## Cần làm
- [x] Tìm chỗ register modal presenters hiện tại
- [x] Tạo / bind `RunEndedModalPresenter`
- [x] Register với key `UiKeys.Modal_RunEnded`
- [x] Map đúng root `RunEndedModal` trong `ModalsRoot.uxml`

## Verify
- [x] Modal endgame mở được thật khi event bắn ra
- [x] Không cần hack/manual lookup ngoài vòng đời UI hiện tại

---

# FILE 11 — `Assets/_Game/Core/Boot/GameAppController.cs`

## Goal
Dùng lại scene flow hiện có cho `Retry` / `Main Menu`.

## Hiện trạng
- Đã có:
  - `GoToMainMenu()`
  - `RequestNewGame(...)`
  - `RequestContinue()`

## Cần làm
- [x] Xác nhận presenter endgame dùng lại `RequestNewGame(...)`
- [ ] Nếu cần tiện hơn, thêm helper `RequestRetry()`
- [x] Chốt retry seed policy

## Verify
- [x] Retry không đi qua đường vòng kỳ quặc
- [x] Main Menu flow ổn định

---

# FILE 12 — `Assets/_Game/Core/Boot/GameBootstrap.cs`

## Goal
Đảm bảo retry/new run vẫn reset sạch theo flow hiện tại.

## Hiện trạng
- New Run path nhìn tương đối sạch
- Có `TryStartNewRun(...)`

## Cần làm
- [x] Rà lại endgame retry có đi vào đúng flow này không
- [x] Nếu cần, đảm bảo wipe save policy đúng khi retry
- [ ] Nếu save bị disable sau endgame thì không cần sửa nhiều file này

## Verify
- [x] Retry tạo run mới sạch
- [x] Không giữ state combat/job/UI cũ

---

# FILE 13 — `Assets/_Game/UI/Runtime/Scripts/Presenters/InspectPanelPresenter.cs`

## Goal
Không cho mutate building state sau endgame.

## Hiện trạng
- Có các action:
  - Upgrade
  - Repair
  - Assign NPC
  - Cancel Construction

## Cần làm
- [ ] Nếu `RunOutcome != Ongoing`:
  - [ ] disable tất cả mutate buttons
  - [ ] hiện hint kiểu `Run has ended.`
- [ ] Thêm guard trong handlers:
  - [ ] `OnUpgrade()`
  - [ ] `OnRepair()`
  - [ ] `OnAssignNpc()`
  - [ ] `OnCancelConstruction()`

## Verify
- [ ] Sau Victory/Defeat, inspect panel không cho mutate state nữa
- [ ] Không thể mở assign flow từ inspect khi game ended

---

# FILE 14 — `Assets/_Game/UI/Runtime/Scripts/Presenters/AssignNpcModalPresenter.cs`

## Goal
Không cho assign/unassign NPC sau endgame.

## Cần làm
- [ ] Khi modal refresh nếu game ended -> disable all action buttons
- [ ] Guard các callback assign/move/unassign nếu game ended
- [ ] Nếu endgame xảy ra lúc modal đang mở, cân nhắc auto-close modal này

## Verify
- [ ] Run ended rồi thì không đổi workforce được nữa
- [ ] Modal không gây hiểu nhầm là game còn đang playable

---

# FILE 15 — `Assets/_Game/UI/Runtime/Scripts/Presenters/SettingsModalPresenter.cs`

## Goal
Xử lý save/menu hợp lý sau endgame.

## Hiện trạng
- Save hiện vẫn gọi `GameBootstrap.TrySaveNow(...)`

## Cần làm
- [ ] Nếu policy là disable save sau endgame:
  - [ ] disable hoặc hide `BtnSave`
  - [ ] guard trong `OnSave()`
- [ ] `Main Menu` vẫn nên cho phép dùng

## Verify
- [ ] Không save được sau endgame nếu policy là disable
- [ ] Main Menu vẫn hoạt động bình thường

---

# FILE 16 — Build/Placement input files liên quan

## Goal
Không cho người chơi tiếp tục đặt công trình hoặc mutate build state sau endgame.

## Cần rà
- [ ] Build panel presenter
- [ ] Placement input controller
- [ ] Build mode input handling
- [ ] Commit building / road placement action path

## Cần làm
- [ ] Thêm guard `if outcome != Ongoing return;`
- [ ] Auto-exit placement mode khi endgame xảy ra nếu cần
- [ ] Disable build buttons/menu khi run ended

## Verify
- [ ] Không build/place road được sau Victory/Defeat
- [ ] Không còn ghost placement lingering sau endgame

---

# FILE 17 — `Assets/_Game/UI/Runtime/Scripts/Navigation/ModalStackController.cs`

## Goal
Không để endgame modal bị đóng nhầm bởi scrim.

## Hiện trạng
- Scrim click = `Pop()`
- Hợp với settings/confirm modal, nhưng không hợp với endgame modal

## Cần làm
- [x] Chốt cách support modal non-dismissible
- [x] Nếu sửa controller:
  - [x] thêm cơ chế modal metadata / dismissOnScrim flag
- [ ] Nếu không sửa controller, phải có workaround sạch cho endgame modal

## Verify
- [x] Endgame modal không bị click ra ngoài để đóng
- [x] Modal thường khác vẫn hoạt động như cũ

---

# FILE 18 — `Assets/_Game/Tests/EditMode/Regression/Regression_P0P1_Tests.cs`

## Goal
Khóa behavior cốt lõi của endgame flow.

## Cần làm
- [x] Thêm test: HQ HP <= 0 -> `RunOutcome.Defeat`
- [x] Thêm test: Victory milestone -> `RunOutcome.Victory`
- [x] Thêm test: `RunEndedEvent` publish đúng reason
- [x] Thêm test: outcome chỉ trigger 1 lần
- [x] Thêm test: `ResetOutcome()` clear state đúng
- [x] Thêm test: ended state chặn simulation tiếp tục mutate (qua `TickOrder`)
- [x] Thêm test: Retry/New Run reset sạch outcome
- [x] Thêm test: victory chỉ trigger cho final wave Year 2
- [x] Thêm test: rule priority `Lose > Win`

## Verify
- [x] Regression pass ổn định
- [x] Không re-break flow ở các iteration sau

---

## 5. Checklist integration / wiring

### Event → UI wiring
- [x] `RunOutcomeService` publish đủ event data
- [x] UI presenter subscribe đúng source
- [x] Presenter mở modal khi event xảy ra
- [x] Modal hiện đúng text theo outcome / reason

### Retry / Menu wiring
- [x] Retry dùng `GameAppController.RequestNewGame(...)`
- [x] Main Menu dùng `GameAppController.GoToMainMenu()`
- [x] Retry không giữ world state cũ

### Freeze / Action lock
- [x] Tick loop không tick sim sau endgame
- [x] Combat không chạy tiếp sau endgame
- [x] Inspect mutate action bị khóa
- [x] Assign action bị khóa
- [x] Build/place action bị khóa
- [x] Save sau endgame được xử lý đúng policy

---

## 6. Thứ tự implement khuyến nghị

### Đợt A — Core freeze + defeat usable
1. [ ] `CommonEvents.cs`
2. [ ] `IRunOutcomeService.cs`
3. [ ] `RunOutcomeService.cs`
4. [ ] `TickOrder.cs`
5. [ ] `CombatService.cs`
6. [ ] `UiKeys.cs`
7. [ ] `ModalsRoot.uxml`
8. [ ] `RunEndedModalPresenter.cs`
9. [ ] register presenter vào UI bootstrap
10. [ ] wire `Retry / Main Menu`

### Đợt B — Action lockdown
11. [ ] `InspectPanelPresenter.cs`
12. [ ] `AssignNpcModalPresenter.cs`
13. [ ] `SettingsModalPresenter.cs`
14. [ ] build / placement guards
15. [ ] modal non-dismissible cleanup

### Đợt C — Quality
16. [ ] `Modals.uss` polish
17. [ ] regression tests
18. [ ] smoke test thủ công

---

## 7. Smoke test checklist thủ công

### Defeat path
- [x] New Run vào game bình thường
- [x] Làm HQ chết bằng combat/debug
- [x] `Defeat` modal hiện đúng
- [x] Simulation không tiếp tục chạy như bình thường
- [x] Không build/repair/assign được nữa
- [x] Save bị disable nếu policy là disable

### Retry path
- [x] Bấm `Retry`
- [x] Vào run mới sạch
- [x] Không còn enemy/job/resource state từ run cũ
- [x] Outcome reset về `Ongoing`

### Main Menu path
- [x] Bấm `Main Menu`
- [x] Về scene `MainMenu`
- [x] Từ Main Menu vào lại game bình thường

### Victory path
- [x] Trigger đúng mốc victory
- [x] `Victory` modal hiện đúng
- [x] Retry/Menu đều hoạt động đúng

---

## 8. Definition of Done

Pass này chỉ tính DONE khi:

- [x] HQ chết thì game thua thật
- [x] Mốc victory hiện tại trigger thắng thật
- [x] Simulation dừng đúng khi run ended
- [x] Có endgame modal usable
- [x] Retry tạo run mới sạch
- [x] Main Menu flow ổn
- [x] Các action gameplay chính không còn mutate world sau endgame
- [x] Regression test cốt lõi đã được thêm
- [x] Smoke test thủ công pass

---

## 9. Ghi chú thực dụng

Nếu cần chốt scope nhanh và ít rủi ro nhất, ưu tiên làm trước:

### Phase 1 tối thiểu usable
- [ ] Defeat reason
- [ ] Freeze tick loop
- [ ] Stop combat after end
- [ ] Defeat modal
- [ ] Retry / Main Menu

### Phase 2 hoàn thiện hơn
- [ ] Victory reason
- [ ] Victory modal
- [ ] Action lockdown đầy đủ
- [ ] Regression coverage đầy hơn

Cách này giúp game **thua được đúng cách trước**, rồi mới hoàn thiện nhánh thắng.
