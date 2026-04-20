using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class AmmoRuntimeState
    {
        internal readonly Dictionary<int, JobId> SupplyJobByForgeAndType = new();
        internal readonly Dictionary<int, JobId> CraftJobByForge = new();
        internal readonly Dictionary<int, JobId> HaulAmmoJobByArmory = new();
        internal readonly List<NpcId> NpcIds = new(64);
        internal readonly HashSet<int> WorkplacesWithNpc = new();
        internal int LastNpcVersionForWorkplaces = -1;

        internal void Clear()
        {
            SupplyJobByForgeAndType.Clear();
            CraftJobByForge.Clear();
            HaulAmmoJobByArmory.Clear();
            NpcIds.Clear();
            WorkplacesWithNpc.Clear();
            LastNpcVersionForWorkplaces = -1;
        }
    }
}
