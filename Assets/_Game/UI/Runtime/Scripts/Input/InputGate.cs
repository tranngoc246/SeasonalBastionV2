using SeasonalBastion.UI;

namespace SeasonalBastion.UI.Input
{
    /// <summary>
    /// Gate world input = !modalOpen && !pointerOverBlockingUi.
    /// </summary>
    public sealed class InputGate : IInputGate
    {
        private readonly UIStateStore _store;
        private bool _pointerOverBlockingUi;

        public InputGate(UIStateStore store)
        {
            _store = store;
        }

        public bool IsPointerOverBlockingUi => _pointerOverBlockingUi;

        public bool IsWorldInputAllowed
        {
            get
            {
                bool modalOpen = _store != null && _store.HasModal;
                return !modalOpen && !_pointerOverBlockingUi;
            }
        }

        public void SetPointerOverBlockingUi(bool value)
        {
            _pointerOverBlockingUi = value;
        }
    }
}