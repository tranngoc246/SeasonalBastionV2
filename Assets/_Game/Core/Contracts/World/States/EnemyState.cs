// PATCH v0.1.3 — Contracts canonical EnemyState with DefId and Lane fields
namespace SeasonalBastion.Contracts
{
    public struct EnemyState
    {
        public EnemyId Id;
        public string DefId;
        public CellPos Cell;
        public int Hp;
        public int Lane;
        public float MoveProgress01;
    }
}
