using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class BuildOrderWorkplaceResolver : IBuildWorkplaceResolver
    {
        private readonly GameServices _s;
        private readonly IJobWorkplacePolicy _workplacePolicy;
        private readonly List<BuildingId> _buildingIdsBuf = new(128);

        public BuildOrderWorkplaceResolver(GameServices s)
        {
            _s = s;
            _workplacePolicy = s?.JobWorkplacePolicy;
        }

        public BuildingId ResolveBuildWorkplace()
        {
            if (_s?.Balance != null)
            {
                var balanced = _s.Balance.ResolveBuilderWorkplace();
                if (balanced.Value != 0)
                    return balanced;
            }

            if (_s?.WorldState?.Buildings == null || _s.DataRegistry == null)
                return default;

            _buildingIdsBuf.Clear();
            foreach (var id in _s.WorldState.Buildings.Ids) _buildingIdsBuf.Add(id);
            _buildingIdsBuf.Sort((a, b) => a.Value.CompareTo(b.Value));

            // Prefer dedicated build workplaces first (e.g. Builder Hut).
            // HQ should only act as fallback when no constructed Build-role workplace exists.
            for (int i = 0; i < _buildingIdsBuf.Count; i++)
            {
                var bid = _buildingIdsBuf[i];
                if (!_s.WorldState.Buildings.Exists(bid)) continue;

                var bs = _s.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                bool hasBuildRole = _workplacePolicy != null
                    ? _workplacePolicy.HasRole(bs.DefId, WorkRoleFlags.Build)
                    : (_s.DataRegistry.GetBuilding(bs.DefId).WorkRoles & WorkRoleFlags.Build) != 0;

                bool isHq = _s.DataRegistry.TryGetBuilding(bs.DefId, out var def) && def != null && def.IsHQ;
                if (hasBuildRole && !isHq)
                    return bid;
            }

            // Fallback: HQ can service build/repair work if no Builder Hut-style workplace exists.
            for (int i = 0; i < _buildingIdsBuf.Count; i++)
            {
                var bid = _buildingIdsBuf[i];
                if (!_s.WorldState.Buildings.Exists(bid)) continue;

                var bs = _s.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                bool isHq = _s.DataRegistry.TryGetBuilding(bs.DefId, out var def) && def != null && def.IsHQ;
                if (isHq) return bid;
            }

            // Last fallback: any remaining Build-role workplace (covers unusual data setups).
            for (int i = 0; i < _buildingIdsBuf.Count; i++)
            {
                var bid = _buildingIdsBuf[i];
                if (!_s.WorldState.Buildings.Exists(bid)) continue;

                var bs = _s.WorldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                bool hasBuildRole = _workplacePolicy != null
                    ? _workplacePolicy.HasRole(bs.DefId, WorkRoleFlags.Build)
                    : (_s.DataRegistry.GetBuilding(bs.DefId).WorkRoles & WorkRoleFlags.Build) != 0;
                if (hasBuildRole) return bid;
            }

            return default;
        }
    }
}
