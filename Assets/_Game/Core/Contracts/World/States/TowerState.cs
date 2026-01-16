// PATCH v0.1.2 — Contracts canonical TowerState
namespace SeasonalBastion.Contracts
{
    public struct TowerState
    {
        public TowerId Id;
        public CellPos Cell;
        public int Ammo;
        public int AmmoCap;
        public int Hp;
        public int HpMax;
    }
}
