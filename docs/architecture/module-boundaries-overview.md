# Seasonal Bastion — Module Boundaries Overview

> Mục đích: Ghi lại sơ đồ kiến trúc tổng thể của project và boundary giữa các module chính trước khi bắt đầu implementation Wave 1.
> Trọng tâm: repo structure, runtime flow, domain boundaries, và các điểm cần giữ kỷ luật khi mở rộng codebase.

---

## 1. Kiến trúc tổng thể project

```text
SeasonalBastionV2
│
├─ Assets/
│  ├─ Scenes/                  -> scene Unity
│  ├─ Settings/                -> project/game settings assets
│  ├─ _Game/
│  │  ├─ Core/                 -> boot, contracts, loop, runstart, common infra
│  │  ├─ World/                -> world state + stores + indexing
│  │  ├─ Grid/                 -> spatial occupancy / map interaction
│  │  ├─ Build/                -> build orders, cancel, rebuild-after-load
│  │  ├─ Economy/              -> storage, resource flow, ammo-related economy
│  │  ├─ Jobs/                 -> assignment, enqueue, execution, cleanup
│  │  ├─ Combat/               -> waves, enemies, damage/combat loop
│  │  ├─ Save/                 -> save/load DTOs, apply, migration
│  │  ├─ Rewards/              -> run outcome / rewards
│  │  ├─ UI/                   -> UIToolkit presenters, UXML, USS, UI services
│  │  ├─ Debug/                -> debug HUD/panels/tools for iteration
│  │  ├─ Defs/                 -> authored data / definitions
│  │  ├─ Resources/            -> Unity Resources assets (RunStart configs, etc.)
│  │  ├─ Input/                -> game input layer
│  │  ├─ Art/                  -> game visuals/art-side runtime assets
│  │  ├─ Editor/               -> editor-only tooling
│  │  └─ Tests/                -> editmode/runtime/regression tests
│  │
│  └─ TextMesh Pro, UI Toolkit, etc.
│
├─ docs/
│  ├─ architecture/            -> technical architecture notes
│  ├─ GDD/                     -> design / roadmap / backlog / implementation docs
│  └─ stabilization-checklist.md
│
├─ Packages/
├─ ProjectSettings/
└─ CHANGELOG.md
```

---

## 2. Runtime flow khi bấm New Run

```text
[Main Menu]
    │
    ▼
GameAppController
    │
    ├─ RequestNewGame(seed, wipeSave)
    │
    ▼
Load Scene: "Game"
    │
    ▼
GameBootstrap
    │
    ├─ GameServicesFactory.Create(...)
    │       │
    │       ├─ EventBus
    │       ├─ DataRegistry / DataValidator
    │       ├─ RunClockService
    │       ├─ NotificationService
    │       ├─ RunStartRuntime
    │       ├─ WorldState / WorldOps / WorldIndex
    │       ├─ GridMap
    │       ├─ PlacementService
    │       ├─ StorageService / ResourceFlowService
    │       ├─ JobBoard / JobScheduler / ClaimService
    │       ├─ BuildOrderService
    │       ├─ AmmoService
    │       ├─ CombatService
    │       ├─ RewardService / RunOutcomeService
    │       └─ SaveService
    │
    └─ TryStartNewRun(...)
            │
            ▼
        GameLoop.StartNewRun(seed, startMapConfig)
            │
            ▼
        RunStartFacade.TryApply(...)
            │
            ├─ RunStartInputParser
            ├─ RunStartConfigValidator
            ├─ RunStartRuntimeCacheBuilder
            ├─ RunStartWorldBuilder
            ├─ RunStartZoneInitializer
            ├─ RunStartHqResolver.BuildLanes
            ├─ RunStartStorageInitializer
            ├─ RunStartNpcSpawner
            └─ RunStartValidator
            │
            ▼
        Gameplay world becomes valid
            │
            ▼
        GameLoop.Tick(...)
            │
            ├─ RunClockService.Tick
            ├─ Jobs / Economy / Build / Combat tick flows
            └─ EventBus publishes state changes
            │
            ▼
        UI reads GameServices + events
            │
            ▼
        HudPresenter / BuildPanelPresenter / InspectPanelPresenter / etc.
```

---

## 3. Kiến trúc runtime theo lớp

