namespace SeasonalBastion.Contracts
{
    public readonly struct MapSize
    {
        public readonly int Width;
        public readonly int Height;

        public MapSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public static MapSize Default => new MapSize(64, 64);

        public bool IsValid => Width > 0 && Height > 0;
    }
}
