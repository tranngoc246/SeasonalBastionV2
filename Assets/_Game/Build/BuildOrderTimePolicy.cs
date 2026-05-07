using System;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class BuildOrderTimePolicy
    {
        private readonly BalanceService _balance;

        public BuildOrderTimePolicy(BalanceService balance)
        {
            _balance = balance;
        }

        public float ComputeWorkSecondsTotalFromChunks(int chunks)
        {
            if (chunks <= 0) chunks = (_balance != null ? _balance.FallbackBuildChunksL1 : 2);

            float chunkSec = _balance != null ? _balance.BuildChunkSec : 6f;
            int builderTier = _balance != null ? _balance.GetBuilderTier() : 1;
            float mult = _balance != null ? _balance.GetBuildSpeedMult(builderTier) : 1f;

            float total = chunks * chunkSec * mult;
            if (total < 0.1f) total = 0.1f;
            return total;
        }

        public float ComputeRepairSeconds(int hp, int maxHp)
        {
            if (maxHp <= 0) return 0f;
            int missing = maxHp - hp;
            if (missing <= 0) return 0f;

            float chunkSec = _balance != null ? _balance.RepairChunkSec : 4f;
            float healPct = _balance != null ? _balance.RepairHealPct : 0.15f;

            int perChunk = Math.Max(1, (int)Math.Ceiling(maxHp * healPct));
            int chunks = (missing + perChunk - 1) / perChunk;

            int builderTier = _balance != null ? _balance.GetBuilderTier() : 1;
            float timeMult = _balance != null ? _balance.GetRepairTimeMult(builderTier) : 1f;

            float total = chunks * chunkSec * timeMult;
            return total < chunkSec ? chunkSec : total;
        }

        public float ComputeWorkSecondsTotal(BuildingDef def)
        {
            int chunks = def.BuildChunksL1 > 0 ? def.BuildChunksL1 : (_balance != null ? _balance.FallbackBuildChunksL1 : 2);

            float chunkSec = _balance != null ? _balance.BuildChunkSec : 6f;
            int builderTier = _balance != null ? _balance.GetBuilderTier() : 1;
            float mult = _balance != null ? _balance.GetBuildSpeedMult(builderTier) : 1f;

            float total = chunks * chunkSec * mult;
            if (total < 0.1f) total = 0.1f;
            return total;
        }
    }
}
