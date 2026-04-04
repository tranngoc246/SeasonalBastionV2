namespace SeasonalBastion
{
    internal readonly struct AmmoMetricsSnapshot
    {
        internal readonly int TotalTowers;
        internal readonly int TowersWithoutAmmo;
        internal readonly int ActiveResupplyJobs;
        internal readonly int ArmoryAvailableAmmo;

        internal AmmoMetricsSnapshot(int totalTowers, int towersWithoutAmmo, int activeResupplyJobs, int armoryAvailableAmmo)
        {
            TotalTowers = totalTowers;
            TowersWithoutAmmo = towersWithoutAmmo;
            ActiveResupplyJobs = activeResupplyJobs;
            ArmoryAvailableAmmo = armoryAvailableAmmo;
        }
    }
}
