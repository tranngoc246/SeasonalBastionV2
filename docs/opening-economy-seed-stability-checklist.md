# Opening Economy Seed Stability - Checklist

> Mục tiêu: khóa độ ổn định của opening economy cho `Hybrid` resource generation theo hướng dễ debug, dễ test, và ít rủi ro khi refactor.
>
> Checklist này bám theo codebase hiện tại của `SeasonalBastionV2`, ưu tiên thứ tự triển khai thực dụng thay vì chỉ liệt kê ý tưởng.

---

## 1. Mục tiêu của batch này

Sau batch này, project nên đạt:

- `Hybrid` mode tạo được opening usable qua nhiều seed
- fallback chain rõ ràng: `Generated -> AuthoredFallback -> LegacyFallback`
- runtime/debug state cho biết run đã dùng mode nào và fail ở đâu
- phân biệt được zone/patch nào là `starter`, `bonus`, `authored`, `legacy`
- harvest opening ưu tiên starter patches hợp lý
- có regression + smoke checklist đủ để balance tiếp mà không làm vỡ opener

---

## 2. Nguyên tắc triển khai

- Không retune JSON trước khi có debug visibility
- Không nhét quality gate sâu vào rect placement logic nếu đó là concern cấp whole-opening
- Tách rõ:
  - rect validity
  - opening quality
  - fallback policy
  - patch selection policy
- Ưu tiên thêm test khóa intent trước hoặc song song với refactor

---

## 3. Thứ tự triển khai khuyến nghị

### Phase A - Debug / fallback visibility
1. `Assets/_Game/Core/RunStart/RunStartZoneInitializer.cs`
2. `Assets/_Game/Core/Contracts/Run/RunStartRuntimeTypes.cs`
3. `Assets/_Game/Core/RunStart/RunStartRuntimeCacheBuilder.cs`
4. `Assets/_Game/Tests/EditMode/RunStart/ResourceZoneGenerationTests.cs`

### Phase B - Quality gate cho generation
5. `Assets/_Game/Core/RunStart/RunStartResourceZoneGenerator.cs`
6. `Assets/_Game/Core/RunStart/RunStartConfigValidator.cs`

### Phase C - Data tuning
7. `Assets/_Game/Resources/RunStart/StartMapConfig_RunStart_64x64_v0.1.json`

### Phase D - Harvest opening stability
8. `Assets/_Game/Core/ResourcePatchState.cs`
9. `Assets/_Game/Core/ResourcePatchService.cs`
10. `Assets/_Game/Jobs/HarvestTargetSelectionHelper.cs`
11. `Assets/_Game/Jobs/Executors/HarvestExecutor.cs`

### Phase E - Regression / smoke / docs
12. `Assets/_Game/Tests/EditMode/RunStart/ResourceZoneGenerationTests.cs`
13. `Assets/_Game/Tests/EditMode/Jobs/HarvestOpeningStabilityTests.cs`
14. `docs/opening-economy-smoke-matrix.md`
15. `CHANGELOG.md`

---

# 4. Checklist chi tiết file-by-file

---

## FILE 1 - `Assets/_Game/Core/RunStart/RunStartZoneInitializer.cs`

### Goal
Biến nơi này thành source of truth rõ ràng cho fallback chain và generation outcome.

### Hiện trạng
- đã có route theo `AuthoredOnly / Hybrid / GeneratedOnly`
- đã có record basic debug state vào `RunStartRuntime`
- vẫn còn pass/fail khá thô
- chưa phân biệt rõ `technical failure` vs `quality failure`

### Cần làm
- [ ] refactor lại flow generated/authored/legacy cho dễ đọc hơn
- [ ] tách helper rõ:
  - [ ] `TryApplyGeneratedZones(...)`
  - [ ] `TryApplyAuthoredFallback(...)`
  - [ ] `ApplyLegacyFallbackZones(...)`
  - [ ] `RecordAppliedMode(...)`
- [ ] phân biệt rõ reason:
  - [ ] generation fail technical
  - [ ] generation trả empty
  - [ ] generation fail quality gate
- [ ] đảm bảo `Hybrid` chỉ rơi sang authored khi generated thật sự không đạt
- [ ] đảm bảo authored unavailable mới rơi tiếp sang legacy

### Verify
- [ ] nhìn code thấy rõ exact fallback chain
- [ ] runtime state cho biết fail ở bước nào
- [ ] không còn fallback ngầm khó truy dấu

