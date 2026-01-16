# PART 12 — RUN OUTCOME + REWARDS + META PROGRESSION HOOKS (PREMIUM ROGUELITE) — SPEC v0.1

> Mục tiêu: hoàn thiện “1 run chơi hoàn chỉnh” theo premium roguelite:
- End conditions: Win/Lose/Abort
- Rewards cadence: after wave / after day / end of season
- Reward choices (pick 1 of 3) + rarity + weighted pools
- Meta progression: unlocks, account progression, permanent perks
- Run summary + telemetry hooks
- Persistence: save meta separate from run save

Phần này không cần live-ops phức tạp; chỉ cần đúng loop, dễ mở rộng.

---

## 1) Run model split: RunSave vs MetaSave

### 1.1 RunSave (ephemeral)
- WorldState (Part 6)
- RunClock state (Part 2)
- BuildOrders + BuildSites (Part 9)
- JobBoard + pending requests (Part 8/10/11)
- Current rewards pending (if player is choosing)
- RNG seed for deterministic reward offerings

### 1.2 MetaSave (persistent)
- Unlocks progression (Part 3)
- Permanent upgrades/perks chosen
- Account currencies (e.g., Shards, Essence)
- Achievements stats

> MUST: MetaSave survive after run ends. RunSave may be deleted on new run.

---

## 2) End conditions

### 2.1 Lose conditions
- HQ destroyed (combat) → Defeat
- Starvation (optional) → Defeat (not v0.1)
- Time-out (optional) → Defeat/Score

### 2.2 Win conditions (v0.1)
Pick one simple:
- Survive through Winter Day N (final boss wave) → Victory
or
- Reach “SeasonScore” threshold by end of Winter → Victory

v0.1 recommended: **Survive Final Wave** (clear and exciting).

### 2.3 Abort
Player can quit run manually:
- Treat as Abort (no rewards or partial, your choice)

RunOutcome enum:
```csharp
public enum RunOutcome { Ongoing, Victory, Defeat, Abort }
```

---

## 3) Reward cadence (when do we grant choices?)

### 3.1 Recommended cadence for pacing
- **After each Defend day** (end of day) → pick 1 reward
- Optional: after boss wave → extra reward
- End of run: grant meta currency based on performance

Why: day-level reward keeps decision frequency reasonable, not interrupting waves.

### 3.2 Reward UI mode
When reward selection active:
- Pause sim or set speed to 0 (unscaled UI still works)
- Player must pick to continue

Events:
- `RewardSelectionStartedEvent`
- `RewardSelectedEvent`
- `RewardSelectionEndedEvent`

---

## 4) Reward system data

### 4.1 RewardDef
```csharp
public enum RewardRarity { Common, Uncommon, Rare, Epic, Legendary }
public enum RewardCategory { Economy, Combat, Workforce, Storage, Ammo, Utility }

public sealed class RewardDef : ScriptableObject
{
    public string Id;
    public RewardCategory Category;
    public RewardRarity Rarity;

    public string Title;
    public string Description;

    public RewardEffect[] Effects;
    public bool IsMetaOnly;     // if true affects MetaSave
    public bool IsRunOnly;      // if true affects Run state only

    public int Weight;          // within its pool
}
```

### 4.2 RewardEffect (typed, data-driven)
v0.1 keep small set, avoid scripting:
```csharp
public enum RewardEffectType
{
    AddStorageCapPercent,      // e.g. +20% Warehouse cap
    AddTowerDamagePercent,     // +10% tower damage
    AddTowerRange,             // +1 range
    ReduceCraftTimePercent,    // -15% forge craft time
    IncreaseHarvestAmount,     // +1 per cycle
    IncreaseNPCMoveSpeedRoad,  // +10% on road
    AddStartingAmmo,           // +X ammo buffer to armory at day start
    UnlockBuilding,            // unlock a building def
    AddMetaCurrency,           // after run
}
```

Effect payload:
```csharp
[System.Serializable]
public struct RewardEffect
{
    public RewardEffectType Type;
    public string TargetId;    // building def id, tower def id, etc.
    public float ValueF;
    public int ValueI;
}
```

---

## 5) Reward offering algorithm (deterministic)

### 5.1 Inputs
- Pool by cadence (e.g., EndOfDay pool)
- Progress info:
  - day index
  - current unlock tier
  - performance metrics (survival, damage taken, etc.)
- RNG seeded per run

### 5.2 Offer 3 choices
Algorithm:
1) Decide rarity for each slot using weighted rarity table (varies by day)
2) For each slot:
   - Select a reward from category pools matching rarity and constraints
   - Avoid duplicates in same offering
   - Avoid offering rewards already owned (if stack not allowed)
3) Return list of 3 RewardDefIds

Determinism:
- Use your Run RNG (seed stored in RunSave)
- Store offered reward ids in RunSave so reload shows same options.

### 5.3 Constraints
- If Forge not unlocked, avoid forge-related rewards unless it unlocks forge.
- If no tower built, still allow “tower damage” but less weight.

---

## 6) Reward application (effects engine)

