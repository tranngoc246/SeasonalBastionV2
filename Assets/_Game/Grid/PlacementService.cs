// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class PlacementService : IPlacementService
    {
        private readonly IGridMap _grid;
        private readonly IWorldState _world;
        private readonly IDataRegistry _data;
        private readonly IWorldIndex _index;
        private readonly IEventBus _bus;

        public PlacementService(IGridMap grid, IWorldState world, IDataRegistry data, IWorldIndex index, IEventBus bus)
        { _grid = grid; _world = world; _data = data; _index = index; _bus = bus; }

        public bool CanPlaceRoad(CellPos c)
        {
            if (!_grid.IsInside(c)) return false;
            if (_grid.IsBlocked(c)) return false;
            // TODO: enforce orthogonal placement is a tool-level rule
            return true;
        }

        public void PlaceRoad(CellPos c)
        {
            if (!CanPlaceRoad(c)) return;
            _grid.SetRoad(c, true);
            _bus.Publish(new RoadPlacedEvent(c));
        }

        public PlacementResult ValidateBuilding(string buildingDefId, CellPos anchor, Dir4 rotation)
        {
            // TODO:
            // - bounds check footprint
            // - overlap check
            // - entry/road (driveway len=1)
            // - site blocking
            return new PlacementResult(true, PlacementFailReason.None, default);
        }

        public BuildingId CommitBuilding(string buildingDefId, CellPos anchor, Dir4 rotation)
        {
            // TODO: call Validate + apply occupancy + create building in world
            // NOTE: driveway conversion must be deterministic here
            throw new System.NotImplementedException();
        }
    }
}
