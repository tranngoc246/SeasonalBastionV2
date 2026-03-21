namespace SeasonalBastion.RunStart
{
    internal static class RunStartConfigValidator
    {
        public static bool Validate(GameServices s, StartMapConfigDto cfg, out string error)
        {
            error = null;
            if (s == null) { error = "services=null"; return false; }
            if (cfg == null || cfg.map == null) { error = "StartMapConfig missing map"; return false; }

            if (s.GridMap != null)
            {
                if (cfg.map.width != s.GridMap.Width || cfg.map.height != s.GridMap.Height)
                {
                    error = $"StartMapConfig map size {cfg.map.width}x{cfg.map.height} != GridMap {s.GridMap.Width}x{s.GridMap.Height}";
                    return false;
                }
            }

            return ValidateStartMapHeader(cfg, out error);
        }

        internal static bool ValidateStartMapHeader(StartMapConfigDto cfg, out string error)
        {
            error = null;

            if (cfg.schemaVersion != 1)
            {
                error = $"StartMapConfig schemaVersion={cfg.schemaVersion} unsupported (expect 1).";
                return false;
            }

            if (cfg.coordSystem == null)
            {
                error = "StartMapConfig missing coordSystem.";
                return false;
            }

            if (!string.Equals(cfg.coordSystem.origin, "bottom-left", System.StringComparison.OrdinalIgnoreCase))
            {
                error = $"coordSystem.origin='{cfg.coordSystem.origin}' (expect 'bottom-left').";
                return false;
            }

            if (!string.Equals(cfg.coordSystem.indexing, "0-based", System.StringComparison.OrdinalIgnoreCase))
            {
                error = $"coordSystem.indexing='{cfg.coordSystem.indexing}' (expect '0-based').";
                return false;
            }

            if (cfg.lockedInvariants == null || cfg.lockedInvariants.Length == 0)
            {
                error = "StartMapConfig missing lockedInvariants (expect non-empty).";
                return false;
            }

            return true;
        }
    }
}
