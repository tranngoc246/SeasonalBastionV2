using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion.DebugTools
{
    /// <summary>
    /// Day30: Tower combat gizmos:
    /// - Draw tower range (wire sphere)
    /// - Draw line to current deterministic target (nearest in range, tie by EnemyId)
    /// This recomputes selection for visualization (no dependency on TowerCombatSystem internals).
    /// </summary>
    public sealed class DebugTowerCombatGizmos : MonoBehaviour
    {
        [Header("Mapping")]
        [SerializeField] private Vector3 _gridOrigin = Vector3.zero;
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private bool _useXZ = true;
        [SerializeField] private float _planeY = 0f;
        [SerializeField] private float _planeZ = 0f;

        private GameBootstrap _boot;
        private readonly List<TowerId> _towers = new(64);
        private readonly List<EnemyId> _enemies = new(128);
        private readonly List<BuildingId> _buildings = new(64);

        private void Awake()
        {
            _boot = FindObjectOfType<GameBootstrap>();
        }

        private void OnDrawGizmos()
        {
            if (_boot == null) _boot = FindObjectOfType<GameBootstrap>();
            var s = _boot != null ? _boot.Services : null;
            if (s == null || s.WorldState == null || s.DataRegistry == null) return;

            var w = s.WorldState;
            var data = s.DataRegistry;

            if (w.Towers == null || w.Enemies == null || w.Buildings == null) return;
            if (w.Towers.Count <= 0)
            {
                // No TowerStore entries -> nothing to draw (avoid silent fail)
                // Keep this as a warning in-editor so you know why gizmos missing.
#if UNITY_EDITOR
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(_gridOrigin, 0.1f);
#endif
                return;
            }

            // deterministic lists
            _towers.Clear();
            foreach (var id in w.Towers.Ids) _towers.Add(id);
            _towers.Sort((a, b) => a.Value.CompareTo(b.Value));

            _enemies.Clear();
            foreach (var id in w.Enemies.Ids) _enemies.Add(id);
            _enemies.Sort((a, b) => a.Value.CompareTo(b.Value));

            for (int i = 0; i < _towers.Count; i++)
            {
                var tid = _towers[i];
                if (!w.Towers.Exists(tid)) continue;

                var t = w.Towers.Get(tid);

                if (!TryResolveTowerDefId(s, t.Cell, out var towerDefId))
                    towerDefId = "bld_tower_arrow_t1";

                TowerDef tdef = null;
                try { tdef = data.GetTower(towerDefId); }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DebugTowerCombatGizmos] Failed to resolve tower def '{towerDefId}': {ex}");
                }

                if (tdef == null) continue;

                var center = CellToWorldCenter(t.Cell);
                float range = Mathf.Max(0f, tdef.Range);
                float rWorld = range * Mathf.Max(0.0001f, _cellSize);

                // Range ring
                Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
                Gizmos.DrawWireSphere(center, rWorld);

                // Target line (deterministic)
                if (TryPickTargetInRange(t.Cell, tdef.Range, w, out var eid))
                {
                    var e = w.Enemies.Get(eid);
                    var ec = CellToWorldCenter(e.Cell);

                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(center, ec);
                    Gizmos.DrawSphere(ec, _cellSize * 0.10f);
                }
            }
        }

        private bool TryPickTargetInRange(CellPos towerCell, float range, IWorldState w, out EnemyId best)
        {
            best = default;
            if (w == null || w.Enemies == null) return false;
            if (_enemies.Count == 0) return false;

            float r = Mathf.Max(0f, range);
            float r2 = r * r;

            int bestD2 = int.MaxValue;
            int bestId = int.MaxValue;
            bool found = false;

            for (int i = 0; i < _enemies.Count; i++)
            {
                var eid = _enemies[i];
                if (!w.Enemies.Exists(eid)) continue;
                var e = w.Enemies.Get(eid);
                if (e.Hp <= 0) continue;

                int dx = e.Cell.X - towerCell.X;
                int dy = e.Cell.Y - towerCell.Y;
                int d2 = dx * dx + dy * dy;

                if (d2 > r2) continue;

                int idv = eid.Value;
                if (d2 < bestD2 || (d2 == bestD2 && idv < bestId))
                {
                    bestD2 = d2;
                    bestId = idv;
                    best = eid;
                    found = true;
                }
            }

            return found;
        }

        private bool TryResolveTowerDefId(GameServices s, CellPos towerAnchor, out string towerDefId)
        {
            towerDefId = null;
            var w = s.WorldState;
            var data = s.DataRegistry;
            if (w == null || data == null || w.Buildings == null) return false;

            _buildings.Clear();
            foreach (var bid in w.Buildings.Ids) _buildings.Add(bid);
            _buildings.Sort((a, b) => a.Value.CompareTo(b.Value));

            for (int i = 0; i < _buildings.Count; i++)
            {
                var bid = _buildings[i];
                if (!w.Buildings.Exists(bid)) continue;
                var b = w.Buildings.Get(bid);
                if (!b.IsConstructed) continue;
                if (b.Anchor.X != towerAnchor.X || b.Anchor.Y != towerAnchor.Y) continue;

                if (data.TryGetBuilding(b.DefId, out var bdef) && bdef != null && bdef.IsTower)
                {
                    towerDefId = b.DefId;
                    return true;
                }
            }

            return false;
        }

        private Vector3 CellToWorldCenter(CellPos c)
        {
            float cell = Mathf.Max(0.0001f, _cellSize);
            float wx = _gridOrigin.x + (c.X + 0.5f) * cell;

            if (_useXZ)
            {
                float wz = _gridOrigin.z + (c.Y + 0.5f) * cell;
                return new Vector3(wx, _planeY, wz);
            }
            else
            {
                float wy = _gridOrigin.y + (c.Y + 0.5f) * cell;
                return new Vector3(wx, wy, _planeZ);
            }
        }
    }
}
