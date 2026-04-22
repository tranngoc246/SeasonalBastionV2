using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    /// <summary>
    /// Reusable resource bar presenter with mock data only.
    /// No game-service integration yet.
    /// </summary>
    public sealed class ResourceBarPresenter
    {
        private const string HiddenKeyClass = "resource-item__key--hidden";

        private readonly VisualElement _root;
        private readonly VisualElement _list;
        private readonly List<ResourceItemBinding> _bindings = new();

        public ResourceBarPresenter(VisualElement root)
        {
            _root = root;
            _list = root?.Q<VisualElement>("ResourceBarList");
            CacheBindings();
        }

        public void BindMockData()
        {
            SetItems(ResourceBarMockData.CreateDefault());
        }

        public void SetItems(IReadOnlyList<ResourceItemViewModel> items)
        {
            if (items == null)
                return;

            for (var index = 0; index < _bindings.Count; index++)
            {
                if (index < items.Count)
                    Apply(_bindings[index], items[index]);
                else
                    SetVisible(_bindings[index], false);
            }
        }

        public void UpdateValue(string itemName, string valueText)
        {
            var binding = FindBinding(itemName);
            if (binding == null)
                return;

            binding.ValueLabel.text = valueText ?? "0";
            SetVisible(binding, true);
        }

        public void UpdateItem(ResourceItemViewModel item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemName))
                return;

            var binding = FindBinding(item.ItemName);
            if (binding == null)
                return;

            Apply(binding, item);
        }

        public IReadOnlyList<ResourceItemViewModel> CreateMockSnapshot()
        {
            return ResourceBarMockData.CreateDefault();
        }

        private void CacheBindings()
        {
            _bindings.Clear();
            if (_list == null)
                return;

            CacheBinding("ResourceItemWood");
            CacheBinding("ResourceItemStone");
            CacheBinding("ResourceItemIron");
            CacheBinding("ResourceItemFood");
            CacheBinding("ResourceItemAmmo");
        }

        private void CacheBinding(string instanceName)
        {
            var instance = _list.Q<TemplateContainer>(instanceName);
            if (instance == null)
                return;

            var itemRoot = instance.Q<VisualElement>("ResourceItem");
            var iconLabel = instance.Q<Label>("IconText");
            var keyLabel = instance.Q<Label>("KeyText");
            var valueLabel = instance.Q<Label>("ValueText");

            if (itemRoot == null || iconLabel == null || keyLabel == null || valueLabel == null)
                return;

            _bindings.Add(new ResourceItemBinding(instanceName, instance, itemRoot, iconLabel, keyLabel, valueLabel));
        }

        private ResourceItemBinding FindBinding(string itemName)
        {
            for (var index = 0; index < _bindings.Count; index++)
            {
                var binding = _bindings[index];
                if (string.Equals(binding.ItemName, itemName, StringComparison.Ordinal))
                    return binding;
            }

            return null;
        }

        private static void Apply(ResourceItemBinding binding, ResourceItemViewModel item)
        {
            if (binding == null || item == null)
                return;

            binding.IconLabel.text = string.IsNullOrWhiteSpace(item.IconText) ? "?" : item.IconText;
            binding.KeyLabel.text = item.KeyText ?? string.Empty;
            binding.ValueLabel.text = item.ValueText ?? "0";

            if (item.ShowKey)
                binding.KeyLabel.RemoveFromClassList(HiddenKeyClass);
            else
                binding.KeyLabel.AddToClassList(HiddenKeyClass);

            SyncModifierClass(binding, item.ModifierClass);
            SetVisible(binding, item.Visible);
        }

        private static void SyncModifierClass(ResourceItemBinding binding, string modifierClass)
        {
            if (!string.IsNullOrWhiteSpace(binding.CurrentModifierClass))
                binding.ItemRoot.RemoveFromClassList(binding.CurrentModifierClass);

            binding.CurrentModifierClass = null;

            if (!string.IsNullOrWhiteSpace(modifierClass))
            {
                binding.ItemRoot.AddToClassList(modifierClass);
                binding.CurrentModifierClass = modifierClass;
            }
        }

        private static void SetVisible(ResourceItemBinding binding, bool visible)
        {
            binding.Instance.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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
