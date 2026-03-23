using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class BuildOrderWorkplaceResolver
    {
        private readonly GameServices _s;
        private readonly List<BuildingId> _buildingIdsBuf = new(128);

        public BuildOrderWorkplaceResolver(GameServices s)
        {
            _s = s;
        }

        public BuildingId ResolveBuildWorkplace()
        {
            if (_s?.WorldState?.Buildings == null || _s.DataRegistry == null)
                return default;

            _buildingIdsBuf.Clear();
            foreach (var id in _s.WorldState.Buildings.Ids) _buildingIdsBuf.Add(id);
            _buildingIdsBuf.Sort((a, b) => a.Value.CompareTo(b.Value));

            // Prefer HQ (constructed)
            for (int i = 0; i < _buildingIdsBuf.Count; i++)
            {
                var bid = _buildingIdsBuf[i];
                if (!_s.WorldState.Buildings.Exists(bid)) continue;

                var bs = _s.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                var def = _s.DataRegistry.GetBuilding(bs.DefId);
                if (def.IsHQ) return bid;
            }

            // Fallback: any workplace with Build role
            for (int i = 0; i < _buildingIdsBuf.Count; i++)
            {
                var bid = _buildingIdsBuf[i];
                if (!_s.WorldState.Buildings.Exists(bid)) continue;

                var bs = _s.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                var def = _s.DataRegistry.GetBuilding(bs.DefId);
                if ((def.WorkRoles & WorkRoleFlags.Build) != 0) return bid;
            }

            return default;
        }
    }
}
