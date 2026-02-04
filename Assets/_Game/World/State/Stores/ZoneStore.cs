using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class ZoneStore : IZoneStore
    {
        private readonly List<ZoneState> _zones = new(4);

        public IReadOnlyList<ZoneState> Zones => _zones;

        public void Clear() => _zones.Clear();

        public void Add(ZoneState z) => _zones.Add(z);

        public ZoneState GetByResource(ResourceType rt)
        {
            for (int i = 0; i < _zones.Count; i++)
                if (_zones[i].Resource == rt) return _zones[i];

            throw new Exception("Zone missing for " + rt);
        }

        public CellPos PickCell(ResourceType rt, CellPos preferNear)
        {
            var z = GetByResource(rt);
            var cells = z.Cells;
            if (cells == null || cells.Count == 0) return preferNear;

            // deterministic “nearest”
            int best = 0;
            int bestD = int.MaxValue;

            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                int dx = c.X - preferNear.X; if (dx < 0) dx = -dx;
                int dy = c.Y - preferNear.Y; if (dy < 0) dy = -dy;
                int d = dx + dy;

                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }

            return cells[best];
        }
    }
}
