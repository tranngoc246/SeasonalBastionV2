# Task Breakdown — Opening Economy Seed Stability

## Goal
Khóa độ ổn định của **opening economy** khi dùng `Hybrid` resource generation, để nhiều seed khác nhau vẫn cho ra đầu game:
- luôn có đủ tài nguyên starter để vào loop cơ bản
- không có patch spawn ở vị trí nhìn hợp lệ nhưng pathing/harvest dùng dở
- fallback giữa `Hybrid / AuthoredOnly / GeneratedOnly` rõ ràng, debug được
- có regression + smoke checklist đủ mạnh để balance tiếp theo không làm vỡ opener

---

## Vì sao đây là mục tiêu tiếp theo
Codebase hiện tại đã có backbone khá tốt cho resource generation:
- có `RunStartResourceZoneGenerator`
- có `RunStartZoneInitializer` route theo mode
- có `ResourcePatchService`
- harvesting đã consume patch thật
- overlay / inspect / highlight đã usable

Nhưng rủi ro gameplay lớn nhất còn lại không phải là “có generate được không”, mà là:
- seed khác nhau có thể cho opener mạnh/yếu quá lệch
- starter zones có thể đạt **distance/bounds** nhưng vẫn không thật sự thuận cho harvest/logistics
- `Hybrid` hiện đang thiên về “generate được thì dùng”, chưa có layer **quality gate** rõ ràng cho opening
- runtime cache / debug metadata hiện phản ánh zone cuối cùng, nhưng chưa đủ để truy ra **zone nào là starter guarantee, zone nào là bonus, zone nào do fallback**

Nói ngắn gọn: feature đã usable, giờ cần chuyển từ **works** sang **stable + debuggable**.

---

## Out of Scope
Không làm chung batch này:
- fade/exhausted visual polish cho patch cạn
- biome/noise worldgen sâu hơn
- resource node/deposit simulation mới
- rebalance toàn bộ economy mid/late game
- stickiness nâng cao cho harvest worker nếu chưa cần để khóa opening

Batch này chỉ tập trung vào **ổn định đầu game qua nhiều seed**.

---

## Chốt definition of done cho batch này
Sau batch này, project nên đạt:

1. `Hybrid` mode luôn tạo được một bộ starter patches usable quanh HQ cho map chuẩn hiện tại.
2. Với một tập seed smoke test cố định, opener không rơi vào case:
   - thiếu hẳn Wood/Food/Stone starter
   - patch bị quá xa hoặc quá awkward so với HQ/road opening
   - worker không chọn được target harvest hợp lệ khi patch còn tài nguyên
3. Nếu generation không đạt quality tối thiểu, hệ thống fallback có chủ đích và trace được.
4. Runtime/test/debug phân biệt được:
   - starter-generated
   - bonus-generated
   - authored-fallback
   - legacy-fallback (nếu còn giữ)
5. Có regression tests đủ để refactor generator mà không làm vỡ opening.

---

## Các vấn đề cụ thể cần khóa

### 1) Starter guarantee hiện mới dừng ở “có patch gần HQ”
Hiện test mới khóa kiểu:
- có Wood/Food/Stone gần HQ
- cùng seed ra cùng layout
- khác seed ra layout khác
- cell không out-of-bounds

Nhưng chưa khóa các chất lượng gameplay quan trọng hơn:
- patch có reachable path từ khu HQ hay không
- patch có bị road/bố cục đầu game làm thành “gần mà khó dùng” hay không
- total starter supply có đủ cho opening loop tối thiểu hay không
- iron starter-lite có đang chen vào ring làm giảm quality cho wood/food/stone không

### 2) Generator hiện chưa có khái niệm quality scoring cho whole opening
`RunStartResourceZoneGenerator` hiện pick từng rect hợp lệ theo rule, nhưng chưa có pass đánh giá tổng thể cả bộ zones:
- coverage theo resource type
- phân bố theo quadrant / lane / road accessibility
- congestion quanh HQ
- độ đối xứng/playability giữa nhiều seed

### 3) Hybrid fallback hiện đúng về mặt an toàn, nhưng chưa tốt về mặt debugability
`RunStartZoneInitializer` hiện:
- `Hybrid`: thử generated, fail thì authored
- không ghi rõ lý do fail / mode cuối cùng đã dùng
- `RunStartRuntimeCacheBuilder` chưa lưu origin/quality/fallback reason

