# CHANGELOG

## 2026-03-27

### Tóm tắt
Đợt cập nhật này chưa thay đổi gameplay runtime trực tiếp, mà tập trung vào việc **rà soát trạng thái hiện tại của endgame flow** và chốt một bộ tài liệu implementation đủ cụ thể để bắt tay vào làm mà không bị mơ hồ phạm vi. Kết quả của pass này là đã xác định rõ phần nào của backend thắng/thua đã có sẵn, phần nào còn thiếu ở freeze simulation / UI / Retry flow, và bổ sung checklist triển khai bám sát đúng codebase hiện tại.

### Review hiện trạng endgame flow
- Rà soát code hiện tại quanh các cụm:
  - `RunOutcomeService`
  - `RunEndedEvent`
  - `TickOrder`
  - `CombatService`
  - `GameAppController`
  - UI modal/presenter hiện có
- Xác nhận backend outcome cơ bản đã tồn tại:
  - `Defeat` khi HQ HP về 0
  - `Victory` theo rule hiện đang hard-code ở `RunOutcomeService`
  - publish `RunEndedEvent`
- Xác nhận các phần còn thiếu mang tính player-facing / flow control:
  - chưa có modal/panel `Victory` / `Defeat`
  - chưa có wiring từ `RunEndedEvent` sang UI
  - tick loop hiện chưa dừng simulation sau khi run end
  - combat chưa có guard rõ để tự dừng khi outcome không còn `Ongoing`
  - các action gameplay như inspect/assign/build/save chưa được rà soát đầy đủ để khóa sau endgame

### Docs / implementation planning
- Viết spec và task breakdown chi tiết cho endgame flow để thống nhất hướng làm trước khi code.
- Bóc checklist implementation **file-by-file** bám theo codebase hiện tại, thay vì checklist mức ý tưởng.
- Thêm file docs mới:
  - `docs/implementation-checklist-endgame-flow.md`
- Checklist mới cover đầy đủ các nhóm việc:
  - mở rộng `RunEndedEvent` / `IRunOutcomeService`
  - chuẩn hóa `RunOutcomeService`
  - freeze simulation ở `TickOrder`
  - chặn combat tiếp tục chạy sau endgame
  - thêm `RunEnded` modal + presenter
  - wire `Retry` / `Main Menu`
  - khóa các action gameplay còn mutate state sau endgame
  - thêm regression + smoke test checklist

### Kết luận / bước tiếp theo đã chốt
- Endgame flow hiện tại là một cụm **đã có nửa backend nhưng chưa hoàn thiện player-facing flow**.
- Hướng triển khai được chốt theo thứ tự ít rủi ro:
  1. chuẩn hóa outcome/reason ở core
  2. freeze simulation khi run ended
  3. thêm UI modal `RunEnded`
  4. wire `Retry` / `Main Menu`
  5. khóa các action gameplay quan trọng sau endgame
  6. thêm regression và smoke test
- Khuyến nghị thực dụng cho pass implementation đầu tiên:
  - hoàn thiện nhánh **Defeat** trước
  - sau đó mới polish/hoàn thiện nhánh **Victory**

## 2026-03-25

### Tóm tắt
Đợt cập nhật này chuyển từ stabilization sang **hoàn thiện backbone cho M1 / Wave 1**, tập trung vào các flow người chơi đầu tiên: **New Run / Save / Continue**, cùng với baseline start package, HUD clock và Main Menu tối thiểu để smoke test ngay trong game. Sau đó repo được nối tiếp sang **M1 / Wave 2 - M1-B2**, chốt placement feedback rõ ràng hơn cho build preview.

### M1 / Wave 2 / M1-B2 Placement feedback
- Nâng `PlacementInputController` để placement preview đọc rõ hơn ở runtime:
  - giữ footprint overlay theo `ValidateBuilding(...)`
  - tách visual **entry marker** cho valid/invalid thay vì chỉ dùng chung driveway/invalid tile
  - thêm **front / entry direction marker** theo `Dir4` để người chơi đọc được mặt trước của building
