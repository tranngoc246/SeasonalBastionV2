using SeasonalBastion.Contracts;
using System.Collections.Generic;

namespace SeasonalBastion
{
    internal sealed class TowerResupplyPlanner
    {
        private readonly AmmoService _owner;

        internal TowerResupplyPlanner(AmmoService owner)
        {
            _owner = owner;
        }

        internal void CleanupResupplyTowerInFlight() => _owner.CleanupResupplyTowerInFlight_Core();
        internal void EnsureResupplyTowerJobs() => _owner.EnsureResupplyTowerJobs_Core();
        internal bool TryCreateNextResupplyTowerJob() => _owner.TryCreateNextResupplyTowerJob_Core();
        internal bool TryPickBestResupplySource(TowerState towerState, out BuildingId source, out BuildingState sourceState, out int availableAmmo) => _owner.TryPickBestResupplySource_Core(towerState, out source, out sourceState, out availableAmmo);
        internal void EvaluateResupplySources(IReadOnlyList<BuildingId> candidates, CellPos targetCell, int rank, ref BuildingId bestSource, ref BuildingState bestState, ref int bestAmmo, ref int bestRank, ref int bestDist, ref int bestId) => _owner.EvaluateResupplySources_Core(candidates, targetCell, rank, ref bestSource, ref bestState, ref bestAmmo, ref bestRank, ref bestDist, ref bestId);
        internal void LogPotentialResupplyDeadlock() => _owner.LogPotentialResupplyDeadlock_Core();
        internal void UpdateDebugMetrics() => _owner.UpdateDebugMetrics_Core();
    }
}
