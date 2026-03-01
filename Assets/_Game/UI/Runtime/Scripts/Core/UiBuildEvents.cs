namespace SeasonalBastion.UI
{
    /// <summary>
    /// UI -> Gameplay signal: user picked a building and pressed BUILD.
    /// Gameplay/Placement controller should subscribe and enter placement mode.
    /// </summary>
    public readonly struct UiBuildRequestedEvent
    {
        public readonly string DefId;
        public UiBuildRequestedEvent(string defId) { DefId = defId; }
    }
}