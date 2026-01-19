using UnityEngine;
using UnityEngine.InputSystem;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.DebugTools
{    
    public sealed class DebugGameViewMouseCellTracker : MonoBehaviour
    {
        [SerializeField] private Camera _cameraOverride;
        [SerializeField] private DebugBuildingTool _mappingSource;

        // fallback nếu mappingSource chưa có
        [SerializeField] private Vector3 _gridOrigin = Vector3.zero;
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private bool _useXZ = false;
        [SerializeField] private float _planeY = 0f;
        [SerializeField] private float _planeZ = 0f;

        private Camera Cam => _cameraOverride != null ? _cameraOverride : Camera.main;

        private void Awake()
        {
            if (_mappingSource == null)
                _mappingSource = FindObjectOfType<DebugBuildingTool>();
        }

        private void OnDisable()
        {
            MouseCellSharedState.Clear();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            // Sync mapping (single source)
            if (_mappingSource == null)
                _mappingSource = FindObjectOfType<DebugBuildingTool>();

            if (_mappingSource != null)
            {
                _gridOrigin = _mappingSource.GridOrigin;
                _cellSize = Mathf.Max(0.0001f, _mappingSource.CellSize);
                _useXZ = _mappingSource.UseXZ;
                _planeY = _mappingSource.PlaneY;
                _planeZ = _mappingSource.PlaneZ;
            }

            var cam = Cam;
            if (cam == null || Mouse.current == null)
            {
                MouseCellSharedState.Clear();
                return;
            }

            Vector2 mouse = Mouse.current.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(mouse);

            Plane plane = _useXZ
                ? new Plane(Vector3.up, new Vector3(0f, _planeY, 0f))
                : new Plane(Vector3.forward, new Vector3(0f, 0f, _planeZ)); // XY at z=planeZ

            if (!plane.Raycast(ray, out float enter))
            {
                MouseCellSharedState.Clear();
                return;
            }

            Vector3 hit = ray.GetPoint(enter);
            Vector3 local = hit - _gridOrigin;

            int x = Mathf.FloorToInt(local.x / _cellSize);
            int y = _useXZ ? Mathf.FloorToInt(local.z / _cellSize) : Mathf.FloorToInt(local.y / _cellSize);

            var cell = new CellPos(x, y);

            Vector3 center = _useXZ
                ? _gridOrigin + new Vector3((x + 0.5f) * _cellSize, _planeY, (y + 0.5f) * _cellSize)
                : _gridOrigin + new Vector3((x + 0.5f) * _cellSize, (y + 0.5f) * _cellSize, _planeZ);

            MouseCellSharedState.Set(cell, center);
        }
    }
}
