using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class EnemyTargetResolver
    {
        private readonly IWorldState _worldState;
        private readonly IDataRegistry _dataRegistry;
        private readonly RunStartRuntime _runStartRuntime;
        private readonly List<BuildingId> _tmpBuildingIds;
        private BuildingId _hqId;
        private bool _hqCached;

        public EnemyTargetResolver(
            IWorldState worldState,
            IDataRegistry dataRegistry,
            RunStartRuntime runStartRuntime,
            List<BuildingId> tmpBuildingIds)
        {
            _worldState = worldState;
            _dataRegistry = dataRegistry;
            _runStartRuntime = runStartRuntime;
            _tmpBuildingIds = tmpBuildingIds;
        }

        public void EnsureHqCached()
        {
            var buildings = _worldState?.Buildings;
            if (buildings == null || _dataRegistry == null)
                return;

            if (_hqCached && _hqId.Value != 0 && buildings.Exists(_hqId))
                return;

            _hqCached = true;
            _hqId = default;

            _tmpBuildingIds.Clear();
            foreach (var buildingId in buildings.Ids)
                _tmpBuildingIds.Add(buildingId);
            _tmpBuildingIds.Sort((a, b) => a.Value.CompareTo(b.Value));

            for (int i = 0; i < _tmpBuildingIds.Count; i++)
            {
                var buildingId = _tmpBuildingIds[i];
                if (!buildings.Exists(buildingId))
                    continue;

                var building = buildings.Get(buildingId);
                if (!building.IsConstructed)
                    continue;

                bool isHq = DefIdTierUtil.IsBase(building.DefId, "bld_hq");
                if (!isHq && _dataRegistry.TryGetBuilding(building.DefId, out var def) && def != null)
                    isHq = def.IsHQ;

                if (isHq)
                {
                    _hqId = buildingId;
                    return;
                }
            }
        }

        public BuildingId GetCachedHqId()
            => _hqId;

        public bool TryResolveLaneTarget(int laneId, out CellPos target, out Dir4 dirToHQ)
        {
            target = default;
            dirToHQ = Dir4.S;

            if (_runStartRuntime == null)
                return false;

            if (_runStartRuntime.Lanes != null && _runStartRuntime.Lanes.TryGetValue(laneId, out var lane))
            {
                target = lane.TargetHQ;
                dirToHQ = lane.DirToHQ;
                return true;
            }

            if (_runStartRuntime.SpawnGates != null)
            {
                for (int i = 0; i < _runStartRuntime.SpawnGates.Count; i++)
                {
                    var gate = _runStartRuntime.SpawnGates[i];
                    if (gate.Lane != laneId)
                        continue;

                    dirToHQ = gate.DirToHQ;
                    break;
                }
            }

            return false;
        }
    }
}