```text
┌─────────────────────────────────────────────┐
│                 APP / SCENE                 │
│ GameAppController, GameBootstrap            │
└─────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────┐
│              COMPOSITION / CORE             │
│ GameServicesFactory, GameLoop, EventBus     │
│ RunClockService, NotificationService        │
│ RunStartFacade + validators/builders        │
└─────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────┐
│          STATE & WORLD REPRESENTATION       │
│ WorldState, WorldOps, WorldIndex, GridMap   │
│ Stores, IDs, runtime state, occupancy       │
└─────────────────────────────────────────────┘
            │              │               │
            │              │               │
            ▼              ▼               ▼
┌────────────────┐ ┌────────────────┐ ┌────────────────┐
│   BUILD        │ │   ECONOMY      │ │   JOBS         │
│ Build orders   │ │ Storage        │ │ Assignment     │
│ Build sites    │ │ Resource flow  │ │ Enqueue        │
│ Cancel/rebuild │ │ Ammo economy   │ │ Execute/cleanup│
└────────────────┘ └────────────────┘ └────────────────┘
            │              │               │
            └──────────────┴───────┬───────┘
                                   ▼
                          ┌────────────────┐
                          │   COMBAT       │
                          │ Waves/enemies  │
                          │ Tower firing   │
                          │ HQ damage      │
                          └────────────────┘
                                   │
                                   ▼
                          ┌────────────────┐
                          │ SAVE / LOAD    │
                          │ DTOs / Apply   │
                          │ runtime rebuild│
                          └────────────────┘
                                   │
                                   ▼
                          ┌────────────────┐
                          │ UI / DEBUG     │
                          │ HUD / Panels   │
                          │ Inspect / Tools│
                          └────────────────┘
```

---

## 4. Boundary giữa các domain chính

### 4.1 Core
**Vai trò**
- khởi tạo hệ thống
- loop/tick
- contracts/events
- runstart orchestration
- common infrastructure

**Nên biết**
- cách tạo services
- tick order
- event bus
- app/scene flow
- run bootstrap

**Không nên làm**
- chứa gameplay rule chi tiết của từng domain nếu không thực sự cross-cutting
- trở thành nơi “quẳng tạm” service vì chưa biết để đâu

---

### 4.2 World
**Vai trò**
- giữ state canonical của entities
- stores cho building/NPC/tower/site/enemy/zone
- world indexing / lookup

**Nên biết**
- entity existence
- ids
- state snapshots
- world mutation hợp lệ

**Không nên làm**
- tự quyết định gameplay strategy
- chứa logic assignment/combat/build quá sâu

---

### 4.3 Grid
**Vai trò**
- spatial truth
- occupancy
- road/building/site footprint
- position-level legality

**Nên biết**
- cell blocked hay không
- road/building/site nằm đâu
- spatial relations

**Không nên làm**
- biết quá nhiều về jobs/economy
- thành nơi lưu gameplay meaning ngoài spatial meaning

---

### 4.4 Build
**Vai trò**
- place order
- upgrade order
- cancel/refund
- site progress
- rebuild-after-load

**Nên biết**
- build sites
- build orders
- placement validation result
- resource costs cần cho build

**Contract/policy hiện dùng để giữ boundary sạch hơn**
- `IBuildWorkplaceResolver` → Build hỏi workplace phù hợp qua service thay vì tự hard-wire selection policy trong order service
- `IBuildJobOrchestrator` → Build diễn đạt nhu cầu build jobs qua interface, không bám trực tiếp vào concrete planner ở call site chính

**Không nên làm**
- tự làm thay toàn bộ job scheduling
- tự quản worker assignment như một thế giới riêng

---

### 4.5 Economy
**Vai trò**
- storage
- resource flow
- local/global inventory semantics
- ammo as economic resource

**Nên biết**
- resource amounts
- caps
- source/destination flow
- storage legality

**Không nên làm**
- thay thế job logic
- trực tiếp điều khiển UI
- chứa combat rules

---

### 4.6 Jobs
**Vai trò**
- ai làm gì
- workplace/jobset filtering
- enqueue/assign/execute/cleanup
- claim handling

**Nên biết**
- NPC state
- workplace role
- job state machine
- claim ownership

**Contract/policy hiện dùng để giữ boundary sạch hơn**
- `IJobWorkplacePolicy` → shared source of truth cho workplace-role mapping giữa Jobs và Build-side resolver
- `IBuildJobOrchestrator` là boundary để Build yêu cầu job orchestration mà không kéo concrete `BuildJobPlanner` vào mọi nơi

