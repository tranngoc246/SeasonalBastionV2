// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class WorldIndexService : IWorldIndex
    {
        private readonly IWorldState _w;
        private readonly IDataRegistry _data;

        private readonly System.Collections.Generic.List<BuildingId> _warehouses = new();
        private readonly System.Collections.Generic.List<BuildingId> _producers  = new();
        private readonly System.Collections.Generic.List<BuildingId> _forges     = new();
        private readonly System.Collections.Generic.List<BuildingId> _armories   = new();
        private readonly System.Collections.Generic.List<TowerId> _towers        = new();

        public System.Collections.Generic.IReadOnlyList<BuildingId> Warehouses => _warehouses;
        public System.Collections.Generic.IReadOnlyList<BuildingId> Producers  => _producers;
        public System.Collections.Generic.IReadOnlyList<BuildingId> Forges     => _forges;
        public System.Collections.Generic.IReadOnlyList<BuildingId> Armories   => _armories;
        public System.Collections.Generic.IReadOnlyList<TowerId>    Towers     => _towers;

        public WorldIndexService(IWorldState w, IDataRegistry data){ _w = w; _data = data; }

        public void RebuildAll()
        {
            _warehouses.Clear(); _producers.Clear(); _forges.Clear(); _armories.Clear(); _towers.Clear();

            foreach (var bid in _w.Buildings.Ids)
            {
                // TODO: read BuildingDef flags
            }
            foreach (var tid in _w.Towers.Ids)
            {
                _towers.Add(tid);
            }
        }

        public void OnBuildingCreated(BuildingId id) { /* TODO incremental */ }
        public void OnBuildingDestroyed(BuildingId id) { /* TODO incremental */ }
    }
}
