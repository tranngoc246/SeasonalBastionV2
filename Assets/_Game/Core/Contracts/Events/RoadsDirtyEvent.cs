namespace SeasonalBastion.Contracts
{
    /// <summary>
    /// B-lite: roads changed somewhere (place/remove/auto-driveway/load).
    /// RoadRuntimeView should throttle-resync from GridMap.
    /// </summary>
    public readonly struct RoadsDirtyEvent { }
}
