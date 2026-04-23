using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Views
{
    public sealed class BuildingCardFactory
    {
        private readonly VisualTreeAsset _template;

        public BuildingCardFactory(VisualElement prototype)
        {
            _template = prototype as VisualTreeAsset ?? prototype?.visualTreeAssetSource;
        }

        public BuildingCardView Create()
        {
            var cardRoot = CreateCardRoot();
            return cardRoot != null ? new BuildingCardView(cardRoot) : null;
        }

        private VisualElement CreateCardRoot()
        {
            if (_template == null)
                return null;

            var instance = _template.CloneTree();
            var cardRoot = instance?.Q<VisualElement>(BuildingCardView.RootElementName);
            if (cardRoot == null)
                return null;

            cardRoot.RemoveFromHierarchy();
            cardRoot.RemoveFromClassList(BuildingCardView.HiddenClass);
            return cardRoot;
        }
    }
}