Vì vậy khi gặp một seed tệ, sẽ khó trả lời nhanh:
- generator fail hẳn?
- generator pass nhưng quality kém?
- đang dùng authored fallback?
- đang rơi về legacy fallback?

### 4) Harvesting đã patch-aware nhưng chưa có regression riêng cho opening stability
`HarvestTargetSelectionHelper` + `HarvestExecutor` đã tiến bộ rõ, nhưng chưa có test khóa chuỗi sau:
- run start -> patches được build đúng
- harvest worker có thể pick target trên starter patch
- patch cạn thì retarget sang patch khác trong opening hợp lý
- không bị trạng thái “starter có đó nhưng worker không dùng nổi”

---

# Implementation Plan — file-by-file

---

## FILE 1 — TẠO MỚI `docs/opening-economy-smoke-matrix.md`

### Goal
Chốt một bộ smoke matrix cố định cho QA / manual verify / future balancing.

### Cần làm
- [ ] Tạo bảng seed smoke test cố định, ví dụ 12–20 seed đại diện
- [ ] Với mỗi seed, ghi các mục phải check:
  - [ ] có đủ `Wood / Food / Stone` starter
  - [ ] `Iron` không chiếm chỗ quá hung trong vòng gần HQ
  - [ ] worker farm/lumber hiện có pick được target thật
  - [ ] patch overlay/inspect đúng với runtime state
  - [ ] ít nhất 1–2 hướng expand có bonus patches hợp lý
- [ ] Ghi rõ mức pass/fail:
  - [ ] blocker
  - [ ] playable but weak
  - [ ] good
- [ ] Thêm phần "known acceptable variance" để không biến mọi khác biệt seed thành bug

### Verify
- [ ] Có 1 checklist QA đủ ngắn để chạy thật
- [ ] Có seed set cố định dùng lại được qua nhiều batch

---

## FILE 2 — CẬP NHẬT `Assets/_Game/Core/RunStart/RunStartResourceZoneGenerator.cs`

### Goal
Thêm lớp **opening quality gate** thay vì chỉ dừng ở rect-level validity.

### Cần làm
- [ ] Sau khi generate candidate zones, thêm bước evaluate toàn bộ opening layout
- [ ] Tách helper kiểu:
  - [ ] `EvaluateOpeningLayout(...)`
  - [ ] `HasStarterCoverage(...)`
  - [ ] `TryScoreStarterAccessibility(...)`
  - [ ] `TryScoreBonusDistribution(...)`
- [ ] Chốt một bộ rule pass tối thiểu cho map hiện tại:
  - [ ] có ít nhất 1 patch `Wood` starter usable
  - [ ] có ít nhất 1 patch `Food` starter usable
  - [ ] có ít nhất 1 patch `Stone` starter usable
  - [ ] khoảng cách / path cost tới các starter chính không vượt ngưỡng quá tệ
  - [ ] tổng starter cells hoặc starter amount không dưới ngưỡng tối thiểu
- [ ] Nếu candidate fail quality:
  - [ ] retry với deterministic variant khác (bounded retry count)
  - [ ] nếu vẫn fail thì trả error rõ ràng thay vì im lặng `0 zones`
- [ ] Tránh làm thành optimizer quá nặng; chỉ cần pass/fail + score đủ practical

### Gợi ý rule thực dụng cho pass đầu
- [ ] dùng `Pathfinder.TryEstimateCost(...)` nếu có để ước lượng accessibility từ HQ anchor hoặc HQ ring
- [ ] fallback Manhattan nếu chưa estimate được nhưng cần log rõ loại fallback
- [ ] score thấp hơn nếu starter patches dồn cùng 1 phía của HQ
- [ ] score thấp hơn nếu patch quá sát road spine chính gây cảm giác nghẽn build space

### Verify
- [ ] cùng seed vẫn deterministic
- [ ] generated layout quality tốt hơn rõ trên smoke seeds
- [ ] không tăng thời gian start run quá mức đáng kể

---

## FILE 3 — CẬP NHẬT `Assets/_Game/Core/RunStart/RunStartZoneInitializer.cs`

