using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public static class TerrainRules
    {
        public static bool IsBuildableTerrain(TerrainType terrain)
        {
            return terrain == TerrainType.Land;
        }

        public static bool IsWalkableTerrain(TerrainType terrain)
        {
            return terrain == TerrainType.Land || terrain == TerrainType.Shore;
        }
    }
}
