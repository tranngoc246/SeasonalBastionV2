using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class BuildOrderWorkplaceResolver : IBuildWorkplaceResolver
    {
        private readonly BalanceService _balance;
        private readonly IWorldState _worldState;
        private readonly IDataRegistry _dataRegistry;
        private readonly IJobWorkplacePolicy _workplacePolicy;
        private readonly List<BuildingId> _buildingIdsBuf = new(128);

        public BuildOrderWorkplaceResolver(BalanceService balance, IWorldState worldState, IDataRegistry dataRegistry, IJobWorkplacePolicy workplacePolicy)
        {
            _balance = balance;
            _worldState = worldState;
            _dataRegistry = dataRegistry;
            _workplacePolicy = workplacePolicy;
        }

        public BuildingId ResolveBuildWorkplace()
        {
            if (_balance != null)
            {
                var balanced = _balance.ResolveBuilderWorkplace();
                if (balanced.Value != 0)
                    return balanced;
            }

            if (_worldState?.Buildings == null || _dataRegistry == null)
                return default;

            _buildingIdsBuf.Clear();
            foreach (var id in _worldState.Buildings.Ids)
                _buildingIdsBuf.Add(id);
            _buildingIdsBuf.Sort((a, b) => a.Value.CompareTo(b.Value));

            // Prefer dedicated build workplaces first (e.g. Builder Hut).
            // HQ should only act as fallback when no constructed Build-role workplace exists.
            for (int i = 0; i < _buildingIdsBuf.Count; i++)
            {
                var bid = _buildingIdsBuf[i];
                if (!_worldState.Buildings.Exists(bid)) continue;

                var bs = _worldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                bool hasBuildRole = _workplacePolicy != null
                    ? _workplacePolicy.HasRole(bs.DefId, WorkRoleFlags.Build)
                    : (_dataRegistry.GetBuilding(bs.DefId).WorkRoles & WorkRoleFlags.Build) != 0;

                bool isHq = _dataRegistry.TryGetBuilding(bs.DefId, out var def) && def != null && def.IsHQ;
                if (hasBuildRole && !isHq)
                    return bid;
            }

            // Fallback: HQ can service build/repair work if no Builder Hut-style workplace exists.
            for (int i = 0; i < _buildingIdsBuf.Count; i++)
            {
                var bid = _buildingIdsBuf[i];
                if (!_worldState.Buildings.Exists(bid)) continue;

                var bs = _worldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                bool isHq = _dataRegistry.TryGetBuilding(bs.DefId, out var def) && def != null && def.IsHQ;
                if (isHq) return bid;
            }

            // Last fallback: any remaining Build-role workplace (covers unusual data setups).
            for (int i = 0; i < _buildingIdsBuf.Count; i++)
            {
                var bid = _buildingIdsBuf[i];
                if (!_worldState.Buildings.Exists(bid)) continue;

                var bs = _worldState.Buildings.Get(bid);
                if (!bs.IsConstructed) continue;

                bool hasBuildRole = _workplacePolicy != null
                    ? _workplacePolicy.HasRole(bs.DefId, WorkRoleFlags.Build)
                    : (_dataRegistry.GetBuilding(bs.DefId).WorkRoles & WorkRoleFlags.Build) != 0;
                if (hasBuildRole) return bid;
            }

            return default;
        }
    }
}
