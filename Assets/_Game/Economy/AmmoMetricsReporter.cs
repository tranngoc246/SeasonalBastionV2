using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class AmmoMetricsReporter
    {
        private readonly IWorldState _worldState;
        private readonly IWorldIndex _worldIndex;
        private readonly IStorageService _storageService;
        private AmmoMetricsSnapshot _lastSnapshot;

        internal AmmoMetricsReporter(IWorldState worldState, IWorldIndex worldIndex, IStorageService storageService)
        {
            _worldState = worldState;
            _worldIndex = worldIndex;
            _storageService = storageService;
        }

        internal AmmoMetricsSnapshot LastSnapshot => _lastSnapshot;

        internal void UpdateDebugMetrics(int activeResupplyJobs)
        {
            int totalTowers = CountTowersWithoutAmmo(out int towersWithoutAmmo);
            int armoryAvailableAmmo = CountArmoryAvailableAmmo();
            _lastSnapshot = new AmmoMetricsSnapshot(totalTowers, towersWithoutAmmo, activeResupplyJobs, armoryAvailableAmmo);
        }

        internal void Clear()
        {
            _lastSnapshot = default;
        }

        private int CountTowersWithoutAmmo(out int towersWithoutAmmo)
        {
            towersWithoutAmmo = 0;
            int totalTowers = 0;

            IReadOnlyList<TowerId> towers = _worldIndex?.Towers;
            if (towers == null || _worldState == null)
                return 0;

            for (int i = 0; i < towers.Count; i++)
            {
                var towerId = towers[i];
                if (!_worldState.Towers.Exists(towerId))
                    continue;

                totalTowers++;
                var tower = _worldState.Towers.Get(towerId);
                if (tower.Ammo <= 0)
                    towersWithoutAmmo++;
            }

            return totalTowers;
        }

        private int CountArmoryAvailableAmmo()
        {
            if (_worldIndex?.Armories == null || _worldState == null || _storageService == null)
                return 0;

            int total = 0;
            var armories = _worldIndex.Armories;
            for (int i = 0; i < armories.Count; i++)
            {
                var armory = armories[i];
                if (!_worldState.Buildings.Exists(armory))
                    continue;

                var building = _worldState.Buildings.Get(armory);
                if (!building.IsConstructed)
                    continue;

                total += Math.Max(0, _storageService.GetAmount(armory, ResourceType.Ammo));
            }

            return total;
        }
    }
}