**Không nên làm**
- giữ “source of truth” riêng cho world/economy/build
- sửa tắt nhiều domain state mà không qua service hợp lý

---

### 4.7 Combat
**Vai trò**
- waves
- enemy pressure
- tower fire/damage
- HQ threat

**Nên biết**
- tower state
- enemy state
- ammo availability
- wave schedule

**Không nên làm**
- ôm luôn logistics/economy
- tự sinh ra world truths trái với World/Grid

---

### 4.8 Save
**Vai trò**
- snapshot state
- load/apply
- migration
- runtime sanitation/rebuild

**Nên biết**
- DTOs
- persistable state
- runtime state nào phải clear/rebuild

**Không nên làm**
- tự “phát minh” gameplay state mới
- chữa gameplay bugs bằng save hacks

---

### 4.9 UI
**Vai trò**
- hiển thị state
- gửi user intent
- contextual actions
- readability

**Nên biết**
- GameServices interface đủ dùng
- event bus
- selected entity/panel state

**Không nên làm**
- viết gameplay rule thật trong presenter
- mutate world sâu mà không qua services/domain API

---

## 5. Sơ đồ phụ thuộc mong muốn giữa module

```text
Core
 ├─ owns boot / composition / loop / events
 ├─ talks to World / Grid / domain services
 └─ exposes state/services to UI

World <---- Grid
  │
  ├────> Build
  ├────> Economy
  ├────> Jobs
  ├────> Combat
  └────> Save

Economy <----> Jobs
Build   <----> Jobs
Combat  <----> Economy
Combat  <----> World
Save    <----> all runtime state domains

UI  ----reads----> Core/GameServices + domain state
UI  ----sends----> intents/actions through services/presenters
```

---

## 6. Sơ đồ riêng cho Wave 1

```text
MainMenu
  │
  ▼
GameAppController
  │
  ▼
GameBootstrap
  │
  ├─ GameServicesFactory
  ├─ GameLoop
  └─ TryStartNewRun
       │
       ▼
    RunStartFacade
       │
       ├─ parse config
       ├─ validate config
       ├─ build world
       ├─ init storage
       ├─ init towers
       ├─ spawn NPCs
       └─ validate runtime
       │
       ▼
    RunClockService starts ticking
       │
       ▼
    HudPresenter reads:
       ├─ Year / Season / Day / Phase
       ├─ resources
       └─ timescale
```

### Tức là với Wave 1, đường đi xương sống là:
- `GameAppController`
- `GameBootstrap`
- `GameLoop`
- `RunStartFacade`
- `RunStartWorldBuilder`
- `RunStartStorageInitializer`
- `RunStartTowerInitializer`
- `RunStartNpcSpawner`
- `RunClockService`
- `HudPresenter`

---

## 7. Kết luận kiến trúc

### Điểm tốt
- Có backbone rõ
- Có domain modules đúng
- Có UI presenter layer
- Có runstart orchestration tách riêng
- Có save/load layer riêng
- Có regression tests theo domain

### Điểm phải canh
- `Core` có thể phình
- `Debug` có thể thành nợ
- `Jobs/Economy/Build/Combat` phải giữ ranh giới
- UI không được nhúng gameplay logic sâu
- không để `BuildOrderService` hoặc debug flow kéo ngược concrete policy/planner vào call site sau khi đã tách interface

### Cập nhật sau batch cleanup 2026-03-25
- `BuildOrderService` đã chuyển sang dùng `IBuildWorkplaceResolver` và `IBuildJobOrchestrator` qua `GameServices`
- `BuildOrderWorkplaceResolver` và `JobScheduler` đã dùng chung `IJobWorkplacePolicy` để tránh lệch rule workplace-role giữa Build và Jobs
- mục tiêu của batch này là cleanup boundary, **không đổi gameplay semantics**
- đây là nền để vào M1/Wave 1 mà không làm Build/Jobs dính nhau hơn khi mở rộng feature

### Kết luận một câu
**Kiến trúc project hiện tại đủ tốt để bắt đầu implementation theo M1; việc quan trọng nhất từ đây không phải thêm module, mà là giữ boundary sạch khi mở rộng gameplay.**
