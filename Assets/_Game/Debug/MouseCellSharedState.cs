using UnityEngine;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.DebugTools
{    
    public static class MouseCellSharedState
    {
        public static bool HasValue { get; private set; }
        public static CellPos Cell { get; private set; }
        public static Vector3 CellCenterWorld { get; private set; }
        public static float LastUpdateRealtime { get; private set; }

        public static void Set(CellPos cell, Vector3 centerWorld)
        {
            HasValue = true;
            Cell = cell;
            CellCenterWorld = centerWorld;
            LastUpdateRealtime = Time.realtimeSinceStartup;
        }

        public static void Clear()
        {
            HasValue = false;
            Cell = default;
            CellCenterWorld = default;
            LastUpdateRealtime = 0f;
        }
    }
}
