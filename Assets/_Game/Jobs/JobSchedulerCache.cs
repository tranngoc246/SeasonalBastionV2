using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class JobSchedulerCache
    {
        private readonly IWorldState _w;

        internal JobSchedulerCache(IWorldState w)
        {
            _w = w;
        }

        internal void BuildSortedNpcIds(List<NpcId> npcIds)
        {
            npcIds.Clear();
            foreach (var id in _w.Npcs.Ids) npcIds.Add(id);
            npcIds.Sort((a, b) => a.Value.CompareTo(b.Value));
        }

        internal void BuildSortedBuildingIds(List<BuildingId> buildingIds)
        {
            buildingIds.Clear();
            foreach (var id in _w.Buildings.Ids) buildingIds.Add(id);
            buildingIds.Sort((a, b) => a.Value.CompareTo(b.Value));
        }

        internal void BuildWorkplaceHasNpcSet(
            IReadOnlyList<NpcId> npcIds,
            HashSet<int> workplacesWithNpc,
            Dictionary<int, int> workplaceNpcCount)
        {
            workplacesWithNpc.Clear();
            workplaceNpcCount.Clear();

            for (int i = 0; i < npcIds.Count; i++)
            {
                var nid = npcIds[i];
                if (!_w.Npcs.Exists(nid)) continue;

                var ns = _w.Npcs.Get(nid);
                int wp = ns.Workplace.Value;
                if (wp == 0) continue;

                workplacesWithNpc.Add(wp);

                if (workplaceNpcCount.TryGetValue(wp, out var c)) workplaceNpcCount[wp] = c + 1;
                else workplaceNpcCount[wp] = 1;
            }
        }
    }
}
