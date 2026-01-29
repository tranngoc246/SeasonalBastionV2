# Seasonal Bastion — VS#3 Dev Log (Nhật ký triển khai)

> File này dùng để ghi lại tiến độ theo ngày (Day 32, Day 33, …) khi triển khai **Vertical Slice #3** dựa trên nền VS2.
>
> Quy ước:
> - Mỗi Day gồm: **Mục tiêu**, **Đã làm**, **Kết quả/Acceptance**, **Ghi chú/Pitfalls**, **Việc tiếp theo**.
> - Khi hoàn thành hạng mục, tick ✅.
>
> Phạm vi thư mục (trong repo Unity): `Assets/_Game/…`
> - Dev log canonical nên đặt tại: `Assets/_Game/Art/VS3_DEV_LOG.md`.
> - Debug UI/tool nên đi qua **DebugHUDHub** (tránh rải hotkey gây chồng input), trừ khi spec nói khác.

---

## Day 32 — RunOutcome (Victory Year 2) + Debug Jump + Clock Load Hardening

### Mục tiêu
- [x] Chốt rule RunOutcome theo GDD/VS3:
  - [x] **Defeat**: HQ HP <= 0
  - [x] **Victory**: survive hết **Winter – Year 2 (Day 4)**
  - [x] Trigger đúng **1 lần** (không double-end)
- [x] Debug hỗ trợ test nhanh:
  - [x] HUD hiển thị Year/Season/Day/Phase + RunOutcome
  - [x] Có nút **Jump Winter Y2 D4** (Start / Near End) để xác nhận Victory
- [x] Hardening load clock:
  - [x] LoadSnapshot set Phase trước khi clamp timeScale (Defend clamp đúng)

### Đã làm
- [v] **RunOutcomeService**
  - Defeat: kiểm tra HQ HP <= 0 mỗi tick → `Outcome=Defeat` và publish `RunEndedEvent`.
  - Victory (VS3/GDD): lắng nghe `DayEndedEvent`:
    - Condition: `YearIndex == 2 && Season == Winter && DayIndex >= 4` → `Outcome=Victory`.
  - Guard: nếu `Outcome != Ongoing` → không xử lý lại (chặn trigger nhiều lần).
- [v] **RunClockService.LoadSnapshot hardening**
  - Fix thứ tự:
    - Parse season/day/year → set `CurrentSeason` + `CurrentPhase` (Build/Defend) → set dayTimer → apply timeScale.
  - Tránh case load vào Defend nhưng clamp/timeScale bị áp sai do phase chưa set.
- [v] **DebugRunClockHUD (VS3 Day32 tools)**
  - Hiển thị thêm:
    - RunOutcome hiện tại
    - Dòng rule: “Victory = End of Winter Day 4 Year 2”
  - Thêm quick jumps:
    - `Jump: Winter Y2 D4 (Start)`
    - `Jump: Winter Y2 D4 (Near End)` (set dayTimer gần cuối để trigger Victory nhanh)

### Kết quả / Acceptance
- [v] Jump `Winter Y2 D4 (Near End)` → sau ~0.2s rollover day → `RunOutcome = Victory`.
- [v] HQ HP về 0 → `RunOutcome = Defeat` ngay lập tức.
- [v] Không còn double-trigger RunEnded (Victory/Defeat chỉ set một lần).
- [v] LoadSnapshot vào Defend giữ timeScale hợp lệ theo policy clamp.

### Ghi chú / Pitfalls
- Rule Victory VS3 khác VS2 (VS2 win ở Winter Y1); đã chuẩn hoá theo GDD/VS3: **Winter Year 2**.
- Cần đảm bảo `DayEndedEvent` có `YearIndex` (đã có trong codebase VS2/VS3) để rule không suy diễn.
- Debug jump dùng `LoadSnapshot` nên chỉ phục vụ test; không dùng cho gameplay thực.

