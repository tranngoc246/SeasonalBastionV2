namespace SeasonalBastion.Contracts
{
    public interface ITerrainMap
    {
        int Width { get; }
        int Height { get; }

        bool IsInside(CellPos c);
        TerrainType Get(CellPos c);
        void Set(CellPos c, TerrainType terrain);
        void ClearAll();
    }
}