- Bổ sung support Inspector/config cho placement preview:
  - `TileEntryValid`
  - `TileEntryInvalid`
  - `TileFrontNorth/East/South/West`
  - `FrontArrowSprite` fallback khi chưa author tile front riêng
- Làm rõ invalid state hơn ở ghost preview:
  - màu invalid đậm hơn
  - alpha mạnh hơn
  - footprint fill nhỏ hơn nhẹ để bớt bị đọc như world highlight thường
- Xiết cleanup preview khi thoát mode / cancel / pointer bị UI chặn / đổi sang road tool để tránh marker rác còn sót.
- Trong quá trình test đã xác nhận một bẫy config dễ gặp: nếu `Tile Front *` bị gán nhầm sang `Tile_Prev_BAD` thì placement hợp lệ vẫn có thể hiện một ô đỏ gần driveway; đây là lỗi config asset chứ không phải logic placement invalid.
- M1-B2 hiện đã ổn ở mức player-facing: nhìn ra được **valid vs invalid**, **footprint**, **entry point**, và **front direction** trong build preview.

### Wave 1 / Main Menu / New Run
- Thêm **Main Menu tối thiểu** trong scene `MainMenu` với các nút:
  - `New Run`
  - `Continue`
  - `Quit`
- Nối `New Run` vào flow thật qua `GameAppController` / `GameBootstrap` để có thể vào gameplay state hợp lệ từ menu.
- Bổ sung patch reset cho `GameLoop.StartNewRun()`:
  - reset clock theo đường start thật của `RunClockService`
  - clear `RunStartRuntime` caches trước khi dựng run mới
  - giúp New Run lặp lại sạch hơn giữa nhiều lần start liên tiếp.
- Thêm regression khóa behavior **New Run lần 2 không leak state từ lần 1**.

### HUD / Clock / Debug support
- Sửa `HudPresenter` để bind chắc hơn với run clock state thật:
  - `Year`
  - `Season`
  - `Day`
  - `Phase`
- Bổ sung refresh từ service để tránh stale label khi rollover year/season.
- Thêm quick action debug **`Winter D4 Near End`** để test tự nhiên rollover sang năm mới nhanh hơn.

### Start map / Start package baseline
- Giữ và khóa baseline `StartMapConfig_RunStart_64x64_v0.1` cho Wave 1.
- Harden `RunStartNpcSpawner`:
  - nếu `spawnCell` config không hợp lệ / bị block / out-of-bounds nhẹ
  - runtime sẽ relocate NPC sang cell hợp lệ gần đó thay vì spawn cứng tại vị trí xấu.
- Thêm regression baseline cho Wave 1 start package, xác nhận:
  - gates / lanes đúng
  - HQ / houses / farmhouse / lumber camp / arrow tower có mặt
  - tower start full ammo
  - HQ storage seed đúng
  - NPC workplace mapping đúng
  - NPC không spawn vào building/site blocked cell.

### Save / Continue / Settings Save
- Sửa flow `Continue` để hành xử **explicit** hơn:
  - không có `run_save.json` thì không load scene game
  - `Continue` fail thì không fallback âm thầm sang `NewGame`
- Sửa nút **Save** trong Settings modal để gọi save game thật qua `GameBootstrap.TrySaveNow(...)` thay vì chỉ hiện toast giả.
- Thêm regression khóa behavior `Continue`:
  - restore đúng state từ save
  - không inject lại baseline RunStart
  - giữ đúng clock/timescale/roads/storage/workplace từ save
  - vẫn clear transient runtime state của NPC an toàn.

### Regression / coverage cập nhật thêm
- Save/load runtime cache rebuild sau load đã được unskip bằng fixture hợp lệ hơn (có HQ thật trong loaded world state).
- Combat after load đã có regression cho 2 nhánh quan trọng:
  - defend + còn enemy restore → không double-spawn
  - defend + không còn enemy restore → restart spawn đúng
