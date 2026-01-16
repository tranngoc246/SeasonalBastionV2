// PATCH v0.1.2 — Contracts canonical BuildingState
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
    }
}
