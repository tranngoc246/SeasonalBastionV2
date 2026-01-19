using UnityEngine;
using UnityEngine.InputSystem;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.DebugTools
{
    public struct DebugGridMap
    {
        public Vector3 Origin;
        public float CellSize;
        public bool UseXZ;
        public float PlaneY;
        public float PlaneZ;
        public float Thin; // thickness for gizmos (Y for XZ, Z for XY)

        public void Set(Vector3 origin, float cellSize, bool useXZ, float planeY, float planeZ, float thin)
        {
            Origin = origin;
            CellSize = Mathf.Max(0.0001f, cellSize);
            UseXZ = useXZ;
            PlaneY = planeY;
            PlaneZ = planeZ;
            Thin = Mathf.Max(0.0001f, thin);
        }

        public void SyncFrom(DebugBuildingTool src, float thinFallback)
        {
            if (src == null) return;
            Set(src.GridOrigin, src.CellSize, src.UseXZ, src.PlaneY, src.PlaneZ, thinFallback);
        }
    }

    public static class DebugGridUtil
    {
        public static bool TryGetMouseCell(Camera cam, in DebugGridMap m, out CellPos cell, out Vector3 centerWorld)
        {
            cell = default;
            centerWorld = default;

            if (cam == null) return false;
            if (Mouse.current == null) return false;

            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

            Plane plane = m.UseXZ
                ? new Plane(Vector3.up, new Vector3(0f, m.PlaneY, 0f))
                : new Plane(Vector3.forward, new Vector3(0f, 0f, m.PlaneZ)); // XY @ z=PlaneZ

            if (!plane.Raycast(ray, out float enter)) return false;

            Vector3 hit = ray.GetPoint(enter);
            WorldToCell(hit, m, out cell);
            centerWorld = CellToCenter(cell, m);
            return true;
        }

        public static void WorldToCell(Vector3 world, in DebugGridMap m, out CellPos cell)
        {
            Vector3 local = world - m.Origin;
            int x = Mathf.FloorToInt(local.x / m.CellSize);
            int y = m.UseXZ ? Mathf.FloorToInt(local.z / m.CellSize) : Mathf.FloorToInt(local.y / m.CellSize);
            cell = new CellPos(x, y);
        }

        public static Vector3 CellToCenter(CellPos c, in DebugGridMap m)
        {
            float wx = m.Origin.x + (c.X + 0.5f) * m.CellSize;

            if (m.UseXZ)
            {
                float wy = m.PlaneY + (m.Thin * 0.5f);
                float wz = m.Origin.z + (c.Y + 0.5f) * m.CellSize;
                return new Vector3(wx, wy, wz);
            }
            else
            {
                float wy = m.Origin.y + (c.Y + 0.5f) * m.CellSize;
                return new Vector3(wx, wy, m.PlaneZ);
            }
        }

        public static Vector3 CellBoxSize(in DebugGridMap m, float scale01)
        {
            float s = Mathf.Clamp01(scale01) * m.CellSize;
            // XZ: thin on Y, XY: thin on Z
            return m.UseXZ ? new Vector3(s, m.Thin, s) : new Vector3(s, s, m.Thin);
        }
    }
}