- Wave 1 hiện đã có regression đáng tin cho:
  - start package baseline
  - clean reset giữa repeated New Runs
  - continue restore-state

### Wave 2 / workforce assignment loop player-facing
- Gom rule workforce dùng chung vào `WorkforceAssignmentRules` để thống nhất:
  - worker cap theo building/level
  - đếm worker assigned theo workplace
  - validate target có nhận worker hay đã full slot
- Nâng `AssignNpcModalPresenter` để flow assignment dễ đọc và usable hơn:
  - thêm summary `Unassigned` / `Assigned here`
  - sort danh sách NPC theo thứ tự **assigned here trước**, rồi **unassigned**, rồi **assigned elsewhere** để NPC vừa assign hiện ngay trên cùng
  - hiện status rõ hơn cho từng NPC (`Unassigned`, `Assigned here`, `Assigned elsewhere`)
  - action button đổi rõ ý nghĩa hơn: `Assign here` / `Move here` / `Unassign`
  - assign từ UI giờ bị chặn đúng nếu building chưa xây xong, không nhận worker, hoặc đã full slot
  - khi workplace full, modal hiện hint rõ và button row đổi sang `Full` thay vì disable im lặng
- `InspectPanelPresenter` chuyển sang dùng chung workforce rules để số worker/slot trong inspect và modal không lệch nhau.
- Sửa `TutorialHintsService` để hint player-facing trỏ đúng flow mới: chọn workplace rồi bấm `ASSIGN NPC`, không còn nhắc debug NPC tool.
- Bổ sung regression cho workforce:
  - NPC `unassigned` không bị assign job qua `JobAssignmentService`
  - slot cap respect đúng, nhưng NPC đang ở đúng workplace vẫn không bị tự chặn bởi check full slot khi refresh/move logic

### Boundary cleanup / docs
- Hoàn tất 3 phase cleanup boundary `Build / Jobs / RunStart`:
  - `IBuildWorkplaceResolver`
  - `IBuildJobOrchestrator`
  - `IJobWorkplacePolicy`
- Cập nhật `docs/architecture/module-boundaries-overview.md` để ghi lại boundary cleanup sau stabilization.
- Cập nhật `docs/GDD/40_Implementation/SEASONAL_BASTION_IMPLEMENTATION_CHECKLIST_M1_WAVE1_v1.0_VN.md` theo trạng thái thực tế:
  - phần nào đã done
  - phần nào done-ish nhưng còn polish
  - smoke test nào đã pass thật trong repo.
- `Assets/_Game/Tests/Game.Tests.asmdef` được mở rộng references để test assembly chạy được với cụm regression mới (gồm cả combat/economy).

### Ghi chú
- Trạng thái hiện tại của Wave 1: **usable vertical-slice backbone**.
- Các flow player-facing đầu tiên (`New Run / Save / Continue`) đều đã có đường chạy thật và có regression quan trọng khóa behavior.
- Các việc còn lại chủ yếu là polish UX/UI hoặc chuyển sang gameplay tasks cho wave tiếp theo.

## 2026-03-24

### Tóm tắt
Đợt cập nhật này tập trung vào việc hoàn thiện vòng **stabilization thủ công** cho Build, RunStart và Save/Load; đồng thời cải thiện workflow debug để test nhanh hơn ngay trong game.

### Build / UI / huỷ construction
- Thêm nút **`CANCEL CONSTRUCTION`** vào Inspect panel cho công trình đang xây.
- Mở rộng `IBuildOrderService` để UI có thể gọi trực tiếp:
  - `CancelBySite(...)`
  - `CancelByBuilding(...)`
- Nối flow UI chính để người chơi có thể huỷ construction mà không cần dùng debug tool.
- Fix rollback auto-road khi huỷ build:
  - huỷ đúng driveway được auto-create cho order đó
  - không xoá nhầm road cũ đã tồn tại từ trước nếu cell driveway bị trùng

