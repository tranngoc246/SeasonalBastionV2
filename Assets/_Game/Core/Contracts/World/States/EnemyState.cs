// PATCH v0.1.4 — Contracts canonical EnemyState with deterministic combat ownership metadata
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

        // Optional save/load-safe wave ownership metadata.
        // Empty/null means enemy is not currently attributed to a tracked wave.
        public string WaveId;
        public int WaveYear;
        public int WaveDay;
        public Season WaveSeason;
    }
}
