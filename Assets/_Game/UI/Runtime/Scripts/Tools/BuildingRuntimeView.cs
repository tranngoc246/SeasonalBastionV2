using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    /// <summary>
    /// Minimal runtime renderer for buildings based on WorldState.Buildings (grid-authority).
    /// No prefabs required. Deterministic.
    ///
    /// - Shows 1 rectangle per building scaled by footprint (SizeX/SizeY), positioned at footprint center.
    /// - Color:
    ///   - IsConstructed = true  -> green-ish
    ///   - IsConstructed = false -> orange-ish (site-like)
    /// </summary>
    public sealed class BuildingRuntimeView : MonoBehaviour
    {
        [Header("Sync")]
        [SerializeField] private float _refreshInterval = 0.20f;

        [Header("Rendering")]
        [Tooltip("Optional. If null, auto-generate a 1x1 white sprite at runtime.")]
        [SerializeField] private Sprite _cellSprite;

        [SerializeField] private int _sortingOrder = 1200;

        [SerializeField] private float _alpha = 0.9f;

        private GameServices _s;
        private WorldSelectionController _mapper;

        private float _t;

        // BuildingId.Value -> renderer GO
        private readonly Dictionary<int, SpriteRenderer> _views = new();

        private Color _constructed;
        private Color _site;

        private void Awake()
        {
            EnsureCellSprite();

            _constructed = new Color(0.15f, 0.85f, 0.35f, _alpha);
            _site = new Color(1.00f, 0.55f, 0.10f, _alpha);
        }

        public void Bind(GameServices s, WorldSelectionController mapper)
        {
            _s = s;
            _mapper = mapper;
            _t = 0f;

            // Force first sync
            SyncNow();
        }

        public void Unbind()
        {
            _s = null;
            _mapper = null;
            ClearAll();
        }

        private void Update()
        {
            if (_s == null || _s.WorldState == null || _mapper == null) return;

            _t += Time.unscaledDeltaTime;
            if (_t < _refreshInterval) return;
            _t = 0f;

            SyncNow();
        }

        private void SyncNow()
        {
            if (_s == null || _s.WorldState == null || _mapper == null) return;

            var store = _s.WorldState.Buildings;
            if (store == null) return;

            // Mark all existing views as "potentially stale"
            // We'll remove the ones not seen in this pass.
            _staleKeys.Clear();
            foreach (var k in _views.Keys) _staleKeys.Add(k);

            foreach (var id in store.Ids)
            {
                int key = id.Value;
                _staleKeys.Remove(key);

                var st = store.Get(id);
                EnsureCellSprite();

                // Create or get view
                if (!_views.TryGetValue(key, out var sr) || sr == null)
                {
                    sr = CreateViewGO(key);
                    _views[key] = sr;
                }

                ApplyState(sr, st);
            }

            // Remove stale
            for (int i = 0; i < _staleKeys.Count; i++)
            {
                int dead = _staleKeys[i];
                if (_views.TryGetValue(dead, out var sr) && sr != null)
                {
                    Destroy(sr.gameObject);
                }
                _views.Remove(dead);
            }
        }

        private readonly List<int> _staleKeys = new(256);

        private SpriteRenderer CreateViewGO(int idValue)
        {
            var go = new GameObject($"Building_{idValue}");
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _cellSprite;
            sr.sortingOrder = _sortingOrder;

            return sr;
        }

        private void ApplyState(SpriteRenderer sr, BuildingState st)
        {
            if (sr == null) return;

            // Color
            sr.color = st.IsConstructed ? _constructed : _site;

            // Footprint size from def (preferred). Fallback 1x1 if missing.
            int w = 1, h = 1;
            TryGetFootprint(st.DefId, st.Rotation, out w, out h);

            float cs = _mapper.CellSize;

            // Position: center of footprint
            // anchor is bottom-left cell of footprint
            Vector3 anchorCenter = _mapper.CellToWorldCenter(st.Anchor);

            // center offset in world axes (assume XY plane in this project)
            float offX = (w - 1) * 0.5f * cs;
            float offY = (h - 1) * 0.5f * cs;

            // We can infer up-axis by sampling mapper (works for XY)
            // In your project screenshot it's XY, so this is correct.
            var pos = anchorCenter + new Vector3(offX, offY, 0f);

            sr.transform.position = pos;
            sr.transform.localScale = new Vector3(w * cs, h * cs, 1f);

            sr.gameObject.SetActive(true);
        }

        private bool TryGetFootprint(string defId, Dir4 rot, out int w, out int h)
        {
            w = 1; h = 1;
            if (_s == null || _s.DataRegistry == null) return false;
            if (string.IsNullOrEmpty(defId)) return false;

            try
            {
                var def = _s.DataRegistry.GetBuilding(defId);
                if (def == null) return false;

                int bw = Mathf.Max(1, def.SizeX);
                int bh = Mathf.Max(1, def.SizeY);

                // rotate swaps w/h for E/W if your footprint rotates 90 degrees
                if (rot == Dir4.E || rot == Dir4.W)
                {
                    w = bh;
                    h = bw;
                }
                else
                {
                    w = bw;
                    h = bh;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ClearAll()
        {
            foreach (var kv in _views)
            {
                if (kv.Value != null)
                    Destroy(kv.Value.gameObject);
            }
            _views.Clear();
        }

        private void EnsureCellSprite()
        {
            if (_cellSprite != null) return;

            // runtime 1x1 white sprite
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;

            _cellSprite = Sprite.Create(
                tex,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 1f
            );
        }
    }
}
