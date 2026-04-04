using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class AmmoMetricsReporter
    {
        private readonly AmmoService _owner;
        private readonly GameServices _s;

        internal AmmoMetricsReporter(AmmoService owner)
        {
            _owner = owner;
            _s = owner.Services;
        }

        internal void UpdateDebugMetrics()
        {
            _owner.Debug_TotalTowers = 0;
            _owner.Debug_TowersWithoutAmmo = 0;
            _owner.Debug_ArmoryAvailableAmmo = 0;
            _owner.Debug_ActiveResupplyJobs = _owner.CountTrackedActiveResupplyJobs_Core();

            var towers = _s.WorldIndex.Towers;
            if (towers != null)
            {
                for (int i = 0; i < towers.Count; i++)
                {
                    var tid = towers[i];
                    if (!_s.WorldState.Towers.Exists(tid)) continue;
                    _owner.Debug_TotalTowers++;
                    var tower = _s.WorldState.Towers.Get(tid);
                    if (tower.Ammo <= 0)
                        _owner.Debug_TowersWithoutAmmo++;
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
                    _owner.Debug_ArmoryAvailableAmmo += Math.Max(0, _s.StorageService.GetAmount(armory, ResourceType.Ammo));
                }
            }
        }
    }
}
