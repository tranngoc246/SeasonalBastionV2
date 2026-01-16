// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public static class DeterminismUtil
    {
        public static int Manhattan(CellPos a, CellPos b) => System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y);
    }
}