### Việc tiếp theo (Day 33)
- Save/Load regression “Year2-ready”:
  - persist enemies để load không mất quái
  - reset wave khi load (restart schedule) nhưng **giữ enemies còn sống**

---

## Day 33 — Save/Load Regression + Persist Enemies + Reset Wave When Load

### Mục tiêu
- [x] Save/Load không lệch clock snapshot:
  - [x] yearIndex / season / dayIndex / dayTimer / timeScale
- [x] Apply load theo thứ tự an toàn:
  - [x] clear → roads → sites → buildings → towers → npcs → enemies → rebuild index → clock
- [x] **Reset wave khi load** (policy v0.1):
  - [x] Load vào Defend → restart wave schedule “từ đầu ngày”
  - [x] Enemies có trong save **được restore** (không xoá) và coi như thuộc wave hiện tại
- [x] Debug regression tool:
  - [x] 6 checkpoints (mỗi season) + 1 checkpoint **mid-wave**

### Đã làm
- [v] **SaveService — Persist enemies**
  - Serialize danh sách enemies vào save file:
    - id, defId, cell, hp, lane, moveProgress01
  - Bổ sung `CombatFile` minimal để applier biết Defend active (fallback derive từ season nếu thiếu).
- [v] **SaveLoadApplier — Restore enemies + Reset combat**
  - Restore `dto.world.Enemies` vào `WorldState.Enemies` sau NPC/tower và trước rebuild index.
  - Sau khi restore clock snapshot, gọi `CombatService.ResetAfterLoad(dto.combat)`:
    - Nếu phase/flag là Defend → gọi `OnDefendPhaseStarted()` để restart waves
    - Không xoá enemies đã restore (leftover quái vẫn tồn tại)
- [v] **CombatService — ResetAfterLoad API (runtime-only)**
  - Method `ResetAfterLoad(CombatDTO dto)`:
    - Relatch clock (phase/season/day/year)
    - Nếu Defend: `OnDefendPhaseStarted()` (restart day waves)
    - Nếu Build: `OnDefendPhaseEnded()`
- [v] **DebugSaveLoadHUD — Regression**
  - Nút “Run Regression (6 checkpoints + mid-wave)”
  - Checkpoints:
    - Spring Y1 D1 / D6
    - Summer Y1 D1 / D6
    - Autumn Y1 D1 (Defend)
    - Winter Y2 D1 (Defend)
    - MidWave: Autumn Y1 D1 (force reset defend + tick để spawn vài enemy), sau đó Save/Load/Apply và verify enemy count preserved
  - Validation:
    - Clock snapshot match (year/season/day/dayTimer/timeScale)
    - Counts match: buildings/sites/npcs/towers/enemies
    - Road count match

### Kết quả / Acceptance
- [v] Regression pass:
  - Load xong clock giữ đúng snapshot (không drift year/season/day).
  - World counts giữ nguyên (bao gồm enemies).
  - Road count giữ nguyên sau apply.
- [v] Mid-wave:
  - Spawn được enemies → Save → Load+Apply:
    - enemies vẫn còn (không bị mất)
    - wave schedule restart lại (reset-wave policy) khi đang Defend
- [v] Không phát sinh vòng lặp asmdef (chỉ sửa trong cụm Save/Combat/Debug, không tạo dependency ngược).

### Ghi chú / Pitfalls
- Policy “Reset wave khi load” là lựa chọn scope đơn giản:
  - Không serialize internal timers của WaveDirector (sau này nếu cần resume 100% sẽ mở rộng CombatDTO).
- Mid-wave spawn trong regression phụ thuộc wave defs + lanes:
  - Nếu không spawn được enemies, cần kiểm tra `Waves.json` hoặc `RunStartRuntime.Lanes` đã có.
- Thứ tự apply phải giữ ổn định để tránh dangling references (npcs workplace/buildings index).

### Việc tiếp theo
- Day 34+: Combat stability pass (nếu theo VS3):
  - polish điều kiện wave end (nếu cần wait aliveCount=0)
  - harden load consistency cho combat timers nếu mở scope

