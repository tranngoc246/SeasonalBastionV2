// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class TowerStore : EntityStore<TowerId, TowerState>, ITowerStore
    {
        public override int ToInt(TowerId id) => id.Value;
        public override TowerId FromInt(int v) => new TowerId(v);
    }
}
