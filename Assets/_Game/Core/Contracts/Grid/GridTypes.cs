namespace SeasonalBastion.Contracts
{
    public enum CellOccupancyKind { Empty, Road, Building, Site }

    public readonly struct CellOccupancy
    {
        public readonly CellOccupancyKind Kind;
        public readonly BuildingId Building;
        public readonly SiteId Site;
        public CellOccupancy(CellOccupancyKind k, BuildingId b, SiteId s)
        { Kind=k; Building=b; Site=s; }
    }
}
