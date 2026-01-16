// PATCH v0.1.1 — DataRegistry implements Part25 IDataRegistry
using System.Collections.Generic;
using UnityEngine;
using SeasonalBastion.Contracts;

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

        public DataRegistry(DefsCatalog catalog)
        {
            _catalog = catalog;
            // TODO(v0.1): parse _catalog.* TextAssets and fill maps.
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

        // Helper for later boot load (optional)
        public void ClearAll()
        {
            _buildings.Clear();
            _enemies.Clear();
            _waves.Clear();
            _rewards.Clear();
            _recipes.Clear();
        }
    }
}