### 6.1 RunModifiers (ephemeral)
Store applied run-only buffs in a single struct to query quickly:

```csharp
public struct RunModifiers
{
    public float StorageCapMult;      // 1.0 default
    public float TowerDamageMult;
    public int TowerRangeBonus;

    public float CraftTimeMult;
    public int HarvestAmountBonus;

    public float RoadSpeedBonusMult;

    public int DailyArmoryAmmoBonus;
}
```

### 6.2 MetaModifiers (persistent)
```csharp
public struct MetaModifiers
{
    public float GlobalHarvestMult;
    public float GlobalTowerDamageMult;
    public int StartingNPCBonus;
}
```

### 6.3 Effect application rules
- RewardSelected:
  - Apply each effect:
    - If run-only: mutate RunModifiers
    - If meta: mutate MetaSave or Unlocks
- Some effects require immediate recalculation:
  - StorageCapMult: apply to all storage caps now (iterate buildings, re-apply caps clamp)
  - TowerRangeBonus: update towers runtime

### 6.4 Unlock reward
- `UnlockBuilding` triggers UnlockService grant (Part 3)
- If building becomes available mid-run, player can build it immediately (if desired).

---

## 7) Run summary & scoring

### 7.1 Metrics tracked
- Days survived
- Waves cleared
- Total enemies killed
- HQ damage taken
- Resources produced/consumed
- Towers shots fired, ammo shortages time

### 7.2 Score formula (v0.1)
Simple:
- Score = daysSurvived * 1000 + wavesCleared * 200 + kills * 10 - hqDamageTaken * 2

### 7.3 Meta currency rewards
- Currency = clamp(score / 1000, min..max)
- Add to MetaSave on run end.

---

## 8) RunOutcomeService (or RunEndController)

### 8.1 Responsibilities
- Observe events:
  - HQ HP reaches 0 => Defeat
  - Final wave cleared => Victory
  - Player abort => Abort
- Freeze run sim and open summary UI
- Apply end-of-run meta rewards
- Persist MetaSave
- Optionally persist Run history

### 8.2 API
```csharp
public sealed class RunOutcomeService
{
    public RunOutcome Outcome { get; private set; } = RunOutcome.Ongoing;

    public void OnHQDestroyed() => End(RunOutcome.Defeat);
    public void OnFinalWaveCleared() => End(RunOutcome.Victory);
    public void AbortRun() => End(RunOutcome.Abort);

    private void End(RunOutcome outcome)
    {
        if (Outcome != RunOutcome.Ongoing) return;
        Outcome = outcome;

        // pause sim, open UI, compute rewards
    }
}
```

---

## 9) RewardSelectionService (flow)

### 9.1 State
```csharp
public sealed class RewardSelectionService
{
    public bool IsActive { get; private set; }
    public string[] CurrentOfferIds; // length 3
    public int OfferDayIndex;

    public void StartSelection(int dayIndex, string[] offerIds)
    {
        IsActive = true;
        OfferDayIndex = dayIndex;
        CurrentOfferIds = offerIds;
        // pause sim, open UI
    }

    public void Choose(int slotIndex)
    {
        var rewardId = CurrentOfferIds[slotIndex];
        // apply reward
        IsActive = false;
        CurrentOfferIds = null;
        // resume sim
    }
}
```

### 9.2 When to start
- At end of defend day, WaveDirector/RunClock emits `DefendDayEndedEvent`
- RewardOfferService generates offer → RewardSelectionService starts.

---

## 10) Meta progression design (minimal but public-ready)

### 10.1 What persists
- Unlock nodes (buildings, perks)
- Meta currency
- Permanent perk levels

### 10.2 Minimal meta tree
v0.1 propose:
- Tier 0: start kit (HQ+farm+lumber+tower)
- Tier 1: unlock Warehouse
- Tier 2: unlock Forge
- Tier 3: unlock Armory
- Tier 4: unlock new tower types / extra NPC capacity

Unlock conditions:
- Spend meta currency OR reach run milestone (days survived).

### 10.3 Prevent content explosion
- Keep < 30 meta nodes for first public version.

---

## 11) Persistence approach (files)
- `meta_save.json` in persistent data path
- `run_save.json` for continue run
- Versioning:
  - include `schemaVersion` and migration steps (Part 0 Validator/Migrator can help)

---

## 12) Notifications (Part 4)
- On reward available:
  - `reward.available` (Info) (but UI should be obvious)
- On victory/defeat:
  - `combat.defeat` / `combat.victory` (Error/Info)
- On unlock gained:
  - `unlock.new` (Info)

---

## 13) QA Checklist (Part 12)
- [ ] Rewards offer deterministic across save/load
- [ ] Picking reward applies modifiers immediately
- [ ] MetaSave persists across runs
- [ ] Victory/Defeat flow pauses sim and shows summary
- [ ] End-of-run currency granted correctly
- [ ] Unlock gating respected in offer pool

---

## 14) Next Part (Part 13 đề xuất)
**UI Screens & UX Flow**: main menu, run setup, in-run HUD, reward screen, summary screen, settings, save/load UX.

