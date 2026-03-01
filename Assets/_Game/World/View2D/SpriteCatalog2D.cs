using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeasonalBastion.View2D
{
    [CreateAssetMenu(menuName = "SeasonalBastion/View2D Sprite Catalog", fileName = "SpriteCatalog2D")]
    public sealed class SpriteCatalog2D : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string Id;
            public Sprite Sprite;
        }

        [Header("Buildings: key = BuildingDefId")]
        public Entry[] Buildings;

        [Header("NPC: key = NpcDefId")]
        public Entry[] Npcs;

        [Header("Enemy: key = EnemyDefId")]
        public Entry[] Enemies;

        private Dictionary<string, Sprite> _b;
        private Dictionary<string, Sprite> _n;
        private Dictionary<string, Sprite> _e;

        private void OnEnable()
        {
            _b = BuildMap(Buildings);
            _n = BuildMap(Npcs);
            _e = BuildMap(Enemies);
        }

        private static Dictionary<string, Sprite> BuildMap(Entry[] arr)
        {
            var map = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            if (arr == null) return map;

            for (int i = 0; i < arr.Length; i++)
            {
                var id = arr[i].Id;
                var sp = arr[i].Sprite;
                if (string.IsNullOrEmpty(id) || sp == null) continue;
                map[id] = sp;
            }

            return map;
        }

        public bool TryGetBuilding(string defId, out Sprite sp)
        {
            sp = null;
            if (_b == null || string.IsNullOrEmpty(defId)) return false;
            return _b.TryGetValue(defId, out sp) && sp != null;
        }

        public bool TryGetNpc(string defId, out Sprite sp)
        {
            sp = null;
            if (_n == null || string.IsNullOrEmpty(defId)) return false;
            return _n.TryGetValue(defId, out sp) && sp != null;
        }

        public bool TryGetEnemy(string defId, out Sprite sp)
        {
            sp = null;
            if (_e == null || string.IsNullOrEmpty(defId)) return false;
            return _e.TryGetValue(defId, out sp) && sp != null;
        }
    }
}