---

## Day 34 — Combat stability pass (enemy stuck + wave resolve)

### Mục tiêu
- [x] Không còn case wave không kết thúc vì enemy kẹt / path fail.
- [x] Debug rõ ràng: hiển thị counter alive/spawned + trạng thái resolve.

### Đầu vào / Scope (đã chốt)
- [v] Resolve rule (WaveDirector):
  - Wave chỉ kết thúc khi **spawn done + aliveCount == 0**.
  - Có **timeout safety** để tránh softlock (log warn + force resolve).
- [v] Enemy mover fallback:
  - Nếu path fail liên tiếp N lần → fallback step theo **dirToHQ** hoặc **local BFS radius nhỏ**.
  - Nếu enemy phá building về 0 HP → **clear occupancy** để không bị blocked vĩnh viễn.
- [v] Debug:
  - Show counters: planned/spawned/alive + spawnDone + resolve timer + nút force resolve.

### Đã làm
- [v] **EnemySystem — fallback & unstuck**
  - Track `pathFailStreak` theo EnemyId.
  - Khi `TryFindNextStep` fail liên tiếp (>= threshold):
    - Ưu tiên step theo `dirToHQ` (kèm left/right/opposite)
    - Nếu vẫn fail → local BFS trong radius nhỏ để tìm bước đi cải thiện Manhattan tới HQ.
  - Reset streak khi move thành công / cleanup enemy.
  - Khi building bị đánh về 0 HP:
    - Mark not constructed (an toàn tối thiểu)
    - Clear footprint occupancy trên grid để không chặn đường vĩnh viễn.
- [v] **WaveDirector — resolve rule + timeout safety**
  - Spawn phase: chạy tới hết entries → set `spawnDone = true` (KHÔNG end ngay).
  - Resolve phase:
    - Chỉ end wave khi `spawnDone && aliveCount == 0`
    - Nếu `resolveElapsed >= timeout` → log warn + force end wave để tránh treo.
  - Expose counters cho debug: planned/spawned/spawnDone/alive/resolveElapsed/timeout.
- [v] **CombatService — expose wave debug info**
  - Public getters: active wave id, planned/spawned, alive count, spawnDone, resolve timers.
  - Debug action: `ForceResolveWave()` gọi xuống WaveDirector.
- [v] **Debug HUD — hiển thị Wave counters + cuộn Home**
  - Thêm `DebugWaveHUD` (Hub-controlled) để hiển thị:
    - WaveActive, WaveId
    - Spawned/Planned, SpawnDone
    - AliveEnemies
    - ResolveTimer/Timeout
    - Nút **Force Resolve Wave**
  - Sửa `DebugHUDHub.DrawHome()`:
    - Bọc nội dung bằng `ScrollView`
    - Thêm toggle ẩn/hiện từng section (Data/Clock/Lanes/Save/Wave)
    - Tránh “HUD quá dài” làm Wave không hiện.

### Kết quả / Acceptance
- [v] Chạy **Winter 1 ngày**: wave luôn kết thúc, không còn treo do enemy kẹt/path fail.
- [v] Khi xảy ra trường hợp bất thường (lane/path/data), timeout safety đảm bảo không softlock (có warn log).
- [v] Debug Wave hiển thị đầy đủ counters và thao tác force resolve để test nhanh.

### Ghi chú / Pitfalls
- Local BFS chỉ dùng radius nhỏ (deterministic, không over-engineer) → mục tiêu là “thoát kẹt”, không thay thế full pathfinding.
- Timeout safety là “last resort” để tránh treo; log warn để dễ trace data/path issue.
- Clear occupancy khi building HP=0 là cực quan trọng để tránh grid blocked vĩnh viễn (softlock resolve).

