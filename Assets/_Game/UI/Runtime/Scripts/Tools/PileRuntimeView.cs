using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    /// <summary>
    /// M4: Render resource piles on the grid (Wood/Food).
    /// Poll-based (no event bus) for simplicity; deterministic state is in WorldState.Piles.
    /// </summary>
    public sealed class PileRuntimeView : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private float _refreshInterval = 0.2f;

        [Header("Sorting")]
        [SerializeField] private int _sortingOrder = 3;

        private GameServices _s;
        private WorldSelectionController _sel;

        private float _t;

        // pileId.Value -> renderer
        private readonly Dictionary<int, SpriteRenderer> _nodes = new();

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
            if (_s.WorldState == null || _s.WorldState.Piles == null) return;

            _t += Time.unscaledDeltaTime;
            if (_t < _refreshInterval) return;
            _t = 0f;

            RebuildAll();
        }

        private void EnsureSprite()
        {
            if (_sprite != null) return;

            // Create a simple 1x1 white sprite
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, true);

            _sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private void RebuildAll()
        {
            var piles = _s.WorldState.Piles;

            // Mark all existing as stale
            var alive = new HashSet<int>();

            foreach (var pid in piles.Ids)
            {
                int key = pid.Value;
                alive.Add(key);

                var st = piles.Get(pid);

                if (!_nodes.TryGetValue(key, out var sr) || sr == null)
                {
                    sr = CreateNode(key);
                    _nodes[key] = sr;
                }

                // position & appearance
                sr.transform.position = _sel.CellToWorldCenter(st.Cell);
                sr.sortingOrder = _sortingOrder;

                // Color by resource (simple)
                sr.color = (st.Resource == ResourceType.Wood) ? new Color(0.55f, 0.35f, 0.2f, 1f)
                         : (st.Resource == ResourceType.Food) ? new Color(0.2f, 0.7f, 0.25f, 1f)
                         : Color.gray;

                // Scale by amount a bit (optional, helps visibility)
                float s = 0.35f + Mathf.Clamp(st.Amount, 0, 40) / 80f; // 0.35..0.85
                sr.transform.localScale = new Vector3(s, s, 1f);
            }

            // Remove dead
            RemoveMissing(alive);
        }

        private SpriteRenderer CreateNode(int key)
        {
            var go = new GameObject($"Pile_{key}");
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite;
            sr.sortingOrder = _sortingOrder;
            sr.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            return sr;
        }

        private void RemoveMissing(HashSet<int> alive)
        {
            // collect to avoid modifying dictionary during enumeration
            var toRemove = ListPool<int>.Get();
            foreach (var kv in _nodes)
            {
                if (!alive.Contains(kv.Key))
                    toRemove.Add(kv.Key);
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                int key = toRemove[i];
                if (_nodes.TryGetValue(key, out var sr) && sr != null)
                    Destroy(sr.gameObject);

                _nodes.Remove(key);
            }

            ListPool<int>.Release(toRemove);
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

        // Small local pool to reduce GC (no dependency on debug tools)
        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> _pool = new();

            public static List<T> Get() => _pool.Count > 0 ? _pool.Pop() : new List<T>(64);

            public static void Release(List<T> list)
            {
                list.Clear();
                _pool.Push(list);
            }
        }
    }
}
