namespace SeasonalBastion.Contracts
{
    public struct BuildingState
    {
        public BuildingId Id;
        public string DefId;
        public CellPos Anchor;
        public Dir4 Rotation;
        public int Level;
        public bool IsConstructed;
        public int Wood;
        public int Food;
        public int Stone;
        public int Iron;
        public int Ammo;
    }
}
