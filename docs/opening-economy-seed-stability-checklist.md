# Opening Economy Seed Stability - Progress Checklist

> Mục tiêu: khóa độ ổn định của opening economy cho `Hybrid` resource generation theo hướng dễ debug, dễ test, và ít rủi ro khi refactor.
>
> File này là **checklist tiến độ sống** cho batch opening economy stability. Mỗi lần làm việc liên quan batch này, **bắt buộc phải cập nhật lại file ngay trong cùng lượt làm**.

---

## 0. Quy ước bắt buộc khi dùng file này

### Rule bắt buộc
- Mỗi lần làm bất kỳ việc gì thuộc batch này, **phải cập nhật file này trước khi kết thúc phiên làm việc / trước khi commit**.
- Không được coi task là xong nếu code đã đổi nhưng file này chưa phản ánh tiến độ mới nhất.
- Khi có thay đổi trạng thái, phải cập nhật đồng thời:
  - trạng thái checklist
  - mục `Progress log`
  - nếu có, mục `Blockers / follow-up`

### Trạng thái chuẩn
- `[ ]` chưa làm
- `[~]` đang làm / làm một phần / cần verify thêm
- `[x]` đã xong
- `[!]` có blocker hoặc risk cần theo dõi

### Cách cập nhật mỗi lần làm
Sau mỗi phiên làm, cập nhật tối thiểu:
1. tick/untick các mục liên quan
2. thêm 1 dòng vào `Progress log`
3. nếu phát hiện lệch giữa doc và code, sửa doc ngay theo trạng thái thực tế

---

## 1. Mục tiêu của batch này

Sau batch này, project nên đạt:

- [~] `Hybrid` mode tạo được opening usable qua nhiều seed
- [~] fallback chain rõ ràng: `Generated -> AuthoredFallback -> LegacyFallback`
- [~] runtime/debug state cho biết run đã dùng mode nào và fail ở đâu
- [~] phân biệt được zone/patch nào là `starter`, `bonus`, `authored`, `legacy`
- [ ] harvest opening ưu tiên starter patches hợp lý
- [ ] có regression + smoke checklist đủ để balance tiếp mà không làm vỡ opener

---

## 2. Nguyên tắc triển khai

- [x] Không retune JSON trước khi có debug visibility
- [x] Không nhét quality gate sâu vào rect placement logic nếu đó là concern cấp whole-opening
- [x] Tách rõ concern giữa rect validity, opening quality, fallback policy, patch selection policy
- [~] Ưu tiên thêm test khóa intent trước hoặc song song với refactor

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

### Trạng thái thực tế
**Status: [~] phần lớn đã làm, còn thiếu vài điểm phase 1**

### Đã có trong code
- [x] route theo `AuthoredOnly / Hybrid / GeneratedOnly`
- [x] helper tách tương đối rõ:
  - [x] `TryApplyGeneratedZones(...)`
  - [x] `TryApplyAuthoredFallback(...)`
  - [x] `ApplyLegacyFallbackZones(...)`
  - [x] `RecordAppliedMode(...)`
- [x] fallback chain đọc được khá rõ trong code
- [x] có record debug state vào `RunStartRuntime`
- [x] có phân biệt failure stage:
  - [x] generation fail technical
  - [x] generation trả empty
  - [x] generation missing config
  - [x] authored unavailable
- [x] `Hybrid` chỉ rơi sang authored khi generated không áp dụng được
- [x] authored unavailable mới rơi tiếp sang legacy

### Còn thiếu / cần verify
- [ ] chưa thấy distinction riêng cho `quality failure` ở level initializer
- [ ] GeneratedOnly hiện vẫn fallback sang authored/legacy, cần verify có đúng intent thiết kế cuối cùng không
- [~] pass/fail vẫn còn hơi coarse, nhất là khi quality gate phase B chưa vào đủ

### Verify phase 1
- [x] nhìn code thấy rõ exact fallback chain
- [x] runtime state cho biết fail ở bước nào
- [x] không còn fallback ngầm khó truy dấu

---

## FILE 2 - `Assets/_Game/Core/Contracts/Run/RunStartRuntimeTypes.cs`

### Goal
Mở rộng runtime metadata để debug opening issues theo seed.

### Trạng thái thực tế
**Status: [~] đã có core metadata, nhưng zone-level semantics chưa đủ sâu**