### Việc tiếp theo
- Day 35 (nếu theo VS3): polish combat pacing:
  - refine wave timeout tuning (theo Balance Tables)
  - nếu cần: rule “wave end wait alive==0” đã ổn, ưu tiên kiểm tra spawn/lane defs để giảm timeout.

---

## Day 35 — Boss minimal + reward hook (placeholder)

### Mục tiêu
- [x] Có boss tối thiểu trong Winter (theo pacing), data-driven.
- [x] Reward placeholder khi end season: emit event `EndSeasonRewardRequested`.

### Đầu vào / Scope (đã chốt)
- [v] BossDef: boss là enemy có HP lớn hơn + damage cao hơn (data-driven từ Enemies defs).
- [v] Boss spawn:
  - Boss xuất hiện ở **wave cuối** của ngày/season cần boss (ưu tiên Winter pacing).
  - Không tạo double-boss nếu wave json đã có boss.
- [v] Reward hook:
  - Khi kết thúc **ngày cuối của season** → publish `EndSeasonRewardRequested` (placeholder, chưa cần UI chọn reward).

### Đã làm
- [v] **Contracts — Event placeholder**
  - Thêm struct `EndSeasonRewardRequested` vào CommonEvents:
    - Payload: `Season`, `YearIndex`, `DayIndex`.
- [v] **RewardService — emit end-season reward request**
  - Subscribe `DayEndedEvent`.
  - Detect end season theo số ngày lock v0.1 (Spring 6 / Summer 6 / Autumn 4 / Winter 4).
  - Khi `DayIndex == maxDaysOfSeason`:
    - Publish `EndSeasonRewardRequested(season, yearIndex, dayIndex)`.
- [v] **WaveCalendarResolver — boss wave injection (minimal)**
  - Khi resolve waves cho (year, season, day):
    - Nếu list đã có `IsBoss == true` → skip (tránh double boss).
    - Nếu data registry có boss enemy schedule đúng mốc (BossYear/BossSeason/BossDay) nhưng **không có wave boss**:
      - Auto-add 1 wave boss minimal:
        - `IsBoss = true`, `WaveIndex = 9999`
        - Entries: `{ enemyId = <boss>, count = 1 }`
  - Kết quả: boss xuất hiện đúng mốc ngay cả khi `Waves.json` chưa khai báo wave boss cho mốc đó.
- [v] **Combat/Wave Debug — boss flag**
  - Expose `ActiveIsBoss` (WaveDirector) + `ActiveWaveIsBoss` (CombatService).
  - DebugWaveHUD hiển thị `IsBossWave` để xác nhận nhanh trong runtime.

### Kết quả / Acceptance
- [v] Boss xuất hiện đúng mốc (wave cuối của ngày/season target), không crash khi bị giết.
- [v] End of season (ngày cuối) luôn publish `EndSeasonRewardRequested` (placeholder) qua EventBus.
- [v] Không tạo vòng lặp asmdef:
  - Injection nằm ở Boot/Resolver, Debug chỉ đọc state từ Combat, Rewards chỉ subscribe events.

### Ghi chú / Pitfalls
- Boss “data-driven” nghĩa là tuning HP/damage nằm ở Enemies defs; code chỉ đảm bảo boss được spawn đúng lịch.
- Injection dùng WaveIndex lớn để chắc chắn “cuối ngày” (không phụ thuộc thứ tự trong json).
- Reward event hiện là placeholder: hệ UI/RewardPicker có thể hook vào sau.

### Việc tiếp theo
- Day 36 (nếu theo VS3): reward UI tối thiểu (listen `EndSeasonRewardRequested` → popup placeholder + pick 1).
- Nếu boss pacing cần chặt hơn: bổ sung logic “boss chỉ spawn nếu all previous waves resolved” (hiện đã đảm bảo qua wave resolve rule Day34).

---

## Day 36 — Producer loop chuẩn (subset theo data) + local storage cap

