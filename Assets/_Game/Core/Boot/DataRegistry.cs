using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeasonalBastion
{
    public sealed class DataRegistry : IDataRegistry
    {
        private readonly DefsCatalog _catalog;

        // Minimal in-memory maps (v0.1). Populate later from JSON/TextAssets (Part 1/17).
        private readonly Dictionary<string, BuildingDef> _buildings = new();
        private readonly Dictionary<string, EnemyDef> _enemies = new();
        private readonly Dictionary<string, WaveDef> _waves = new();
        private readonly Dictionary<string, RewardDef> _rewards = new();
        private readonly Dictionary<string, RecipeDef> _recipes = new();

        // --- JSON wrappers (Unity JsonUtility needs top-level object) ---
        [Serializable]
        private sealed class BuildingsRoot
        {
            public BuildingJson[] buildings;
        }

        [Serializable]
        private class StorageCapsJson
        {
            public int L1;
            public int L2;
            public int L3;
        }

        [Serializable]
        private sealed class BuildingJson
        {
            public string id;
            public int sizeX = 1;
            public int sizeY = 1;
            public int baseLevel = 1;

            // Optional explicit workplace roles. If omitted/empty, we derive from tag booleans.
            // Example: ["Harvest"], ["HaulBasic"], ["Build","HaulBasic"].
            public string[] workRoles;

            // Optional tag booleans (default false).
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

        public DataRegistry(DefsCatalog catalog)
        {
            _catalog = catalog;
            // TODO(v0.1): parse _catalog.* TextAssets and fill maps.
            LoadAllFromCatalog(); // Day 4: load Buildings now so Placement can use it immediately
        }

        // --- Public optional helpers for Debug tools / UI later ---
        public IReadOnlyCollection<string> GetAllBuildingIds() => _buildings.Keys;

        public void ClearAll()
        {
            _buildings.Clear();
            _enemies.Clear();
            _waves.Clear();
            _rewards.Clear();
            _recipes.Clear();
        }

        private void LoadAllFromCatalog()
        {
            ClearAll();

            // Day 4 needs Buildings only.
            LoadBuildings(_catalog != null ? _catalog.Buildings : null);
        }

        private void LoadBuildings(TextAsset ta)
        {
            if (ta == null)
            {
                Debug.LogWarning("[DataRegistry] Buildings TextAsset is null (DefsCatalog.Buildings). Building placement will fail.");
                return;
            }

            var json = ta.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[DataRegistry] Buildings TextAsset is empty.");
                return;
            }

            BuildingsRoot root;
            try
            {
                root = JsonUtility.FromJson<BuildingsRoot>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataRegistry] Failed to parse Buildings JSON: {e.Message}");
                return;
            }

            var arr = root != null ? root.buildings : null;
            if (arr == null || arr.Length == 0)
            {
                Debug.LogWarning("[DataRegistry] Buildings JSON parsed, but 'buildings' array is empty/missing.");
                return;
            }

            int added = 0;
            for (int i = 0; i < arr.Length; i++)
            {
                var bj = arr[i];
                if (bj == null) continue;

                var id = (bj.id ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning($"[DataRegistry] Buildings[{i}] has empty id. Skipped.");
                    continue;
                }

                int w = Mathf.Max(1, bj.sizeX);
                int h = Mathf.Max(1, bj.sizeY);
                int lvl = Mathf.Max(1, bj.baseLevel);

                // BuildingDef is a pure data contract type; we only need these fields for Day 4 placement.
                var def = new BuildingDef
                {
                    DefId = id,
                    SizeX = w,
                    SizeY = h,
                    BaseLevel = lvl,
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
                    CapAmmo = ToCaps(bj.capAmmo)
                };

                // If duplicates exist, last wins (deterministic by file order).
                if (_buildings.ContainsKey(id))
                    Debug.LogWarning($"[DataRegistry] Duplicate BuildingDef id='{id}'. Overwriting.");

                _buildings[id] = def;
                added++;
            }

            Debug.Log($"[DataRegistry] Loaded Buildings: {added} (TextAsset: {ta.name})");
        }

        public T GetDef<T>(string id) where T : UnityEngine.Object
        {
            // v0.1: if you later store ScriptableObject defs, you can route here.
            // For now, keep contracts satisfied.
            throw new KeyNotFoundException($"No ScriptableObject def for type {typeof(T).Name} id='{id}'.");
        }

        public bool TryGetDef<T>(string id, out T def) where T : UnityEngine.Object
        {
            def = null;
            return false;
        }

        private static StorageCapsByLevel ToCaps(StorageCapsJson j)
        {
            if (j == null) return default;
            return new StorageCapsByLevel { L1 = j.L1, L2 = j.L2, L3 = j.L3 };
        }

        private static WorkRoleFlags ParseWorkRolesOrDerive(BuildingJson bj)
        {
            // 1) If JSON explicitly specifies roles, trust it.
            if (bj != null && bj.workRoles != null && bj.workRoles.Length > 0)
            {
                WorkRoleFlags f = WorkRoleFlags.None;
                for (int i = 0; i < bj.workRoles.Length; i++)
                {
                    var s = (bj.workRoles[i] ?? string.Empty).Trim();
                    if (s.Length == 0) continue;

                    // Case-insensitive, tolerate minor variants.
                    if (string.Equals(s, "Harvest", StringComparison.OrdinalIgnoreCase)) f |= WorkRoleFlags.Harvest;
                    else if (string.Equals(s, "HaulBasic", StringComparison.OrdinalIgnoreCase)) f |= WorkRoleFlags.HaulBasic;
                    else if (string.Equals(s, "Build", StringComparison.OrdinalIgnoreCase)) f |= WorkRoleFlags.Build;
                    else if (string.Equals(s, "Craft", StringComparison.OrdinalIgnoreCase)) f |= WorkRoleFlags.Craft;
                    else if (string.Equals(s, "Armory", StringComparison.OrdinalIgnoreCase)) f |= WorkRoleFlags.Armory;
                }

                return f;
            }

            // 2) Otherwise, derive from tag booleans (Day13 LOCKED rules).
            WorkRoleFlags roles = WorkRoleFlags.None;
            if (bj == null) return roles;

            if (bj.isProducer) roles |= WorkRoleFlags.Harvest;
            if (bj.isWarehouse) roles |= WorkRoleFlags.HaulBasic;
            if (bj.isHQ) roles |= (WorkRoleFlags.Build | WorkRoleFlags.HaulBasic);
            if (bj.isForge) roles |= WorkRoleFlags.Craft;
            if (bj.isArmory) roles |= WorkRoleFlags.Armory;

            return roles;
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
    }
}