### Save / Load
- Bổ sung quick actions vào **Essential Debug Panel**:
  - `Save Run`
  - `Load + Apply`
  - `Quick Save+Load`
  - `Delete Save`
  - `Run SaveLoad Matrix`
  - `Internal CI SaveLoad`
- Fix refresh road sau load bằng cách publish `RoadsDirtyEvent()` sau `Load + Apply`.
- Rebuild lại runtime cache cần thiết sau load để tránh mất trạng thái runtime phụ thuộc RunStart config.
- Bổ sung refresh resource UI sau load để HUD/inspect phản ánh lại tài nguyên đúng trạng thái save.
- Tích hợp **resume combat tự động sau load** trong debug flow để tránh phải spawn enemy thêm lần nữa mới đánh thức combat loop.

### Smoke test / stabilization
- Hoàn tất smoke test thủ công cho:
  - Jobs
  - Build
  - RunStart
- Save/load hiện đã pass ở mức manual cho các case chính:
  - active build site
  - active `BuildWork`
  - active `RepairWork`
  - queued haul jobs
  - NPC đang giữ `CurrentJob`
  - auto-road sau khi đặt build site
- `docs/stabilization-checklist.md` đã được:
  - dịch sang tiếng Việt
  - cập nhật trạng thái pass thực tế
  - rút gọn lại theo các mục còn actionable

### Regression / baseline cập nhật thêm
- Jobs:
  - `JobAssignmentService`: role filter đúng, workplace roles invalid thì không assign và notify đúng
  - `JobExecutionService`: current job missing/terminal sẽ dọn state NPC đúng sau tick
  - `JobStateCleanupService`: cleanup NPC job sẽ clear current job, set idle, và release claims
  - `JobEnqueueService`: harvest respect slot cap theo số NPC workplace và không enqueue khi local cap đã đầy
- Build:
  - `BuildOrderCreationService`: fail sớm đúng cho thiếu tài nguyên / placement invalid / upgrade bị khóa
  - `BuildOrderCancellationService`: không xóa nhầm road cũ, refund đúng storage, cancel repair xóa tracked repair job
  - `BuildJobPlanner`: stale tracked `BuildWork` được prune và `BuildWork` được recreate sau terminal state
  - `BuildOrderTickProcessor`: complete path của upgrade order xử lý đúng
  - rebuild-after-load không duplicate active order
  - smoke case độc lập verify rebuild-after-load khôi phục đúng progress + placeholder binding của place order
- RunStart / SaveLoad runtime:
  - `RunStartValidator`: `GATE_NOT_CONNECTED`, `GATE_NOT_ROAD`, `NPC_SPAWN_OOB`, `NPC_WORKPLACE_UNBUILT`
  - `RunStartPlacementHelper`: relocation đúng và vẫn tôn trọng `BuildableRect`
  - `RunStartStorageInitializer`: seed đúng starting storage vào HQ constructed duy nhất
  - `RunStartHqResolver`: chọn HQ target deterministically khi có nhiều candidate
  - `RunStartFacade`: config header invalid fail trước khi tạo partial world/runtime state
  - `RunStartWorldBuilder`: invalid building def fail fast
  - `SaveLoadApplier`: stale `Npc.CurrentJob` từ save bị clear và NPC được reset về idle để runtime assignment rebuild sạch
  - rebuild runtime cache sau load đã có regression test, nhưng hiện vẫn `Ignore` có chủ đích trong EditMode fixture rút gọn khi thiếu production defs/config đầy đủ
- `docs/stabilization-checklist.md` đã được cập nhật lại theo trạng thái pass mới nhất.

