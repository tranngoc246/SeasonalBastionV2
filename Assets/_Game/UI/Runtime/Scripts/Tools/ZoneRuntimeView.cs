using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    /// <summary>
    /// Debug/runtime overlay for Zones (resource work areas).
    /// Reads deterministic state from WorldState.Zones (ZoneStore).
    /// Poll-based for simplicity (MinimalShip).
    /// </summary>
    public sealed class ZoneRuntimeView : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private float _refreshInterval = 0.5f;

        [Header("Visual")]
        [Range(0f, 1f)]
        [SerializeField] private float _alpha = 0.18f;

        [SerializeField] private float _scale = 0.95f;

        [Header("Sorting")]
        // BuildingRuntimeView is 1200 in your project.
        // Zone should normally be below buildings, but above ground/roads.
        [SerializeField] private int _sortingOrder = 1150;

        private GameServices _s;
        private WorldSelectionController _sel;
        private float _t;

        // key(long) -> renderer
        private readonly Dictionary<long, SpriteRenderer> _nodes = new();
        private Sprite _sprite;

        public void Bind(GameServices s, WorldSelectionController sel)
        {
            _s = s;
            _sel = sel;
            _t = 0f;

            EnsureSprite();
            RebuildAll();
        }

        public void Unbind()
        {
            ClearAll();
            _s = null;
            _sel = null;
        }

        private void Update()
        {
            if (_s == null || _sel == null) return;
            if (_s.WorldState == null || _s.WorldState.Zones == null) return;

            _t += Time.unscaledDeltaTime;
            if (_t < _refreshInterval) return;
            _t = 0f;

            RebuildAll();
        }

        private void EnsureSprite()
        {
            if (_sprite != null) return;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, true);

            _sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private void RebuildAll()
        {
            var zones = _s.WorldState.Zones.Zones;

            var alive = new HashSet<long>();

            for (int zi = 0; zi < zones.Count; zi++)
            {
                var z = zones[zi];
                if (z == null || z.Cells == null) continue;

                var col = ColorFor(z.Resource);
                col.a = _alpha;

                for (int ci = 0; ci < z.Cells.Count; ci++)
                {
                    var c = z.Cells[ci];

                    long key = MakeKey(z.Id, c.X, c.Y);
                    alive.Add(key);

                    if (!_nodes.TryGetValue(key, out var sr) || sr == null)
                    {
                        sr = CreateNode(key);
                        _nodes[key] = sr;
                    }

                    sr.transform.position = _sel.CellToWorldCenter(c);
                    sr.sortingOrder = _sortingOrder;
                    sr.color = col;

                    float s = Mathf.Max(0.05f, _scale);
                    sr.transform.localScale = new Vector3(s, s, 1f);
                }
            }

            RemoveMissing(alive);
        }

        private SpriteRenderer CreateNode(long key)
        {
            var go = new GameObject($"ZoneCell_{key}");
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite;
            sr.sortingOrder = _sortingOrder;

            return sr;
        }

        private void RemoveMissing(HashSet<long> alive)
        {
            var toRemove = ListPool<long>.Get();

            foreach (var kv in _nodes)
            {
                if (!alive.Contains(kv.Key))
                    toRemove.Add(kv.Key);
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                long key = toRemove[i];
                if (_nodes.TryGetValue(key, out var sr) && sr != null)
                    Destroy(sr.gameObject);

                _nodes.Remove(key);
            }

            ListPool<long>.Release(toRemove);
        }

        private void ClearAll()
        {
            foreach (var kv in _nodes)
            {
                if (kv.Value != null)
                    Destroy(kv.Value.gameObject);
            }
            _nodes.Clear();
        }

        private Color ColorFor(ResourceType rt)
        {
            // Keep it readable at low alpha:
            // Wood: brown, Food: green, Stone: gray-blue, Iron: bluish
            return rt switch
            {
                ResourceType.Wood => new Color(0.70f, 0.45f, 0.20f, 1f),
                ResourceType.Food => new Color(0.20f, 0.75f, 0.25f, 1f),
                ResourceType.Stone => new Color(0.55f, 0.65f, 0.75f, 1f),
                ResourceType.Iron => new Color(0.35f, 0.55f, 0.85f, 1f),
                _ => new Color(0.75f, 0.75f, 0.75f, 1f)
            };
        }

        // Stable key per (zoneId, x, y)
        private static long MakeKey(int zoneId, int x, int y)
        {
            unchecked
            {
                long k = zoneId;
                k = (k << 21) ^ x;
                k = (k << 21) ^ y;
                return k;
            }
        }

        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> _pool = new();

            public static List<T> Get() => _pool.Count > 0 ? _pool.Pop() : new List<T>(256);

            public static void Release(List<T> list)
            {
                list.Clear();
                _pool.Push(list);
            }
        }
    }
}
