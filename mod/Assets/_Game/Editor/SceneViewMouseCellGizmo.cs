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

        private static Vector3 _gridOrigin = Vector3.zero;
        private static float _cellSize = 1f;
        private static bool _useXZ = false;
        private static float _planeY = 0f;
        private static float _planeZ = 0f;

        static SceneViewMouseCellGizmo()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        [MenuItem("Tools/SeasonalBastion/Toggle Mouse Cell Gizmo %#g")]
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

            PullMapping();

            // PlayMode: prefer GameView mouse (bridge)
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

            // EditMode: SceneView mouse
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag || e.type == EventType.Repaint)
            {
                if (TryGetCellFromSceneMouse(e.mousePosition, out var cell, out var center))
                {
                    DrawCell(cell, center);
                    if (e.type != EventType.Repaint) view.Repaint();
                }
            }
        }

        private static void PullMapping()
        {
            var tool = Object.FindObjectOfType<DebugBuildingTool>();
            if (tool == null) return;
            _gridOrigin = tool.GridOrigin;
            _cellSize = Mathf.Max(0.0001f, tool.CellSize);
            _useXZ = tool.UseXZ;
            _planeY = tool.PlaneY;
            _planeZ = tool.PlaneZ;
        }

        private static bool TryGetCellFromSceneMouse(Vector2 guiMousePos, out CellPos cell, out Vector3 center)
        {
            cell = default;
            center = default;

            Ray ray = HandleUtility.GUIPointToWorldRay(guiMousePos);
            Plane plane = _useXZ
                ? new Plane(Vector3.up, new Vector3(0f, _planeY, 0f))
                : new Plane(Vector3.forward, new Vector3(0f, 0f, _planeZ));

            if (!plane.Raycast(ray, out float enter)) return false;

            Vector3 hit = ray.GetPoint(enter);
            Vector3 local = hit - _gridOrigin;

            int x = Mathf.FloorToInt(local.x / _cellSize);
            int y = _useXZ ? Mathf.FloorToInt(local.z / _cellSize) : Mathf.FloorToInt(local.y / _cellSize);
            cell = new CellPos(x, y);

            center = _useXZ
                ? _gridOrigin + new Vector3((x + 0.5f) * _cellSize, _planeY, (y + 0.5f) * _cellSize)
                : _gridOrigin + new Vector3((x + 0.5f) * _cellSize, (y + 0.5f) * _cellSize, _planeZ);

            return true;
        }

        private static void DrawCell(CellPos cell, Vector3 center)
        {
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            Vector3 size = _useXZ
                ? new Vector3(_cellSize, 0.02f, _cellSize)
                : new Vector3(_cellSize, _cellSize, 0.02f);

            Handles.DrawWireCube(center, size);
            Handles.Label(center + Vector3.up * 0.06f, $"Cell ({cell.X},{cell.Y})");

            float r = _cellSize * 0.15f;
            Handles.DrawLine(center + new Vector3(-r, 0f, 0f), center + new Vector3(r, 0f, 0f));
            if (_useXZ)
                Handles.DrawLine(center + new Vector3(0f, 0f, -r), center + new Vector3(0f, 0f, r));
            else
                Handles.DrawLine(center + new Vector3(0f, -r, 0f), center + new Vector3(0f, r, 0f));
        }
    }
}
#endif
