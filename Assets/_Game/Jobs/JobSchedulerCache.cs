using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class JobSchedulerCache
    {
        private readonly IWorldState _w;
        private int _lastNpcVersion = -1;
        private int _lastBuildingVersion = -1;
        private int _lastWorkplaceNpcVersion = -1;

        internal JobSchedulerCache(IWorldState w)
        {
            _w = w;
        }

        internal void BuildSortedNpcIds(List<NpcId> npcIds)
        {
            int version = _w.Npcs != null ? _w.Npcs.Version : 0;
            if (_lastNpcVersion == version)
                return;

            npcIds.Clear();
            foreach (var id in _w.Npcs.Ids) npcIds.Add(id);
            npcIds.Sort((a, b) => a.Value.CompareTo(b.Value));
            _lastNpcVersion = version;
        }

        internal void BuildSortedBuildingIds(List<BuildingId> buildingIds)
        {
            int version = _w.Buildings != null ? _w.Buildings.Version : 0;
            if (_lastBuildingVersion == version)
                return;

            buildingIds.Clear();
            foreach (var id in _w.Buildings.Ids) buildingIds.Add(id);
            buildingIds.Sort((a, b) => a.Value.CompareTo(b.Value));
            _lastBuildingVersion = version;
        }

        internal void BuildWorkplaceHasNpcSet(
            IReadOnlyList<NpcId> npcIds,
            HashSet<int> workplacesWithNpc,
            Dictionary<int, int> workplaceNpcCount)
        {
            int version = _w.Npcs != null ? _w.Npcs.Version : 0;
            if (_lastWorkplaceNpcVersion == version)
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

            _lastWorkplaceNpcVersion = version;
        }
    }
}
