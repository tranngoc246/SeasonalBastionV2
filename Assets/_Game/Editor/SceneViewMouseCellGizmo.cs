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

        static SceneViewMouseCellGizmo()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        [MenuItem("Tools/Seasonal Bastion/Toggle Mouse Cell Gizmo %#g")]
        private static void Toggle()
        {
            _enabled = !_enabled;
            SceneView.RepaintAll();
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (!_enabled) return;

            var tool = Object.FindObjectOfType<DebugBuildingTool>();
            if (tool == null) return;

            float cellSize = Mathf.Max(0.0001f, tool.CellSize);
            bool useXZ = tool.UseXZ;
            Vector3 origin = tool.GridOrigin;
            float planeY = tool.PlaneY;
            float planeZ = tool.PlaneZ;

            if (Application.isPlaying && MouseCellSharedState.HasValue)
            {
                float age = Time.realtimeSinceStartup - MouseCellSharedState.LastUpdateRealtime;
                if (age < 0.25f)
                {
                    DrawCell(MouseCellSharedState.Cell, MouseCellSharedState.CellCenterWorld, cellSize, useXZ);
                    view.Repaint();
                    return;
                }
            }

            var e = Event.current;
            if (e == null) return;

            if (e.type != EventType.MouseMove && e.type != EventType.MouseDrag && e.type != EventType.Repaint)
                return;

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Plane plane = useXZ
                ? new Plane(Vector3.up, new Vector3(0f, planeY, 0f))
                : new Plane(Vector3.forward, new Vector3(0f, 0f, planeZ));

            if (!plane.Raycast(ray, out float enter)) return;

            Vector3 hit = ray.GetPoint(enter);
            Vector3 local = hit - origin;

            int x = Mathf.FloorToInt(local.x / cellSize);
            int y = useXZ ? Mathf.FloorToInt(local.z / cellSize) : Mathf.FloorToInt(local.y / cellSize);

            var cell = new CellPos(x, y);

            Vector3 center = useXZ
                ? origin + new Vector3((x + 0.5f) * cellSize, planeY, (y + 0.5f) * cellSize)
                : origin + new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, planeZ);

            DrawCell(cell, center, cellSize, useXZ);
            if (e.type != EventType.Repaint) view.Repaint();
        }

        private static void DrawCell(CellPos cell, Vector3 center, float cellSize, bool useXZ)
        {
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            Vector3 size = useXZ
                ? new Vector3(cellSize, 0.02f, cellSize)
                : new Vector3(cellSize, cellSize, 0.02f);

            Handles.DrawWireCube(center, size);
            Handles.Label(center + Vector3.up * 0.06f, $"Cell ({cell.X},{cell.Y})");

            float r = cellSize * 0.15f;
            Handles.DrawLine(center + new Vector3(-r, 0f, 0f), center + new Vector3(r, 0f, 0f));
            if (useXZ)
                Handles.DrawLine(center + new Vector3(0f, 0f, -r), center + new Vector3(0f, 0f, r));
            else
                Handles.DrawLine(center + new Vector3(0f, -r, 0f), center + new Vector3(0f, r, 0f));
        }
    }
}
#endif
