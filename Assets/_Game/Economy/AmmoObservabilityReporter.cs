namespace SeasonalBastion
{
    internal sealed class AmmoObservabilityReporter
    {
        private readonly AmmoObservabilityState _state;
        private readonly System.Func<int> _getPendingRequests;

        internal AmmoObservabilityReporter(AmmoObservabilityState state, System.Func<int> getPendingRequests)
        {
            _state = state;
            _getPendingRequests = getPendingRequests;
        }

        internal void Update(AmmoMetricsSnapshot metrics)
        {
            _state.ArmoryStatus = metrics.ArmoryAvailableAmmo switch
            {
                <= 0 => "Empty",
                >= 200 => "Full",
                _ => "Available"
            };

            if (metrics.TowersWithoutAmmo > 0 && metrics.ActiveResupplyJobs <= 0 && metrics.ArmoryAvailableAmmo <= 0)
                _state.ResupplyStatus = "No ammo source available";
            else if (metrics.TowersWithoutAmmo > 0 && metrics.ActiveResupplyJobs > 0)
                _state.ResupplyStatus = "Resupply job pending";
            else if (metrics.TowersWithoutAmmo > 0 && _getPendingRequests() > 0 && metrics.ActiveResupplyJobs <= 0 && metrics.ArmoryAvailableAmmo > 0)
                _state.ResupplyStatus = "Resupply blocked";
            else
                _state.ResupplyStatus = "Stable";
        }
    }
}
