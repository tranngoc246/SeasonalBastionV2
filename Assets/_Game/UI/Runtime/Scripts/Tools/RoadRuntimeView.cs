using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    /// <summary>
    /// Minimal road visualization without tilemap.
    /// Creates a sprite per road cell.
    /// This is intentionally simple for mini-game slice.
    /// </summary>
    public sealed class RoadRuntimeView : MonoBehaviour
    {
        [Header("Rendering")]
        [SerializeField] private int _sortingOrder = -10;
        [SerializeField] private float _zOffset = 0.02f; // XY: slightly behind previews
        [SerializeField] private float _dirtyRebuildDelay = 0.12f;

        private bool _dirty;
        private float _dirtyAtUnscaled;

        private System.Action<RoadsDirtyEvent> _onDirty;
        private System.Action<RoadPlacedEvent> _onPlaced;

        private GameServices _s;
        private WorldSelectionController _mapper;
        private Transform _root;

        private readonly Dictionary<CellPos, SpriteRenderer> _instances = new();

        private void Update()
        {
            if (!_dirty) return;
            if (Time.unscaledTime - _dirtyAtUnscaled < _dirtyRebuildDelay) return;

            _dirty = false;
            RebuildAll();
        }

        private void MarkDirty()
        {
            _dirty = true;
            _dirtyAtUnscaled = Time.unscaledTime;
        }

        private void EnsureRoot()
        {
            if (_root != null) return;
            var go = new GameObject("__RoadRuntimeView");
            go.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            _root = go.transform;
        }

        public void Bind(GameServices s, WorldSelectionController mapper)
        {
            _s = s;
            _mapper = mapper;

            // subscribe events
            if (_s != null && _s.EventBus != null)
            {
                _onDirty ??= _ => MarkDirty();
                _s.EventBus.Subscribe(_onDirty);

                // Optional: nếu có nơi chỉ publish RoadPlacedEvent mà quên RoadsDirty
                _onPlaced ??= _ => MarkDirty();
                _s.EventBus.Subscribe(_onPlaced);
            }

            RebuildAll();
        }

        public void Unbind()
        {
            if (_s != null && _s.EventBus != null)
            {
                if (_onDirty != null) _s.EventBus.Unsubscribe(_onDirty);
                if (_onPlaced != null) _s.EventBus.Unsubscribe(_onPlaced);
            }

            ClearAll();
            _s = null;
            _mapper = null;
            _dirty = false;
        }

        public void RebuildAll()
        {
            if (_s == null || _s.GridMap == null || _mapper == null) return;

            EnsureRoot();
            ClearAll();

            var map = _s.GridMap;
            var sprite = RuntimeSpriteFactory.WhiteSprite;
            float cellSize = Mathf.Max(0.0001f, _mapper.CellSize);

            // Scan full grid (64x64 = 4096) => OK for init.
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var c = new CellPos(x, y);
                    if (!map.IsRoad(c)) continue;
                    CreateRoadInstance(sprite, cellSize, c);
                }
            }
        }

        public void SetRoad(CellPos cell, bool isRoad)
        {
            if (_mapper == null) return;
            EnsureRoot();

            if (isRoad)
            {
                if (_instances.ContainsKey(cell)) return;
                CreateRoadInstance(RuntimeSpriteFactory.WhiteSprite, Mathf.Max(0.0001f, _mapper.CellSize), cell);
            }
            else
            {
                if (_instances.TryGetValue(cell, out var sr))
                {
                    if (sr != null) Destroy(sr.gameObject);
                    _instances.Remove(cell);
                }
            }
        }

        private void CreateRoadInstance(Sprite sprite, float cellSize, CellPos cell)
        {
            var go = new GameObject($"road_{cell.X}_{cell.Y}");
            go.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            go.transform.SetParent(_root, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(0.60f, 0.60f, 0.60f, 0.35f);
            sr.sortingOrder = _sortingOrder;

            go.transform.localScale = new Vector3(cellSize, cellSize, 1f);

            var pos = _mapper.CellToWorldCenter(cell);
            pos.z += _zOffset;
            go.transform.position = pos;

            _instances[cell] = sr;
        }

        private void ClearAll()
        {
            foreach (var kv in _instances)
            {
                if (kv.Value != null)
                    Destroy(kv.Value.gameObject);
            }
            _instances.Clear();
        }
    }
}
