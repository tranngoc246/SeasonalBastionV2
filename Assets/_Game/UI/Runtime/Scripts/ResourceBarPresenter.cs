using System;
using System.Reflection;
using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    internal sealed class ResourceBarPresenter
    {
        private const float CompactWidthThreshold = 980f; // bạn có thể chỉnh 900/1024 tuỳ thích
        private const int FlashMs = 150;

        private readonly GameServices _s;
        private readonly VisualElement _root;

        private VisualElement _bar;

        private VisualElement _chipWood;
        private VisualElement _chipStone;
        private VisualElement _chipFood;
        private VisualElement _chipIron;
        private VisualElement _chipAmmo;

        private Label _wood;
        private Label _stone;
        private Label _food;
        private Label _iron;
        private Label _ammo;

        private readonly int _pollMs;
        private IVisualElementScheduledItem _scheduled;

        // cached reflection if storage service doesn't expose interface here
        private MethodInfo _miGetTotal;

        // last totals to detect increases
        private int _lastWood = int.MinValue;
        private int _lastStone = int.MinValue;
        private int _lastFood = int.MinValue;
        private int _lastIron = int.MinValue;
        private int _lastAmmo = int.MinValue;

        private bool _compact;

        public ResourceBarPresenter(VisualElement root, GameServices s, float pollSeconds = 0.33f)
        {
            _root = root;
            _s = s;
            _pollMs = Mathf.Clamp((int)(pollSeconds * 1000f), 200, 1000);
        }

        public void Bind()
        {
            _bar = _root.Q<VisualElement>("ResourceBar");

            _chipWood = _root.Q<VisualElement>("ChipWood");
            _chipStone = _root.Q<VisualElement>("ChipStone");
            _chipFood = _root.Q<VisualElement>("ChipFood");
            _chipIron = _root.Q<VisualElement>("ChipIron");
            _chipAmmo = _root.Q<VisualElement>("ChipAmmo");

            _wood = _root.Q<Label>("LblResWood");
            _stone = _root.Q<Label>("LblResStone");
            _food = _root.Q<Label>("LblResFood");
            _iron = _root.Q<Label>("LblResIron");
            _ammo = _root.Q<Label>("LblResAmmo");

            if (_bar == null ||
                _wood == null || _stone == null || _food == null || _iron == null || _ammo == null)
            {
                Debug.LogWarning("[UI] ResourceBar missing elements. Check HUD.uxml names.");
                return;
            }

            Refresh(); // initial paint

            _scheduled = _root.schedule.Execute(Refresh).Every(_pollMs);
        }

        public void Unbind()
        {
            _scheduled?.Pause();
            _scheduled = null;
        }

        private void Refresh()
        {
            var storage = _s?.StorageService;
            if (storage == null) return;

            // --- Compact mode based on current width ---
            float w = _root.resolvedStyle.width;
            if (w > 0f)
            {
                bool shouldCompact = w < CompactWidthThreshold;
                if (shouldCompact != _compact)
                {
                    _compact = shouldCompact;
                    if (_compact) _bar.AddToClassList("is-compact");
                    else _bar.RemoveFromClassList("is-compact");
                }
            }

            // --- Totals ---
            int wood = GetTotalSafe(storage, ResourceType.Wood);
            int stone = GetTotalSafe(storage, ResourceType.Stone);
            int food = GetTotalSafe(storage, ResourceType.Food);
            int iron = GetTotalSafe(storage, ResourceType.Iron);
            int ammo = GetTotalSafe(storage, ResourceType.Ammo);

            // --- Apply text + flash if increased ---
            Apply(_wood, _chipWood, ref _lastWood, wood);
            Apply(_stone, _chipStone, ref _lastStone, stone);
            Apply(_food, _chipFood, ref _lastFood, food);
            Apply(_iron, _chipIron, ref _lastIron, iron);
            Apply(_ammo, _chipAmmo, ref _lastAmmo, ammo);
        }

        private void Apply(Label lbl, VisualElement chip, ref int last, int now)
        {
            if (lbl == null) return;

            // first time init
            if (last == int.MinValue)
            {
                last = now;
                lbl.text = now.ToString();
                return;
            }

            if (now != last)
            {
                lbl.text = now.ToString();

                if (chip != null && now > last)
                {
                    // flash on increase
                    chip.AddToClassList("flash");
                    chip.schedule.Execute(() => chip.RemoveFromClassList("flash")).StartingIn(FlashMs);
                }

                last = now;
            }
        }

        /// <summary>
        /// Ưu tiên gọi storage.GetTotal(ResourceType) nếu có; fallback reflection.
        /// </summary>
        private int GetTotalSafe(object storageService, ResourceType t)
        {
            try
            {
                if (_miGetTotal == null)
                {
                    var ty = storageService.GetType();
                    _miGetTotal = ty.GetMethod("GetTotal", BindingFlags.Instance | BindingFlags.Public, null,
                        new[] { typeof(ResourceType) }, null);
                }

                if (_miGetTotal != null)
                {
                    var r = _miGetTotal.Invoke(storageService, new object[] { t });
                    if (r is int i) return i;
                    if (r is long l) return (int)Mathf.Clamp(l, int.MinValue, int.MaxValue);
                }
            }
            catch { /* ignore */ }

            return 0;
        }
    }
}
