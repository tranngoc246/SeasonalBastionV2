using UnityEngine;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.DebugTools
{
    public static class MouseCellSharedState
    {
        public static bool HasValue;
        public static CellPos Cell;
        public static Vector3 CellCenterWorld;
        public static float LastUpdateRealtime;
    }
}
