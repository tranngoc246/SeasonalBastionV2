using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class JobSchedulerCache
    {
        private readonly IWorldState _w;
        private int _lastNpcCount = -1;
        private int _lastBuildingCount = -1;
        private int _lastWorkplaceNpcCountSource = -1;

        internal JobSchedulerCache(IWorldState w)
        {
            _w = w;
        }

        internal void BuildSortedNpcIds(List<NpcId> npcIds)
        {
            int count = _w.Npcs != null ? _w.Npcs.Count : 0;
            if (_lastNpcCount == count && npcIds.Count == count)
                return;

            npcIds.Clear();
            foreach (var id in _w.Npcs.Ids) npcIds.Add(id);
            npcIds.Sort((a, b) => a.Value.CompareTo(b.Value));
            _lastNpcCount = count;
        }

        internal void BuildSortedBuildingIds(List<BuildingId> buildingIds)
        {
            int count = _w.Buildings != null ? _w.Buildings.Count : 0;
            if (_lastBuildingCount == count && buildingIds.Count == count)
                return;

            buildingIds.Clear();
            foreach (var id in _w.Buildings.Ids) buildingIds.Add(id);
            buildingIds.Sort((a, b) => a.Value.CompareTo(b.Value));
            _lastBuildingCount = count;
        }

        internal void BuildWorkplaceHasNpcSet(
            IReadOnlyList<NpcId> npcIds,
            HashSet<int> workplacesWithNpc,
            Dictionary<int, int> workplaceNpcCount)
        {
            if (_lastWorkplaceNpcCountSource == npcIds.Count && workplacesWithNpc.Count > 0)
                return;

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

            _lastWorkplaceNpcCountSource = npcIds.Count;
        }
    }
}
