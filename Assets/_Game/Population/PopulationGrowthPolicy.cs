using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class PopulationGrowthPolicy
    {
        private readonly IRunClock _runClock;
        private readonly int _foodReserveDaysRequiredForGrowth;

        public PopulationGrowthPolicy(IRunClock runClock, int foodReserveDaysRequiredForGrowth)
        {
            _runClock = runClock;
            _foodReserveDaysRequiredForGrowth = foodReserveDaysRequiredForGrowth;
        }

        public bool CanGrowToday(PopulationState state, int availableFoodBeforeConsume)
        {
            if (_runClock != null && _runClock.CurrentPhase == Phase.Defend)
                return false;

            if (state.StarvedToday)
                return false;

            if (state.PopulationCurrent >= state.PopulationCap)
                return false;

            int reserveRequired = state.DailyFoodNeed * _foodReserveDaysRequiredForGrowth;
            if (availableFoodBeforeConsume < reserveRequired)
                return false;

            return true;
        }
    }
}
