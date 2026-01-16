# PART 25 (CONTRACTS) — FILE MAP CHI TIẾT v0.1
Nguồn: `PART25_Technical_InterfacesPack_Services_Events_DTOs_LOCKED_SPEC_v0.1.md`

Mục tiêu: tách Part 25 thành **các file C# cụ thể** (đúng folder) để bạn (và Copilot) không bị mông lung và không lệch signature.

> Quy tắc vàng (Contracts):
- **Không** `MonoBehaviour`, **không** `UnityEngine`, **không** logic chạy game.
- Chỉ chứa: **interfaces + enums + structs (DTO/events) + id types**.
- Namespace khuyến nghị: `SeasonalBastion.Contracts.<Module>` (bạn có thể đổi, nhưng phải nhất quán).

---

## 0) Root folder
Tất cả đặt dưới:
`Assets/Game/Core/Contracts/`

Khuyến nghị tạo file asmdef:
- `Assets/Game/Core/Contracts/Game.Contracts.asmdef`

---

## 1) Common (Id / Cell / Enums nền)
### 1.1 `Assets/Game/Core/Contracts/Common/IdTypes.cs`
Chứa (readonly struct):
- `BuildingId`
- `NpcId`
- `TowerId`
- `EnemyId`
- `SiteId`
- `JobId`
- `NotificationId`

### 1.2 `Assets/Game/Core/Contracts/Common/CellTypes.cs`
Chứa:
- `CellPos` (struct)
- `Dir4` (enum)

### 1.3 `Assets/Game/Core/Contracts/Common/RunEnums.cs`
Chứa:
- `Season` (enum)
- `Phase` (enum)
- `RunOutcome` (enum)

### 1.4 `Assets/Game/Core/Contracts/Common/ResourceTypes.cs`
Chứa:
- `ResourceType` (enum)  *(wood/food/... theo spec)*
> Lưu ý: Ammo trong spec là pipeline riêng; nếu bạn có `ResourceType.Ammo` thì giữ đúng spec bạn đã lock.

---

## 2) Service access pattern (container field list)
### 2.1 `Assets/Game/Core/Contracts/Core/GameServices.cs`
Chứa:
- `public sealed class GameServices`
  - Fields tham chiếu interface services: `IDataRegistry`, `IRunClock`, `INotificationService`, `IGridMap`, `IWorldState`, `IWorldOps`, `IWorldIndex`, `IPlacementService`, `IStorageService`, `IResourceFlowService`, `IClaimService`, `IJobBoard`, `IJobScheduler`, `IBuildOrderService`, `IAmmoService`, `ICombatService`, `IRewardService`, `IRunOutcomeService`, `ISaveService`, optional `IAudioService`, `IFXService`…
> Đây là “hợp đồng wiring”: runtime factory sẽ new concrete và gán vào đây.

---

## 3) Data Registry + Validation
### 3.1 `Assets/Game/Core/Contracts/Data/IDataRegistry.cs`
Chứa:
- `public interface IDataRegistry`

### 3.2 `Assets/Game/Core/Contracts/Data/IDataValidator.cs`
Chứa:
- `public interface IDataValidator`

> (Nếu Part 25 có DTO def tối thiểu) bạn có thể thêm:
- `Assets/Game/Core/Contracts/Data/DefDTOs.cs`

---

## 4) Run Clock + Speed Controls
### 4.1 `Assets/Game/Core/Contracts/Run/IRunClock.cs`
Chứa:
- `public interface IRunClock`
- (nếu spec có) các DTO nhỏ liên quan speed.

---

## 5) Notifications (UI stack + spam control)
### 5.1 `Assets/Game/Core/Contracts/Notifications/NotificationTypes.cs`
Chứa:
- `NotificationSeverity` (enum)
- `NotificationPayload` (struct)
- *(nếu spec có)* các key/dedupe fields

### 5.2 `Assets/Game/Core/Contracts/Notifications/INotificationService.cs`
Chứa:
- `public interface INotificationService`

### 5.3 `Assets/Game/Core/Contracts/Notifications/NotificationViewModel.cs`
Chứa:
- `NotificationViewModel` (DTO) *(nếu spec yêu cầu tách riêng)*

---

## 6) Grid Map + Occupancy + Road
### 6.1 `Assets/Game/Core/Contracts/Grid/GridTypes.cs`
Chứa:
- `CellOccupancyKind` (enum)
- `CellOccupancy` (struct)