### Mục tiêu
- [x] Farm/Lumber sinh resource theo balance; có local cap; full thì dừng đúng.
- [x] Notify key: `producer.local.full` (anti-spam friendly).
- [x] Debug: đổi mode/tab bằng F1–F7 hoạt động ổn định.

### Đã làm
- [v] **ProducerLoopService (mới)**
  - Scan buildings định kỳ (nhẹ) và chọn subset producer: Farmhouse/LumberCamp.
  - Nếu local chưa full và workplace queue trống → enqueue `JobArchetype.Harvest`.
  - Nếu full → cancel Harvest đang chờ (nếu có) + push notify `producer.local.full`.
- [v] **Local storage cap per building**
  - Dùng `StorageService.GetCap/GetAmount` theo (BuildingId, ResourceType) để chặn tăng vượt cap.
  - Khi full: producer dừng tạo job và HarvestExecutor cancel thay vì fail loop.
- [v] **Notification anti-spam**
  - Dedupe theo key + cooldown để tránh spam.
  - Fix compile: `NotificationPayload` là `readonly struct` → tạo bằng constructor `new NotificationPayload(buildingId, default, null)` (không dùng object initializer).
- [v] **Tick order**
  - Tick ProducerLoop trước JobScheduler để job được assign ngay trong cùng tick.
- [v] **Debug hotkeys F1–F7**
  - Root cause: Hub không poll hotkeys khi thiếu Router / wiring router-hub không resolve.
  - Fix: thêm fallback self-poll hotkeys trong `DebugHUDHub` khi scene không có `DebugInputRouter`, và harden `HandleHotkeys` (chống `Key.None`).
  - Đảm bảo `DebugInputRouter` resolve `_hub` (GetComponentInChildren/FindObjectOfType).

### Kết quả / Acceptance
- [v] Chạy 2–3 ngày: local Food/Wood tăng dần → đạt cap → dừng đúng (không tăng thêm).
- [v] Khi full: có notify `producer.local.full` nhưng không spam.
- [v] F1–F7 đổi mode/tab ổn định.

### Ghi chú / Pitfalls
- Producer loop hiện chạy tối thiểu trong Build phase (scope Day36); có thể mở rộng cho Defend sau nếu cần.
- Subset mapping hiện: Farmhouse→Food, LumberCamp→Wood (theo subset yêu cầu).

### Việc tiếp theo
- Day 37 (gợi ý): HUD debug producer/local storage (compact), hoặc mở rộng producer types nếu scope VS3 cho phép.

---

## Day 37 — HaulBasic (Producer → Central Storage) + claim hardening

### Mục tiêu
- [x] NPC vận chuyển làm giảm local producer và tăng central storage; không deadlock/softlock.
- [x] Không có 2 NPC bốc cùng stack; không loop vô hạn khi kho đầy.
- [x] Debug: hiển thị số lượng haul jobs đang active.

### Đầu vào / Scope (đã chốt)
- [v] JobProvider (deterministic):
  - Ưu tiên nguồn local **gần full** (theo % đầy), tie-break deterministic.
- [v] Executor hardening:
  - Claim source/dest để tránh tranh chấp.
  - Nếu destination full → fallback **return source** (nhất quán).
- [v] Debug:
  - Counter số job `HaulBasic` đang active trên HUD.

### Đã làm
- [v] **HaulBasicExecutor — ưu tiên nguồn local gần full (deterministic)**
  - Chọn source theo thứ tự ưu tiên:
    1) % đầy (fill ratio) cao hơn
    2) distance (Manhattan) nhỏ hơn
    3) BuildingId nhỏ hơn (tie-break deterministic)
  - Dùng local cap mapping lock v0.1 để tính fill ratio (Farm/Lumber/Quarry/Iron).
