using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    /// <summary>
    /// M4: Render NPCs as simple sprites moving on the grid.
    /// Uses npcState.Cell (deterministic). If later you have world-pos float, you can swap.
    /// </summary>
    public sealed class NpcRuntimeView : MonoBehaviour
    {
        [Header("Sorting")]
        [SerializeField] private int _sortingOrder = 20;

        private GameServices _s;
        private WorldSelectionController _sel;

        private readonly Dictionary<int, SpriteRenderer> _nodes = new();
        private Sprite _sprite;

        public void Bind(GameServices s, WorldSelectionController sel)
        {
            _s = s;
            _sel = sel;
            EnsureSprite();
            RebuildAll();
        }

        public void Unbind()
        {
            ClearAll();
            _s = null;
            _sel = null;
        }

        private void EnsureSprite()
        {
            if (_sprite != null) return;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, true);

            _sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private void Update()
        {
            if (_s == null || _sel == null) return;
            if (_s.WorldState == null) return;

            // Cheap: rebuild ids occasionally; update positions every frame
            RebuildAll();
            UpdatePositions();
        }

        private void RebuildAll()
        {
            var npcs = _s.WorldState.Npcs;
            if (npcs == null) return;

            var alive = new HashSet<int>();

            foreach (var nid in npcs.Ids)
            {
                int key = nid.Value;
                alive.Add(key);

                if (!_nodes.TryGetValue(key, out var sr) || sr == null)
                {
                    sr = CreateNode(key);
                    _nodes[key] = sr;
                }
            }

            // remove missing
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

        private void UpdatePositions()
        {
            var npcs = _s.WorldState.Npcs;
            foreach (var kv in _nodes)
            {
                var nid = new NpcId(kv.Key);
                if (!npcs.Exists(nid)) continue;

                var st = npcs.Get(nid);

                // Use deterministic grid cell
                kv.Value.transform.position = _sel.CellToWorldCenter(st.Cell);

                kv.Value.sortingOrder = _sortingOrder;
                kv.Value.color = new Color(0.2f, 0.6f, 0.95f, 1f);
                kv.Value.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
            }
        }

        private SpriteRenderer CreateNode(int key)
        {
            var go = new GameObject($"Npc_{key}");
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite;
            sr.sortingOrder = _sortingOrder;
            sr.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
            return sr;
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
