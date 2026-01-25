using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeasonalBastion
{
    /// <summary>
    /// Runtime JSON-backed data registry.
    /// v0.1: JSON TextAssets are referenced via DefsCatalog.asset.
    /// Notes:
    /// - Keep maps deterministic (case-insensitive keys, last wins by file order).
    /// - Collect load errors so DataValidator can gate play.
    /// </summary>
    public sealed class DataRegistry : IDataRegistry
    {
        private readonly DefsCatalog _catalog;

        private readonly Dictionary<string, BuildingDef> _buildings = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, NpcDef> _npcs = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TowerDef> _towers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EnemyDef> _enemies = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, WaveDef> _waves = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RewardDef> _rewards = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RecipeDef> _recipes = new(StringComparer.OrdinalIgnoreCase);

        private readonly List<string> _loadErrors = new(32);

        // --- JSON wrappers (Unity JsonUtility needs top-level object) ---
        [Serializable] private sealed class BuildingsRoot { public BuildingJson[] buildings; }
        [Serializable] private sealed class NpcsRoot { public NpcJson[] npcs; }
        [Serializable] private sealed class TowersRoot { public TowerJson[] towers; }
        [Serializable] private sealed class EnemiesRoot { public EnemyJson[] enemies; public EnemyScalingJson scaling; }
        [Serializable] private sealed class RecipesRoot { public RecipeJson[] recipes; }
        [Serializable] private sealed class WavesRoot { public WaveJson[] waves; public WavesNotesJson notes; }
        [Serializable] private sealed class RewardsRoot { public RewardJson[] rewards; }

        [Serializable] private sealed class EnemyScalingJson { public EnemyScalingYearJson year2; }
        [Serializable] private sealed class EnemyScalingYearJson { public float hpMul = 1f; public float damageMul = 1f; public float countMul = 1f; }

        [Serializable] private sealed class WavesNotesJson { public string source; public string year2Outline; }

        [Serializable] private sealed class StorageCapsJson { public int L1; public int L2; public int L3; }

        [Serializable]
        private sealed class BuildingJson
        {
            public string id;
            public int sizeX = 1;
            public int sizeY = 1;
            public int baseLevel = 1;
            public int hp = 1; // VS2 Day22: building durability

            public string[] workRoles;

            // VS2 Day18: construction delivery gate (L1 build)
            public BuildingCostJson[] buildCostsL1;
            public int buildChunksL1 = 0;

            public bool isHQ = false;
            public bool isWarehouse = false;
            public bool isProducer = false;
            public bool isHouse = false;
            public bool isForge = false;
            public bool isArmory = false;
            public bool isTower = false;

            public StorageCapsJson capWood;
            public StorageCapsJson capFood;
            public StorageCapsJson capStone;
            public StorageCapsJson capIron;
            public StorageCapsJson capAmmo;
        }

        [Serializable]
        private sealed class NpcJson
        {
            public string id;
            public string role;
            public float baseMoveSpeed = 1f;
            public float roadSpeedMultiplier = 1f;
            public StorageCapsJson carryCore;
        }

        [Serializable]
        private sealed class TowerUnlockJson
        {
            public int year = 1;
            public string season = "Spring";
            public int day = 1;
        }

        [Serializable]
        private sealed class TowerCostJson
        {
            public int res;
            public int amt;
        }

        [Serializable]
        private sealed class BuildingCostJson
        {
            public int res;
            public int amt;
        }

        [Serializable]
        private sealed class TowerJson
        {
            public string id;
            public int tier = 1;
            public int hp = 1;
            public float range = 1f;
            public float rof = 1f;
            public int damage = 1;

            public float slowPct = 0f;
            public float slowSec = 0f;
            public string aoe;
            public int dotDps = 0;
            public int dotSec = 0;

            public int ammoMax = 0;
            public int ammoPerShot = 1;
            public float needsAmmoThresholdPct = 0.25f;

            public TowerCostJson[] buildCost;
            public int buildChunks = 1;
            public TowerUnlockJson unlock;
        }

        [Serializable]
        private sealed class EnemyJson
        {
            public string id;
            public int maxHp = 1;
            public float moveSpeed = 1f;
            public int damageToHQ = 1;
            public int damageToBuildings = 1;
            public float range = 0f;

            public bool isBoss = false;
            public int year = 0;
            public string season = "Spring";
            public int day = 0;

            public float auraSlowRofPct = 0f;
        }

        [Serializable]
        private sealed class RecipeCostJson
        {
            public int type;
            public int amount;
        }

        [Serializable]
        private sealed class RecipeJson
        {
            public string id;
            public int inputType;
            public int inputAmount = 1;
            public int outputType;
            public int outputAmount = 1;
            public RecipeCostJson[] extraInputs;
            public float craftTimeSec = 0f;
        }

        [Serializable]
        private sealed class WaveEntryJson
        {
            public string enemyId;
            public int count = 1;
        }

        [Serializable]
        private sealed class WaveJson
        {
            public string id;
            public int waveIndex = 0;
            public int year = 1;
            public string season = "Autumn";
            public int day = 1;
            public bool isBoss = false;
            public WaveEntryJson[] entries;
        }

        [Serializable]
        private sealed class RewardJson
        {
            public string id;
            public string title;
        }

        public DataRegistry(DefsCatalog catalog)
        {
            _catalog = catalog;
            LoadAllFromCatalog();
        }

        // --- Debug helpers (not part of Part25 contract) ---
        public IReadOnlyCollection<string> GetAllBuildingIds() => _buildings.Keys;
        public IReadOnlyCollection<string> GetAllNpcIds() => _npcs.Keys;
        public IReadOnlyCollection<string> GetAllTowerIds() => _towers.Keys;
        public IReadOnlyCollection<string> GetAllEnemyIds() => _enemies.Keys;
        public IReadOnlyCollection<string> GetAllWaveIds() => _waves.Keys;
        public IReadOnlyCollection<string> GetAllRewardIds() => _rewards.Keys;
        public IReadOnlyCollection<string> GetAllRecipeIds() => _recipes.Keys;
        public IReadOnlyList<string> GetLoadErrors() => _loadErrors;

        public bool TryGetEnemy(string id, out EnemyDef def) => _enemies.TryGetValue(id, out def);
        public bool TryGetWave(string id, out WaveDef def) => _waves.TryGetValue(id, out def);

        public void ClearAll()
        {
            _buildings.Clear();
            _npcs.Clear();
            _towers.Clear();
            _enemies.Clear();
            _waves.Clear();
            _rewards.Clear();
            _recipes.Clear();
            _loadErrors.Clear();
        }

        private void LoadAllFromCatalog()
        {
            ClearAll();

            LoadBuildings(_catalog != null ? _catalog.Buildings : null);
            LoadNpcs(_catalog != null ? _catalog.Npcs : null);
            LoadTowers(_catalog != null ? _catalog.Towers : null);
            LoadEnemies(_catalog != null ? _catalog.Enemies : null);
            LoadRecipes(_catalog != null ? _catalog.Recipes : null);
            LoadWaves(_catalog != null ? _catalog.Waves : null);
            LoadRewards(_catalog != null ? _catalog.Rewards : null);

            if (_loadErrors.Count > 0)
                Debug.LogError($"[DataRegistry] Load finished with {_loadErrors.Count} error(s). Use DebugHUDHub -> Validate Data to inspect.");
        }

        private void LoadBuildings(TextAsset ta)
        {
            if (ta == null)
            {
                _loadErrors.Add("Buildings TextAsset is null (DefsCatalog.Buildings)");
                return;
            }

            var json = ta.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                _loadErrors.Add($"Buildings TextAsset '{ta.name}' is empty");
                return;
            }

            BuildingsRoot root;
            try { root = JsonUtility.FromJson<BuildingsRoot>(json); }
            catch (Exception e)
            {
                _loadErrors.Add($"Buildings parse failed: {e.Message}");
                return;
            }

            var arr = root != null ? root.buildings : null;
            if (arr == null || arr.Length == 0)
            {
                _loadErrors.Add("Buildings JSON missing/empty 'buildings' array");
                return;
            }

            int added = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < arr.Length; i++)
            {
                var bj = arr[i];
                if (bj == null) continue;

                var id = (bj.id ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id))
                {
                    _loadErrors.Add($"Buildings[{i}] has empty id");
                    continue;
                }

                if (!seen.Add(id))
                    _loadErrors.Add($"Duplicate Building id '{id}' in Buildings.json");

                int w = Mathf.Max(1, bj.sizeX);
                int h = Mathf.Max(1, bj.sizeY);
                int lvl = Mathf.Max(1, bj.baseLevel);

                var def = new BuildingDef
                {
                    DefId = id,
                    SizeX = w,
                    SizeY = h,
                    BaseLevel = lvl,
                    MaxHp = Mathf.Max(1, bj.hp),
                    IsHQ = bj.isHQ,
                    IsWarehouse = bj.isWarehouse,
                    IsProducer = bj.isProducer,
                    IsHouse = bj.isHouse,
                    IsForge = bj.isForge,
                    IsArmory = bj.isArmory,
                    IsTower = bj.isTower,
                    WorkRoles = ParseWorkRolesOrDerive(bj),
                    CapWood = ToCaps(bj.capWood),
                    CapFood = ToCaps(bj.capFood),
                    CapStone = ToCaps(bj.capStone),
                    CapIron = ToCaps(bj.capIron),
                    CapAmmo = ToCaps(bj.capAmmo),
                    BuildCostsL1 = ToCosts(bj.buildCostsL1, ctx: $"Building '{id}' buildCostsL1"),
                    BuildChunksL1 = Mathf.Max(0, bj.buildChunksL1)
                };

                _buildings[id] = def;
                added++;
            }

            Debug.Log($"[DataRegistry] Loaded Buildings: {added} (TextAsset: {ta.name})");
        }

        private void LoadNpcs(TextAsset ta)
        {
            if (ta == null)
            {
                Debug.LogWarning("[DataRegistry] Npcs TextAsset is null (DefsCatalog.Npcs). Using empty NPC defs.");
                return;
            }

            var json = ta.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                _loadErrors.Add($"Npcs TextAsset '{ta.name}' is empty");
                return;
            }

            NpcsRoot root;
            try { root = JsonUtility.FromJson<NpcsRoot>(json); }
            catch (Exception e)
            {
                _loadErrors.Add($"Npcs parse failed: {e.Message}");
                return;
            }

            var arr = root != null ? root.npcs : null;
            if (arr == null || arr.Length == 0)
            {
                _loadErrors.Add("Npcs JSON missing/empty 'npcs' array");
                return;
            }

            int added = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < arr.Length; i++)
            {
                var nj = arr[i];
                if (nj == null) continue;

                var id = (nj.id ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id)) { _loadErrors.Add($"Npcs[{i}] has empty id"); continue; }
                if (!seen.Add(id)) _loadErrors.Add($"Duplicate Npc id '{id}' in Npcs.json");

                var role = (nj.role ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(role)) _loadErrors.Add($"Npc '{id}' has empty role");

                var def = new NpcDef
                {
                    DefId = id,
                    Role = role,
                    BaseMoveSpeed = Mathf.Max(0.01f, nj.baseMoveSpeed),
                    RoadSpeedMultiplier = Mathf.Max(0.01f, nj.roadSpeedMultiplier),
                    CarryCore = ToCaps(nj.carryCore)
                };

                _npcs[id] = def;
                added++;
            }

            Debug.Log($"[DataRegistry] Loaded Npcs: {added} (TextAsset: {ta.name})");
        }

        private void LoadTowers(TextAsset ta)
        {
            if (ta == null)
            {
                Debug.LogWarning("[DataRegistry] Towers TextAsset is null (DefsCatalog.Towers). Using empty Tower defs.");
                return;
            }

            var json = ta.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                _loadErrors.Add($"Towers TextAsset '{ta.name}' is empty");
                return;
            }

            TowersRoot root;
            try { root = JsonUtility.FromJson<TowersRoot>(json); }
            catch (Exception e)
            {
                _loadErrors.Add($"Towers parse failed: {e.Message}");
                return;
            }

            var arr = root != null ? root.towers : null;
            if (arr == null || arr.Length == 0)
            {
                _loadErrors.Add("Towers JSON missing/empty 'towers' array");
                return;
            }

            int added = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < arr.Length; i++)
            {
                var tj = arr[i];
                if (tj == null) continue;

                var id = (tj.id ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id)) { _loadErrors.Add($"Towers[{i}] has empty id"); continue; }
                if (!seen.Add(id)) _loadErrors.Add($"Duplicate Tower id '{id}' in Towers.json");

                int ammoMax = Mathf.Max(0, tj.ammoMax);
                int ammoPerShot = tj.ammoPerShot;

                if (ammoMax <= 0) ammoPerShot = 0;
                else
                {
                    if (ammoPerShot <= 0) ammoPerShot = 1;
                    if (ammoPerShot > ammoMax) ammoPerShot = ammoMax;
                }

                float thrPct = tj.needsAmmoThresholdPct;
                if (thrPct <= 0f) thrPct = 0.25f;
                thrPct = Mathf.Clamp01(thrPct);

                var def = new TowerDef
                {
                    DefId = id,
                    Tier = Mathf.Max(1, tj.tier),
                    MaxHp = Mathf.Max(1, tj.hp),
                    Range = Mathf.Max(0f, tj.range),
                    Rof = Mathf.Max(0.01f, tj.rof),
                    Damage = Mathf.Max(0, tj.damage),
                    SlowPct = Mathf.Max(0f, tj.slowPct),
                    SlowSec = Mathf.Max(0f, tj.slowSec),
                    Aoe = tj.aoe ?? string.Empty,
                    DotDps = Mathf.Max(0, tj.dotDps),
                    DotSec = Mathf.Max(0, tj.dotSec),
                    AmmoMax = ammoMax,
                    AmmoPerShot = ammoPerShot,
                    NeedsAmmoThresholdPct = thrPct,
                    BuildCost = ToCosts(tj.buildCost, ctx: $"Tower '{id}' buildCost"),
                    BuildChunks = Mathf.Max(1, tj.buildChunks),
                    Unlock = ToUnlock(tj.unlock, $"Tower '{id}' unlock")
                };

                _towers[id] = def;
                added++;
            }

            ValidateTowers();

            Debug.Log($"[DataRegistry] Loaded Towers: {added} (TextAsset: {ta.name})");
        }

        private void LoadEnemies(TextAsset ta)
        {
            if (ta == null)
            {
                Debug.LogWarning("[DataRegistry] Enemies TextAsset is null (DefsCatalog.Enemies). Using empty Enemy defs.");
                return;
            }

            var json = ta.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                _loadErrors.Add($"Enemies TextAsset '{ta.name}' is empty");
                return;
            }

            EnemiesRoot root;
            try { root = JsonUtility.FromJson<EnemiesRoot>(json); }
            catch (Exception e)
            {
                _loadErrors.Add($"Enemies parse failed: {e.Message}");
                return;
            }

            var arr = root != null ? root.enemies : null;
            if (arr == null || arr.Length == 0)
            {
                _loadErrors.Add("Enemies JSON missing/empty 'enemies' array");
                return;
            }

            int added = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < arr.Length; i++)
            {
                var ej = arr[i];
                if (ej == null) continue;

                var id = (ej.id ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id)) { _loadErrors.Add($"Enemies[{i}] has empty id"); continue; }
                if (!seen.Add(id)) _loadErrors.Add($"Duplicate Enemy id '{id}' in Enemies.json");

                var def = new EnemyDef
                {
                    DefId = id,
                    MaxHp = Mathf.Max(1, ej.maxHp),
                    MoveSpeed = Mathf.Max(0.01f, ej.moveSpeed),
                    DamageToHQ = Mathf.Max(0, ej.damageToHQ),
                    DamageToBuildings = Mathf.Max(0, ej.damageToBuildings),
                    Range = Mathf.Max(0f, ej.range),
                    IsBoss = ej.isBoss,
                    BossYear = Mathf.Max(0, ej.year),
                    BossSeason = ParseSeason(ej.season, ctx: $"Enemy '{id}' season"),
                    BossDay = Mathf.Max(0, ej.day),
                    AuraSlowRofPct = Mathf.Max(0f, ej.auraSlowRofPct)
                };

                _enemies[id] = def;
                added++;
            }

            Debug.Log($"[DataRegistry] Loaded Enemies: {added} (TextAsset: {ta.name})");
        }

        private void LoadRecipes(TextAsset ta)
        {
            if (ta == null)
            {
                Debug.LogWarning("[DataRegistry] Recipes TextAsset is null (DefsCatalog.Recipes). Using empty Recipe defs.");
                return;
            }

            var json = ta.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                _loadErrors.Add($"Recipes TextAsset '{ta.name}' is empty");
                return;
            }

            RecipesRoot root;
            try { root = JsonUtility.FromJson<RecipesRoot>(json); }
            catch (Exception e)
            {
                _loadErrors.Add($"Recipes parse failed: {e.Message}");
                return;
            }

            var arr = root != null ? root.recipes : null;
            if (arr == null || arr.Length == 0)
            {
                _loadErrors.Add("Recipes JSON missing/empty 'recipes' array");
                return;
            }

            int added = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < arr.Length; i++)
            {
                var rj = arr[i];
                if (rj == null) continue;

                var id = (rj.id ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id)) { _loadErrors.Add($"Recipes[{i}] has empty id"); continue; }
                if (!seen.Add(id)) _loadErrors.Add($"Duplicate Recipe id '{id}' in Recipes.json");

                var def = new RecipeDef
                {
                    DefId = id,
                    InputType = ParseResourceType(rj.inputType, $"Recipe '{id}' inputType"),
                    InputAmount = Mathf.Max(1, rj.inputAmount),
                    OutputType = ParseResourceType(rj.outputType, $"Recipe '{id}' outputType"),
                    OutputAmount = Mathf.Max(1, rj.outputAmount),
                    ExtraInputs = ToCosts(rj.extraInputs, ctx: $"Recipe '{id}' extraInputs"),
                    CraftTimeSec = Mathf.Max(0f, rj.craftTimeSec)
                };

                _recipes[id] = def;
                added++;
            }

            Debug.Log($"[DataRegistry] Loaded Recipes: {added} (TextAsset: {ta.name})");
        }

        private void LoadWaves(TextAsset ta)
        {
            if (ta == null)
            {
                Debug.LogWarning("[DataRegistry] Waves TextAsset is null (DefsCatalog.Waves). Using empty Wave defs.");
                return;
            }

            var json = ta.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                _loadErrors.Add($"Waves TextAsset '{ta.name}' is empty");
                return;
            }

            WavesRoot root;
            try { root = JsonUtility.FromJson<WavesRoot>(json); }
            catch (Exception e)
            {
                _loadErrors.Add($"Waves parse failed: {e.Message}");
                return;
            }

            var arr = root != null ? root.waves : null;
            if (arr == null || arr.Length == 0)
            {
                _loadErrors.Add("Waves JSON missing/empty 'waves' array");
                return;
            }

            int added = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < arr.Length; i++)
            {
                var wj = arr[i];
                if (wj == null) continue;

                var id = (wj.id ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id)) { _loadErrors.Add($"Waves[{i}] has empty id"); continue; }
                if (!seen.Add(id)) _loadErrors.Add($"Duplicate Wave id '{id}' in Waves.json");

                var entries = ToWaveEntries(wj.entries, ctx: $"Wave '{id}' entries");

                var def = new WaveDef
                {
                    DefId = id,
                    WaveIndex = wj.waveIndex,
                    Year = Mathf.Max(1, wj.year),
                    Season = ParseSeason(wj.season, ctx: $"Wave '{id}' season"),
                    Day = Mathf.Max(1, wj.day),
                    IsBoss = wj.isBoss,
                    Entries = entries
                };

                _waves[id] = def;
                added++;
            }

            Debug.Log($"[DataRegistry] Loaded Waves: {added} (TextAsset: {ta.name})");
        }

        private void LoadRewards(TextAsset ta)
        {
            if (ta == null)
            {
                Debug.LogWarning("[DataRegistry] Rewards TextAsset is null (DefsCatalog.Rewards). Using empty Reward defs.");
                return;
            }

            var json = ta.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                _loadErrors.Add($"Rewards TextAsset '{ta.name}' is empty");
                return;
            }

            RewardsRoot root;
            try { root = JsonUtility.FromJson<RewardsRoot>(json); }
            catch (Exception e)
            {
                _loadErrors.Add($"Rewards parse failed: {e.Message}");
                return;
            }

            var arr = root != null ? root.rewards : null;
            if (arr == null || arr.Length == 0)
            {
                _loadErrors.Add("Rewards JSON missing/empty 'rewards' array");
                return;
            }

            int added = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < arr.Length; i++)
            {
                var rj = arr[i];
                if (rj == null) continue;

                var id = (rj.id ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id)) { _loadErrors.Add($"Rewards[{i}] has empty id"); continue; }
                if (!seen.Add(id)) _loadErrors.Add($"Duplicate Reward id '{id}' in Rewards.json");

                var def = new RewardDef
                {
                    DefId = id,
                    Title = (rj.title ?? string.Empty).Trim()
                };

                _rewards[id] = def;
                added++;
            }

            Debug.Log($"[DataRegistry] Loaded Rewards: {added} (TextAsset: {ta.name})");
        }

        // --- IDataRegistry contract (Part25) ---
        public T GetDef<T>(string id) where T : UnityEngine.Object
        {
            throw new KeyNotFoundException($"No ScriptableObject def for type {typeof(T).Name} id='{id}'.");
        }

        public bool TryGetDef<T>(string id, out T def) where T : UnityEngine.Object
        {
            def = null;
            return false;
        }

        public BuildingDef GetBuilding(string id)
        {
            if (_buildings.TryGetValue(id, out var v)) return v;
            throw new KeyNotFoundException($"BuildingDef not found: '{id}'");
        }

        public EnemyDef GetEnemy(string id)
        {
            if (_enemies.TryGetValue(id, out var v)) return v;
            throw new KeyNotFoundException($"EnemyDef not found: '{id}'");
        }

        public WaveDef GetWave(string id)
        {
            if (_waves.TryGetValue(id, out var v)) return v;
            throw new KeyNotFoundException($"WaveDef not found: '{id}'");
        }

        public RewardDef GetReward(string id)
        {
            if (_rewards.TryGetValue(id, out var v)) return v;
            throw new KeyNotFoundException($"RewardDef not found: '{id}'");
        }

        public RecipeDef GetRecipe(string id)
        {
            if (_recipes.TryGetValue(id, out var v)) return v;
            throw new KeyNotFoundException($"RecipeDef not found: '{id}'");
        }

        public NpcDef GetNpc(string id)
        {
            if (_npcs.TryGetValue(id, out var v)) return v;
            throw new KeyNotFoundException($"NpcDef not found: '{id}'");
        }

        public TowerDef GetTower(string id)
        {
            if (_towers.TryGetValue(id, out var v)) return v;
            throw new KeyNotFoundException($"TowerDef not found: '{id}'");
        }

        // --- Helpers ---
        private static StorageCapsByLevel ToCaps(StorageCapsJson j)
        {
            if (j == null) return default;
            return new StorageCapsByLevel { L1 = j.L1, L2 = j.L2, L3 = j.L3 };
        }

        private static WorkRoleFlags ParseWorkRolesOrDerive(BuildingJson bj)
        {
            if (bj != null && bj.workRoles != null && bj.workRoles.Length > 0)
            {
                WorkRoleFlags f = WorkRoleFlags.None;
                for (int i = 0; i < bj.workRoles.Length; i++)
                {
                    var s = (bj.workRoles[i] ?? string.Empty).Trim();
                    if (s.Length == 0) continue;

                    if (string.Equals(s, "Harvest", StringComparison.OrdinalIgnoreCase)) f |= WorkRoleFlags.Harvest;
                    else if (string.Equals(s, "HaulBasic", StringComparison.OrdinalIgnoreCase)) f |= WorkRoleFlags.HaulBasic;
                    else if (string.Equals(s, "Build", StringComparison.OrdinalIgnoreCase)) f |= WorkRoleFlags.Build;
                    else if (string.Equals(s, "Craft", StringComparison.OrdinalIgnoreCase)) f |= WorkRoleFlags.Craft;
                    else if (string.Equals(s, "Armory", StringComparison.OrdinalIgnoreCase)) f |= WorkRoleFlags.Armory;
                }

                return f;
            }

            WorkRoleFlags roles = WorkRoleFlags.None;
            if (bj == null) return roles;

            if (bj.isProducer) roles |= WorkRoleFlags.Harvest;
            if (bj.isWarehouse) roles |= WorkRoleFlags.HaulBasic;
            if (bj.isHQ) roles |= (WorkRoleFlags.Build | WorkRoleFlags.HaulBasic);
            if (bj.isForge) roles |= WorkRoleFlags.Craft;
            if (bj.isArmory) roles |= WorkRoleFlags.Armory;

            return roles;
        }

        private Season ParseSeason(string s, string ctx)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                _loadErrors.Add($"{ctx}: empty season");
                return Season.Spring;
            }

            if (Enum.TryParse<Season>(s.Trim(), ignoreCase: true, out var season))
                return season;

            _loadErrors.Add($"{ctx}: invalid season '{s}'");
            return Season.Spring;
        }

        private ResourceType ParseResourceType(int v, string ctx)
        {
            if (v < 0 || v > 4)
            {
                _loadErrors.Add($"{ctx}: invalid resource enum value {v}");
                return ResourceType.Wood;
            }

            return (ResourceType)v;
        }

        private CostDef[] ToCosts(BuildingCostJson[] arr, string ctx)
        {
            if (arr == null || arr.Length == 0) return null;

            var outArr = new CostDef[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                var c = arr[i] ?? new BuildingCostJson();
                var res = ParseResourceType(c.res, $"{ctx}[{i}].res");
                outArr[i] = new CostDef { Resource = res, Amount = Mathf.Max(0, c.amt) };
            }
            return outArr;
        }

        private CostDef[] ToCosts(TowerCostJson[] arr, string ctx)
        {
            if (arr == null || arr.Length == 0) return null;

            var outArr = new CostDef[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                var c = arr[i] ?? new TowerCostJson();
                var res = ParseResourceType(c.res, $"{ctx}[{i}].res");
                outArr[i] = new CostDef { Resource = res, Amount = Mathf.Max(0, c.amt) };
            }
            return outArr;
        }

        private CostDef[] ToCosts(RecipeCostJson[] arr, string ctx)
        {
            if (arr == null || arr.Length == 0) return null;

            var outArr = new CostDef[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                var c = arr[i] ?? new RecipeCostJson();
                var res = ParseResourceType(c.type, $"{ctx}[{i}].type");
                outArr[i] = new CostDef { Resource = res, Amount = Mathf.Max(0, c.amount) };
            }
            return outArr;
        }

        private UnlockDef ToUnlock(TowerUnlockJson u, string ctx)
        {
            if (u == null)
            {
                _loadErrors.Add($"{ctx}: missing unlock object");
                return default;
            }

            return new UnlockDef
            {
                Year = Mathf.Max(1, u.year),
                Season = ParseSeason(u.season, ctx + ".season"),
                Day = Mathf.Max(1, u.day)
            };
        }

        private WaveEntryDef[] ToWaveEntries(WaveEntryJson[] arr, string ctx)
        {
            if (arr == null || arr.Length == 0)
            {
                _loadErrors.Add($"{ctx}: empty entries array");
                return Array.Empty<WaveEntryDef>();
            }

            var outArr = new WaveEntryDef[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                var e = arr[i];
                var enemyId = (e != null ? e.enemyId : null) ?? string.Empty;
                enemyId = enemyId.Trim();
                if (enemyId.Length == 0)
                    _loadErrors.Add($"{ctx}[{i}] has empty enemyId");

                outArr[i] = new WaveEntryDef { EnemyId = enemyId, Count = Mathf.Max(1, e != null ? e.count : 1) };
            }
            return outArr;
        }

        private void ValidateTowers()
        {
            foreach (var kv in _towers)
            {
                var id = kv.Key;
                var d = kv.Value;

                if (d.Rof <= 0f)
                    _loadErrors.Add($"Tower '{id}': rof<=0 (shots/sec). Must be > 0.");

                if (d.AmmoMax < 0)
                    _loadErrors.Add($"Tower '{id}': ammoMax<0");

                if (d.AmmoMax == 0 && d.AmmoPerShot != 0)
                    _loadErrors.Add($"Tower '{id}': ammoMax=0 but ammoPerShot!=0 (expected 0).");

                if (d.AmmoMax > 0 && d.AmmoPerShot <= 0)
                    _loadErrors.Add($"Tower '{id}': ammoMax>0 but ammoPerShot<=0");

                if (d.AmmoMax > 0 && d.AmmoPerShot > d.AmmoMax)
                    _loadErrors.Add($"Tower '{id}': ammoPerShot({d.AmmoPerShot}) > ammoMax({d.AmmoMax})");

                if (d.NeedsAmmoThresholdPct <= 0f || d.NeedsAmmoThresholdPct > 1f)
                    _loadErrors.Add($"Tower '{id}': needsAmmoThresholdPct out of range (0..1]");
            }
        }
    }
}
