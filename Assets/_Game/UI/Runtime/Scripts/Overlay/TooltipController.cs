using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Overlay
{
    /// <summary>
    /// Tooltip tối giản: 1 label, show/hide thủ công.
    /// </summary>
    public sealed class TooltipController
    {
        private VisualElement _host;
        private Label _label;

        public void Bind(VisualElement host)
        {
            _host = host;
            if (_host == null) return;

            _host.pickingMode = PickingMode.Ignore;

            _label = new Label("");
            _label.style.display = DisplayStyle.None;
            _label.pickingMode = PickingMode.Ignore;
            _label.style.paddingLeft = 6;
            _label.style.paddingRight = 6;
            _label.style.paddingTop = 4;
            _label.style.paddingBottom = 4;

            _host.Add(_label);
        }

        public void Show(string text)
        {
            if (_label == null) return;
            _label.text = text ?? "";
            _label.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            if (_label == null) return;
            _label.style.display = DisplayStyle.None;
        }

        public void Tick(float dt)
        {
            // Placeholder: nếu bạn muốn tooltip follow mouse, xử lý ở đây.
        }
    }
}