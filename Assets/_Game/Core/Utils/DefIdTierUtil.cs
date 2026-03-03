using System;

namespace SeasonalBastion
{
    public static class DefIdTierUtil
    {
        // "bld_farmhouse_t2" -> "bld_farmhouse"
        public static string BaseId(string defId)
        {
            if (string.IsNullOrEmpty(defId)) return defId;

            int idx = defId.LastIndexOf("_t", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return defId;

            int p = idx + 2;
            if (p >= defId.Length) return defId;

            // chỉ strip nếu phần sau "_t" toàn là digit
            for (int i = p; i < defId.Length; i++)
            {
                char c = defId[i];
                if (c < '0' || c > '9') return defId;
            }

            return defId.Substring(0, idx);
        }

        public static bool IsBase(string defId, string baseId)
        {
            if (string.IsNullOrEmpty(defId) || string.IsNullOrEmpty(baseId)) return false;
            return string.Equals(BaseId(defId), baseId, StringComparison.OrdinalIgnoreCase);
        }
    }
}