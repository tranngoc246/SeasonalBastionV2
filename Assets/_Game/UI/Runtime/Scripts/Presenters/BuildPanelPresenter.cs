using System;
using System.Collections.Generic;
using SeasonalBastion.UI.Views;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    public sealed class BuildPanelPresenter : UiPresenterBase
    {
        private const string SelectedClass = "is-selected";

        private Button _btnClose;
        private Button _btnBuildConfirm;
        private Label _buildHint;
        private VisualElement _detailIcon;
        private VisualElement _grid;

        private Button _tabAll;
        private Button _tabStorage;
        private Button _tabFarm;
        private Button _tabTower;
        private Button _tabOther;

        private Label _detailIconText;
        private Label _detailName;
        private Label _detailSub;
        private Label _detailDescription;
        private VisualElement _detailCosts;
        private Label _detailCostHint;

        private readonly Dictionary<string, Button> _categoryButtons = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, BuildingCardBinding> _cardBindings = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<CategoryBinding> _categoryBindings = new();

        private BuildingCardFactory _cardFactory;
        private string _selectedCardId;
        private string _selectedCategoryId = BuildPanelCategories.All;

        public event Action CloseRequested;
        public event Action BuildRequested;
        public event Action<string> CategorySelected;
        public event Action<string> CardSelected;

        protected override void OnBind()
        {
            Root.AddToClassList(UiKeys.Class_BlockWorld);
            Root.pickingMode = PickingMode.Position;

            _btnClose = Root.Q<Button>("BtnClose");
            _btnBuildConfirm = Root.Q<Button>("BtnBuildConfirm");
            _buildHint = Root.Q<Label>("LblBuildHint");
            _grid = Root.Q<VisualElement>("BuildGridContainer");
            _detailIcon = Root.Q<VisualElement>("DetailIcon");

            _tabAll = Root.Q<Button>("BtnTabAll");
            _tabStorage = Root.Q<Button>("BtnTabStorage");
            _tabFarm = Root.Q<Button>("BtnTabFarm");
            _tabTower = Root.Q<Button>("BtnTabTower");
            _tabOther = Root.Q<Button>("BtnTabOther");

            _detailIconText = Root.Q<Label>("DetailIconText");
            _detailName = Root.Q<Label>("DetailName");
            _detailSub = Root.Q<Label>("DetailSub");
            _detailDescription = Root.Q<Label>("DetailDescription");
            _detailCosts = Root.Q<VisualElement>("DetailCosts");
            _detailCostHint = Root.Q<Label>("DetailCostHint");

            _cardFactory = new BuildingCardFactory(Root);

            CacheCategoryButtons();
            RegisterCallbacks();
            RenderCategorySelection();
        }

        protected override void OnUnbind()
        {
            UnregisterCallbacks();

            for (var index = 0; index < _categoryBindings.Count; index++)
                _categoryBindings[index].Detach();

            _categoryBindings.Clear();
            _categoryButtons.Clear();
            _cardBindings.Clear();
            _cardFactory = null;
            _selectedCardId = null;
        }

        protected override void OnRefresh()
        {
        }

        public void Show() => UiElementUtil.SetVisible(Root, true);
        public void Hide() => UiElementUtil.SetVisible(Root, false);
        public void SetVisible(bool visible) => UiElementUtil.SetVisible(Root, visible);

        public void SetBuildHint(string text)
        {
            if (_buildHint != null)
                _buildHint.text = text ?? string.Empty;
        }

        public void SetCategories(IReadOnlyList<BuildCategoryViewModel> categories)
        {
            if (categories == null || categories.Count == 0)
                return;

            foreach (var category in categories)
            {
                if (category == null || string.IsNullOrWhiteSpace(category.Id))
                    continue;

                if (_categoryButtons.TryGetValue(category.Id, out var button) && button != null)
                {
                    button.text = string.IsNullOrWhiteSpace(category.DisplayName) ? category.Id : category.DisplayName;
                    button.SetEnabled(category.IsEnabled);
                }
            }
        }

        public void SetSelectedCategory(string categoryId)
        {
            _selectedCategoryId = string.IsNullOrWhiteSpace(categoryId) ? BuildPanelCategories.All : categoryId;
            RenderCategorySelection();
        }

        public void SetCards(IReadOnlyList<BuildingCardViewModel> cards)
        {
            _cardBindings.Clear();

            if (_grid == null)
                return;

            _grid.Clear();

            if (cards == null)
                return;

            for (var index = 0; index < cards.Count; index++)
            {
                var viewModel = cards[index];
                if (viewModel == null || string.IsNullOrWhiteSpace(viewModel.Id))
                    continue;

                var cardView = _cardFactory?.Create();
                if (cardView?.Root == null)
                    continue;

                cardView.Bind(
                    viewModel.DisplayName,
                    viewModel.CostText,
                    viewModel.IconText,
                    viewModel.StatusText,
                    viewModel.IsSelected,
                    viewModel.IsLocked);

                var capturedId = viewModel.Id;
                cardView.Root.RegisterCallback<ClickEvent>(_ => CardSelected?.Invoke(capturedId));

                _cardBindings[capturedId] = new BuildingCardBinding(cardView);
                _grid.Add(cardView.Root);
            }

            SetSelectedCard(_selectedCardId);
        }

        public void SetSelectedCard(string id)
        {
            _selectedCardId = id;

            foreach (var pair in _cardBindings)
            {
                var isSelected = !string.IsNullOrWhiteSpace(id) && string.Equals(pair.Key, id, StringComparison.OrdinalIgnoreCase);
                SetClass(pair.Value.View.Root, SelectedClass, isSelected);
            }
        }

        public void SetSelectedBuilding(BuildingDetailViewModel detail)
        {
            if (detail == null)
            {
                SetClass(_detailIcon, "is-locked", false);
                SetText(_detailIconText, "?");
                SetText(_detailName, "Chọn 1 building");
                SetText(_detailSub, string.Empty);
                SetText(_detailDescription, string.Empty);
                _detailCosts?.Clear();
                SetStatusText(string.Empty);
                SetBuildButtonEnabled(false);
                return;
            }

            SetClass(_detailIcon, "is-locked", !detail.CanBuild && !string.IsNullOrWhiteSpace(detail.StatusText));
            SetText(_detailIconText, detail.IconText);
            SetText(_detailName, detail.DisplayName);
            SetText(_detailSub, detail.SubText);
            SetText(_detailDescription, detail.Description);
            RenderCostRows(detail.CostRows);
            SetStatusText(detail.StatusText);
            SetBuildButtonEnabled(detail.CanBuild);
        }

        public void SetBuildButtonEnabled(bool enabled)
        {
            _btnBuildConfirm?.SetEnabled(enabled);
        }

        private void CacheCategoryButtons()
        {
            _categoryButtons[BuildPanelCategories.All] = _tabAll;
            _categoryButtons[BuildPanelCategories.Storage] = _tabStorage;
            _categoryButtons[BuildPanelCategories.Farm] = _tabFarm;
            _categoryButtons[BuildPanelCategories.Tower] = _tabTower;
            _categoryButtons[BuildPanelCategories.Other] = _tabOther;
        }

        private void RegisterCallbacks()
        {
            if (_btnClose != null)
                _btnClose.clicked += HandleCloseClicked;

            if (_btnBuildConfirm != null)
                _btnBuildConfirm.clicked += HandleBuildClicked;

            RegisterCategoryClick(_tabAll, BuildPanelCategories.All);
            RegisterCategoryClick(_tabStorage, BuildPanelCategories.Storage);
            RegisterCategoryClick(_tabFarm, BuildPanelCategories.Farm);
            RegisterCategoryClick(_tabTower, BuildPanelCategories.Tower);
            RegisterCategoryClick(_tabOther, BuildPanelCategories.Other);
        }

        private void UnregisterCallbacks()
        {
            if (_btnClose != null)
                _btnClose.clicked -= HandleCloseClicked;

            if (_btnBuildConfirm != null)
                _btnBuildConfirm.clicked -= HandleBuildClicked;
        }

        private void RegisterCategoryClick(Button button, string categoryId)
        {
            if (button == null)
                return;

            _categoryBindings.Add(new CategoryBinding(button, () => HandleCategoryButtonClicked(categoryId)));
        }

        private void HandleCloseClicked() => CloseRequested?.Invoke();
        private void HandleBuildClicked() => BuildRequested?.Invoke();

        private void HandleCategoryButtonClicked(string categoryId)
        {
            if (string.IsNullOrWhiteSpace(categoryId))
                return;

            SetSelectedCategory(categoryId);
            CategorySelected?.Invoke(categoryId);
        }

        private void RenderCategorySelection()
        {
            foreach (var pair in _categoryButtons)
            {
                if (pair.Value == null)
                    continue;

                SetClass(pair.Value, SelectedClass, string.Equals(pair.Key, _selectedCategoryId, StringComparison.OrdinalIgnoreCase));
            }
        }

        private void RenderCostRows(IReadOnlyList<BuildingCostRowViewModel> rows)
        {
            if (_detailCosts == null)
                return;

            _detailCosts.Clear();
            if (rows == null)
                return;

            for (var index = 0; index < rows.Count; index++)
            {
                var rowViewModel = rows[index];
                if (rowViewModel == null)
                    continue;

                var row = new VisualElement();
                row.AddToClassList("cost-row");
                if (!rowViewModel.IsAffordable)
                    row.AddToClassList("insufficient");

                row.Add(new Label(rowViewModel.ResourceLabel ?? string.Empty));
                row.Add(new Label(rowViewModel.ValueText ?? string.Empty));
                _detailCosts.Add(row);
            }
        }

        private void SetStatusText(string text)
        {
            if (_detailCostHint == null)
                return;

            _detailCostHint.text = text ?? string.Empty;
            SetClass(_detailCostHint, "hidden", string.IsNullOrWhiteSpace(text));
        }

        private static void SetText(Label label, string value)
        {
            if (label != null)
                label.text = value ?? string.Empty;
        }

        private static void SetClass(VisualElement element, string className, bool enabled)
        {
            if (element == null)
                return;

            if (enabled)
                element.AddToClassList(className);
            else
                element.RemoveFromClassList(className);
        }

        private sealed class BuildingCardBinding
        {
            public BuildingCardBinding(BuildingCardView view)
            {
                View = view;
            }

            public BuildingCardView View { get; }
        }

        private sealed class CategoryBinding
        {
            private readonly Button _button;
            private readonly Action _callback;

            public CategoryBinding(Button button, Action callback)
            {
                _button = button;
                _callback = callback;
                _button.clicked += _callback;
            }

            public void Detach()
            {
                if (_button != null && _callback != null)
                    _button.clicked -= _callback;
            }
        }
    }

    public static class BuildPanelCategories
    {
        public const string All = "all";
        public const string Storage = "storage";
        public const string Farm = "farm";
        public const string Tower = "tower";
        public const string Other = "other";
    }

    [Serializable]
    public sealed class BuildCategoryViewModel
    {
        public string Id;
        public string DisplayName;
        public bool IsEnabled = true;
    }

    [Serializable]
    public sealed class BuildingCardViewModel
    {
        public string Id;
        public string DisplayName;
        public string CostText;
        public string StatusText;
        public string IconText;
        public bool IsLocked;
        public bool IsSelected;
    }

    [Serializable]
    public sealed class BuildingDetailViewModel
    {
        public string Id;
        public string DisplayName;
        public string SubText;
        public string Description;
        public string StatusText;
        public string IconText;
        public bool CanBuild;
        public List<BuildingCostRowViewModel> CostRows = new();
    }

    [Serializable]
    public sealed class BuildingCostRowViewModel
    {
        public string ResourceLabel;
        public string ValueText;
        public bool IsAffordable;
    }
}