### GDD / planning / backlog
- Hợp nhất và viết lại bộ GDD working set hiện tại thành cấu trúc gọn, thực dụng và dễ dùng hơn trong `Docs/GDD`:
  - `Docs/GDD/00_Master/SEASONAL_BASTION_GDD_COMPLETE_v1.0_VN.md`
  - `Docs/GDD/10_Specs/SEASONAL_BASTION_UI_SPEC_v1.0_VN.md`
  - `Docs/GDD/10_Specs/SEASONAL_BASTION_UX_ONBOARDING_SPEC_v1.0_VN.md`
  - `Docs/GDD/10_Specs/SEASONAL_BASTION_CONTENT_SCOPE_SPEC_v1.0_VN.md`
  - `Docs/GDD/20_Roadmap/SEASONAL_BASTION_VERTICAL_SLICE_ROADMAP_v1.0_VN.md`
  - `Docs/GDD/30_Backlog/SEASONAL_BASTION_BACKLOG_M1_VERTICAL_SLICE_v1.0_VN.md`
  - `Docs/GDD/30_Backlog/SEASONAL_BASTION_BACKLOG_M2_YEAR1_COMPLETE_v1.0_VN.md`
  - `Docs/GDD/30_Backlog/SEASONAL_BASTION_BACKLOG_M3_BASE_RUN_COMPLETE_v1.0_VN.md`
- Viết trọn bộ implementation checklist cho toàn bộ M1:
  - `Docs/GDD/40_Implementation/SEASONAL_BASTION_IMPLEMENTATION_CHECKLIST_M1_WAVE1_v1.0_VN.md`
  - `Docs/GDD/40_Implementation/SEASONAL_BASTION_IMPLEMENTATION_CHECKLIST_M1_WAVE2_v1.0_VN.md`
  - `Docs/GDD/40_Implementation/SEASONAL_BASTION_IMPLEMENTATION_CHECKLIST_M1_WAVE3_v1.0_VN.md`
  - `Docs/GDD/40_Implementation/SEASONAL_BASTION_IMPLEMENTATION_CHECKLIST_M1_WAVE4_v1.0_VN.md`
  - `Docs/GDD/40_Implementation/SEASONAL_BASTION_IMPLEMENTATION_CHECKLIST_M1_WAVE5_v1.0_VN.md`
- Bổ sung `Docs/GDD/40_Implementation/SEASONAL_BASTION_WAVE1_CODE_MAP_v1.0_VN.md` để map từng task Wave 1 sang các file code hiện tại cần đụng tới.
- Thêm `Docs/GDD/README.md` giải thích cấu trúc thư mục, thứ tự đọc, và quy ước cập nhật.
- Di chuyển toàn bộ bộ GDD working set ra khỏi `Assets/` sang `docs/GDD` để tránh `.meta` noise và tách tài liệu khỏi Unity asset tree.
- Xóa sạch các file `.meta` còn sót trong `docs/GDD` sau khi di chuyển, để bộ docs trở về đúng vai trò markdown/docs thuần ngoài Unity asset tree.
- Thêm `docs/architecture/module-boundaries-overview.md` để ghi lại sơ đồ kiến trúc tổng thể, runtime flow, và boundary giữa các module/domain chính trước khi bắt đầu implementation Wave 1.

### Boundary cleanup sau stabilization
- Đã khóa regression cho cụm `SaveLoadApplier` / `CombatService.ResetAfterLoad(...)` ở 2 nhánh quan trọng:
  - defend + còn enemy restore từ save → không double-spawn wave mới ngay
  - defend + không còn enemy restore → wave restart bình thường sau load
- Gỡ `Ignore` có chủ đích ở regression rebuild runtime cache sau load bằng cách dựng test fixture đúng điều kiện tối thiểu (có HQ hợp lệ trong loaded world state).
- Làm sạch boundary `Build / Jobs / RunStart` theo 3 phase nhỏ, không đổi gameplay behavior:
  - `IBuildWorkplaceResolver` để Build không tự hard-wire workplace selection
  - `IBuildJobOrchestrator` để `BuildOrderService` không phụ thuộc trực tiếp vào concrete planner
  - `IJobWorkplacePolicy` để Build resolver và Jobs dùng chung source of truth cho workplace-role policy
