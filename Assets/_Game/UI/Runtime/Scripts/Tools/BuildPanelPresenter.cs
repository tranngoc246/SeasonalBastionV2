using SeasonalBastion.Contracts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using static SeasonalBastion.Contracts.AmmoUsedEvent;

namespace SeasonalBastion
{
    /// <summary>
    /// Build panel:
    /// - Always shows ALL BuildingDefs (except HQ special).
    /// - Tabs by feature (Storage / Production / Defense / Other) when tab buttons exist in UXML.
    /// - Locked: dim + non-clickable + pushed to bottom.
    /// - Unlocked: normal + selectable.
    /// - Refresh on UnlocksChangedEvent.
    /// </summary>
    internal sealed class BuildPanelPresenter
    {
        private readonly GameServices _s;
        private readonly ToolModeController _toolMode;

        private readonly VisualElement _panel;
        private readonly Button _btnClose;
        private readonly ScrollView _list;

        // Optional tab buttons (may not exist in older UXML)
        private readonly Button _tabStorage;
        private readonly Button _tabProduction;
        private readonly Button _tabDefense;
        private readonly Button _tabOther;

        private readonly List<string> _ids = new();
        private readonly List<Entry> _entries = new(128);

        private static MethodInfo s_getAllBuildingIdsMI;

        public bool IsVisible { get; private set; }

        private enum Tab { Storage, Production, Defense, Other, All }

        private Tab _tab = Tab.Storage;

        private sealed class Entry
        {
            public string Id;
            public BuildingDef Def;
            public Tab Tab;
            public bool Unlocked;
        }

        // HQ is special singleton — never appears in build list
        private const string HQ_DEF_ID = "bld_hq_t1";

        public BuildPanelPresenter(VisualElement root, GameServices s, ToolModeController toolMode)
        {
            _s = s;
            _toolMode = toolMode;

            _panel = root.Q<VisualElement>("BuildPanel");
            _btnClose = root.Q<Button>("BtnBuildClose");
            _list = root.Q<ScrollView>("BuildList");

            _tabStorage = root.Q<Button>("BtnTabStorage");
            _tabProduction = root.Q<Button>("BtnTabProduction");
            _tabDefense = root.Q<Button>("BtnTabDefense");
            _tabOther = root.Q<Button>("BtnTabOther");

            // If no tabs in UXML, fallback to showing all in one list.
            if (_tabStorage == null && _tabProduction == null && _tabDefense == null && _tabOther == null)
                _tab = Tab.All;
        }

        public void Bind()
        {
            Hide();

            if (_btnClose != null) _btnClose.clicked += Hide;

            // Tabs (optional)
            if (_tabStorage != null) _tabStorage.clicked += OnTabStorage;
            if (_tabProduction != null) _tabProduction.clicked += OnTabProduction;
            if (_tabDefense != null) _tabDefense.clicked += OnTabDefense;
            if (_tabOther != null) _tabOther.clicked += OnTabOther;

            if (_s?.EventBus != null)
                _s.EventBus.Subscribe<UnlocksChangedEvent>(OnUnlocksChanged);

            RebuildList();
            RefreshTabButtonStates();
        }

        public void Unbind()
        {
            if (_btnClose != null) _btnClose.clicked -= Hide;

            if (_tabStorage != null) _tabStorage.clicked -= OnTabStorage;
            if (_tabProduction != null) _tabProduction.clicked -= OnTabProduction;
            if (_tabDefense != null) _tabDefense.clicked -= OnTabDefense;
            if (_tabOther != null) _tabOther.clicked -= OnTabOther;

            if (_s?.EventBus != null)
                _s.EventBus.Unsubscribe<UnlocksChangedEvent>(OnUnlocksChanged);
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
            RefreshTabButtonStates();
        }

        public void Hide()
        {
            if (_panel == null) return;
            _panel.AddToClassList("hidden");
            IsVisible = false;
        }

        private void OnUnlocksChanged(UnlocksChangedEvent ev)
        {
            if (IsVisible)
                RebuildList();
        }

