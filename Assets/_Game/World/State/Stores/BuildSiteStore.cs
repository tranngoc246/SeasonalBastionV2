// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class BuildSiteStore : EntityStore<SiteId, BuildSiteState>, IBuildSiteStore
    {
        public override int ToInt(SiteId id) => id.Value;
        public override SiteId FromInt(int v) => new SiteId(v);
    }
}