- [v] **HaulBasicExecutor — claim hardening & kho đầy**
  - Pickup: claim `StorageSource` theo (source, resource) để không 2 NPC remove cùng lúc.
  - Deliver: claim `StorageDest` theo (dest, resource) để tránh đẩy chồng vào cùng slot.
  - Nếu không có dest có space hoặc reroute fail:
    - Nếu đang carry → **refund về source** rồi cancel job.
    - Nếu chưa pickup → cancel job (không fail loop).
  - Fallback policy nhất quán: **return source** (không drop ra world).
- [v] **JobBoard — debug counter**
  - Thêm helper `CountActiveJobs(JobArchetype.HaulBasic)` để đếm job đang chạy (exclude Completed/Failed/Cancelled).
- [v] **DebugHUDHub — show active haul jobs**
  - Hiển thị `HaulBasic jobs active: N` trên Home tab để theo dõi deadlock/queue.

### Kết quả / Acceptance
- [v] Không có 2 NPC bốc cùng stack/resource từ 1 producer (claim source).
- [v] Khi kho trung tâm đầy: job không loop vô hạn; nếu đang carry thì trả hàng về source trước khi cancel.
- [v] Debug HUD hiển thị rõ số haul jobs active để test nhanh.

### Ghi chú / Pitfalls
- “Claim amount” theo stack size chưa implement (ClaimKey chỉ (kind,A,B)); scope Day37 đủ để chặn double pickup/double deposit.
- Local cap mapping cần đồng bộ với Producer/Harvest (Day36) để fill ratio phản ánh đúng.

### Việc tiếp theo
- Day 38 (gợi ý): hiển thị thêm breakdown haul theo resource / per-dest, hoặc policy ưu tiên dest theo free-space.

---

## Day 38 — Ammo pipeline polish (Forge→Armory→Resupply) + anti-spam requests

### Mục tiêu
- [ ] Ammo loop chạy êm trong combat dài (Year2):
  - resupply không spam
  - tower không bị thiếu ammo lâu / không bị “kẹt” khi armory refill lại
- [ ] Debug rõ ràng để quan sát request/jobs.

### Đầu vào / Scope (đã chốt)
- [v] Resupply requests:
  - dedupe per `TowerId`
  - priority: `0 ammo` > `low ammo`
  - cooldown
- [v] HaulAmmo:
  - chunk size + capacity clamp (avoid over-carry)
- [v] Guard:
  - tower full khi deliver → return ammo về armory (không mất ammo)

### Đã làm (tạm thời)
- [v] **Triaging bug “armory refill nhưng chỉ resupply khi có enemy”**
  - Kiểm tra tick chain:
    - `AmmoService` được tick mỗi frame theo simDt, không bị gate theo combat.
  - Kết luận: root cause không phải do TickOrder, mà nhiều khả năng do lifecycle của request/in-flight job:
    - request bị consume / job bị cancel khi armory 0
    - khi armory có ammo lại, tower không phát sinh ammo-change nên không tạo request mới → gây stall.
- [ ] **Chưa apply code changes Day38**
  - Theo yêu cầu “tạm thời bỏ qua”, chưa triển khai các thay đổi:
    - Wake-up resupply khi armory ammo tăng
    - Requeue request khi resupply job Cancel/Fail
    - Clamp carry cap (HaulAmmo/Resupply)
    - Debug counters (pending/inflight)

### Kết quả / Acceptance
- [ ] Chưa đạt (đang tạm hoãn phần code). Dùng để ghi nhận phân tích & hướng fix tiếp theo.

### Ghi chú / Pitfalls
- Bug này không phụ thuộc việc tower có target hay không; thường là do cơ chế request chỉ phát sinh khi ammo thay đổi.
- Fix chuẩn cần đảm bảo:
  - armory refill → resupply scheduler “wake up” (dù tower idle)
  - job cancel/fail → nếu tower vẫn thiếu ammo thì re-enqueue (anti-spam bằng cooldown/dedupe)

### Việc tiếp theo
- Khi quay lại Day38:
  - Thêm “wake-up” theo armory ammo sum tăng.
  - Hardening: requeue khi job cancel/fail + clamp carry cap.
  - Debug: show pending urgent/normal + inflight haul/resupply để test 2 ngày liên tục với 3 towers.

