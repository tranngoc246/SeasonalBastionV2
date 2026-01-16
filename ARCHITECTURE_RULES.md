# SEASONAL BASTION V2 — ARCHITECTURE RULES (LOCKED)
**Purpose**: Prevent compilation errors and maintain clean architecture  
**Status**: Authoritative reference for all development

---

## ??? CORE PRINCIPLES

### 1. Two-Layer Architecture

```
????????????????????????????????????????????
?  CONTRACTS LAYER (Pure Interfaces)       ?
?  - Interfaces (IRunClock, IEventBus...)  ?
?  - DTOs (structs, readonly)              ?
?  - Enums (Season, Phase, JobArchetype)   ?
?  - NO IMPLEMENTATION LOGIC               ?
????????????????????????????????????????????
                  ?
                  ? (runtime implements)
????????????????????????????????????????????
?  RUNTIME LAYER (Concrete Implementations)?
?  - Services (RunClockService, EventBus)  ?
?  - MonoBehaviours (GameBootstrap)        ?
?  - Store implementations                 ?
?  - CAN use Contracts types               ?
????????????????????????????????????????????
```

**Golden Rule:** Contracts are stable definitions; runtime can change without breaking contracts.

---

## ?? FOLDER STRUCTURE (LOCKED)

### Root: `Assets\_Game\`

```
Assets/_Game/
??? Core/
?   ??? Contracts/          ? Game.Contracts assembly
?   ?   ??? Common/         (IdTypes, CellTypes, RunEnums...)
?   ?   ??? Run/            (IRunClock)
?   ?   ??? Events/         (IEventBus, event structs)
?   ?   ??? World/          (IWorldState, IWorldOps, Stores, States)
?   ?   ??? Grid/           (IGridMap, GridTypes)
?   ?   ??? Economy/        (IStorageService, IResourceFlowService...)
?   ?   ??? Jobs/           (IJobBoard, IJobScheduler, JobTypes)
?   ?   ??? Build/          (IBuildOrderService, BuildTypes)
?   ?   ??? Combat/         (ICombatService)
?   ?   ??? Rewards/        (IRewardService, IRunOutcomeService)
?   ?   ??? Save/           (ISaveService, SaveDTOs)
?   ?   ??? Notifications/  (INotificationService)
?   ?   ??? Data/           (IDataRegistry, IDataValidator)
?   ?   ??? ... (other interface categories)
?   ?
?   ??? Runtime/            ? Game.Core assembly (implementations)
?   ?   ??? Events/         (EventBus.cs)
?   ?   ??? Loop/           (RunClockService.cs, NotificationService.cs)
?   ?   ??? Utils/          (Log, TimeUtil...)
?   ?   ??? GameServices.cs (service container)
?   ?
?   ??? Boot/               ? Game.Boot assembly
?       ??? GameBootstrap.cs (composition root)
?
??? World/                  ? Game.World assembly
?   ??? State/
?   ?   ??? Stores/         (EntityStore, BuildingStore...)
?   ?   ??? States/         (runtime state logic if needed)
?   ??? Ops/                (WorldOps.cs)
?
??? Grid/                   ? Game.Grid assembly
?   ??? GridMap.cs, PlacementService.cs...
?
??? Economy/                ? Game.Economy assembly
?   ??? StorageService.cs, ResourceFlowService.cs...
?
??? Jobs/                   ? Game.Jobs assembly
?   ??? JobBoard.cs, ClaimService.cs, JobScheduler.cs
?   ??? Executors/          (HarvestExecutor, HaulBasicExecutor...)
?
??? Build/                  ? Game.Build assembly
?   ??? BuildOrderService.cs
?
??? Combat/                 ? Game.Combat assembly
?   ??? CombatService.cs, WaveDirector.cs...
?
??? Rewards/                ? Game.Rewards assembly
?   ??? RewardService.cs, RunOutcomeService.cs
?
??? Save/                   ? Game.Save assembly
?   ??? SaveService.cs
?
??? UI/                     ? Game.UI assembly
?   ??? (UI controllers and binders)
?
??? Defs/                   ? Game.Defs assembly
    ??? (ScriptableObject defs: BuildingDef, EnemyDef...)
```

---

## ?? ASSEMBLY REFERENCE RULES

### Rule 1: Every Runtime Assembly References Contracts
```json
{
  "name": "Game.AnyModule",
  "references": [
    "Game.Contracts",  ? REQUIRED for all runtime
    "Game.Core",
    "..." 
  ]
}
```

### Rule 2: Dependency Flow (One Direction Only)

```
Boot ? (references everything)
  ?
Save/UI ? (references most modules)
  ?
Build/Combat/Rewards ? (references World, Grid, Economy, Jobs)
  ?
Jobs ? (references World, Economy)
  ?
Economy/Grid ? (references World)
  ?
World ? (references Core, Contracts only)
  ?
Core ? (references Contracts only)
  ?
Contracts ? (references NOTHING)
```

**NO CIRCULAR REFERENCES!**

### Rule 3: Contracts Assembly Isolation
- Contracts **NEVER** references any runtime assembly
- Contracts **CAN** reference Unity Engine (for Vector3, MonoBehaviour base types)
- Keep Contracts minimal: interfaces, structs, enums only

---

## ?? NAMESPACE RULES