---

## FILE 2 - `Assets/_Game/Core/Contracts/Run/RunStartRuntimeTypes.cs`

### Goal
Mở rộng runtime metadata để debug opening issues theo seed.

### Hiện trạng
- đã có:
  - `ResourceGenerationModeRequested`
  - `ResourceGenerationModeApplied`
  - `ResourceGenerationFailureReason`
  - `OpeningQualityBand`
- `ZoneRect` mới có `Origin`

### Cần làm
- [ ] cân nhắc thêm `OpeningQualityScore`
- [ ] cân nhắc thêm `ResourceGenerationFailureStage`
- [ ] nếu cần, gom debug state thành struct riêng
- [ ] mở rộng `ZoneRect` hoặc side-channel metadata để phân biệt:
  - [ ] `starter-generated`
  - [ ] `bonus-generated`
  - [ ] `authored-fallback`
  - [ ] `legacy-fallback`
- [ ] giữ backward compatibility cho code đang chỉ cần rect/type/origin

### Verify
- [ ] runtime state đủ để đọc nhanh seed xấu đang fail kiểu gì
- [ ] zone metadata không bị mất nghĩa sau khi apply world

---

## FILE 3 - `Assets/_Game/Core/RunStart/RunStartRuntimeCacheBuilder.cs`

### Goal
Đảm bảo runtime cache không chỉ mirror shape của zones, mà còn mirror debug meaning.

### Hiện trạng
- `ApplyRuntimeZonesFromWorld(...)` rebuild bounds tốt
- origin hiện chỉ được suy ra từ applied mode chung
- chưa có phân biệt zone-level starter/bonus

### Cần làm
- [ ] sync zone metadata từ world/runtime debug state vào runtime cache
- [ ] nếu chưa muốn đổi `ZoneRect` nhiều, thêm side-channel map trong `RunStartRuntime`
- [ ] giữ tương thích với overlay/inspect hiện tại
- [ ] tránh để mọi zone cùng một `Origin` chung chung nếu thực tế khác nhau

### Verify
- [ ] debug helper/test có thể biết zone đến từ đâu
- [ ] starter/bonus distinction còn tồn tại sau cache rebuild

---

## FILE 4 - `Assets/_Game/Tests/EditMode/RunStart/ResourceZoneGenerationTests.cs`

### Goal
Khóa behavior fallback/debug trước khi refactor generator mạnh tay.

### Cần làm sớm
- [ ] thêm test cho fallback semantics mới
- [ ] thêm test cho metadata mới trong runtime
- [ ] thêm test generated fail quality -> authored fallback
- [ ] thêm test authored unavailable -> legacy fallback

### Cần làm sau
- [ ] thêm multi-seed regression nhỏ
- [ ] thêm starter accessibility assertions
- [ ] thêm quality band / score assertions

### Verify
- [ ] refactor phase A/B không làm trôi behavior mong muốn

---

## FILE 5 - `Assets/_Game/Core/RunStart/RunStartResourceZoneGenerator.cs`

### Goal
Nâng generator từ `spawn hợp lệ` lên `opening usable`.

### Hiện trạng
- mạnh ở rect validity:
  - distance from HQ
  - occupancy legality
  - separation
  - deterministic fallback sweep
- chưa có quality pass cấp whole-opening

### Cần làm
- [ ] giữ `TryPickZoneRect(...)` là rect-level concern
- [ ] thêm pass evaluate toàn opening sau khi generate xong
- [ ] tách helper như:
  - [ ] `EvaluateOpeningLayout(...)`
  - [ ] `HasStarterCoverage(...)`
  - [ ] `ScoreStarterAccessibility(...)`
  - [ ] `ScoreDistribution(...)`
- [ ] thêm deterministic bounded retry khi quality chưa đạt
- [ ] trả error/reason rõ nếu hết retry vẫn fail

### Rule nên khóa
- [ ] có ít nhất 1 `Wood` starter usable
- [ ] có ít nhất 1 `Food` starter usable
- [ ] có ít nhất 1 `Stone` starter usable
- [ ] path cost tới starter chính không quá tệ
- [ ] starter không dồn hết cùng một phía HQ
- [ ] iron starter-lite không phá opening ring quá mạnh

