namespace SeasonalBastion.UI.Input
{
    public interface IInputGate
    {
        bool IsWorldInputAllowed { get; }
        bool IsPointerOverBlockingUi { get; }

        void SetPointerOverBlockingUi(bool value);
    }
}