// Deprecated shim kept intentionally for any still-uncommitted local branches.
// Runtime code in repo should use DefIdTierUtil from Game.Core instead.
using System;

namespace SeasonalBastion
{
    [Obsolete("Use DefIdTierUtil in Game.Core instead.")]
    internal static class JobDefIdUtil
    {
        internal static string NormalizeBuildingDefId(string defId) => DefIdTierUtil.BaseId(defId?.Trim());
        internal static bool EqualsCanonical(string defId, string canonicalBaseId) => DefIdTierUtil.IsBase(defId, canonicalBaseId?.Trim());
    }
}
