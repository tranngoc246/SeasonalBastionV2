namespace SeasonalBastion.Contracts
{
    public struct PopulationState
    {
        public int PopulationCurrent;
        public int PopulationCap;
        public float GrowthProgressDays;
        public int StarvationDays;
        public bool StarvedToday;
        public int DailyFoodNeed;
    }
}
