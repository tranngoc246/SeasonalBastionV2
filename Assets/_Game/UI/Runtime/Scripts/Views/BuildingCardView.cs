using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Views
{
    public sealed class BuildingCardView
    {
        public const string RootElementName = "BuildingCard";
        public const string HiddenClass = "hidden";

        private const string SelectedClass = "is-selected";
        private const string LockedClass = "is-locked";

        private readonly VisualElement _root;
        private readonly Label _iconText;
        private readonly Label _name;
        private readonly Label _cost;
        private readonly Label _status;

        public BuildingCardView(VisualElement root)
        {
            _root = root;
            _iconText = root?.Q<Label>("BuildingCardIconText");
            _name = root?.Q<Label>("BuildingCardName");
            _cost = root?.Q<Label>("BuildingCardCost");
            _status = root?.Q<Label>("BuildingCardStatus");
        }

        public VisualElement Root => _root;

        public void Bind(string displayName, string costText, string iconText, string statusText, bool isSelected, bool isLocked)
        {
            if (_iconText != null)
                _iconText.text = string.IsNullOrWhiteSpace(iconText) ? "?" : iconText;

            if (_name != null)
                _name.text = displayName ?? string.Empty;

            if (_cost != null)
                _cost.text = costText ?? string.Empty;

            if (_status != null)
            {
                _status.text = statusText ?? string.Empty;
                if (string.IsNullOrWhiteSpace(statusText))
                    _status.AddToClassList(HiddenClass);
                else
                    _status.RemoveFromClassList(HiddenClass);
            }

            SetClass(_root, SelectedClass, isSelected);
            SetClass(_root, LockedClass, isLocked);
        }

        private static void SetClass(VisualElement element, string className, bool enabled)
        {
            if (element == null)
                return;

            if (enabled)
                element.AddToClassList(className);
            else
                element.RemoveFromClassList(className);
        }
    }
}
