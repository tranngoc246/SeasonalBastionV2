using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class GridMap : IGridMap
    {
        private readonly int _w, _h;
        private readonly CellOccupancy[] _cells;

        public GridMap(int width, int height)
        {
            _w = width; _h = height;
            _cells = new CellOccupancy[_w * _h];
            // default Empty
        }

        public int Width => _w;
        public int Height => _h;

        public bool IsInside(CellPos c) => c.X >= 0 && c.Y >= 0 && c.X < _w && c.Y < _h;

        private int Idx(CellPos c) => c.Y * _w + c.X;

        public CellOccupancy Get(CellPos c)
        {
            if (!IsInside(c)) return new CellOccupancy(CellOccupancyKind.Empty, default, default);
            return _cells[Idx(c)];
        }

        public bool IsRoad(CellPos c) => Get(c).Kind == CellOccupancyKind.Road;

        public bool IsBlocked(CellPos c)
        {
            var o = Get(c).Kind;
            return o == CellOccupancyKind.Building || o == CellOccupancyKind.Site;
        }

        public void SetRoad(CellPos c, bool isRoad)
        {
            if (!IsInside(c)) return;
            _cells[Idx(c)] = isRoad
                ? new CellOccupancy(CellOccupancyKind.Road, default, default)
                : new CellOccupancy(CellOccupancyKind.Empty, default, default);
        }

        public void SetBuilding(CellPos c, BuildingId id)
        {
            if (!IsInside(c)) return;
            _cells[Idx(c)] = new CellOccupancy(CellOccupancyKind.Building, id, default);
        }

        public void ClearBuilding(CellPos c)
        {
            if (!IsInside(c)) return;
            _cells[Idx(c)] = new CellOccupancy(CellOccupancyKind.Empty, default, default);
        }

        public void SetSite(CellPos c, SiteId id)
        {
            if (!IsInside(c)) return;
            _cells[Idx(c)] = new CellOccupancy(CellOccupancyKind.Site, default, id);
        }

        public void ClearSite(CellPos c)
        {
            if (!IsInside(c)) return;
            _cells[Idx(c)] = new CellOccupancy(CellOccupancyKind.Empty, default, default);
        }

        public void ClearAll()
        {
            // Reset entire occupancy buffer to Empty (deterministic, no allocations)
            for (int i = 0; i < _cells.Length; i++)
                _cells[i] = default;
        }
    }
}
