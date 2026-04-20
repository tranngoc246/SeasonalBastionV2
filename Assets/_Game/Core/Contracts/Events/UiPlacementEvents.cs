namespace SeasonalBastion.Contracts
{
    public enum UiToolMode
    {
        Select,
        BuildPlacement,
        Road,
        Remove,
    }

    /// <summary>UI -> Gameplay: user picked a building and pressed BUILD.</summary>
    public readonly struct UiBeginPlaceBuildingEvent
    {
        public readonly string DefId;
        public UiBeginPlaceBuildingEvent(string defId) { DefId = defId; }
    }

    /// <summary>UI -> Gameplay: tool mode changed.</summary>
    public readonly struct UiToolModeRequestedEvent
    {
        public readonly UiToolMode Mode;
        public UiToolModeRequestedEvent(UiToolMode mode) { Mode = mode; }
    }

    public readonly struct UiOpenBuildPanelRequestedEvent { }
    public readonly struct UiCloseBuildPanelRequestedEvent { }

    public readonly struct UiInspectSelectionRequestedEvent
    {
        public readonly int Kind;
        public readonly int Id;
        public UiInspectSelectionRequestedEvent(int kind, int id) { Kind = kind; Id = id; }
    }

    public readonly struct UiClearInspectRequestedEvent { }

    public readonly struct UiOpenModalRequestedEvent
    {
        public readonly string ModalKey;
        public UiOpenModalRequestedEvent(string modalKey) { ModalKey = modalKey; }
    }

    public readonly struct UiPlacementStartedEvent
    {
        public readonly string DefId;
        public UiPlacementStartedEvent(string defId) { DefId = defId; }
    }

    public readonly struct UiPlacementFinishedEvent
    {
        public readonly string DefId;
        public readonly bool Confirmed;
        public UiPlacementFinishedEvent(string defId, bool confirmed) { DefId = defId; Confirmed = confirmed; }
    }

    public readonly struct UiInspectActionRequestedEvent
    {
        public readonly string Action;
        public readonly int TargetId;
        public UiInspectActionRequestedEvent(string action, int targetId) { Action = action; TargetId = targetId; }
    }
}
