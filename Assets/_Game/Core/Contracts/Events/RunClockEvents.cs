namespace SeasonalBastion.Contracts
{
    // RunClock events (VS2). These are published via IEventBus.

    public readonly struct SeasonDayChangedEvent
    {
        public readonly Season Season;
        public readonly int DayIndex;
        public SeasonDayChangedEvent(Season s, int dayIndex){Season=s;DayIndex=dayIndex;}
    }

    public readonly struct DayStartedEvent
    {
        public readonly Season Season;
        public readonly int DayIndex;
        public readonly int YearIndex;
        public readonly Phase Phase;
        public DayStartedEvent(Season s, int dayIndex, int yearIndex, Phase p){Season=s;DayIndex=dayIndex;YearIndex=yearIndex;Phase=p;}
    }

    public readonly struct DayEndedEvent
    {
        public readonly Season Season;
        public readonly int DayIndex;
        public readonly int YearIndex;
        public DayEndedEvent(Season s, int dayIndex, int yearIndex){Season=s;DayIndex=dayIndex;YearIndex=yearIndex;}
    }

    public readonly struct SeasonChangedEvent
    {
        public readonly Season From;
        public readonly Season To;
        public SeasonChangedEvent(Season from, Season to){From=from;To=to;}
    }

    public readonly struct YearChangedEvent
    {
        public readonly int FromYear;
        public readonly int ToYear;
        public YearChangedEvent(int from, int to){FromYear=from;ToYear=to;}
    }

    public readonly struct PhaseChangedEvent
    {
        public readonly Phase From;
        public readonly Phase To;
        public PhaseChangedEvent(Phase from, Phase to){From=from;To=to;}
    }

    public readonly struct TimeScaleChangedEvent
    {
        public readonly float From;
        public readonly float To;
        public readonly bool Forced;
        public TimeScaleChangedEvent(float from, float to, bool forced){From=from;To=to;Forced=forced;}
    }
}
