using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// Derived lists for quick queries (Warehouses/Producers/Houses/Forges/Armories/Towers).
    /// Deterministic ordering: lists are kept sorted by id.Value (ascending).
    /// </summary>
    public sealed class WorldIndexService : IWorldIndex
    {
        private readonly IWorldState _w;
        private readonly IDataRegistry _data;

        private readonly List<BuildingId> _warehouses = new();
        private readonly List<BuildingId> _producers = new();
        private readonly List<BuildingId> _houses = new();
        private readonly List<BuildingId> _forges = new();
        private readonly List<BuildingId> _armories = new();
        private readonly List<TowerId> _towers = new();

        // Idempotency guards (avoid duplicates if hooked via both direct call + event bus)
        private readonly HashSet<int> _warehousesSet = new();
        private readonly HashSet<int> _producersSet = new();
        private readonly HashSet<int> _housesSet = new();
        private readonly HashSet<int> _forgesSet = new();
        private readonly HashSet<int> _armoriesSet = new();
        private readonly HashSet<int> _towersSet = new();

        public IReadOnlyList<BuildingId> Warehouses => _warehouses;
        public IReadOnlyList<BuildingId> Producers => _producers;
        public IReadOnlyList<BuildingId> Houses => _houses;
        public IReadOnlyList<BuildingId> Forges => _forges;
        public IReadOnlyList<BuildingId> Armories => _armories;
        public IReadOnlyList<TowerId> Towers => _towers;

        public WorldIndexService(IWorldState w, IDataRegistry data)
        {
            _w = w;
            _data = data;
        }

        public void RebuildAll()
        {
            ClearAllLists();

            // Buildings: sort by id.Value for deterministic order (EntityStore uses Dictionary)
            var buildingIds = new List<BuildingId>();
            foreach (var bid in _w.Buildings.Ids) buildingIds.Add(bid);
            buildingIds.Sort((a, b) => a.Value.CompareTo(b.Value));

            for (int i = 0; i < buildingIds.Count; i++)
                OnBuildingCreated(buildingIds[i]);

            // Towers: sort for determinism
            var towerIds = new List<TowerId>();
            foreach (var tid in _w.Towers.Ids) towerIds.Add(tid);
            towerIds.Sort((a, b) => a.Value.CompareTo(b.Value));

            for (int i = 0; i < towerIds.Count; i++)
                AddUniqueSorted(_towers, _towersSet, towerIds[i].Value, towerIds[i]);
        }

        public void OnBuildingCreated(BuildingId id)
        {
            if (!_w.Buildings.Exists(id)) return;

            var st = _w.Buildings.Get(id);

            // Index only constructed buildings (construction sites / placeholders are excluded).
            if (!st.IsConstructed) return;

            BuildingDef def;
            try { def = _data.GetBuilding(st.DefId); }
            catch { return; }

            // Resolve classification (prefer explicit tags; fallback by def id for VS#1).
            ResolveTags(st.DefId, def,
                out bool isHQ,
                out bool isWarehouse,
                out bool isProducer,
                out bool IsHouse,
                out bool isForge,
                out bool isArmory,
                out bool isTower);

            // VS#1: treat HQ as a warehouse destination (no separate HQ list in IWorldIndex).
            if (isHQ) isWarehouse = true;

            if (isWarehouse) AddUniqueSorted(_warehouses, _warehousesSet, id.Value, id);
            if (isProducer) AddUniqueSorted(_producers, _producersSet, id.Value, id);
            if (IsHouse) AddUniqueSorted(_houses, _housesSet, id.Value, id);
            if (isProducer) AddUniqueSorted(_producers, _producersSet, id.Value, id);
            if (isForge) AddUniqueSorted(_forges, _forgesSet, id.Value, id);
            if (isArmory) AddUniqueSorted(_armories, _armoriesSet, id.Value, id);

            // Towers are separate store (TowerId). Ignore building.IsTower here in v0.1.
        }

        public void OnBuildingDestroyed(BuildingId id)
        {
            Remove(_warehouses, _warehousesSet, id.Value);
            Remove(_producers, _producersSet, id.Value);
            Remove(_houses, _housesSet, id.Value);
            Remove(_forges, _forgesSet, id.Value);
            Remove(_armories, _armoriesSet, id.Value);
        }

        private void ClearAllLists()
        {
            _warehouses.Clear(); _warehousesSet.Clear();
            _producers.Clear(); _producersSet.Clear();
            _houses.Clear(); _housesSet.Clear();
            _forges.Clear(); _forgesSet.Clear();
            _armories.Clear(); _armoriesSet.Clear();
            _towers.Clear(); _towersSet.Clear();
        }

        private static void AddUniqueSorted(List<BuildingId> list, HashSet<int> set, int key, BuildingId id)
        {
            if (!set.Add(key)) return;
            // keep list sorted by id.Value (ascending)
            int idx = list.BinarySearch(id, BuildingIdComparer.Instance);
            if (idx < 0) idx = ~idx;
            list.Insert(idx, id);
        }

        private static void AddUniqueSorted(List<TowerId> list, HashSet<int> set, int key, TowerId id)
        {
            if (!set.Add(key)) return;
            int idx = list.BinarySearch(id, TowerIdComparer.Instance);
            if (idx < 0) idx = ~idx;
            list.Insert(idx, id);
        }

        private static void Remove(List<BuildingId> list, HashSet<int> set, int key)
        {
            if (!set.Remove(key)) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Value == key)
                {
                    list.RemoveAt(i);
                    return;
                }
            }
        }

        private static void ResolveTags(
            string defId,
            BuildingDef def,
            out bool isHQ,
            out bool isWarehouse,
            out bool isProducer,
            out bool IsHouse,
            out bool isForge,
            out bool isArmory,
            out bool isTower)
        {
            // Prefer explicit JSON tags if present.
            bool anyTagged = def.IsHQ || def.IsWarehouse || def.IsProducer || def.IsHouse || def.IsForge || def.IsArmory || def.IsTower;

            if (anyTagged)
            {
                isHQ = def.IsHQ;
                isWarehouse = def.IsWarehouse;
                isProducer = def.IsProducer;
                IsHouse = def.IsHouse;
                isForge = def.IsForge;
                isArmory = def.IsArmory;
                isTower = def.IsTower;
                return;
            }

            // VS#1 fallback by known ids (keeps game playable even if defs are minimal).
            isHQ = string.Equals(defId, "HQ", StringComparison.OrdinalIgnoreCase);
            isWarehouse = string.Equals(defId, "Warehouse", StringComparison.OrdinalIgnoreCase);
            isProducer = string.Equals(defId, "Farm", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(defId, "Lumber", StringComparison.OrdinalIgnoreCase);
            IsHouse = string.Equals(defId, "House", StringComparison.OrdinalIgnoreCase);
            isForge = false;
            isArmory = false;
            isTower = false;
        }

        private sealed class BuildingIdComparer : IComparer<BuildingId>
        {
            public static readonly BuildingIdComparer Instance = new();
            public int Compare(BuildingId x, BuildingId y) => x.Value.CompareTo(y.Value);
        }

        private sealed class TowerIdComparer : IComparer<TowerId>
        {
            public static readonly TowerIdComparer Instance = new();
            public int Compare(TowerId x, TowerId y) => x.Value.CompareTo(y.Value);
        }
    }
}