### Verify
- [ ] cùng seed vẫn deterministic
- [ ] seed xấu bị reject có lý do rõ
- [ ] smoke seeds cho opening ổn hơn rõ rệt

---

## FILE 6 - `Assets/_Game/Core/RunStart/RunStartConfigValidator.cs`

### Goal
Xiết validator theo semantic gameplay, không chỉ schema/range.

### Hiện trạng
- đã validate mode, resourceType, count range, distance range, rect size range
- chưa validate opening quality intent

### Cần làm
- [ ] thêm semantic validation helper cho resource generation
- [ ] flag các config rủi ro:
  - [ ] thiếu starter `Wood/Food/Stone`
  - [ ] distance starter quá xa
  - [ ] rect quá nhỏ
  - [ ] count quá thấp
  - [ ] iron starter quá gần HQ nếu vượt intent
- [ ] cân nhắc warning/helper trước khi fail hard toàn bộ

### Verify
- [ ] config “hợp lệ về cú pháp nhưng tệ gameplay” bị lộ sớm hơn

---

## FILE 7 - `Assets/_Game/Resources/RunStart/StartMapConfig_RunStart_64x64_v0.1.json`

### Goal
Retune rules sau khi đã có visibility + quality gate.

### Lưu ý
- không sửa file này đầu tiên
- chỉ tune sau khi đã debug được generated/authored/legacy behavior

### Cần làm
- [ ] rà lại `starterRules`
- [ ] kiểm tra `Wood/Food` có đủ opening guarantee chưa
- [ ] kiểm tra `Stone` có bị đẩy quá xa không
- [ ] cân nhắc đẩy `Iron starter-lite` xa HQ hơn nếu đang chen opener
- [ ] rà `bonusRules` để không lấn vùng starter quá mạnh
- [ ] thêm notes rõ hơn nếu cần để future tuning không phá intent

### Verify
- [ ] nhiều seed cho cảm giác opening ổn định hơn
- [ ] vẫn giữ đa dạng layout đủ tốt

---

## FILE 8 - `Assets/_Game/Core/ResourcePatchState.cs`

### Goal
Cho patch-level metadata đủ dùng cho opening policy và debug.

### Hiện trạng
- state còn rất tối giản

### Cần làm
- [ ] thêm metadata như:
  - [ ] `OriginKind`
  - [ ] `GenerationBucket` (`starter/bonus/authored/legacy`)
  - [ ] nếu cần, source/rule label
- [ ] giữ state đủ nhẹ để không làm patch system nặng lên quá mức

### Verify
- [ ] patch selection có dữ liệu để ưu tiên starter đúng cách

---

## FILE 9 - `Assets/_Game/Core/ResourcePatchService.cs`

### Goal
Nâng patch service từ storage/query cơ bản lên support debug/evaluation/opening policy.

### Hiện trạng
- rebuild từ zones ổn
- score hiện thiên về dist + richness
- chưa biết patch là starter hay bonus

### Cần làm
- [ ] rebuild patch metadata từ runtime/zone metadata
- [ ] thêm helper phục vụ test/evaluation:
  - [ ] list patches theo resource
  - [ ] list patches còn tài nguyên theo bucket
  - [ ] nếu cần, helper best patch by path cost
- [ ] rà lại assumption `ComputeInitialAmount(...)`

### Verify
- [ ] test và helper không phải scan thủ công quá nhiều
- [ ] patch metadata usable cho harvest selection

---

## FILE 10 - `Assets/_Game/Jobs/HarvestTargetSelectionHelper.cs`

### Goal
Đảm bảo worker opening ưu tiên starter patches hợp lý.

### Hiện trạng
- đã path-aware
- vẫn khá trung lập giữa starter và bonus

### Cần làm
- [ ] thêm bias nhẹ cho starter patches
- [ ] không cho bonus patch thắng chỉ vì hơn chút richness nếu path gần ngang nhau
- [ ] nếu strict path estimate fail, fallback vẫn nên ưu tiên starter usable trước
- [ ] tận dụng metadata patch nếu đã thêm

### Verify
- [ ] farmer/lumber worker ưu tiên patch starter hợp lý
- [ ] bonus patch không hút worker quá sớm

---

## FILE 11 - `Assets/_Game/Jobs/Executors/HarvestExecutor.cs`

### Goal
Làm retarget/depletion trong opening mượt và deterministic hơn.

