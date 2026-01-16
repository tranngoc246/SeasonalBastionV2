// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class WorldState : IWorldState
    {
        public IBuildingStore Buildings { get; } = new BuildingStore();
        public INpcStore Npcs { get; } = new NpcStore();
        public ITowerStore Towers { get; } = new TowerStore();
        public IEnemyStore Enemies { get; } = new EnemyStore();
        public IBuildSiteStore Sites { get; } = new BuildSiteStore();

        private RunModifiers _mods;
        public ref RunModifiers RunMods => ref _mods;
    }
}
