using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion.DebugTools
{
    /// <summary>
    /// Day29: SceneView gizmos for enemies (cell-based).
    /// Attach to any GameObject in scene.
    /// </summary>
    public sealed class DebugEnemyGizmos : MonoBehaviour
    {
        [Header("Mapping")]
        [SerializeField] private Vector3 _gridOrigin = Vector3.zero;
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private bool _useXZ = false;
        [SerializeField] private float _planeY = 0f; // when UseXZ
        [SerializeField] private float _planeZ = 0f; // when XY
        [SerializeField] private float _thin = 0.05f;

        [Header("Visual")]
        [SerializeField] private float _radiusScale = 0.18f;

        private GameBootstrap _boot;

        private void Awake()
        {
            _boot = FindObjectOfType<GameBootstrap>();
        }

        private void OnDrawGizmos()
        {
            if (_boot == null) _boot = FindObjectOfType<GameBootstrap>();
            var s = _boot != null ? _boot.Services : null;
            if (s == null || s.WorldState == null || s.WorldState.Enemies == null) return;

            var enemies = s.WorldState.Enemies;
            if (enemies.Count <= 0) return;

            float cell = Mathf.Max(0.0001f, _cellSize);
            float r = cell * Mathf.Max(0.01f, _radiusScale);

            // Deterministic draw order
            var ids = new List<EnemyId>(enemies.Count);
            foreach (var id in enemies.Ids) ids.Add(id);
            ids.Sort((a, b) => a.Value.CompareTo(b.Value));

            Gizmos.color = Color.red;

            for (int i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (!enemies.Exists(id)) continue;

                var st = enemies.Get(id);
                var center = CellToWorldCenter(st.Cell);
                Gizmos.DrawSphere(center, r);

#if UNITY_EDITOR
                // Optional: draw id
                UnityEditor.Handles.color = Color.white;
                UnityEditor.Handles.Label(center + (_useXZ ? new Vector3(0, _thin, 0) : new Vector3(0, 0, _thin)), $"E#{id.Value}");
#endif
            }
        }

        private Vector3 CellToWorldCenter(CellPos c)
        {
            float wx = _gridOrigin.x + (c.X + 0.5f) * _cellSize;

            if (_useXZ)
            {
                float wy = _planeY + (_thin * 0.5f);
                float wz = _gridOrigin.z + (c.Y + 0.5f) * _cellSize;
                return new Vector3(wx, wy, wz);
            }
            else
            {
                float wy = _gridOrigin.y + (c.Y + 0.5f) * _cellSize;
                return new Vector3(wx, wy, _planeZ);
            }
        }
    }
}
