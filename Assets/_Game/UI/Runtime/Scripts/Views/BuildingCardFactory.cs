using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Views
{
    public sealed class BuildingCardFactory
    {
        private readonly TemplateContainer _prototype;

        public BuildingCardFactory(VisualElement prototype)
        {
            _prototype = prototype as TemplateContainer;
        }

        public BuildingCardView Create()
        {
            if (_prototype == null)
                return null;

            var clonedRoot = _prototype.contentContainer?.Q<VisualElement>("BuildingCard");
            if (clonedRoot == null)
                return null;

            var cardRoot = new VisualElement { name = "BuildingCard" };
            foreach (var className in clonedRoot.GetClasses())
                cardRoot.AddToClassList(className);

            for (var i = 0; i < clonedRoot.childCount; i++)
            {
                var child = clonedRoot[i];
                if (child is VisualElement childElement)
                    cardRoot.Add(CloneElement(childElement));
            }

            cardRoot.RemoveFromClassList("hidden");
            return new BuildingCardView(cardRoot);
        }

        private static VisualElement CloneElement(VisualElement source)
        {
            var clone = new VisualElement { name = source.name, pickingMode = source.pickingMode };
            foreach (var className in source.GetClasses())
                clone.AddToClassList(className);

            if (source is Label sourceLabel)
            {
                var labelClone = new Label(sourceLabel.text) { name = source.name, pickingMode = source.pickingMode };
                foreach (var className in source.GetClasses())
                    labelClone.AddToClassList(className);
                clone = labelClone;
            }

            for (var i = 0; i < source.childCount; i++)
            {
                if (source[i] is VisualElement child)
                    clone.Add(CloneElement(child));
            }

            return clone;
        }
    }
}
