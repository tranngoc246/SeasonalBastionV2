using System;

namespace SeasonalBastion
{
    internal static class JobDefIdUtil
    {
        internal static string NormalizeBuildingDefId(string defId)
        {
            if (string.IsNullOrWhiteSpace(defId)) return string.Empty;

            defId = defId.Trim();

            if (defId.EndsWith("_t1", StringComparison.OrdinalIgnoreCase)
                || defId.EndsWith("_t2", StringComparison.OrdinalIgnoreCase)
                || defId.EndsWith("_t3", StringComparison.OrdinalIgnoreCase))
            {
                return defId.Substring(0, defId.Length - 3);
            }

            return defId;
        }

        internal static bool EqualsCanonical(string defId, string canonicalBaseId)
        {
            if (string.IsNullOrWhiteSpace(canonicalBaseId)) return false;
            return string.Equals(
                NormalizeBuildingDefId(defId),
                canonicalBaseId.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
