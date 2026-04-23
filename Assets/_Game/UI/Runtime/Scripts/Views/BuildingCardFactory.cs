using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Views
{
    public sealed class BuildingCardFactory
    {
        private readonly VisualTreeAsset _template;

        public BuildingCardFactory(VisualElement scope)
        {
            _template = scope?.visualTreeAssetSource?.ResolveTemplate("BuildingCardTemplate");
        }

        public BuildingCardView Create()
        {
            if (_template == null)
                return null;

            var root = _template.Instantiate();
            return root != null ? new BuildingCardView(root) : null;
        }
    }
}
