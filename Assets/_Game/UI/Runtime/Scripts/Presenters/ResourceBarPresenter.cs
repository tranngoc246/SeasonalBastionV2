using System.Collections.Generic;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    /// <summary>
    /// Mock-bound reusable resource bar presenter.
    /// Builds resource items from a simple data model without depending on game services.
    /// </summary>
    public sealed class ResourceBarPresenter
    {
        private readonly VisualElement _root;
        private readonly VisualElement _list;
        private readonly VisualTreeAsset _resourceItemTemplate;
        private readonly List<VisualElement> _itemInstances = new();

        public ResourceBarPresenter(VisualElement root, VisualTreeAsset resourceItemTemplate)
        {
            _root = root;
            _list = root?.Q<VisualElement>("ResourceBarList");
            _resourceItemTemplate = resourceItemTemplate;
        }

        public void Bind(IReadOnlyList<ResourceItemViewModel> items)
        {
            Clear();

            if (_list == null || _resourceItemTemplate == null || items == null)
                return;

            for (var index = 0; index < items.Count; index++)
            {
                var itemData = items[index];
                if (itemData == null)
                    continue;

                var instance = _resourceItemTemplate.CloneTree();
                var itemRoot = instance.Q<VisualElement>("ResourceItem");
                var iconText = instance.Q<Label>("IconText");
                var valueText = instance.Q<Label>("ValueText");

                if (itemRoot != null && !string.IsNullOrWhiteSpace(itemData.BlockModifierClass))
                    itemRoot.AddToClassList(itemData.BlockModifierClass);

                if (iconText != null)
                    iconText.text = string.IsNullOrWhiteSpace(itemData.IconText) ? "?" : itemData.IconText;

                if (valueText != null)
                    valueText.text = itemData.ValueText ?? "0";

                _list.Add(instance);
                _itemInstances.Add(instance);
            }
        }

        public void BindMockData()
        {
            Bind(ResourceBarMockData.CreateDefault());
        }

        public void Clear()
        {
            for (var index = 0; index < _itemInstances.Count; index++)
                _itemInstances[index]?.RemoveFromHierarchy();

            _itemInstances.Clear();
        }
    }

    public sealed class ResourceItemViewModel
    {
        public string IconText { get; set; }
        public string ValueText { get; set; }
        public string BlockModifierClass { get; set; }
    }

    public static class ResourceBarMockData
    {
        public static IReadOnlyList<ResourceItemViewModel> CreateDefault()
        {
            return new[]
            {
                new ResourceItemViewModel { IconText = "W", ValueText = "120" },
                new ResourceItemViewModel { IconText = "S", ValueText = "80" },
                new ResourceItemViewModel { IconText = "I", ValueText = "45" },
                new ResourceItemViewModel { IconText = "F", ValueText = "210" },
                new ResourceItemViewModel { IconText = "A", ValueText = "32" },
            };
        }
    }
}
