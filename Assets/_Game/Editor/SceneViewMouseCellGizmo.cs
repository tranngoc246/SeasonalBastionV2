#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using SeasonalBastion.Contracts;
using SeasonalBastion.DebugTools;

namespace SeasonalBastion.EditorTools
{
    [InitializeOnLoad]
    public static class SceneViewMouseCellGizmo
    {
        private static bool _enabled = true;

        // Fallback mapping nếu không có DebugBuildingTool
        private static Vector3 _gridOrigin = new Vector3(-9, -5, 0);
        private static float _cellSize = 1f;
        private static bool _useXZ = false;
        private static float _planeY = 0f;

        static SceneViewMouseCellGizmo()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        [MenuItem("Tools/SeasonalBastion/Toggle Mouse Cell Gizmo %#g")] // Ctrl+Shift+G
        private static void Toggle()
        {
            _enabled = !_enabled;
            SceneView.RepaintAll();
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (!_enabled) return;
            var e = Event.current;
            if (e == null) return;

            // 1) Nếu đang Play: ưu tiên data từ GameView mouse (bridge)
            if (Application.isPlaying && MouseCellSharedState.HasValue)
            {
                float age = Time.realtimeSinceStartup - MouseCellSharedState.LastUpdateRealtime;
                if (age < 0.25f)
                {
                    DrawCell(MouseCellSharedState.Cell, MouseCellSharedState.CellCenterWorld);
                    view.Repaint();
                    return;
                }
            }

            // 2) Nếu không Play (hoặc data stale): fallback hover theo chuột SceneView
            PullMappingFromBuildingTool();

            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.Repaint)
            {
                if (TryGetCellFromSceneMouse(e.mousePosition, out var cell, out var center))
                {
                    DrawCell(cell, center);
                    if (e.type != EventType.Repaint) view.Repaint();
                }
            }
        }

        private static void PullMappingFromBuildingTool()
        {
            var tool = Object.FindObjectOfType<DebugBuildingTool>();
            if (tool == null) return;

            _gridOrigin = tool.GridOrigin;
            _cellSize = Mathf.Max(0.0001f, tool.CellSize);
            _useXZ = tool.UseXZ;
            _planeY = tool.PlaneY;
        }

        private static bool TryGetCellFromSceneMouse(Vector2 guiMousePos, out CellPos cell, out Vector3 center)
        {
            cell = default;
            center = default;

            Ray ray = HandleUtility.GUIPointToWorldRay(guiMousePos);
            Plane plane = _useXZ
                ? new Plane(Vector3.up, new Vector3(0f, _planeY, 0f))
                : new Plane(Vector3.forward, Vector3.zero);

            if (!plane.Raycast(ray, out float enter)) return false;

            Vector3 hit = ray.GetPoint(enter);
            Vector3 local = hit - _gridOrigin;

            int x = Mathf.FloorToInt(local.x / _cellSize);
            int y = _useXZ ? Mathf.FloorToInt(local.z / _cellSize) : Mathf.FloorToInt(local.y / _cellSize);

            cell = new CellPos(x, y);

            center = _useXZ
                ? _gridOrigin + new Vector3((x + 0.5f) * _cellSize, _planeY, (y + 0.5f) * _cellSize)
                : _gridOrigin + new Vector3((x + 0.5f) * _cellSize, (y + 0.5f) * _cellSize, 0f);

            return true;
        }

        private static void DrawCell(CellPos cell, Vector3 center)
        {
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            // wire square
            Vector3 size = new Vector3(1f, 0.02f, 1f); // will be scaled by Handles matrix below

            // scale to cellSize without keeping extra state
            Handles.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_cellSize, 1f, _cellSize));

            // draw at scaled space center
            Vector3 scaledCenter = new Vector3(center.x / _cellSize, center.y, center.z / _cellSize);
            Handles.DrawWireCube(scaledCenter, size);

            Handles.matrix = Matrix4x4.identity;

            Handles.Label(center + Vector3.up * 0.06f, $"Cell ({cell.X},{cell.Y})");
        }
    }
}
#endif
