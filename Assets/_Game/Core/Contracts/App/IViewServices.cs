namespace SeasonalBastion.Contracts
{
    /// <summary>
    /// Minimal set services that View2D needs (Contracts-only).
    /// No runtime GameServices dependency -> no asmdef cycles.
    /// </summary>
    public interface IViewServices
    {
        IEventBus EventBus { get; }
        IGridMap GridMap { get; }
        IWorldState WorldState { get; }
        IWorldIndex WorldIndex { get; }   // optional, but useful
        IRunClock RunClock { get; }       // optional (for debug)
        IDataRegistry DataRegistry { get; } // optional (for sprite by defId)
    }

    public interface IViewServicesProvider
    {
        IViewServices GetViewServices();
    }
}