using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class AmmoTowerStateTracker
    {
        private readonly Dictionary<int, int> _lastAmmoByTower = new();
        private readonly Dictionary<int, int> _lastCapByTower = new();
        private readonly Dictionary<int, byte> _lastStateByTower = new();
        private readonly HashSet<int> _towerNeedLogged = new();

        internal Dictionary<int, int> LastAmmoByTower => _lastAmmoByTower;
        internal Dictionary<int, int> LastCapByTower => _lastCapByTower;

        internal void RecordSnapshot(TowerId towerId, int ammo, int cap)
        {
            if (towerId.Value == 0) return;
            _lastAmmoByTower[towerId.Value] = ammo;
            _lastCapByTower[towerId.Value] = cap;
        }

        internal bool MatchesSnapshot(TowerId towerId, int ammo, int cap)
        {
            return _lastAmmoByTower.TryGetValue(towerId.Value, out var lastAmmo)
                && _lastCapByTower.TryGetValue(towerId.Value, out var lastCap)
                && lastAmmo == ammo
                && lastCap == cap;
        }

        internal void SetState(int towerId, byte state)
        {
            if (towerId == 0) return;
            _lastStateByTower[towerId] = state;
        }

        internal bool TryMarkNeedLogged(int towerId)
        {
            return towerId != 0 && _towerNeedLogged.Add(towerId);
        }

        internal void ClearNeedLogged(int towerId)
        {
            if (towerId == 0) return;
            _towerNeedLogged.Remove(towerId);
        }

        internal void RemoveTower(int towerId)
        {
            if (towerId == 0) return;
            _lastAmmoByTower.Remove(towerId);
            _lastCapByTower.Remove(towerId);
            _lastStateByTower.Remove(towerId);
            _towerNeedLogged.Remove(towerId);
        }

        internal void Clear()
        {
            _lastAmmoByTower.Clear();
            _lastCapByTower.Clear();
            _lastStateByTower.Clear();
            _towerNeedLogged.Clear();
        }
    }
}
