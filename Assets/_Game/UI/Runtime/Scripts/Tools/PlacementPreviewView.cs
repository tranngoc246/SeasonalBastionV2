using SeasonalBastion.Contracts;
using System.Collections.Generic;
using UnityEngine;

namespace SeasonalBastion
{
    public sealed class PlacementPreviewView : MonoBehaviour
    {
        [Header("Optional: if null, will auto-generate a 1x1 white sprite at runtime")]
        [SerializeField] private Sprite _cellSprite;

        [SerializeField] private float _alpha = 0.55f;
        [SerializeField] private int _sortingOrderBase = 5000;

        private readonly List<SpriteRenderer> _pool = new();
        private readonly List<SpriteRenderer> _active = new();

        private SpriteRenderer _driveway;

        private Color _valid;
        private Color _invalid;
        private Color _drive;

        private void Awake()
        {
            EnsureCellSprite();

            _valid = new Color(1f, 1f, 0f, _alpha);       // vàng
            _invalid = new Color(1f, 0.25f, 0.25f, _alpha);
            _drive = new Color(1f, 0.6f, 0f, _alpha);     // cam
        }

        public void Clear()
        {
            ReturnAllActiveToPool();
            if (_driveway != null) _driveway.gameObject.SetActive(false);
        }

        public void ShowCells(WorldSelectionController mapper, List<CellPos> cells, bool ok, CellPos suggestedRoadCell)
        {
            if (mapper == null || cells == null) return;

            EnsureCellSprite();
            ReturnAllActiveToPool();

            var col = ok ? _valid : _invalid;

            float cellSize = mapper.CellSize; // bạn đã có property này trong WorldSelectionController
            var scale = new Vector3(cellSize, cellSize, 1f);

            for (int i = 0; i < cells.Count; i++)
            {
                var sr = GetFromPool();
                sr.sprite = _cellSprite;
                sr.color = col;
                sr.transform.position = mapper.CellToWorldCenter(cells[i]);
                sr.transform.localScale = scale;
                sr.sortingOrder = _sortingOrderBase;

                sr.gameObject.SetActive(true);
                _active.Add(sr);
            }

            // driveway marker (1 cái duy nhất)
            EnsureDriveway();

            // Nếu project bạn có "invalid" cell, bạn thay check ở đây cho đúng.
            // Hiện tại: luôn show driveway nếu được truyền vào.
            _driveway.sprite = _cellSprite;
            _driveway.color = _drive;
            _driveway.transform.position = mapper.CellToWorldCenter(suggestedRoadCell);
            _driveway.transform.localScale = scale;
            _driveway.sortingOrder = _sortingOrderBase + 1;
            _driveway.gameObject.SetActive(true);
        }

        private void ReturnAllActiveToPool()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var sr = _active[i];
                if (sr == null) continue;
                sr.gameObject.SetActive(false);
                _pool.Add(sr);
            }
            _active.Clear();
        }

        private SpriteRenderer GetFromPool()
        {
            if (_pool.Count > 0)
            {
                var last = _pool[^1];
                _pool.RemoveAt(_pool.Count - 1);
                return last;
            }

            var go = new GameObject("PreviewCell");
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _cellSprite;
            sr.sortingOrder = _sortingOrderBase;
            return sr;
        }

        private void EnsureDriveway()
        {
            if (_driveway != null) return;

            var go = new GameObject("DrivewayCell");
            go.transform.SetParent(transform, false);

            _driveway = go.AddComponent<SpriteRenderer>();
            _driveway.sprite = _cellSprite;
            _driveway.sortingOrder = _sortingOrderBase + 1;
            _driveway.gameObject.SetActive(false);
        }

        private void EnsureCellSprite()
        {
            if (_cellSprite != null) return;

            // Tạo sprite runtime 1x1 trắng (đảm bảo preview luôn thấy, không phụ thuộc asset)
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
