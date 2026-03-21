namespace SeasonalBastion
{
    public sealed partial class DataRegistry
    {
        private void ValidateTowers()
        {
            foreach (var kv in _towers)
            {
                var id = kv.Key;
                var d = kv.Value;

                if (d.Rof <= 0f)
                    _loadErrors.Add($"Tower '{id}': rof<=0 (shots/sec). Must be > 0.");

                if (d.AmmoMax < 0)
                    _loadErrors.Add($"Tower '{id}': ammoMax<0");

                if (d.AmmoMax == 0 && d.AmmoPerShot != 0)
                    _loadErrors.Add($"Tower '{id}': ammoMax=0 but ammoPerShot!=0 (expected 0).");

                if (d.AmmoMax > 0 && d.AmmoPerShot <= 0)
                    _loadErrors.Add($"Tower '{id}': ammoMax>0 but ammoPerShot<=0");

                if (d.AmmoMax > 0 && d.AmmoPerShot > d.AmmoMax)
                    _loadErrors.Add($"Tower '{id}': ammoPerShot({d.AmmoPerShot}) > ammoMax({d.AmmoMax})");

                if (d.NeedsAmmoThresholdPct <= 0f || d.NeedsAmmoThresholdPct > 1f)
                    _loadErrors.Add($"Tower '{id}': needsAmmoThresholdPct out of range (0..1]");
            }
        }
    }
}