---

## Day 39 — Unlock gating tối thiểu (build list theo mốc time/season)

### Mục tiêu
- [x] Không cho người chơi build mọi thứ từ đầu; unlock theo nhịp GDD/pacing (tối thiểu).
- [x] Data-driven schedule (year/season/day) + API `IsUnlocked(defId)`.
- [x] Build UI/Debug list: chỉ cho chọn/đặt defs unlocked.
- [x] Save/Load: **recompute từ clock** (không persist state).

### Đầu vào / Scope (đã chốt)
- [v] Unlock theo mốc `(Year, Season, Day)` (lexicographic compare).
- [v] Recompute từ `RunClock` (khuyến nghị), không serialize.
- [v] Hiện gating ở `DebugBuildingTool` (flow build hiện tại), HUD build list khác sẽ làm sau nếu có.

### Đã làm
- [v] **Contracts — IUnlockService + DTO schedule**
  - Thêm `IUnlockService.IsUnlocked(defId)`.
  - Thêm `UnlockScheduleDef` + `UnlockEntryDef` (JSON via `JsonUtility`).
- [v] **UnlockService (mới)**
  - Load schedule từ `Resources/UnlockSchedule_v0_1.json` (TextAsset).
  - Tick nhẹ (0.25s) và **recompute** khi `(Year/Season/Day)` đổi.
  - Set `_unlocked` gồm `StartUnlocked` + các entry đã “đến mốc”.
- [v] **Wire vào GameServices**
  - `GameServices` thêm field `UnlockService`.
  - `GameServicesFactory` tạo `UnlockService` sau khi tạo `RunClock` (để unlock dựa theo clock).
  - `TickOrder` tick `UnlockService` mỗi sim tick (không gate combat).
- [v] **Build gating ở DebugBuildingTool**
  - Khi select/commit def:
    - Nếu `!UnlockService.IsUnlocked(defId)` → chặn thao tác + push notification “Locked”.
  - Đảm bảo đầu game chỉ đặt được set start; tới mốc unlock thì đặt được thêm.
- [v] **Fix compile mismatch IRunClock**
  - `IRunClock` không expose `YearIndex` và season property tên `CurrentSeason`.
  - Sửa `UnlockService`:
    - dùng `_clock.CurrentSeason` thay vì `_clock.Season`
    - lấy year bằng helper `GetYearIndex(IRunClock)` (cast `RunClockService`), fallback `1` nếu không cast được.

### Kết quả / Acceptance
- [v] Đầu game: chỉ chọn/đặt được các building trong `StartUnlocked`.
- [v] Khi clock sang mốc unlock (year/season/day) → def mới được mở khóa và đặt được ngay (không cần reload).
- [v] Recompute từ clock nên Save/Load không cần persist unlock state.

### Ghi chú / Pitfalls
- `Season` trong JSON nên dùng **int** (Spring=0, Summer=1, Autumn=2, Winter=3) để tránh JsonUtility parse enum string.
- Gating hiện ở `DebugBuildingTool`; nếu có Build menu HUD khác thì cần apply filter tương tự để “không hiện” những def locked (không chỉ chặn đặt).

### Việc tiếp theo
- Nếu có UI BuildList/HUD:
  - filter list theo `UnlockService.IsUnlocked(defId)` để “không hiện” những def locked (không chỉ chặn đặt).
- (Tuỳ scope) thêm debug panel hiển thị danh sách unlocked hiện tại để test nhanh.

---

## Day 40 — Season Summary UI + metrics (shipable UX)

### Mục tiêu
- [x] Cuối mỗi season hiển thị **Season Summary** (metrics tối thiểu), chỉ xuất hiện **1 lần/season**, dismiss để tiếp tục, không spam.
- [x] Metrics collect tối thiểu:
  - [x] resources gained / spent (tính cả craft)
  - [x] enemies killed
  - [x] buildings built
  - [x] ammo used

