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
- [x] fallback chain rõ ràng: `Generated -> AuthoredFallback -> LegacyFallback`
- [x] runtime/debug state cho biết run đã dùng mode nào và fail ở đâu
- [x] phân biệt được zone/patch nào là `starter`, `bonus`, `authored`, `legacy`
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
**Status: [x] phase 1 đã đóng ở level code/doc, còn verify test runtime trên máy có Unity/.NET phù hợp**

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
- [x] GeneratedOnly hiện đang fallback sang authored/legacy theo behavior code hiện tại, đã phản ánh rõ trong flow hiện có
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
**Status: [x] phase 1 metadata đã đủ để debug fallback + starter/bonus distinction cơ bản**

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
- [x] `Bucket` đã đủ để phân biệt `starter-generated` vs `bonus-generated`
- [x] đã có metadata phân biệt:
  - [x] `starter-generated`
  - [x] `bonus-generated`
  - [x] `authored-fallback`
  - [x] `legacy-fallback`
- [~] backward compatibility có vẻ ổn, nhưng cần verify nơi nào đang đọc `ZoneRect`

### Verify phase 1
- [x] runtime state đủ để đọc nhanh seed xấu fail kiểu gì ở level mode/failure stage
- [x] zone metadata đủ để đọc starter/bonus/authored/legacy semantics cơ bản sau apply world

---

## FILE 3 - `Assets/_Game/Core/RunStart/RunStartRuntimeCacheBuilder.cs`

### Goal
Đảm bảo runtime cache không chỉ mirror shape của zones, mà còn mirror debug meaning.

### Trạng thái thực tế
**Status: [x] phase 1 cache/runtime meaning đã giữ được distinction cần thiết**

### Đã có trong code
- [x] `ApplyRuntimeZonesFromWorld(...)` rebuild bounds tốt
- [x] sync `Origin` theo applied mode
- [x] sync `Bucket` theo applied mode chung
- [x] authored config zones khi load metadata ban đầu có `origin: ConfigAuthored`, `bucket: authored`

### Còn thiếu / cần verify
- [x] origin/bucket giờ ưu tiên metadata gắn trực tiếp trên `ZoneState`
- [x] đã giữ được distinction `starter/bonus` sau cache rebuild
- [ ] chưa thấy side-channel metadata map trong `RunStartRuntime`
- [~] overlay/inspect compatibility có vẻ vẫn giữ được, nhưng cần verify thêm ở runtime thật

### Verify phase 1
- [x] debug helper/test biết zone đến từ generated/authored/legacy
- [x] starter/bonus distinction tồn tại bền vững sau cache rebuild

---

## FILE 4 - `Assets/_Game/Tests/EditMode/RunStart/ResourceZoneGenerationTests.cs`

### Goal
Khóa behavior fallback/debug trước khi refactor generator mạnh tay.

### Trạng thái thực tế
**Status: [x] phase 1 test coverage đã đủ để khóa fallback/debug semantics cơ bản**

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
- [x] test metadata runtime cho starter/bonus distinction cơ bản
- [ ] multi-seed regression nhỏ riêng cho phase A/E acceptance
- [ ] quality band / quality score assertions theo policy thật của phase B
- [ ] starter accessibility assertions mạnh hơn

### Verify phase 1
- [x] refactor phase A hiện đã có lưới an toàn cơ bản
- [~] chưa khóa hết behavior mong muốn của quality/fallback semantics cuối cùng

---

## FILE 5 - `Assets/_Game/Core/RunStart/RunStartResourceZoneGenerator.cs`

### Goal
Nâng generator từ `spawn hợp lệ` lên `opening usable`.

### Trạng thái thực tế
**Status: [~] đã có quality gate cơ bản cho starter coverage, còn thiếu scoring/retry sâu hơn**

### Đã có trong code
- [x] giữ `TryPickZoneRect(...)` là rect-level concern
- [x] thêm pass evaluate toàn opening sau khi generate xong
- [x] có `HasStarterCoverage(...)`
- [x] có quality band và quality score trả ra từ generator
- [x] generated fail quality giờ có semantics riêng (`GeneratedWeak` / `GeneratedQualityGate`)
- [x] quality gate khóa tối thiểu:
  - [x] có `Wood` starter usable
  - [x] có `Food` starter usable
  - [x] có `Stone` starter usable

### Còn thiếu / cần verify
- [ ] chưa có deterministic bounded retry khi quality chưa đạt
- [ ] chưa có score/accessibility/distribution helper sâu hơn
- [ ] chưa khóa rule về path cost / phân bố quanh HQ / iron starter-lite

### Verify phase B
- [x] seed xấu có thể bị reject với lý do rõ
- [~] cùng seed vẫn deterministic theo logic hiện tại, nhưng chưa verify runtime test trên máy này

