// PATCH v0.1.3 — Contracts canonical NpcState with DefId field
namespace SeasonalBastion.Contracts
{
    public struct NpcState
    {
        public NpcId Id;
        public string DefId;
        public CellPos Cell;
        public BuildingId Workplace;
        public JobId CurrentJob;
        public bool IsIdle;
    }
}