### Contracts Layer
```csharp
namespace SeasonalBastion.Contracts
{
    // All interfaces, DTOs, enums
    public interface IRunClock { ... }
    public readonly struct BuildingId { ... }
    public enum Season { Spring, Summer, Autumn, Winter }
}
```

### Runtime Layer
```csharp
namespace SeasonalBastion
{
    // All concrete implementations
    public sealed class RunClockService : IRunClock { ... }
    public sealed class EventBus : IEventBus { ... }
}
```

**DO NOT MIX**: Never put runtime logic in `SeasonalBastion.Contracts` namespace.

---

## ?? FILE NAMING CONVENTIONS

### Contracts (Interfaces)
- `IServiceName.cs` (e.g., `IRunClock.cs`, `IEventBus.cs`)
- `TypesGroup.cs` (e.g., `IdTypes.cs`, `CellTypes.cs`, `JobTypes.cs`)
- `CommonEvents.cs` (event structs)

### Runtime (Implementations)
- `ServiceName.cs` (e.g., `RunClockService.cs`, `EventBus.cs`)
- `StoreImpl.cs` (e.g., `BuildingStore.cs` implements `IBuildingStore`)

### ScriptableObjects (Defs)
- `TypeDef.cs` (e.g., `BuildingDef.cs`, `EnemyDef.cs`)

---

## ?? ANTI-PATTERNS (FORBIDDEN)

### ? Circular Assembly References
```
Game.World ? Game.Jobs ? Game.World  ? FORBIDDEN!
```

### ? Runtime Logic in Contracts
```csharp
// ? WRONG: In Game.Contracts
namespace SeasonalBastion.Contracts
{
    public class RunClockService { ... } // ? NO! This is runtime
}
```

### ? Missing Contracts Reference
```json
{
  "name": "Game.Economy",
  "references": [
    "Game.Core"  // ? Missing "Game.Contracts"!
  ]
}
```

### ? Duplicate Type Definitions
```csharp
// File 1: Assets/_Game/Core/Loop/RunClockService.cs
public class RunClockService { ... }

// File 2: Assets/_Game/Core/Runtime/Run/RunClockService.cs
public class RunClockService_Patched { ... } // ? Delete one!
```

---

## ? CHECKLIST FOR NEW FEATURES

### Adding a New Service

1. **Create interface in Contracts**
   - Location: `Assets\_Game\Core\Contracts\[Category]\IServiceName.cs`
   - Namespace: `SeasonalBastion.Contracts`
   - Example:
     ```csharp
     namespace SeasonalBastion.Contracts
     {
         public interface IMyNewService
         {
             void DoSomething();
         }
     }
     ```

2. **Create implementation in appropriate module**
   - Location: `Assets\_Game\[Module]\ServiceName.cs`
   - Namespace: `SeasonalBastion`
   - Example:
     ```csharp
     using SeasonalBastion.Contracts;
     
     namespace SeasonalBastion
     {
         public sealed class MyNewService : IMyNewService
         {
             public void DoSomething() { ... }
         }
     }
     ```

3. **Update assembly references**
   - Ensure module asmdef includes `"Game.Contracts"`
   - Add service to `GameServices.cs` container

4. **Register in composition root**
   - Add to `GameServicesFactory.Create()` method in Boot

---

## ??? DEBUGGING COMPILATION ERRORS

### Error: CS0234 "Contracts does not exist"
```
CS0234: The type or namespace name 'Contracts' does not exist in the namespace 'SeasonalBastion'
```

**Fix:**
1. Open affected assembly's `.asmdef` file
2. Add `"Game.Contracts"` to `references` array
3. Save and let Unity reimport

### Error: CS0246 "Type could not be found"
```
CS0246: The type or namespace name 'IRunClock' could not be found
```

**Fix:**
1. Verify `using SeasonalBastion.Contracts;` at top of file
2. Verify assembly references `Game.Contracts`
3. Check if interface actually exists in Contracts

### Error: Duplicate type definition
```
CS0101: The namespace 'SeasonalBastion' already contains a definition for 'RunClockService'
```

**Fix:**
1. Search project for all files with that type name
2. Keep ONE canonical implementation
3. Delete duplicates (patches, temp files)

---

## ?? TYPE OWNERSHIP QUICK REFERENCE

| Type Category | Location | Assembly | Namespace |
|--------------|----------|----------|-----------|
| **Interfaces** | `Core/Contracts/[Category]/I*.cs` | Game.Contracts | SeasonalBastion.Contracts |
| **DTOs/Structs** | `Core/Contracts/[Category]/*Types.cs` | Game.Contracts | SeasonalBastion.Contracts |
| **Enums** | `Core/Contracts/Common/*Enums.cs` | Game.Contracts | SeasonalBastion.Contracts |
| **Services** | `Core/Runtime/` or module root | Game.Core / modules | SeasonalBastion |
| **Stores** | `World/State/Stores/` | Game.World | SeasonalBastion |
| **Defs** | `Defs/[Category]/` | Game.Defs | SeasonalBastion |

---

## ?? WHEN TO UPDATE THIS DOCUMENT

Update this file when:
- Adding new assembly definitions
- Changing namespace conventions
- Establishing new architectural patterns
- Discovering new anti-patterns to forbid

**Keep this document as single source of truth!**

---

**Last Updated**: 2024  
**Authority**: Architecture Lead  
**Status**: LOCKED — Changes require team review