### Goal
Làm fallback policy minh bạch và có chủ đích hơn.

### Cần làm
- [ ] Refactor route theo 3 tầng rõ ràng:
  1. [ ] generated pass quality gate
  2. [ ] authored fallback
  3. [ ] legacy fallback
- [ ] Khi generated fail, giữ lại `error`/reason có cấu trúc hơn
- [ ] Tách helper:
  - [ ] `TryApplyGeneratedZones(...)`
  - [ ] `TryApplyAuthoredFallback(...)`
  - [ ] `ApplyLegacyFallbackZones(...)`
  - [ ] `RecordZoneApplicationMode(...)`
- [ ] Với `Hybrid`, chỉ dùng authored fallback khi generated:
  - [ ] fail technical
  - [ ] hoặc fail quality gate
- [ ] Nếu authored config cũng trống/không dùng được, mới rơi về legacy fallback

### Verify
- [ ] đọc code thấy rõ exact fallback chain
- [ ] không còn trạng thái “fallback ngầm nhưng khó biết tại sao”

---

## FILE 4 — CẬP NHẬT `Assets/_Game/Core/RunStart/RunStartRuntime.cs`

### Goal
Lưu runtime metadata đủ để debug opening issues theo seed.

### Cần làm
- [ ] Thêm metadata tối thiểu như:
  - [ ] `ResourceGenerationModeRequested`
  - [ ] `ResourceGenerationModeApplied`
  - [ ] `ResourceGenerationFailureReason`
  - [ ] `OpeningQualityScore` hoặc `OpeningQualityBand`
- [ ] Nếu muốn gọn hơn, gom thành struct riêng:
  - [ ] `RunStartResourceGenerationDebugState`
- [ ] Metadata này không cần player-facing; chủ yếu cho debug/UI dev/test

### Verify
- [ ] Khi smoke test seed xấu, nhìn runtime state biết ngay run đang ở mode nào và fail vì gì

---

## FILE 5 — CẬP NHẬT `Assets/_Game/Core/RunStart/RunStartRuntimeCacheBuilder.cs`

### Goal
Để runtime cache phản ánh không chỉ hình dạng zone, mà còn cả **origin/debug meaning** của chúng.

### Cần làm
- [ ] Mở rộng runtime zone metadata để nếu có thể phân biệt:
  - [ ] starter-generated
  - [ ] bonus-generated
  - [ ] authored
  - [ ] legacy-fallback
- [ ] Nếu chưa muốn đổi `ZoneRect` mạnh tay, thêm map debug side-channel trong `RunStartRuntime`
- [ ] Preserve backward compatibility cho các chỗ chỉ đọc rect/type/cellCount

### Verify
- [ ] debug overlay / logs / test helper có thể biết zone đến từ đâu

---

## FILE 6 — CẬP NHẬT `Assets/_Game/Core/RunStart/RunStartConfigValidator.cs`

### Goal
Xiết validator theo hướng bảo vệ opening quality, không chỉ syntax range.

### Cần làm
- [ ] Thêm validate warning/error cho các config dễ sinh seed tệ:
  - [ ] starter rule thiếu `Wood/Food/Stone`
  - [ ] distance band starter quá rộng hoặc quá xa
  - [ ] starter counts quá thấp cho loại tài nguyên critical
  - [ ] rect sizes quá nhỏ làm total supply quá mỏng
- [ ] Nếu không muốn fail hard hết, ít nhất có helper validate semantic riêng cho editor/test
- [ ] Cân nhắc tách:
  - [ ] `ValidateResourceGenerationSchema(...)`
  - [ ] `ValidateResourceGenerationGameplaySemantics(...)`

### Verify
- [ ] config sai kiểu “compile được nhưng opener tệ” bị phát hiện sớm hơn

---

## FILE 7 — CẬP NHẬT `Assets/_Game/Resources/RunStart/StartMapConfig_RunStart_64x64_v0.1.json`

### Goal
Retune rules cho map chuẩn hiện tại theo tiêu chí opening ổn định.

