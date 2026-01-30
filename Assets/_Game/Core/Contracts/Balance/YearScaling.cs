namespace SeasonalBastion.Contracts
{
    /// <summary>
    /// Day43: year scaling (v0.1 → Y2). Keep simple + deterministic.
    /// </summary>
    public static class YearScaling
    {
        public static float WaveCountMul(int year)
        {
            // Deliverable C: Y2 count +20%
            return year <= 1 ? 1f : 1.2f;
        }

        public static float EnemyHpMul(int year)
        {
            // Deliverable C: Y2 HP ×1.35
            return year <= 1 ? 1f : 1.35f;
        }

        public static float EnemyDamageMul(int year)
        {
            // Deliverable C: Y2 Damage ×1.25
            return year <= 1 ? 1f : 1.25f;
        }
    }
}
