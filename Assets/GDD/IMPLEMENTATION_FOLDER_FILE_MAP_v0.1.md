# IMPLEMENTATION FOLDER + FILE MAP — v0.1 (from PART 25–27)

Tài liệu này là “bản đồ triển khai” (thư mục + file đặt ở đâu + làm gì) để bạn bắt đầu code đúng kiến trúc **Part 25/26/27**.

---

## 0) Nguyên tắc tổng quát

- **Part 25 = Contracts**: toàn bộ *interface / DTO / event structs / id types / enums* đặt vào `Assets/Game/Core/Contracts/`.  
- **Part 26 = Concrete skeletons**: khung class (fields + method signatures + TODO) đặt đúng folder tree.  
- **Part 27 = Sprint plan**: dùng làm checklist triển khai theo ngày (Vertical Slice #1).

---

## 1) Folder tree chuẩn (copy vào Assets)

> Copy nguyên cây này để không bị lệch. (UI/Debug chi tiết sẽ làm sau)

```
Assets/Game/
  Core/
    Contracts/                 // PART25 interfaces & DTOs
    Boot/
      GameBootstrap.cs
      GameServices.cs
      GameServicesFactory.cs
    Loop/
      GameLoop.cs
      TickOrder.cs
    Events/
      EventBus.cs
      GameEvents.cs
    Utils/
      TimeUtil.cs
      DeterminismUtil.cs
      Log.cs
  Defs/
    Buildings/
    Enemies/
    Waves/
    Recipes/
    Rewards/
    Audio/
    FX/
  World/
    State/
      WorldState.cs
      Stores/
        EntityStore.cs
        BuildingStore.cs
        NpcStore.cs
        TowerStore.cs
        EnemyStore.cs
        BuildSiteStore.cs
      States/
        BuildingState.cs
        NpcState.cs
        TowerState.cs
        EnemyState.cs
        BuildSiteState.cs
        RunModifiers.cs
    Ops/
      WorldOps.cs
    Index/
      WorldIndexService.cs
  Grid/
    GridMap.cs
    PlacementService.cs
  Economy/
    StorageService.cs
    ResourceFlowService.cs
  Jobs/
    ClaimService.cs
    JobBoard.cs
    JobScheduler.cs
    Executors/
      JobExecutorRegistry.cs
      HarvestExecutor.cs
      HaulBasicExecutor.cs
      BuildDeliverExecutor.cs
      BuildWorkExecutor.cs
      CraftAmmoExecutor.cs
      ResupplyTowerExecutor.cs
  Build/
    BuildOrderService.cs
  Combat/
    CombatService.cs
    WaveDirector.cs
    TowerCombatSystem.cs
    EnemySystem.cs
  Rewards/
    RewardService.cs
    RunOutcomeService.cs
  Save/
    SaveService.cs
    SaveMigrator.cs
  UI/
    (Part 13 screens/binders)
  Debug/
    (Part 15 dev tools)
```

---

## 2) Bạn phải tạo thêm vài file “khung bắt buộc” (để compile & chạy scene)

### 2.1 `Assets/Game/Core/Boot/GameServices.cs`
- Đây là **service container** đúng theo Part 25.  
- Các field tối thiểu (để các phần khác inject dùng chung) nằm trong Part 25.

### 2.2 `Assets/Game/Core/Boot/GameServicesFactory.cs`
- Chịu trách nhiệm “compose graph” (new các service concrete theo đúng thứ tự).  
- Trong Part 26 có ví dụ Create() để bạn bám.

### 2.3 Scene bootstrap
Tạo:
- `Assets/Scenes/Bootstrap.unity` (hoặc `Assets/Scenes/Game.unity`)
- Empty GameObject tên `Bootstrap`
- Add component `GameBootstrap`

---

## 3) Bảng “File → Vai trò → Cần làm gì trước”

### 3.1 Core/Contracts (PART 25) — COPY 1 LẦN
**Đặt tất cả**:
- Id types: `BuildingId`, `NpcId`, `TowerId`, `EnemyId`, `SiteId`, `JobId`
- CellPos, Dir4
- enums: `ResourceType`, `Phase`, `Season`, `JobArchetype`, v.v.
- Interfaces: `IDataRegistry`, `IRunClock`, `IGridMap`, `IWorldState`, `IPlacementService`, `IStorageService`, `IJobBoard`, `IJobScheduler`, ...
- DTOs/events: `RunSaveDTO`, `BuildingPlacedEvent`, ...

> Mục tiêu: code giữa các module không bị lệch signatures.

### 3.2 Core/Boot
- `GameBootstrap.cs`: MonoBehaviour entry; tạo `GameServices` + `GameLoop`, gọi Tick mỗi frame.
- `GameServicesFactory.cs`: “wiring” new service.
- `GameServices.cs`: container fields.
**Làm trước trong Sprint 1 Day 1**.

### 3.3 Core/Loop
- `GameLoop.cs`: StartNewRun(seed), Tick(dt), Dispose
- `TickOrder.cs`: thứ tự tick cố định (determinism)

### 3.4 Core/Events + Notifications
- `EventBus.cs`: publish/subscribe typed struct events
- `GameEvents.cs`: định nghĩa event structs (hoặc giữ trong Contracts nếu bạn muốn “all in Contracts”)
- `NotificationService.cs`: stack max 3, newest first, cooldown (throttle)

### 3.5 World/State
- `WorldState.cs`: giữ các store + run modifiers
- `Stores/*Store.cs`: CRUD entity states, iterate Ids
- `States/*.cs`: struct state dữ liệu runtime
- `WorldOps.cs`: tạo/destroy entity + bắn event
- `WorldIndexService.cs`: derived lists (warehouses/producers/...)

### 3.6 Grid
- `GridMap.cs`: occupancy array + road flags
- `PlacementService.cs`: rule đặt road + đặt building + driveway len=1

### 3.7 Economy
- `StorageService.cs`: cap/amount per building; rule “Warehouse/HQ không chứa Ammo”
- `ResourceFlowService.cs`: pick source/dest deterministic

### 3.8 Jobs
- `ClaimService.cs`: chống tranh chấp; ReleaseAll để tránh leak
- `JobBoard.cs`: queue theo workplace
- `JobScheduler.cs`: assign job cho NPC idle + tick executor
- `Executors/*`: Harvest, HaulBasic (VS#1), sau đó BuildDeliver/BuildWork, CraftAmmo, ResupplyTower

### 3.9 Build / Combat / Rewards / Save
- Có skeleton để không lệch kiến trúc, nhưng **VS#1 chưa cần hoàn thiện**.

---

## 4) Nên tạo asmdef chưa?

**Có.** Tạo sớm giúp compile nhanh + tách phụ thuộc rõ ràng.

Gợi ý tối thiểu:
- `Assets/Game/Core/Game.Core.asmdef`
- `Assets/Game/World/Game.World.asmdef`
- `Assets/Game/Grid/Game.Grid.asmdef`
- `Assets/Game/Economy/Game.Economy.asmdef`
- `Assets/Game/Jobs/Game.Jobs.asmdef`
- `Assets/Game/Build/Game.Build.asmdef`
- `Assets/Game/Combat/Game.Combat.asmdef`
- `Assets/Game/Rewards/Game.Rewards.asmdef`
- `Assets/Game/Save/Game.Save.asmdef`

Dependency gợi ý (mức tối thiểu):
- World/Grid/Economy/Jobs/Build/Combat/Rewards/Save → reference `Game.Core`
- Economy/Jobs/Build/Combat/Rewards thường sẽ reference `Game.World`
- Placement (Grid) sẽ reference World + Core
- Tránh circular: chỉ “trỏ lên” Core, không trỏ ngược.

---

## 5) Checklist triển khai đúng theo Part 27 (ngắn gọn)

### Sprint 1 Day 1 (quan trọng nhất)
1) Copy Part 25 → `Assets/Game/Core/Contracts/`
2) Copy Part 26 skeletons → đúng folder
3) Tạo scene Bootstrap + `GameBootstrap`
4) Compile clean, Play chạy được và Tick loop chạy (log nhẹ)

---

## 6) Mẹo để không “làm lại từ đầu”

- Đặt file đúng folder ngay từ đầu (đừng để “Scripts/” lộn xộn).
- Contracts (Part 25) coi như “API ổn định” — thay đổi cực hạn chế.
- TickOrder phải ổn định & rõ ràng (deterministic iteration: id tăng dần).
- Chỉ làm UI debug tạm (OnGUI) cho VS#1; UI đẹp làm sau.