### 6.2 `Assets/Game/Core/Contracts/Grid/IGridMap.cs`
Chứa:
- `public interface IGridMap`

---

## 7) World State + Stores + Ops
### 7.1 `Assets/Game/Core/Contracts/World/IWorldState.cs`
Chứa:
- `public interface IWorldState`

### 7.2 `Assets/Game/Core/Contracts/World/IWorldOps.cs`
Chứa:
- `public interface IWorldOps`

### 7.3 Stores (minimal contract)
Đặt trong `Assets/Game/Core/Contracts/World/Stores/`

- `IEntityStore.cs` → `public interface IEntityStore`
- `IBuildingStore.cs` → `public interface IBuildingStore`
- `INpcStore.cs` → `public interface INpcStore`
- `ITowerStore.cs` → `public interface ITowerStore`
- `IEnemyStore.cs` → `public interface IEnemyStore`
- `IBuildSiteStore.cs` → `public interface IBuildSiteStore`

> Lưu ý: Part 25 có `BuildSiteState` (struct DTO). Bạn có thể đặt DTO state ở file riêng (mục 7.4).

### 7.4 World runtime state DTOs (struct)
Đặt trong `Assets/Game/Core/Contracts/World/States/`

- `BuildSiteState.cs` → `BuildSiteState` (struct)
- (Nếu spec về sau bổ sung) `BuildingState`, `NpcState`, `TowerState`, `EnemyState`…  
> Hiện Part 25 chỉ nêu rõ `BuildSiteState` trong contracts; các state khác có thể nằm Part 26/runtime.

---

## 8) World Index (derived lists)
### 8.1 `Assets/Game/Core/Contracts/World/IWorldIndex.cs`
Chứa:
- `public interface IWorldIndex`

---

## 9) Placement (Road Entry + Driveway)
### 9.1 `Assets/Game/Core/Contracts/Placement/PlacementTypes.cs`
Chứa:
- `PlacementFailReason` (enum)
- `PlacementResult` (struct)

### 9.2 `Assets/Game/Core/Contracts/Placement/IPlacementService.cs`
Chứa:
- `public interface IPlacementService`

---

## 10) Storage + Resource Flow
### 10.1 `Assets/Game/Core/Contracts/Economy/StorageDTOs.cs`
Chứa:
- `StorageSnapshot` (struct)
- `StoragePick` (struct)

### 10.2 `Assets/Game/Core/Contracts/Economy/IStorageService.cs`
Chứa:
- `public interface IStorageService`

### 10.3 `Assets/Game/Core/Contracts/Economy/IResourceFlowService.cs`
Chứa:
- `public interface IResourceFlowService`

---

## 11) Claims (Exclusivity)
### 11.1 `Assets/Game/Core/Contracts/Claims/ClaimTypes.cs`
Chứa:
- `ClaimKind` (enum)
- `ClaimKey` (struct)

### 11.2 `Assets/Game/Core/Contracts/Claims/IClaimService.cs`
Chứa:
- `public interface IClaimService`

---

## 12) Jobs (Board + Scheduler + Execution)
### 12.1 `Assets/Game/Core/Contracts/Jobs/JobTypes.cs`
Chứa:
- `JobArchetype` (enum)
- `JobStatus` (enum)
- `Job` (struct DTO)

### 12.2 `Assets/Game/Core/Contracts/Jobs/IJobBoard.cs`
Chứa:
- `public interface IJobBoard`

### 12.3 `Assets/Game/Core/Contracts/Jobs/IJobScheduler.cs`
Chứa:
- `public interface IJobScheduler`

### 12.4 `Assets/Game/Core/Contracts/Jobs/IJobExecutor.cs`
Chứa:
- `public interface IJobExecutor`

---

## 13) Build Pipeline (Orders + Sites + Delivery + Commit)
### 13.1 `Assets/Game/Core/Contracts/Build/BuildTypes.cs`
Chứa:
- `BuildOrderKind` (enum)
- `BuildOrder` (struct DTO)
- `CostProgress` (struct DTO)

### 13.2 `Assets/Game/Core/Contracts/Build/IBuildOrderService.cs`
Chứa:
- `public interface IBuildOrderService`

---

## 14) Ammo Pipeline (Forge + Armory + Tower Requests)
### 14.1 `Assets/Game/Core/Contracts/Ammo/AmmoTypes.cs`
Chứa:
- `AmmoRequestPriority` (enum)
- `AmmoRequest` (struct DTO)

