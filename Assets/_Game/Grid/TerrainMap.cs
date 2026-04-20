using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class TerrainMap : ITerrainMap
    {
        private readonly int _w, _h;
        private readonly TerrainType[] _cells;

        public TerrainMap(int width, int height)
        {
            _w = width;
            _h = height;
            _cells = new TerrainType[_w * _h];
            ClearAll();
        }

        public int Width => _w;
        public int Height => _h;

        public bool IsInside(CellPos c) => c.X >= 0 && c.Y >= 0 && c.X < _w && c.Y < _h;

        public TerrainType Get(CellPos c)
        {
            if (!IsInside(c)) return TerrainType.Sea;
            return _cells[Idx(c)];
        }

        public void Set(CellPos c, TerrainType terrain)
        {
            if (!IsInside(c)) return;
            _cells[Idx(c)] = terrain;
        }

        public void ClearAll()
        {
            for (int i = 0; i < _cells.Length; i++)
                _cells[i] = TerrainType.Sea;
        }

        private int Idx(CellPos c) => c.Y * _w + c.X;
    }
}