- `GameServicesFactory` / `GameServices` đã được cập nhật để inject các dependency boundary mới thay vì để Build new nội bộ các policy/planner quan trọng.

### Ghi chú
- Baseline manual/smoke hiện đã khá chắc cho vòng stabilization đầu tiên.
- Đã khóa một mốc baseline ổn định cho batch stabilization ngày 2026-03-24.
- Bộ tài liệu trong `Assets/GDD` hiện đã đủ để chuyển sang implementation theo milestone mà không cần viết thêm tài liệu lớn ngay.

## 2026-03-23

### Tóm tắt
Đợt stabilization này tập trung vào Jobs, routing workplace cho Build/Repair, hành vi tiếp đạn của tower, và làm sạch đáng kể workflow debug trong game.

### Jobs / Build / Repair
- Sửa workplace routing để **BuilderHut được ưu tiên** cho `BuildWork` / `RepairWork`.
- HQ giờ đóng vai trò **fallback** khi BuilderHut không còn worker rảnh.
- Các job `BuildWork` / `RepairWork` đang queue có thể retarget khi workplace availability thay đổi.
- `RepairWorkExecutor` được đổi từ kiểu hồi máu theo từng cục sang **repair progress liên tục theo tick**, cho cảm giác gần với `BuildWork` hơn.
- Flow repair/build và các smoke scenario liên quan tới Jobs đã được chạy và đánh dấu ổn định cho pass này.

### Tower ammo / Armory priority
- Hành vi resupply tower được mở rộng từ kiểu “chỉ tower low/empty mới xin đạn” thành **“mọi tower chưa full đều có thể xin top-up”**.
- Các tower dưới ngưỡng urgent giờ được xử lý theo kiểu **urgent-first**.
- Thêm soft preemption để một job resupply đang queue có thể **retarget sang tower urgent trước khi đợt giao tiếp theo bắt đầu**.
- `ResupplyTower` vẫn là ưu tiên cao nhất cho worker role Armory, cao hơn các ammo job khác.

### Debug tools / QA workflow
- `DebugHUDHub` được đơn giản hoá thành **Essential Debug Panel** thực dụng hơn.
- Thêm hoặc cải thiện quick actions cho:
  - cấp tài nguyên
  - unlock all
  - damage / heal / repair building
  - complete hovered site / complete all sites
  - drain/refill ammo tower
  - điều khiển time scale, gồm cả `5x`
  - nhảy ngày / mùa
  - spawn enemy theo lane
  - spawn NPC nhanh
- Spawn enemy bằng debug giờ sẽ **tự bật combat debug mode** để enemy di chuyển ngay.
- Bổ sung phần hiển thị **current target** rõ hơn cho building đang hover/chọn, gồm cả thông tin ammo của tower liên kết.
- Thêm **click-to-lock building target** và `Clear Lock`, để debug actions có thể bám vào một building cụ thể thay vì chỉ phụ thuộc hover TTL.
- Phần chọn loại enemy trong debug UI giờ dùng **preset list** thay vì nhập text tự do.

### Checklist / tài liệu stabilization
- `docs/stabilization-checklist.md` được cập nhật để phản ánh regression coverage đã có sẵn trong repo.
- Phần Jobs smoke test được mở rộng với ghi chú pass/fail thực tế hơn.
- Wording của RunStart trong checklist được chuẩn hóa để khớp với tên invariant trong code.
- Các mục Jobs smoke test đã verify trong ngày được tick hoàn thành.

### Ghi chú
- Trọng tâm hiện tại là stabilization thực dụng, chưa phải cleanup/refactor diện rộng.
- Ở thời điểm pass này, save/load stabilization và các smoke pass còn lại của Build / RunStart vẫn còn pending.
