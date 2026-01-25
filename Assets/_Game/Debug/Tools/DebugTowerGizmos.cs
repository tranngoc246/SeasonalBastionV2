using SeasonalBastion.Contracts;
using System.Linq;
using UnityEngine;

namespace SeasonalBastion.DebugTools
{
    /// <summary>
    /// Draw tower markers (TowerStore) as Gizmos in Scene/Game view (enable Gizmos).
    /// XY grid supported via DebugGridUtil (PlaneZ).
    /// </summary>
    [ExecuteAlways]
    public sealed class DebugTowerGizmos : MonoBehaviour
    {
        [Header("Bootstrap (optional auto-find)")]
        [SerializeField] private GameBootstrap _bootstrap;

        [Header("Grid Source (optional)")]
        [SerializeField] private DebugBuildingTool _gridSource;

        [Header("Draw")]
        [SerializeField] private bool _enabled = true;
        [SerializeField] private bool _onlyWhenPlaying = true;
        [SerializeField] private bool _drawLabels = true;
        [SerializeField, Range(0.1f, 1f)] private float _boxScale = 0.85f;

        [SerializeField] private Color _okColor = new Color(0.2f, 0.8f, 1f, 1f);
        [SerializeField] private Color _lowColor = new Color(1f, 0.85f, 0.1f, 1f);
        [SerializeField] private Color _emptyColor = new Color(1f, 0.2f, 0.2f, 1f);

        private DebugGridMap _map;

        private void OnDrawGizmos()
        {
            if (!_enabled) return;
            if (_onlyWhenPlaying && !Application.isPlaying) return;

            if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
            var s = _bootstrap != null ? _bootstrap.Services : null;
            if (s == null || s.WorldState?.Towers == null) return;

            if (_gridSource == null) _gridSource = FindObjectOfType<DebugBuildingTool>(true);

            // Sync grid mapping from DebugBuildingTool (canonical debug grid mapping)
            _map.SyncFrom(_gridSource, thinFallback: 0.06f);

            // If no source, default XY grid at z=0
            if (_gridSource == null)
                _map.Set(Vector3.zero, 1f, useXZ: false, planeY: 0f, planeZ: 0f, thin: 0.06f);

            // Draw by TowerStore ids (or WorldIndex.Towers if you prefer)
            var ids = s.WorldState.Towers.Ids;
            if (ids == null) return;

            foreach (var tid in ids)
            {
                var st = s.WorldState.Towers.Get(tid);

                int cap = st.AmmoCap;
                int cur = st.Ammo;

                int thr = 0;
                if (cap > 0)
                {
                    // Day25 rule: <=25%
                    thr = (cap * 25 + 99) / 100; // ceil
                    if (thr < 1) thr = 1;
                }

                Color col = _okColor;
                if (cap > 0 && cur <= 0) col = _emptyColor;
                else if (cap > 0 && cur <= thr) col = _lowColor;

                Gizmos.color = col;

                Vector3 center = DebugGridUtil.CellToCenter(st.Cell, _map);
                Vector3 size = DebugGridUtil.CellBoxSize(_map, _boxScale);

                Gizmos.DrawWireCube(center, size);

#if UNITY_EDITOR
                if (_drawLabels)
                {
                    UnityEditor.Handles.Label(
                        center + new Vector3(0f, 0.15f, 0f),
                        $"Tower {tid.Value}\nCell({st.Cell.X},{st.Cell.Y})\nAmmo {cur}/{cap}"
                    );
                }
#endif
            }
        }
    }
}
