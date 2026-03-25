using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    public sealed class BuildPanelPresenter : UiPresenterBase
    {
        private enum BuildTab { All, Storage, Farm, Tower, Other }

        private GameServices _s;

        private Button _btnClose;

        // Left
        private ScrollView _scroll;
        private VisualElement _grid;
        private Label _buildHint;

        private Button _tabAll, _tabStorage, _tabFarm, _tabTower, _tabOther;
        private BuildTab _tab = BuildTab.All;

        // Right detail
        private VisualElement _detailIcon;
        private Label _detailIconText;
        private Label _detailName;
        private Label _detailSub;
        private VisualElement _detailCosts;
        private Label _detailCostHint;
        private Button _btnBuildConfirm;

        private readonly List<string> _ids = new(256);
        private string _selectedDefId;

        protected override void OnBind()
        {
            _s = Ctx?.Services as GameServices;

            Root.AddToClassList(UiKeys.Class_BlockWorld);
            Root.pickingMode = PickingMode.Position;

            _btnClose = Root.Q<Button>("BtnClose");

            _scroll = Root.Q<ScrollView>("BuildList");
            _grid = Root.Q<VisualElement>("BuildGridContainer");
            _buildHint = Root.Q<Label>("LblBuildHint");

            _tabAll = Root.Q<Button>("BtnTabAll");
            _tabStorage = Root.Q<Button>("BtnTabStorage");
            _tabFarm = Root.Q<Button>("BtnTabFarm");
            _tabTower = Root.Q<Button>("BtnTabTower");
            _tabOther = Root.Q<Button>("BtnTabOther");

            _detailIcon = Root.Q<VisualElement>("DetailIcon");
            _detailIconText = Root.Q<Label>("DetailIconText");
            _detailName = Root.Q<Label>("DetailName");
            _detailSub = Root.Q<Label>("DetailSub");
            _detailCosts = Root.Q<VisualElement>("DetailCosts");
            _detailCostHint = Root.Q<Label>("DetailCostHint");
            _btnBuildConfirm = Root.Q<Button>("BtnBuildConfirm");

            if (_btnClose != null) _btnClose.clicked += OnClose;

            if (_tabAll != null) _tabAll.clicked += () => SetTab(BuildTab.All);
            if (_tabStorage != null) _tabStorage.clicked += () => SetTab(BuildTab.Storage);
            if (_tabFarm != null) _tabFarm.clicked += () => SetTab(BuildTab.Farm);
            if (_tabTower != null) _tabTower.clicked += () => SetTab(BuildTab.Tower);
            if (_tabOther != null) _tabOther.clicked += () => SetTab(BuildTab.Other);

            if (_btnBuildConfirm != null) _btnBuildConfirm.clicked += OnBuildConfirm;

            // Refresh when resources change (cost enable)
            if (_s?.EventBus != null)
            {
                _s.EventBus.Subscribe<ResourceDeliveredEvent>(OnResourceChanged);
                _s.EventBus.Subscribe<ResourceSpentEvent>(OnResourceChanged);
            }

            RebuildGrid();
            RenderDetail();
        }

        protected override void OnUnbind()
        {
            if (_btnClose != null) _btnClose.clicked -= OnClose;
            if (_btnBuildConfirm != null) _btnBuildConfirm.clicked -= OnBuildConfirm;

            if (_s?.EventBus != null)
            {
                _s.EventBus.Unsubscribe<ResourceDeliveredEvent>(OnResourceChanged);
                _s.EventBus.Unsubscribe<ResourceSpentEvent>(OnResourceChanged);
            }

            _s = null;
        }

        protected override void OnRefresh()
        {
            RebuildGrid();
            RenderDetail();
        }

        private void OnClose()
        {
            // 1) hide qua registry (đúng flow)
            Ctx?.Panels?.HideCurrent();

            // 2) fallback: hide trực tiếp root (chắc chắn)
            if (Root != null) Root.style.display = DisplayStyle.None;

            // 3) optional: clear selection đang chọn trong build panel
            _selectedDefId = null;
        }

        private void SetTab(BuildTab t)
        {
            _tab = t;
            SetSelected(_tabAll, _tab == BuildTab.All);
            SetSelected(_tabStorage, _tab == BuildTab.Storage);
            SetSelected(_tabFarm, _tab == BuildTab.Farm);
            SetSelected(_tabTower, _tab == BuildTab.Tower);
            SetSelected(_tabOther, _tab == BuildTab.Other);

            RebuildGrid();
        }

        private static void SetSelected(Button b, bool on)
        {
            if (b == null) return;
            const string cls = "is-selected";
            if (on) b.AddToClassList(cls);
            else b.RemoveFromClassList(cls);
        }

        private void RebuildGrid()
        {
            if (_grid == null) return;

            _grid.Clear();
            _ids.Clear();

            var s = _s;
            if (s?.DataRegistry == null)
            {
                _grid.Add(new Label("DataRegistry missing"));
                return;
            }

            if (s.DataRegistry is SeasonalBastion.DataRegistry dr)
            {
                foreach (var id in dr.GetAllBuildableNodeIds())
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!s.DataRegistry.IsPlaceableBuildable(id)) continue;
                    if (s.UnlockService != null && !s.UnlockService.IsUnlocked(id)) continue;

                    // Filter by tab
                    if (_tab != BuildTab.All)
                    {
                        var def = s.DataRegistry.GetBuilding(id);
                        var cat = GetCategory(def);
                        if (cat != _tab) continue;
                    }

                    _ids.Add(id);
                }
            }

            _ids.Sort(StringComparer.OrdinalIgnoreCase);

            if ((_selectedDefId == null || !_ids.Contains(_selectedDefId)) && _ids.Count > 0)
                _selectedDefId = _ids[0];

            for (int i = 0; i < _ids.Count; i++)
                _grid.Add(MakeGridItem(_ids[i]));

            if (_buildHint != null)
            {
                _buildHint.text = _ids.Count > 0
                    ? "Chọn công trình rồi bấm BUILD để vào placement mode."
                    : "Chưa có building nào khả dụng để build ở thời điểm này.";
            }
        }

        private BuildTab GetCategory(BuildingDef def)
        {
            if (def == null) return BuildTab.Other;
            if (def.IsWarehouse) return BuildTab.Storage;
            if (def.IsProducer) return BuildTab.Farm;
            if (def.IsTower) return BuildTab.Tower;
            return BuildTab.Other;
        }

        private VisualElement MakeGridItem(string defId)
        {
            var item = new VisualElement();
            item.AddToClassList("build-grid-item");
            item.AddToClassList(UiKeys.Class_BlockWorld);
            item.pickingMode = PickingMode.Position;

            if (string.Equals(defId, _selectedDefId, StringComparison.OrdinalIgnoreCase))
                item.AddToClassList("is-selected");

            var icon = new VisualElement();
            icon.AddToClassList("build-icon");

            // Placeholder “icon”: chữ cái đầu
            var iconText = new Label(GetIconLetter(defId));
            iconText.AddToClassList("build-icon-text");
            icon.Add(iconText);

            var name = new Label(defId);
            name.AddToClassList("build-name");

            item.Add(icon);
            item.Add(name);

            item.RegisterCallback<ClickEvent>(_ =>
            {
                _selectedDefId = defId;
                RebuildGrid();   // update selection highlight
                RenderDetail();
            });

            return item;
        }

        private static string GetIconLetter(string id)
        {
            if (string.IsNullOrEmpty(id)) return "?";
            // bld_warehouse_t1 -> W
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                if (c >= 'a' && c <= 'z') return char.ToUpperInvariant(c).ToString();
                if (c >= 'A' && c <= 'Z') return c.ToString();
            }
            return "?";
        }

        private void RenderDetail()
        {
            var s = _s;

            if (string.IsNullOrEmpty(_selectedDefId) || s?.DataRegistry == null)
            {
                SetText(_detailIconText, "?");
                SetText(_detailName, "Chọn 1 building");
                SetText(_detailSub, "");
                if (_detailCosts != null) _detailCosts.Clear();
                HideHint();
                SetBuildEnabled(false);
                return;
            }

            var def = s.DataRegistry.GetBuilding(_selectedDefId);
            if (def == null)
            {
                SetText(_detailIconText, "?");
                SetText(_detailName, _selectedDefId);
                SetText(_detailSub, "(missing def)");
                if (_detailCosts != null) _detailCosts.Clear();
                HideHint();
                SetBuildEnabled(false);
                return;
            }

            SetText(_detailIconText, GetIconLetter(def.DefId));
            SetText(_detailName, def.DefId);
            SetText(_detailSub, $"{def.SizeX}x{def.SizeY}  Lv{def.BaseLevel}  HP {def.MaxHp}");

            BuildCosts(def);
        }

        private void BuildCosts(BuildingDef def)
        {
            if (_detailCosts == null) return;
            _detailCosts.Clear();

            var st = _s?.StorageService;
            bool canAfford = true;

            var costs = def.BuildCostsL1;
            if (costs == null || costs.Length == 0)
            {
                var row = new Label("No cost");
                _detailCosts.Add(row);
                HideHint();
                SetBuildEnabled(true);
                return;
            }

            for (int i = 0; i < costs.Length; i++)
            {
                var c = costs[i];
                int have = st != null ? st.GetTotal(c.Resource) : 0;
                bool ok = have >= c.Amount;
                if (!ok) canAfford = false;

                var row = new VisualElement();
                row.AddToClassList("cost-row");
                if (!ok) row.AddToClassList("insufficient");

                row.Add(new Label(c.Resource.ToString()));
                row.Add(new Label($"{have}/{c.Amount}"));

                _detailCosts.Add(row);
            }

            if (!canAfford)
            {
                ShowHint("Không đủ tài nguyên để build.");
            }
            else
            {
                HideHint();
            }

            SetBuildEnabled(canAfford);
        }

        private void SetBuildEnabled(bool enabled)
        {
            if (_btnBuildConfirm == null) return;
            _btnBuildConfirm.SetEnabled(enabled);
        }

        private void ShowHint(string t)
        {
            if (_detailCostHint == null) return;
            _detailCostHint.text = t ?? "";
            _detailCostHint.RemoveFromClassList("hidden");
        }

        private void HideHint()
        {
            if (_detailCostHint == null) return;
            _detailCostHint.text = "";
            _detailCostHint.AddToClassList("hidden");
        }

        private static void SetText(Label l, string t)
        {
            if (l == null) return;
            l.text = t ?? "";
        }

        private void OnBuildConfirm()
        {
            if (string.IsNullOrEmpty(_selectedDefId)) return;

            // 1) Ẩn build panel
            Ctx?.Panels?.HideCurrent();

            // 2) Báo hiệu vào “build flow” (để bạn wire sang placement controller)
            var bus = _s?.EventBus;
            if (bus != null)
                bus.Publish(new UiBeginPlaceBuildingEvent(_selectedDefId));

            // 3) Feedback
            _s?.NotificationService?.Push(
                key: "ui.build.begin",
                title: "Build mode",
                body: $"Placing {_selectedDefId}. Click map to place, Q/E to rotate, ESC to cancel.",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(default, default, _selectedDefId),
                cooldownSeconds: 0.5f,
                dedupeByKey: false);
        }

        private void OnResourceChanged(ResourceDeliveredEvent _) => RenderDetail();
        private void OnResourceChanged(ResourceSpentEvent _) => RenderDetail();
    }
}