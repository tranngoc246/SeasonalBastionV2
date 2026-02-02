using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace SeasonalBastion
{
    /// <summary>
    /// M3: Build panel lists unlocked BuildingDefs (data-driven) and lets player pick one.
    /// </summary>
    internal sealed class BuildPanelPresenter
    {
        private readonly GameServices _s;
        private readonly ToolModeController _toolMode;

        private readonly VisualElement _panel;
        private readonly Button _btnClose;
        private readonly ScrollView _list;

        private readonly List<string> _ids = new();

        private static MethodInfo s_getAllBuildingIdsMI;

        public bool IsVisible { get; private set; }

        public BuildPanelPresenter(VisualElement root, GameServices s, ToolModeController toolMode)
        {
            _s = s;
            _toolMode = toolMode;

            _panel = root.Q<VisualElement>("BuildPanel");
            _btnClose = root.Q<Button>("BtnBuildClose");
            _list = root.Q<ScrollView>("BuildList");
        }

        public void Bind()
        {
            Hide();

            if (_btnClose != null) _btnClose.clicked += Hide;
            RebuildList();
        }

        public void Unbind()
        {
            if (_btnClose != null) _btnClose.clicked -= Hide;
        }

        public void Toggle()
        {
            if (IsVisible) Hide();
            else Show();
        }

        public void Show()
        {
            if (_panel == null) return;
            _panel.RemoveFromClassList("hidden");
            IsVisible = true;
            RebuildList();
        }

        public void Hide()
        {
            if (_panel == null) return;
            _panel.AddToClassList("hidden");
            IsVisible = false;
        }

        private void RebuildList()
        {
            if (_list == null || _s == null || _s.DataRegistry == null) return;

            _list.Clear();
            _ids.Clear();

            CollectBuildingIdsCompat(_s.DataRegistry, _ids);
            _ids.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var id in _ids)
            {
                if (string.IsNullOrEmpty(id)) continue;

                // Unlock gating (if service missing, default allow)
                bool unlocked = _s.UnlockService == null || _s.UnlockService.IsUnlocked(id);
                if (!unlocked) continue;

                if (!TryGetBuildingDef(_s.DataRegistry, id, out var def))
                    continue;

                var item = new VisualElement();
                item.AddToClassList("build-item");

                var title = new Label(id);
                title.AddToClassList("build-item-title");

                var sub = new Label(BuildSubText(def));
                sub.AddToClassList("build-item-sub");

                item.Add(title);
                item.Add(sub);

                // Affordability info (soft). Actual placement consumes via BuildOrder service later.
                bool affordable = CanAfford(def);
                if (!affordable)
                    item.AddToClassList("is-disabled");

                item.RegisterCallback<ClickEvent>(_ =>
                {
                    _toolMode?.BeginBuildWithDef(id);
                    Hide();
                });

                _list.Add(item);
            }
        }

        private static void CollectBuildingIdsCompat(IDataRegistry reg, List<string> outIds)
        {
            if (reg == null) return;

            // DataRegistry có helper GetAllBuildingIds() nhưng KHÔNG nằm trong interface.
            // Dùng reflection để không phá Part25 contract.
            try
            {
                s_getAllBuildingIdsMI ??= reg.GetType().GetMethod("GetAllBuildingIds",
                    BindingFlags.Public | BindingFlags.Instance);

                if (s_getAllBuildingIdsMI != null)
                {
                    var obj = s_getAllBuildingIdsMI.Invoke(reg, null);
                    if (obj is IEnumerable enumerable)
                    {
                        foreach (var it in enumerable)
                        {
                            if (it is string s && !string.IsNullOrEmpty(s))
                                outIds.Add(s);
                        }
                    }
                }
            }
            catch
            {
                // ignore: empty list
            }
        }

        private static bool TryGetBuildingDef(IDataRegistry reg, string defId, out BuildingDef def)
        {
            def = null;
            if (reg == null || string.IsNullOrEmpty(defId)) return false;

            try
            {
                def = reg.GetBuilding(defId);
                return def != null;
            }
            catch
            {
                def = null;
                return false;
            }
        }

        private bool CanAfford(BuildingDef def)
        {
            if (_s.StorageService == null) return true;

            var costs = def.BuildCostsL1;
            if (costs == null || costs.Length == 0) return true;

            for (int i = 0; i < costs.Length; i++)
            {
                var c = costs[i];
                if (c.Amount <= 0) continue;
                if (_s.StorageService.GetTotal(c.Resource) < c.Amount) return false;
            }

            return true;
        }

        private static string BuildSubText(BuildingDef def)
        {
            int w = Mathf.Max(1, def.SizeX);
            int h = Mathf.Max(1, def.SizeY);

            string costs = "";
            if (def.BuildCostsL1 != null && def.BuildCostsL1.Length > 0)
            {
                for (int i = 0; i < def.BuildCostsL1.Length; i++)
                {
                    var c = def.BuildCostsL1[i];
                    if (c.Amount <= 0) continue;
                    if (costs.Length > 0) costs += " • ";
                    costs += $"{c.Resource}:{c.Amount}";
                }
            }
            else
            {
                costs = "no cost";
            }

            return $"{w}x{h} • {costs}";
        }
    }
}