### Cần làm
- [ ] Rà lại `starterRules` hiện tại:
  - [ ] Wood/Food có thể giữ 2..3 clusters nhưng cần chốt ngưỡng amount tối thiểu thực dụng
  - [ ] Stone starter có thể giữ 1..2 nhưng tránh quá xa
  - [ ] Iron starter-lite cân nhắc đẩy xa hơn hoặc giảm độ chen vào ring gần HQ nếu đang làm opener nhiễu
- [ ] Rà lại `bonusRules` để outer ring không quá dày gần HQ gây lẫn với starter intent
- [ ] Nếu cần, chốt khoảng distance band rõ hơn theo map 64x64 hiện tại thay vì rule quá chung
- [ ] Ghi note trong JSON rõ hơn mục đích của từng rule để future tuning không phá intent

### Verify
- [ ] smoke test nhiều seed cho cảm giác opening ổn định hơn
- [ ] resource layout vẫn đủ đa dạng, không quay về static giả dạng procedural

---

## FILE 8 — CẬP NHẬT `Assets/_Game/Core/ResourcePatchService.cs`

### Goal
Đảm bảo patch-level data đủ để support opening stability checks và debug.

### Cần làm
- [ ] Cân nhắc thêm metadata nhẹ cho patch:
  - [ ] origin/source kind (starter/bonus/authored/legacy)
  - [ ] initial generation index/rule label nếu cần debug
- [ ] Thêm helper phục vụ test/evaluation:
  - [ ] `GetRemainingPatchesByResource(...)`
  - [ ] `TryGetBestPatchByPathCost(...)` nếu muốn gom logic dần khỏi helper rải rác
- [ ] Xem lại `ComputeInitialAmount(...)` để total amount per cell có hợp với opening goals không
  - [ ] nếu không đổi balance bây giờ thì ít nhất ghi rõ assumption trong doc/test

### Verify
- [ ] test/evaluator không phải tự scan patch state quá thủ công
- [ ] patch metadata đủ để inspect/debug opening issues

---

## FILE 9 — CẬP NHẬT `Assets/_Game/Jobs/HarvestTargetSelectionHelper.cs`

### Goal
Khóa case starter patch có nhưng worker không chọn target tốt trong opening.

### Cần làm
- [ ] Bổ sung ưu tiên nhẹ cho patch starter gần workplace/HQ trong early opening nếu score quá sát nhau
- [ ] Tránh việc worker nhảy sang bonus patch xa chỉ vì richness score hơi tốt hơn
- [ ] Nếu strict path-aware fail, fallback nên vẫn ưu tiên patch starter usable trước patch bonus xa
- [ ] Nếu đã có metadata origin trong patch, tận dụng nó ở đây

### Verify
- [ ] worker farm/lumber ở start map ưu tiên patch starter hợp lý hơn
- [ ] patch bonus không “hút” worker quá sớm khi patch starter vẫn ổn

---

## FILE 10 — CẬP NHẬT `Assets/_Game/Jobs/Executors/HarvestExecutor.cs`

### Goal
Khóa flow opening harvest thật từ target selection đến depletion.

### Cần làm
- [ ] Thêm regression-friendly handling cho case target cell thuộc patch vừa cạn:
  - [ ] retarget nhanh và deterministic hơn
- [ ] Rà lại đoạn `carry <= 0` để không reset timer theo cách tạo cảm giác rung/lặp vô ích
- [ ] Nếu patch starter cạn, cho phép chuyển bonus patch nhưng vẫn theo thứ tự hợp lý
- [ ] Nếu cần, tách helper để test retarget flow độc lập hơn

### Verify
- [ ] opening worker không kẹt ở trạng thái target cũ / cancel spam / đứng yên khi patch đổi trạng thái

---

## FILE 11 — CẬP NHẬT `Assets/_Game/Tests/EditMode/RunStart/ResourceZoneGenerationTests.cs`

### Goal
Nâng test từ “generate được” lên “opening usable”.

### Cần làm
- [ ] Thêm test cho starter accessibility:
  - [ ] HQ -> starter wood reachable/acceptable cost
  - [ ] HQ -> starter food reachable/acceptable cost
  - [ ] HQ -> starter stone reachable/acceptable cost
- [ ] Thêm test cho fallback behavior:
  - [ ] generated fail quality -> authored fallback
  - [ ] authored unavailable -> legacy fallback
- [ ] Thêm test cho metadata:
  - [ ] runtime biết mode applied / failure reason / zone origin
