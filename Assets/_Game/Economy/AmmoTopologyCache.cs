using SeasonalBastion.Contracts;
using System.Collections.Generic;

namespace SeasonalBastion
{
    internal sealed class AmmoTopologyCache
    {
        private readonly AmmoService _owner;

        internal AmmoTopologyCache(AmmoService owner)
        {
            _owner = owner;
        }

        internal void RebuildWorkplaceHasNpcSet() => _owner.RebuildWorkplaceHasNpcSet_Core();
        internal void CleanupDestroyedTowerCaches() => _owner.CleanupDestroyedTowerCaches_Core();
        internal void ScanTowersAndNotify() => _owner.ScanTowersAndNotify_Core();
        internal bool TryPickPreferredHaulerWorkplace(CellPos forgeAnchor, out BuildingId workplace) => _owner.TryPickPreferredHaulerWorkplace_Core(forgeAnchor, out workplace);
        internal bool TryPickNearestWorkplaceFromIndex(IReadOnlyList<BuildingId> list, CellPos from, bool requireNpc, out BuildingId best) => _owner.TryPickNearestWorkplaceFromIndex_Core(list, from, requireNpc, out best);
        internal bool ContainsRequestForTower(List<AmmoRequest> list, TowerId tower) => _owner.ContainsRequestForTower_Core(list, tower);
        internal void ReconcileOutstandingTowerNeeds() => _owner.ReconcileOutstandingTowerNeeds_Core();
    }
}
