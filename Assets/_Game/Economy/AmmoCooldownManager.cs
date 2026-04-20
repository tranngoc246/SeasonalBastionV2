using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class AmmoCooldownManager
    {
        private readonly AmmoService _owner;
        private readonly Dictionary<int, float> _nextReqLowAt = new();
        private readonly Dictionary<int, float> _nextReqEmptyAt = new();

        internal AmmoCooldownManager(AmmoService owner)
        {
            _owner = owner;
        }

        internal bool TryConsumeRequestCooldown(TowerId tower, AmmoRequestPriority priority)
        {
            if (tower.Value == 0)
                return false;

            int tid = tower.Value;
            float simTime = _owner.SimTime;

            if (priority == AmmoRequestPriority.Urgent)
            {
                if (_nextReqEmptyAt.TryGetValue(tid, out var until) && simTime < until)
                    return false;

                _nextReqEmptyAt[tid] = simTime + _owner.ReqCooldownEmptyValue;
                return true;
            }

            if (_nextReqLowAt.TryGetValue(tid, out var lowUntil) && simTime < lowUntil)
                return false;

            _nextReqLowAt[tid] = simTime + _owner.ReqCooldownLowValue;
            return true;
        }

        internal void ClearTower(int towerId)
        {
            if (towerId == 0)
                return;

            _nextReqLowAt.Remove(towerId);
            _nextReqEmptyAt.Remove(towerId);
        }

        internal void ResetForTower(int towerId)
        {
            if (towerId == 0)
                return;

            _nextReqLowAt.Remove(towerId);
            _nextReqEmptyAt.Remove(towerId);
        }

        internal void ClearAll()
        {
            _nextReqLowAt.Clear();
            _nextReqEmptyAt.Clear();
        }
    }
}