### Đã có trong code
- [x] `ResourceGenerationModeRequested`
- [x] `ResourceGenerationModeApplied`
- [x] `ResourceGenerationFailureReason`
- [x] `ResourceGenerationFailureStage`
- [x] `OpeningQualityBand`
- [x] `OpeningQualityScore`
- [x] `ZoneRect.Origin`
- [x] `ZoneRect.Bucket`

### Còn thiếu / cần verify
- [ ] chưa thấy debug struct riêng, hiện state còn flat
- [~] `Bucket` mới đang đủ cho `generated/authored/legacy`, chưa thấy khóa rõ `starter-generated` vs `bonus-generated`
- [ ] chưa có metadata đủ rõ để phân biệt hết:
  - [ ] `starter-generated`
  - [ ] `bonus-generated`
  - [x] `authored-fallback`
  - [x] `legacy-fallback`
- [~] backward compatibility có vẻ ổn, nhưng cần verify nơi nào đang đọc `ZoneRect`

### Verify phase 1
- [~] runtime state đủ để đọc nhanh seed xấu fail kiểu gì ở level mode/failure stage
- [ ] zone metadata chưa đủ giàu để đọc starter/bonus semantics sau apply world

---

## FILE 3 - `Assets/_Game/Core/RunStart/RunStartRuntimeCacheBuilder.cs`

### Goal
Đảm bảo runtime cache không chỉ mirror shape của zones, mà còn mirror debug meaning.

### Trạng thái thực tế
**Status: [~] đã có origin/bucket cơ bản, nhưng zone-level meaning vẫn còn bị làm phẳng**

### Đã có trong code
- [x] `ApplyRuntimeZonesFromWorld(...)` rebuild bounds tốt
- [x] sync `Origin` theo applied mode
- [x] sync `Bucket` theo applied mode chung
- [x] authored config zones khi load metadata ban đầu có `origin: ConfigAuthored`, `bucket: authored`

### Còn thiếu / cần verify
- [ ] origin hiện vẫn suy từ applied mode chung, chưa phản ánh khác biệt zone-level thực tế
- [ ] chưa giữ được distinction `starter/bonus` sau cache rebuild
- [ ] chưa thấy side-channel metadata map trong `RunStartRuntime`
- [~] overlay/inspect compatibility có vẻ vẫn giữ được, nhưng semantic debug chưa đủ sâu

### Verify phase 1
- [~] debug helper/test biết zone đến từ generated/authored/legacy
- [ ] starter/bonus distinction chưa tồn tại bền vững sau cache rebuild

---

## FILE 4 - `Assets/_Game/Tests/EditMode/RunStart/ResourceZoneGenerationTests.cs`

### Goal
Khóa behavior fallback/debug trước khi refactor generator mạnh tay.

### Trạng thái thực tế
**Status: [~] đã có coverage tốt cho fallback cơ bản, còn thiếu quality-failure semantics và metadata sâu hơn**

### Đã có trong test
- [x] deterministic same-seed
- [x] different-seed variance
- [x] generated apply success + runtime cache update
- [x] authored-only preserves authored zones
- [x] generated fail -> authored fallback
- [x] hybrid không có generated/authored -> legacy fallback
- [x] generated without rules -> `GeneratedEmpty`
- [x] starter guarantee cơ bản `Wood/Food/Stone`
- [x] zone bounds stay within map

### Còn thiếu
- [ ] test cho `generated fail quality -> authored fallback`
- [ ] test metadata mới sâu hơn trong runtime, đặc biệt starter/bonus distinction
- [ ] multi-seed regression nhỏ riêng cho phase A/E acceptance
- [ ] quality band / quality score assertions theo policy thật của phase B
- [ ] starter accessibility assertions mạnh hơn

### Verify phase 1
- [~] refactor phase A hiện đã có lưới an toàn cơ bản
- [ ] chưa khóa hết behavior mong muốn của quality/fallback semantics cuối cùng

---

## FILE 5 - `Assets/_Game/Core/RunStart/RunStartResourceZoneGenerator.cs`

### Goal
Nâng generator từ `spawn hợp lệ` lên `opening usable`.

### Status
- [ ] chưa rà ở pass này

---

## FILE 6 - `Assets/_Game/Core/RunStart/RunStartConfigValidator.cs`

### Goal
Xiết validator theo semantic gameplay, không chỉ schema/range.

