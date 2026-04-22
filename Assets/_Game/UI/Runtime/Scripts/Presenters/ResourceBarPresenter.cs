using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    /// <summary>
    /// Reusable resource bar presenter responsible only for resource item binding and updates.
    /// </summary>
    public sealed class ResourceBarPresenter
    {
        private const string HiddenKeyClass = "resource-item__key--hidden";
        private readonly List<ResourceItemBinding> _itemBindings = new();
        private readonly VisualElement _resourceList;

        public ResourceBarPresenter(VisualElement resourceBarRoot)
        {
            _resourceList = resourceBarRoot?.Q<VisualElement>("ResourceBarList");
            CacheItemBindings();
        }

        public void BindMockData()
        {
            SetItems(ResourceBarMockData.CreateDefault());
        }

        public void SetItems(IReadOnlyList<ResourceItemViewModel> items)
        {
            if (items == null)
                return;

            for (var index = 0; index < _itemBindings.Count; index++)
            {
                if (index < items.Count)
                    ApplyItem(_itemBindings[index], items[index]);
                else
                    SetItemVisible(_itemBindings[index], false);
            }
        }

        public void UpdateValue(string itemName, string valueText)
        {
            var itemBinding = FindItemBinding(itemName);
            if (itemBinding == null)
                return;

            itemBinding.ValueLabel.text = valueText ?? "0";
            SetItemVisible(itemBinding, true);
        }

        public void UpdateItem(ResourceItemViewModel itemViewModel)
        {
            if (itemViewModel == null || string.IsNullOrWhiteSpace(itemViewModel.ItemName))
                return;

            var itemBinding = FindItemBinding(itemViewModel.ItemName);
            if (itemBinding == null)
                return;

            ApplyItem(itemBinding, itemViewModel);
        }

        public IReadOnlyList<ResourceItemViewModel> CreateMockSnapshot()
        {
            return ResourceBarMockData.CreateDefault();
        }

        private void CacheItemBindings()
        {
            _itemBindings.Clear();
            if (_resourceList == null)
                return;

            CacheItemBinding("ResourceItemWood");
            CacheItemBinding("ResourceItemStone");
            CacheItemBinding("ResourceItemIron");
            CacheItemBinding("ResourceItemFood");
            CacheItemBinding("ResourceItemAmmo");
        }

        private void CacheItemBinding(string itemName)
        {
            var itemInstance = _resourceList.Q<TemplateContainer>(itemName);
            if (itemInstance == null)
                return;

            var itemRoot = itemInstance.Q<VisualElement>("ResourceItem");
            var iconLabel = itemInstance.Q<Label>("IconText");
            var keyLabel = itemInstance.Q<Label>("KeyText");
            var valueLabel = itemInstance.Q<Label>("ValueText");

            if (itemRoot == null || iconLabel == null || keyLabel == null || valueLabel == null)
                return;

            _itemBindings.Add(new ResourceItemBinding(itemName, itemInstance, itemRoot, iconLabel, keyLabel, valueLabel));
        }

        private ResourceItemBinding FindItemBinding(string itemName)
        {
            for (var index = 0; index < _itemBindings.Count; index++)
            {
                var itemBinding = _itemBindings[index];
                if (string.Equals(itemBinding.ItemName, itemName, StringComparison.Ordinal))
                    return itemBinding;
            }

            return null;
        }

        private static void ApplyItem(ResourceItemBinding itemBinding, ResourceItemViewModel itemViewModel)
        {
            if (itemBinding == null || itemViewModel == null)
                return;

            itemBinding.IconLabel.text = string.IsNullOrWhiteSpace(itemViewModel.IconText) ? "?" : itemViewModel.IconText;
            itemBinding.KeyLabel.text = itemViewModel.KeyText ?? string.Empty;
            itemBinding.ValueLabel.text = itemViewModel.ValueText ?? "0";

            if (itemViewModel.ShowKey)
                itemBinding.KeyLabel.RemoveFromClassList(HiddenKeyClass);
            else
                itemBinding.KeyLabel.AddToClassList(HiddenKeyClass);

            UpdateModifierClass(itemBinding, itemViewModel.ModifierClass);
            SetItemVisible(itemBinding, itemViewModel.Visible);
        }

        private static void UpdateModifierClass(ResourceItemBinding itemBinding, string modifierClass)
        {
            if (!string.IsNullOrWhiteSpace(itemBinding.CurrentModifierClass))
                itemBinding.ItemRoot.RemoveFromClassList(itemBinding.CurrentModifierClass);

            itemBinding.CurrentModifierClass = null;

            if (!string.IsNullOrWhiteSpace(modifierClass))
            {
                itemBinding.ItemRoot.AddToClassList(modifierClass);
                itemBinding.CurrentModifierClass = modifierClass;
            }
        }

        private static void SetItemVisible(ResourceItemBinding itemBinding, bool isVisible)
        {
            itemBinding.Instance.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private sealed class ResourceItemBinding
        {
            public ResourceItemBinding(string itemName, TemplateContainer instance, VisualElement itemRoot, Label iconLabel, Label keyLabel, Label valueLabel)
            {
                ItemName = itemName;
                Instance = instance;
                ItemRoot = itemRoot;
                IconLabel = iconLabel;
                KeyLabel = keyLabel;
                ValueLabel = valueLabel;
            }

            public string ItemName { get; }
            public TemplateContainer Instance { get; }
            public VisualElement ItemRoot { get; }
            public Label IconLabel { get; }
            public Label KeyLabel { get; }
            public Label ValueLabel { get; }
            public string CurrentModifierClass { get; set; }
        }
    }

    [Serializable]
    public sealed class ResourceItemViewModel
    {
        public string ItemName;
        public string IconText;
        public string KeyText;
        public string ValueText;
        public bool ShowKey = true;
        public bool Visible = true;
        public string ModifierClass;
    }

    public static class ResourceBarMockData
    {
        public static IReadOnlyList<ResourceItemViewModel> CreateDefault()
        {
            return new[]
            {
                new ResourceItemViewModel { ItemName = "ResourceItemWood", IconText = "W", KeyText = "Wood", ValueText = "120", ShowKey = true },
                new ResourceItemViewModel { ItemName = "ResourceItemStone", IconText = "S", KeyText = "Stone", ValueText = "80", ShowKey = true },
                new ResourceItemViewModel { ItemName = "ResourceItemIron", IconText = "I", KeyText = "Iron", ValueText = "45", ShowKey = true },
                new ResourceItemViewModel { ItemName = "ResourceItemFood", IconText = "F", KeyText = "Food", ValueText = "210", ShowKey = true },
                new ResourceItemViewModel { ItemName = "ResourceItemAmmo", IconText = "A", KeyText = "Ammo", ValueText = "32", ShowKey = true, ModifierClass = "resource-item--ammo" },
            };
        }
    }
}
