// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using UnityEngine;
using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public static class Log
    {
        public static bool Jobs = true;
        public static bool Economy = true;
        public static bool Combat = true;

        public static void J(string msg) { if (Jobs) UnityEngine.Debug.Log("[JOBS] " + msg); }
        public static void E(string msg) { if (Economy) UnityEngine.Debug.Log("[ECO] " + msg); }
        public static void C(string msg) { if (Combat) UnityEngine.Debug.Log("[COMBAT] " + msg); }
    }
}
