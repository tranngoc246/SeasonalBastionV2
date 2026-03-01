namespace SeasonalBastion.Contracts
{
    public enum UiToolMode
    {
        None,
        Road,
        RemoveRoad
    }

    /// <summary>UI -> Gameplay: user picked a building and pressed BUILD.</summary>
    public readonly struct UiBeginPlaceBuildingEvent
    {
        public readonly string DefId;
        public UiBeginPlaceBuildingEvent(string defId) { DefId = defId; }
    }

    /// <summary>UI -> Gameplay: tool mode changed (road/remove/cancel).</summary>
    public readonly struct UiToolModeRequestedEvent
    {
        public readonly UiToolMode Mode;
        public UiToolModeRequestedEvent(UiToolMode mode) { Mode = mode; }
    }
}