---

## FILE 6 - `Assets/_Game/Core/RunStart/RunStartConfigValidator.cs`

### Goal
Xiết validator theo semantic gameplay, không chỉ schema/range.

### Trạng thái thực tế
**Status: [~] đã thêm semantic validation cơ bản cho starter coverage intent**

### Đã có trong code
- [x] semantic validation helper cho starter coverage intent
- [x] flag config thiếu opening coverage intent cho `Wood/Food/Stone`
- [x] enforce khoảng cách intent cơ bản:
  - [x] `Wood <= 14`
  - [x] `Food <= 14`
  - [x] `Stone <= 16`

### Còn thiếu / cần verify
- [ ] chưa tách warning/helper mềm trước khi fail hard
- [ ] chưa validate iron starter / rect quá nhỏ / count quá thấp theo gameplay semantics sâu hơn

### Verify phase B
- [x] config “hợp lệ về cú pháp nhưng tệ gameplay” đã bị lộ sớm hơn ở mức cơ bản

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
- [x] có distinction giữa starter/bonus/authored/legacy ở mức debug usable cơ bản cho phase 1
- [~] multi-seed smoke không có case thiếu `Wood/Food/Stone` starter usable
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
- [x] `RunStartZoneInitializer.cs`
- [x] `RunStartRuntimeTypes.cs`
- [x] `RunStartRuntimeCacheBuilder.cs`
- [x] `ResourceZoneGenerationTests.cs`

**Kết luận hiện tại cho Phase 1:**
- [x] **Phase 1 đã đóng ở level implementation/doc**
- [x] fallback visibility cơ bản đã có
- [x] runtime mode/failure tracking cơ bản đã có
- [x] test fallback chính đã có
- [x] starter/bonus distinction bền vững ở runtime/cache đã có ở mức phase 1
- [~] quality-failure semantics rõ ở test và flow để dành cho phase B
- [x] behavior hiện tại của `GeneratedOnly` đã được phản ánh rõ trong code/doc

### Đợt 2 - sửa generation
- [~] `RunStartResourceZoneGenerator.cs`
- [~] `RunStartConfigValidator.cs`
- [ ] `StartMapConfig_RunStart_64x64_v0.1.json`

### Đợt 3 - khóa harvest opening
- [ ] `ResourcePatchState.cs`
- [ ] `ResourcePatchService.cs`
- [ ] `HarvestTargetSelectionHelper.cs`
- [ ] `HarvestExecutor.cs`
- [ ] `HarvestOpeningStabilityTests.cs`

---

## 7. Blockers / follow-up cần chốt

- [x] Behavior hiện tại của `GeneratedOnly` đã được ghi nhận: đang fallback sang authored/legacy nếu generated không áp dụng được.
- [x] Metadata generated zones đã tách rõ `starter-generated` và `bonus-generated` ở runtime/cache.
- [x] `quality failure` đã được đưa vào fallback semantics ở phase B với `GeneratedQualityGate`.
- [!] Chưa có bounded retry/scoring sâu hơn cho quality evaluation, đây là phần follow-up tiếp theo của phase B.

---

## 8. Progress log

- 2026-05-06 09:xx GMT+7, đối chiếu code thực tế phase 1 với checklist. Kết luận: phase 1 đã làm được phần lớn fallback visibility và runtime tracking cơ bản, nhưng chưa xong trọn vẹn vì còn thiếu starter/bonus metadata bền vững, quality-failure semantics rõ hơn, và một số test/intent cần chốt.
- 2026-05-06 09:xx GMT+7, chuyển file này từ note/checklist định hướng sang progress checklist sống, thêm rule bắt buộc phải cập nhật file sau mỗi phiên làm / trước khi commit.
- 2026-05-06 09:xx GMT+7, đóng nốt phase 1 ở level code/doc: thêm zone metadata `Origin/Bucket` vào `ZoneState`, preserve `starter-generated` vs `bonus-generated` qua runtime cache, thêm `ZoneRect.IsStarter`, cập nhật test để khóa starter/bonus/authored/legacy distinction. Chưa verify được test command trên máy này vì thiếu .NET SDK/runner phù hợp.
- 2026-05-06 09:xx GMT+7, bắt đầu phase B: thêm quality gate semantics cho generated opening, cho generator trả `qualityBand/qualityScore`, reject seed thiếu starter coverage với reason rõ, map sang `GeneratedQualityGate` ở initializer, thêm semantic validation cơ bản trong `RunStartConfigValidator`, và cập nhật test/checklist tương ứng.

---

## 9. Một câu chốt

**Không được sửa code batch này xong rồi quên cập nhật file này.**

Nếu code và file này lệch nhau, coi như tiến độ chưa được ghi nhận đúng.
