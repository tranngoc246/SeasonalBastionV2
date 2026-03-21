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
    public sealed partial class DataRegistry : IDataRegistry
    {
        private readonly DefsCatalog _catalog;

        private readonly Dictionary<string, BuildingDef> _buildings = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, NpcDef> _npcs = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TowerDef> _towers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EnemyDef> _enemies = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, WaveDef> _waves = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RewardDef> _rewards = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RecipeDef> _recipes = new(StringComparer.OrdinalIgnoreCase);
        // Upgrade Graph (Node/Edge)
        private readonly Dictionary<string, BuildableNodeDef> _buildableNodes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, UpgradeEdgeDef> _upgradeEdgesById = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<UpgradeEdgeDef>> _upgradeEdgesFrom = new(StringComparer.OrdinalIgnoreCase);

        private readonly List<string> _loadErrors = new(32);

        private BalanceConfig _balance;
        public BalanceConfig GetBalanceOrNull() => _balance;

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
        [Serializable] private sealed class BuildablesGraphRoot { public BuildableNodeJson[] nodes; public UpgradeEdgeJson[] upgrades; }
        [Serializable] private sealed class BuildableNodeJson { public string id; public int level = 1; public bool placeable = true; }
        [Serializable] private sealed class UpgradeEdgeJson { public string id; public string from; public string to; public BuildingCostJson[] cost; public int workChunks = 0; public string requiresUnlocked; }

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
        public IReadOnlyCollection<string> GetAllBuildableNodeIds() => _buildableNodes.Keys;
        public IReadOnlyCollection<string> GetAllUpgradeEdgeIds() => _upgradeEdgesById.Keys;
        public IReadOnlyList<string> GetLoadErrors() => _loadErrors;

        public bool TryGetBuildableNode(string id, out BuildableNodeDef node)
        {
            node = null;
            if (string.IsNullOrWhiteSpace(id)) return false;
            return _buildableNodes.TryGetValue(id, out node) && node != null;
        }

        public IReadOnlyList<UpgradeEdgeDef> GetUpgradeEdgesFrom(string fromNodeId)
        {
            if (string.IsNullOrWhiteSpace(fromNodeId)) return Array.Empty<UpgradeEdgeDef>();
            if (_upgradeEdgesFrom.TryGetValue(fromNodeId, out var list) && list != null)
                return list;
            return Array.Empty<UpgradeEdgeDef>();
        }

        public bool TryGetUpgradeEdge(string edgeId, out UpgradeEdgeDef edge)
        {
            edge = null;
            if (string.IsNullOrWhiteSpace(edgeId)) return false;
            return _upgradeEdgesById.TryGetValue(edgeId, out edge) && edge != null;
        }

        // UI helper: node c� trong graph => theo Placeable; legacy => true
        public bool IsPlaceableBuildable(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId)) return false;
            if (_buildableNodes.TryGetValue(nodeId, out var n) && n != null)
                return n.Placeable;
            return true;
        }

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
            _buildableNodes.Clear();
            _upgradeEdgesById.Clear();
            _upgradeEdgesFrom.Clear();
            _balance = null;
            _loadErrors.Clear();
        }

        private void LoadAllFromCatalog()
        {
            DataRegistryLoader.LoadAll(this, _catalog);
        }

        internal void ReportLoadErrorsIfAny()
        {
            if (_loadErrors.Count > 0)
                Debug.LogError($"[DataRegistry] Load finished with {_loadErrors.Count} error(s). Use DebugHUDHub -> Validate Data to inspect.");
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
            if (v < 0 || v > 5)
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
    }
}

