namespace SeasonalBastion.Contracts
{
    public struct ResourcePileState
    {
        public PileId Id;
        public CellPos Cell;
        public ResourceType Resource;
        public int Amount;

        public BuildingId OwnerBuilding;
    }
}