### Đầu vào / Scope (đã chốt)
- [v] Metrics thu thập theo **EventBus** (ít đụng gameplay), deterministic, không over-engineer.
- [v] “Spent” chỉ tính ở điểm **tiêu thụ thật**:
  - Build: lượng resource **apply vào site** (không tính haul/transfer).
  - Craft: lượng input **consume ở Forge** (Iron/Wood).
- [v] UI: 1 overlay panel đơn giản (uGUI + TMP), auto-attach vào `GameBootstrap`, pause bằng timeScale=0, dismiss resume.

### Đã làm
- [v] **Contracts — events cho metrics**
  - Thêm `ResourceSpentEvent`, `EnemyKilledEvent`, `AmmoUsedEvent` (CommonEvents).
- [v] **Emit metrics events tại điểm tiêu thụ**
  - Build: `BuildDeliverExecutor` publish `ResourceSpentEvent(type, apply, sourceBuilding)` khi apply vào site.
  - Craft: `CraftAmmoExecutor` capture `Remove(...)` rồi publish `ResourceSpentEvent(Iron/Wood, removed, forge)` khi consume input.
  - Combat: `TowerCombatSystem` publish:
    - `EnemyKilledEvent(defId, 1)` khi enemy chết.
    - `AmmoUsedEvent(ammoPerShot)` khi trừ ammo per shot.
- [v] **SeasonMetricsService (mới)**
  - Subscribe: `DayStartedEvent`, `ResourceDeliveredEvent`, `ResourceSpentEvent`, `BuildingPlacedEvent`, `EnemyKilledEvent`, `AmmoUsedEvent`.
  - Reset metrics khi vào `DayIndex == 1` của season mới (key = year+season).
  - Expose `GetSnapshot()` (clone arrays) cho UI.
- [v] **Wire vào GameServices**
  - `GameServices` thêm field `SeasonMetrics`.
  - `GameServicesFactory` khởi tạo `SeasonMetricsService`.
  - `GameLoop.ResetForNewRun()` reset metrics (an toàn).
- [v] **SeasonSummaryOverlay (mới)**
  - Listen `EndSeasonRewardRequested` → show panel 1 lần/season (anti-spam guard theo year+season).
  - Render 4–6 dòng:
    - gained total + breakdown
    - spent total + breakdown
    - enemies killed
    - buildings built
    - ammo used
  - Dismiss → resume timeScale về giá trị trước đó.
  - Auto ensure `EventSystem` tồn tại (InputSystemUIInputModule nếu có, fallback Standalone).
- [v] **Debug hardening**
  - `DebugHUDHub` thêm listener để nhìn `EndSeasonRewardRequested` fire trên HUD (count + last payload) phục vụ test nhanh.

### Kết quả / Acceptance
- [v] End Spring: summary xuất hiện đúng **1 lần**, bấm Dismiss OK, không spam.
- [v] Metrics cập nhật đúng trong season:
  - gained tăng theo `ResourceDeliveredEvent`.
  - spent tăng cả build + craft, không inflate do haul/transfer.
  - enemies killed / ammo used cập nhật theo combat.
  - buildings built tăng khi place building.

### Ghi chú / Pitfalls
- Nếu `ResourceType` không đúng 5 loại, cần chỉnh size mảng metrics theo enum (khuyến nghị có `Count`).
- “Spent” không nên publish ở `StorageService.Remove()` vì sẽ đếm nhầm transfer/haul.
- Guard show overlay theo (YearIndex, Season) để tránh double-show nếu event bị publish lại.

### Việc tiếp theo
- Nếu muốn UX mượt hơn:
  - thêm icon/format đẹp hơn (vẫn giữ tối giản)
  - thêm nút “Details” mở rộng breakdown theo resource
- Sau Day40 có thể chuyển sang Day41 (tutorial hints + notification anti-spam).
