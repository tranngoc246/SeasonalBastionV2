using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class AmmoMetricsReporter
    {
        private readonly GameServices _s;
        private AmmoMetricsSnapshot _lastSnapshot;

        internal AmmoMetricsReporter(AmmoService owner)
        {
            _s = owner.Services;
        }

        internal AmmoMetricsSnapshot LastSnapshot => _lastSnapshot;

        internal void UpdateDebugMetrics(int activeResupplyJobs)
        {
            int totalTowers = 0;
            int towersWithoutAmmo = 0;
            int armoryAvailableAmmo = 0;

            var towers = _s.WorldIndex.Towers;
            if (towers != null)
            {
                for (int i = 0; i < towers.Count; i++)
                {
                    var tid = towers[i];
                    if (!_s.WorldState.Towers.Exists(tid)) continue;
                    totalTowers++;
                    var tower = _s.WorldState.Towers.Get(tid);
                    if (tower.Ammo <= 0)
                        towersWithoutAmmo++;
                }
            }

            var armories = _s.WorldIndex.Armories;
            if (armories != null)
            {
                for (int i = 0; i < armories.Count; i++)
                {
                    var armory = armories[i];
                    if (!_s.WorldState.Buildings.Exists(armory)) continue;
                    var st = _s.WorldState.Buildings.Get(armory);
                    if (!st.IsConstructed) continue;
                    armoryAvailableAmmo += Math.Max(0, _s.StorageService.GetAmount(armory, ResourceType.Ammo));
                }
            }

            _lastSnapshot = new AmmoMetricsSnapshot(totalTowers, towersWithoutAmmo, activeResupplyJobs, armoryAvailableAmmo);
        }

        internal void Clear()
        {
            _lastSnapshot = default;
        }
    }
}