### Hiện trạng
- đã consume patch thật
- có retarget khi `carry <= 0`
- vẫn có nguy cơ flow hơi rung/lặp khi patch vừa cạn

### Cần làm
- [ ] tách rõ flow retarget khi patch cạn
- [ ] ưu tiên patch starter còn usable trước bonus nếu phù hợp policy
- [ ] tránh reset timer theo cách gây cảm giác awkward
- [ ] nếu cần, tách helper nhỏ để test retarget dễ hơn

### Verify
- [ ] worker không kẹt target cũ
- [ ] patch cạn thì chuyển patch khác sạch hơn
- [ ] không cancel spam vô ích khi opening patch đổi trạng thái

---

## FILE 12 - `Assets/_Game/Tests/EditMode/Jobs/HarvestOpeningStabilityTests.cs`

### Goal
Khóa integration tối thiểu cho opening harvest.

### Cần làm
- [ ] tạo test setup có HQ + starter producers + starter/bonus patches
- [ ] test worker pick đúng patch starter wood
- [ ] test worker pick đúng patch starter food
- [ ] test patch cạn thì retarget đúng
- [ ] test bonus patch không bị ưu tiên vô lý khi starter còn usable

### Verify
- [ ] opening harvest loop có regression riêng

---

## FILE 13 - `docs/opening-economy-smoke-matrix.md`

### Goal
Có checklist manual seed-smoke cố định để QA và balance dùng lâu dài.

### Cần làm
- [ ] chốt 12-20 seed đại diện
- [ ] mỗi seed check:
  - [ ] đủ `Wood/Food/Stone` starter
  - [ ] iron không chen opener quá mức
  - [ ] farmer/lumber pick target thật
  - [ ] overlay/inspect đúng
  - [ ] có hướng expand hợp lý
- [ ] phân loại:
  - [ ] blocker
  - [ ] playable but weak
  - [ ] good
- [ ] ghi rõ acceptable variance

### Verify
- [ ] dùng lại được ở nhiều batch sau

---

## FILE 14 - `CHANGELOG.md`

### Goal
Ghi lại đúng intent của batch sau khi hoàn tất.

### Cần làm
- [ ] mô tả đây là batch hardening/stability/debugability cho opening economy
- [ ] ghi rõ fallback visibility + quality gate + harvest stabilization + regression coverage

### Verify
- [ ] người đọc changelog hiểu ngay đây là pass productionizing opening economy

---

## 5. Quick acceptance checklist

### Must-have
- [ ] fallback chain trace được rõ
- [ ] runtime state cho biết requested/applied mode + failure reason
- [ ] có distinction giữa starter/bonus/authored/legacy ở mức debug usable
- [ ] multi-seed smoke không có case thiếu `Wood/Food/Stone` starter usable
- [ ] worker opening pick được starter patch hợp lý
- [ ] có regression cho generation + harvest opening

### Nice-to-have
- [ ] có quality score thay vì chỉ quality band
- [ ] có helper/path-cost level debug để giải thích tại sao patch này được chọn
- [ ] có semantic validator warnings cho config tuning

---

## 6. Khuyến nghị thực dụng

Nếu muốn giảm rủi ro và vẫn tiến nhanh, triển khai theo 3 đợt:

### Đợt 1 - nhìn thấy vấn đề
- [ ] `RunStartZoneInitializer.cs`
- [ ] `RunStartRuntimeTypes.cs`
- [ ] `RunStartRuntimeCacheBuilder.cs`
- [ ] `ResourceZoneGenerationTests.cs`

### Đợt 2 - sửa generation
- [ ] `RunStartResourceZoneGenerator.cs`
- [ ] `RunStartConfigValidator.cs`
- [ ] `StartMapConfig_RunStart_64x64_v0.1.json`

### Đợt 3 - khóa harvest opening
- [ ] `ResourcePatchState.cs`
- [ ] `ResourcePatchService.cs`
- [ ] `HarvestTargetSelectionHelper.cs`
- [ ] `HarvestExecutor.cs`
- [ ] `HarvestOpeningStabilityTests.cs`

---

## 7. Một câu chốt

**Sửa `RunStartZoneInitializer.cs` trước, không sửa JSON trước.**

Lý do: trước khi làm generation tốt hơn, hệ thống phải nói rõ nó đang dùng generated/authored/legacy gì, fail ở đâu, và vì sao fallback.