### 14.2 `Assets/Game/Core/Contracts/Ammo/IAmmoService.cs`
Chứa:
- `public interface IAmmoService`

---

## 15) Combat (Waves + Enemies + Towers)
### 15.1 `Assets/Game/Core/Contracts/Combat/ICombatService.cs`
Chứa:
- `public interface ICombatService`

> (Nếu spec có thêm DTO cho wave/combat) tạo:
- `Assets/Game/Core/Contracts/Combat/CombatDTOs.cs`

---

## 16) Rewards + Run Outcome
### 16.1 `Assets/Game/Core/Contracts/Rewards/RewardTypes.cs`
Chứa:
- `RewardOffer` (struct DTO)

### 16.2 `Assets/Game/Core/Contracts/Rewards/IRewardService.cs`
Chứa:
- `public interface IRewardService`

### 16.3 `Assets/Game/Core/Contracts/Rewards/IRunOutcomeService.cs`
Chứa:
- `public interface IRunOutcomeService`

---

## 17) Save / Load
### 17.1 `Assets/Game/Core/Contracts/Save/SaveTypes.cs`
Chứa:
- `SaveResultCode` (enum)
- `SaveResult` (struct)

### 17.2 `Assets/Game/Core/Contracts/Save/ISaveService.cs`
Chứa:
- `public interface ISaveService`

### 17.3 `Assets/Game/Core/Contracts/Save/SaveDTOs.cs`
Chứa:
- DTO save tối thiểu theo spec (run/meta)

---

## 18) Audio / FX (Optional interfaces)
### 18.1 `Assets/Game/Core/Contracts/Audio/IAudioService.cs`
Chứa:
- `public interface IAudioService`

### 18.2 `Assets/Game/Core/Contracts/FX/IFXService.cs`
Chứa:
- `public interface IFXService`

---

## 19) Event Bus + Common Events (Optional but recommended)
### 19.1 `Assets/Game/Core/Contracts/Events/IEventBus.cs`
Chứa:
- `public interface IEventBus`

### 19.2 `Assets/Game/Core/Contracts/Events/CommonEvents.cs`
Chứa (struct events):
- `BuildingPlacedEvent`
- `RoadPlacedEvent`
- `NPCAssignedEvent`
- `ResourceDeliveredEvent`
- `WaveStartedEvent`
- `WaveEndedEvent`
- `RunEndedEvent`
- `RewardPickedEvent`

---

## 20) Thứ tự tạo file (để compile dễ)
1) `Common/*` (Ids, CellPos/Dir4, enums)
2) `Events/IEventBus` + `Events/CommonEvents`
3) `Data/*` + `Run/IRunClock`
4) `Notifications/*`
5) `Grid/*` + `Placement/*`
6) `World/*` + `World/Stores/*`
7) `Economy/*`
8) `Claims/*`
9) `Jobs/*`
10) `Build/*`
11) `Ammo/*`
12) `Combat/*`
13) `Rewards/*`
14) `Save/*`
15) Optional: Audio/FX
16) `Core/GameServices.cs` (container)

---

## 21) Ghi chú quan trọng để không “lệch Part 25”
- Tên type/interface **phải đúng**: Part 25 có 30 interfaces và các enums/structs chính:
  - Interfaces: `IDataRegistry`, `IDataValidator`, `IRunClock`, `INotificationService`, `IGridMap`, `IWorldState`, `IWorldOps`, `IWorldIndex`, store interfaces, `IPlacementService`, `IStorageService`, `IResourceFlowService`, `IClaimService`, `IJobBoard`, `IJobScheduler`, `IJobExecutor`, `IBuildOrderService`, `IAmmoService`, `ICombatService`, `IRewardService`, `IRunOutcomeService`, `ISaveService`, `IAudioService`, `IFXService`, `IEventBus`.
  - Enums: `Season`, `Phase`, `Dir4`, `ResourceType`, `NotificationSeverity`, `CellOccupancyKind`, `PlacementFailReason`, `ClaimKind`, `JobArchetype`, `JobStatus`, `BuildOrderKind`, `AmmoRequestPriority`, `RunOutcome`, `SaveResultCode`.
  - Structs: `CellPos`, `CellOccupancy`, `PlacementResult`, `StorageSnapshot`, `StoragePick`, `ClaimKey`, `Job`, `BuildOrder`, `CostProgress`, `AmmoRequest`, `SaveResult`, events list.