- [ ] Thêm multi-seed regression set nhỏ (ví dụ 8–12 seed cố định)
  - [ ] không cần assert exact snapshot toàn bộ
  - [ ] assert quality band / starter coverage / bounds / determinism

### Verify
- [ ] refactor generator vẫn được khóa ở level gameplay intent

---

## FILE 12 — TẠO MỚI `Assets/_Game/Tests/EditMode/Jobs/HarvestOpeningStabilityTests.cs`

### Goal
Khóa integration tối thiểu giữa run-start patches và harvest opening.

### Cần làm
- [ ] Tạo test setup run-start có HQ + starter producers + resource patches
- [ ] Test các case:
  - [ ] worker pick được target trong starter wood patch
  - [ ] worker pick được target trong starter food patch
  - [ ] patch cạn thì retarget sang patch còn tài nguyên
  - [ ] patch bonus không bị ưu tiên vô lý khi starter patch còn usable
- [ ] Nếu setup full executor quá nặng, có thể tách test helper ở level selection trước rồi thêm 1–2 integration test executor thật

### Verify
- [ ] opening harvest loop có regression coverage riêng, không chỉ dựa vào manual smoke

---

## FILE 13 — CẬP NHẬT `CHANGELOG.md`

### Goal
Ghi lại đúng intent của batch tiếp theo sau khi hoàn thành.

### Cần làm
- [ ] Ghi rõ pass này không phải feature mới hoàn toàn, mà là **stability + fallback + regression hardening** cho hybrid generation opening
- [ ] Tóm tắt các rule quality gate/fallback/test đã khóa

### Verify
- [ ] người đọc changelog hiểu ngay đây là pass productionizing opening economy

---

# Suggested Execution Order

## Phase 1 — Debug/Fallback visibility
1. `RunStartZoneInitializer`
2. `RunStartRuntime`
3. `RunStartRuntimeCacheBuilder`
4. `opening-economy-smoke-matrix.md`

### Outcome cần đạt
- biết mỗi run đang dùng generated/authored/legacy
- biết fail reason và quality band
- có bộ seed smoke test cố định

---

## Phase 2 — Quality gate cho generator
5. `RunStartResourceZoneGenerator`
6. `RunStartConfigValidator`
7. `StartMapConfig_RunStart_64x64_v0.1.json`

### Outcome cần đạt
- generator không chỉ “spawn được” mà còn “spawn đủ tốt cho opening”
- rule config được tune theo map thật

---

## Phase 3 — Harvest opening stability
8. `ResourcePatchService`
9. `HarvestTargetSelectionHelper`
10. `HarvestExecutor`

### Outcome cần đạt
- worker tận dụng starter patches đúng ý
- depletion/retarget trong opening không bị awkward

---

## Phase 4 — Regression hardening
11. `ResourceZoneGenerationTests.cs`
12. `HarvestOpeningStabilityTests.cs`
13. `CHANGELOG.md`

### Outcome cần đạt
- batch này được khóa bằng test + tài liệu, không chỉ dựa vào cảm giác test tay

---

# Practical acceptance checklist

## Must-have
- [ ] 8–12 seed smoke test qua được ở mức playable trở lên
- [ ] không có seed nào thiếu `Wood/Food/Stone` starter usable
- [ ] `Hybrid` fallback chain trace được rõ ràng
- [ ] worker opening harvest được từ starter patches ổn định
- [ ] regression test pass

## Nice-to-have
- [ ] có quality score/band hiển thị trong debug UI hoặc log
- [ ] patch origin hiện ra được trong inspect/debug
- [ ] có thêm 1 command/debug shortcut để reroll seed nhanh trong editor

---

# Recommendation
Nếu chỉ chọn **một mục tiêu hẹp nhất** cho pass tới, mình khuyến nghị chốt là:

> **Thêm opening quality gate + fallback visibility cho hybrid resource generation.**

Lý do:
- đây là phần leverage cao nhất
- sửa ở đây sẽ cải thiện cả generation, harvesting, QA, balancing
- nếu chưa có layer này, mọi tuning seed/map sau đó đều khá mò mẫm

Sau khi xong quality gate + visibility, mới nên làm tiếp harvest opening polish sâu hơn.