### Status
- [ ] chưa rà ở pass này

---

## FILE 7 - `Assets/_Game/Resources/RunStart/StartMapConfig_RunStart_64x64_v0.1.json`

### Goal
Retune rules sau khi đã có visibility + quality gate.

### Status
- [ ] chưa rà ở pass này
- [x] vẫn đúng nguyên tắc: chưa nên sửa file này trước

---

## FILE 8 - `Assets/_Game/Core/ResourcePatchState.cs`

### Status
- [ ] chưa làm

## FILE 9 - `Assets/_Game/Core/ResourcePatchService.cs`

### Status
- [ ] chưa làm

## FILE 10 - `Assets/_Game/Jobs/HarvestTargetSelectionHelper.cs`

### Status
- [ ] chưa làm

## FILE 11 - `Assets/_Game/Jobs/Executors/HarvestExecutor.cs`

### Status
- [ ] chưa làm

## FILE 12 - `Assets/_Game/Tests/EditMode/Jobs/HarvestOpeningStabilityTests.cs`

### Status
- [ ] chưa làm

## FILE 13 - `docs/opening-economy-smoke-matrix.md`

### Status
- [ ] chưa làm

## FILE 14 - `CHANGELOG.md`

### Status
- [ ] chưa làm

---

## 5. Quick acceptance checklist

### Must-have
- [x] fallback chain trace được rõ ở level mode tổng
- [x] runtime state cho biết requested/applied mode + failure reason
- [ ] có distinction giữa starter/bonus/authored/legacy ở mức debug usable đầy đủ
- [ ] multi-seed smoke không có case thiếu `Wood/Food/Stone` starter usable
- [ ] worker opening pick được starter patch hợp lý
- [ ] có regression cho generation + harvest opening

### Nice-to-have
- [x] có quality score thay vì chỉ quality band
- [ ] có helper/path-cost level debug để giải thích tại sao patch này được chọn
- [ ] có semantic validator warnings cho config tuning

---

## 6. Khuyến nghị thực dụng

Nếu muốn giảm rủi ro và vẫn tiến nhanh, triển khai theo 3 đợt:

### Đợt 1 - nhìn thấy vấn đề
- [~] `RunStartZoneInitializer.cs`
- [~] `RunStartRuntimeTypes.cs`
- [~] `RunStartRuntimeCacheBuilder.cs`
- [~] `ResourceZoneGenerationTests.cs`

**Kết luận hiện tại cho Phase 1:**
- [~] **Phase 1 gần xong nhưng chưa pass trọn vẹn**
- [x] fallback visibility cơ bản đã có
- [x] runtime mode/failure tracking cơ bản đã có
- [x] test fallback chính đã có
- [ ] còn thiếu starter/bonus distinction bền vững ở runtime/cache
- [ ] còn thiếu quality-failure semantics rõ ở test và flow
- [ ] còn thiếu xác nhận intent cuối cho `GeneratedOnly` có được fallback hay không

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

## 7. Blockers / follow-up cần chốt

- [!] Chốt intent cuối cho `GeneratedOnly`:
  - có được fallback sang authored/legacy không,
  - hay phải fail cứng nếu generated không đạt.
- [!] Chốt model metadata cho generated zones:
  - có cần tách rõ `starter-generated` và `bonus-generated` ở runtime/cache hay không.
- [!] Chốt thời điểm đưa `quality failure` vào fallback semantics:
  - ngay phase 1.5 / phase 2,
  - hay để sau khi generator quality gate hoàn tất.

---

## 8. Progress log

- 2026-05-06 09:xx GMT+7, đối chiếu code thực tế phase 1 với checklist. Kết luận: phase 1 đã làm được phần lớn fallback visibility và runtime tracking cơ bản, nhưng chưa xong trọn vẹn vì còn thiếu starter/bonus metadata bền vững, quality-failure semantics rõ hơn, và một số test/intent cần chốt.
- 2026-05-06 09:xx GMT+7, chuyển file này từ note/checklist định hướng sang progress checklist sống, thêm rule bắt buộc phải cập nhật file sau mỗi phiên làm / trước khi commit.

---

## 9. Một câu chốt

**Không được sửa code batch này xong rồi quên cập nhật file này.**

Nếu code và file này lệch nhau, coi như tiến độ chưa được ghi nhận đúng.
