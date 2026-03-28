namespace SeasonalBastion.Contracts
{
    public interface IPopulationService
    {
        PopulationState State { get; }

        void Reset();
        void RebuildDerivedState();
        void OnDayStarted();
        void LoadState(float growthProgressDays, int starvationDays, bool starvedToday);
    }
}
