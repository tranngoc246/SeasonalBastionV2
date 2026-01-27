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