        private void OnTabStorage() => SetTab(Tab.Storage);
        private void OnTabProduction() => SetTab(Tab.Production);
        private void OnTabDefense() => SetTab(Tab.Defense);
        private void OnTabOther() => SetTab(Tab.Other);

        private void SetTab(Tab tab)
        {
            if (_tab == tab) return;
            _tab = tab;
            RefreshTabButtonStates();
            RebuildList();
        }

        private void RefreshTabButtonStates()
        {
            // If no tabs in UXML, do nothing.
            SetSelected(_tabStorage, _tab == Tab.Storage);
            SetSelected(_tabProduction, _tab == Tab.Production);
            SetSelected(_tabDefense, _tab == Tab.Defense);
            SetSelected(_tabOther, _tab == Tab.Other);
        }

        private static void SetSelected(Button btn, bool selected)
        {
            if (btn == null) return;
            if (selected) btn.AddToClassList("is-selected");
            else btn.RemoveFromClassList("is-selected");
        }

        private void RebuildList()
        {
            if (_list == null || _s == null || _s.DataRegistry == null) return;

            _list.Clear();
            _ids.Clear();
            _entries.Clear();

            CollectBuildingIdsCompat(_s.DataRegistry, _ids);
            _ids.Sort(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _ids.Count; i++)
            {
                var id = _ids[i];
                if (string.IsNullOrEmpty(id)) continue;

                // Exclude HQ (special singleton)
                if (string.Equals(id, HQ_DEF_ID, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!TryGetBuildingDef(_s.DataRegistry, id, out var def))
                    continue;

                var tab = Categorize(def, id);
                if (_tab != Tab.All && tab != _tab)
                    continue;

                bool unlocked = _s.UnlockService == null || _s.UnlockService.IsUnlocked(id);

                _entries.Add(new Entry
                {
                    Id = id,
                    Def = def,
                    Tab = tab,
                    Unlocked = unlocked
                });
            }

            // Sort: unlocked first, locked last, then by id
            _entries.Sort((a, b) =>
            {
                int au = a.Unlocked ? 0 : 1;
                int bu = b.Unlocked ? 0 : 1;
                int c = au.CompareTo(bu);
                if (c != 0) return c;
                return StringComparer.OrdinalIgnoreCase.Compare(a.Id, b.Id);
            });

            for (int i = 0; i < _entries.Count; i++)
                AddItem(_entries[i]);
        }

        private void AddItem(Entry e)
        {
            var item = new VisualElement();
            item.AddToClassList("build-item");

            var title = new Label(e.Id);
            title.AddToClassList("build-item-title");

            var sub = new Label(BuildSubText(e.Def));
            sub.AddToClassList("build-item-sub");

            item.Add(title);
            item.Add(sub);

            if (!e.Unlocked)
            {
                // Locked: dim + no click
                item.AddToClassList("is-locked");
                // Không register click => không cho chọn
            }
            else
            {
                // Unlocked: allow choose
                item.RegisterCallback<ClickEvent>(_ =>
                {
                    _toolMode?.BeginBuildWithDef(e.Id);
                    Hide();
                });
            }

            _list.Add(item);
        }

        private static Tab Categorize(BuildingDef def, string id)
        {
            if (def != null && def.IsTower) return Tab.Defense;

            var s = (id ?? string.Empty).ToLowerInvariant();

            // Storage-like
            if (s.Contains("warehouse") || s.Contains("storage") || s.Contains("armory"))
                return Tab.Storage;

            // Production-like
            if (s.Contains("farm") || s.Contains("lumber") || s.Contains("quarry") || s.Contains("iron") || s.Contains("forge"))
                return Tab.Production;

            // Defense-like (fallback)
            if (s.Contains("tower") || s.Contains("def") || s.Contains("wall") || s.Contains("gate"))
                return Tab.Defense;

            return Tab.Other;
        }

        private static void CollectBuildingIdsCompat(IDataRegistry reg, List<string> outIds)
        {
            if (reg == null) return;

            // DataRegistry có helper GetAllBuildingIds() nhưng KHÔNG nằm trong interface.
            // Dùng reflection để không phá Part25 contract.
            try
            {
                s_getAllBuildingIdsMI ??= reg.GetType().GetMethod(
                    "GetAllBuildingIds",
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
