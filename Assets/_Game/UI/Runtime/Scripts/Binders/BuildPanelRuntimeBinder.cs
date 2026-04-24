using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using SeasonalBastion.UI.Presenters;

namespace SeasonalBastion.UI.Binders
{
    /// <summary>
    /// Runtime bridge that maps GameServices state into BuildPanel view models.
    /// Keeps BuildPanelPresenter focused on view concerns.
    /// </summary>
    public sealed class BuildPanelRuntimeBinder
    {
        private readonly BuildPanelPresenter _presenter;
        private readonly GameServices _services;
        private readonly BuildPanelViewModelMapper _mapper;
        private readonly List<BuildCategoryViewModel> _categories = new();
        private readonly List<BuildingCardViewModel> _cards = new();
        private readonly StringComparer _idComparer = StringComparer.OrdinalIgnoreCase;

        private string _selectedCategoryId = BuildPanelCategories.All;
        private string _selectedDefId;

        public BuildPanelRuntimeBinder(BuildPanelPresenter presenter, GameServices services)
        {
            _presenter = presenter;
            _services = services;
            _mapper = new BuildPanelViewModelMapper(services);
        }

        public void Bind()
        {
            if (_presenter == null)
                return;

            _presenter.CloseRequested += HandleCloseRequested;
            _presenter.BuildRequested += HandleBuildRequested;
            _presenter.CategorySelected += HandleCategorySelected;
            _presenter.CardSelected += HandleCardSelected;

            if (_services?.EventBus != null)
            {
                _services.EventBus.Subscribe<ResourceDeliveredEvent>(OnRuntimeStateChanged);
                _services.EventBus.Subscribe<ResourceSpentEvent>(OnRuntimeStateChanged);
            }

            Refresh();
        }

        public void Unbind()
        {
            if (_presenter != null)
            {
                _presenter.CloseRequested -= HandleCloseRequested;
                _presenter.BuildRequested -= HandleBuildRequested;
                _presenter.CategorySelected -= HandleCategorySelected;
                _presenter.CardSelected -= HandleCardSelected;
            }

            if (_services?.EventBus == null)
                return;

            _services.EventBus.Unsubscribe<ResourceDeliveredEvent>(OnRuntimeStateChanged);
            _services.EventBus.Unsubscribe<ResourceSpentEvent>(OnRuntimeStateChanged);
        }

        public void Refresh()
        {
            BuildCategories();
            BuildCards();
            PushViewState();
        }

        private void BuildCategories()
        {
            _categories.Clear();
            _categories.AddRange(_mapper.CreateCategories());
        }

        private void BuildCards()
        {
            _cards.Clear();

            var dataRegistry = _services?.DataRegistry;
            if (dataRegistry == null)
                return;

            foreach (var defId in EnumerateBuildableNodeIds(dataRegistry))
            {
                if (!TryCreateCardViewModel(dataRegistry, defId, out var cardViewModel))
                    continue;

                _cards.Add(cardViewModel);
            }

            _cards.Sort((a, b) => string.Compare(a?.DisplayName, b?.DisplayName, StringComparison.OrdinalIgnoreCase));
            SyncSelectedCard();
        }

        private void PushViewState()
        {
            _presenter.SetCategories(_categories);
            _presenter.SetSelectedCategory(_selectedCategoryId);
            _presenter.SetCards(_cards);
            _presenter.SetSelectedCard(_selectedDefId);
            _presenter.SetBuildHint(_cards.Count > 0
                ? "Chọn công trình rồi bấm BUILD để vào placement mode."
                : "Chưa có building nào khả dụng để build ở thời điểm này.");
            _presenter.SetSelectedBuilding(_mapper.CreateDetailViewModel(_selectedDefId));
        }

        private bool TryCreateCardViewModel(IDataRegistry dataRegistry, string defId, out BuildingCardViewModel cardViewModel)
        {
            cardViewModel = null;

            if (string.IsNullOrWhiteSpace(defId))
                return false;

            if (!dataRegistry.IsPlaceableBuildable(defId))
                return false;

            if (_services.UnlockService != null && !_services.UnlockService.IsUnlocked(defId))
                return false;

            if (!dataRegistry.TryGetBuilding(defId, out var def) || def == null)
                return false;

            var categoryId = _mapper.GetCategoryId(def);
            if (!IsCategoryMatch(categoryId))
                return false;

            cardViewModel = _mapper.CreateCardViewModel(def, _selectedDefId);
            return cardViewModel != null;
        }

        private static IEnumerable<string> EnumerateBuildableNodeIds(IDataRegistry dataRegistry)
        {
            if (dataRegistry == null)
                yield break;

            if (dataRegistry is SeasonalBastion.DataRegistry concreteRegistry)
            {
                foreach (var defId in concreteRegistry.GetAllBuildableNodeIds())
                    yield return defId;
                yield break;
            }

            yield break;
        }

        private bool IsCategoryMatch(string categoryId)
        {
            return _idComparer.Equals(_selectedCategoryId, BuildPanelCategories.All)
                || _idComparer.Equals(_selectedCategoryId, categoryId);
        }

        private void SyncSelectedCard()
        {
            if (_cards.Count == 0)
            {
                _selectedDefId = null;
                return;
            }

            if (string.IsNullOrWhiteSpace(_selectedDefId) || _cards.FindIndex(card => _idComparer.Equals(card.Id, _selectedDefId)) < 0)
                _selectedDefId = _cards[0].Id;

            for (var index = 0; index < _cards.Count; index++)
                _cards[index].IsSelected = _idComparer.Equals(_cards[index].Id, _selectedDefId);
        }

        private void HandleCloseRequested()
        {
            _selectedDefId = null;
            _services?.EventBus?.Publish(new UiCloseBuildPanelRequestedEvent());
        }

        private void HandleBuildRequested()
        {
            if (string.IsNullOrWhiteSpace(_selectedDefId))
                return;

            var detail = _mapper.CreateDetailViewModel(_selectedDefId);
            if (detail == null || !detail.CanBuild)
                return;

            _services?.EventBus?.Publish(new UiBeginPlaceBuildingEvent(_selectedDefId));
            _services?.NotificationService?.Push(
                key: "ui.build.begin",
                title: "Chế độ xây dựng",
                body: "Chọn một ô trên bản đồ để đặt công trình. Q/E để xoay, ESC để hủy.",
                severity: NotificationSeverity.Info,
                payload: new NotificationPayload(default, default, _selectedDefId),
                cooldownSeconds: 1.5f,
                dedupeByKey: true);
        }

        private void HandleCategorySelected(string categoryId)
        {
            _selectedCategoryId = string.IsNullOrWhiteSpace(categoryId) ? BuildPanelCategories.All : categoryId;
            Refresh();
        }

        private void HandleCardSelected(string defId)
        {
            _selectedDefId = defId;
            PushViewState();
        }

        private void OnRuntimeStateChanged(ResourceDeliveredEvent _) => Refresh();
        private void OnRuntimeStateChanged(ResourceSpentEvent _) => Refresh();
    }

    internal sealed class BuildPanelViewModelMapper
    {
        private readonly GameServices _services;

        public BuildPanelViewModelMapper(GameServices services)
        {
            _services = services;
        }

        public IReadOnlyList<BuildCategoryViewModel> CreateCategories()
        {
            return new[]
            {
                new BuildCategoryViewModel { Id = BuildPanelCategories.All, DisplayName = "All" },
                new BuildCategoryViewModel { Id = BuildPanelCategories.Storage, DisplayName = "Kho" },
                new BuildCategoryViewModel { Id = BuildPanelCategories.Farm, DisplayName = "Farm" },
                new BuildCategoryViewModel { Id = BuildPanelCategories.Tower, DisplayName = "Tower" },
                new BuildCategoryViewModel { Id = BuildPanelCategories.Other, DisplayName = "Khác" },
            };
        }

        public BuildingCardViewModel CreateCardViewModel(BuildingDef def, string selectedDefId)
        {
            var availability = EvaluateAvailability(def);
            return new BuildingCardViewModel
            {
                Id = def.DefId,
                DisplayName = def.DefId,
                CostText = FormatCostSummary(def),
                StatusText = availability.StatusText,
                IconText = GetIconLetter(def.DefId),
                IsLocked = !availability.CanBuild,
                IsSelected = string.Equals(def.DefId, selectedDefId, StringComparison.OrdinalIgnoreCase),
            };
        }

        public BuildingDetailViewModel CreateDetailViewModel(string defId)
        {
            if (string.IsNullOrWhiteSpace(defId) || _services?.DataRegistry == null)
                return null;

            if (!_services.DataRegistry.TryGetBuilding(defId, out var def) || def == null)
                return null;

            var availability = EvaluateAvailability(def);
            var detail = new BuildingDetailViewModel
            {
                Id = def.DefId,
                DisplayName = def.DefId,
                SubText = $"{def.SizeX}x{def.SizeY}  Lv{def.BaseLevel}  HP {def.MaxHp}",
                Description = BuildDescription(def),
                StatusText = availability.StatusText,
                IconText = GetIconLetter(def.DefId),
                CanBuild = availability.CanBuild,
            };

            if (def.BuildCostsL1 == null || def.BuildCostsL1.Length == 0)
            {
                detail.CostRows.Add(new BuildingCostRowViewModel
                {
                    ResourceLabel = "Cost",
                    ValueText = "No cost",
                    IsAffordable = true,
                });
                return detail;
            }

            for (var index = 0; index < def.BuildCostsL1.Length; index++)
            {
                var cost = def.BuildCostsL1[index];
                var have = _services.StorageService != null ? _services.StorageService.GetTotal(cost.Resource) : 0;
                detail.CostRows.Add(new BuildingCostRowViewModel
                {
                    ResourceLabel = cost.Resource.ToString(),
                    ValueText = $"{have}/{cost.Amount}",
                    IsAffordable = have >= cost.Amount,
                });
            }

            return detail;
        }

        public string GetCategoryId(BuildingDef def)
        {
            if (def == null)
                return BuildPanelCategories.Other;
            if (def.IsWarehouse)
                return BuildPanelCategories.Storage;
            if (def.IsProducer)
                return BuildPanelCategories.Farm;
            if (def.IsTower)
                return BuildPanelCategories.Tower;
            return BuildPanelCategories.Other;
        }

        private BuildAvailability EvaluateAvailability(BuildingDef def)
        {
            if (def == null)
                return BuildAvailability.Disabled("Missing building definition.");

            if (_services?.RunOutcomeService != null && _services.RunOutcomeService.Outcome != RunOutcome.Ongoing)
                return BuildAvailability.Disabled("Run has ended.");

            if (def.BuildCostsL1 == null || def.BuildCostsL1.Length == 0)
                return BuildAvailability.Enabled();

            if (_services?.StorageService == null)
                return BuildAvailability.Disabled("Storage service missing.");

            for (var index = 0; index < def.BuildCostsL1.Length; index++)
            {
                var cost = def.BuildCostsL1[index];
                if (_services.StorageService.GetTotal(cost.Resource) < cost.Amount)
                    return BuildAvailability.Disabled("Không đủ tài nguyên để build.");
            }

            return BuildAvailability.Enabled();
        }

        private static string BuildDescription(BuildingDef def)
        {
            if (def == null)
                return string.Empty;

            if (def.IsWarehouse)
                return "Storage-focused building for keeping core resources available.";
            if (def.IsProducer)
                return "Producer building that supports your economy and food flow.";
            if (def.IsTower)
                return "Defensive building for protecting the bastion against incoming threats.";
            if (def.IsHouse)
                return "Housing building that supports population growth.";
            return "General-purpose structure available in the current build tree.";
        }

        private static string FormatCostSummary(BuildingDef def)
        {
            if (def?.BuildCostsL1 == null || def.BuildCostsL1.Length == 0)
                return "No cost";

            var parts = new string[def.BuildCostsL1.Length];
            for (var index = 0; index < def.BuildCostsL1.Length; index++)
            {
                var cost = def.BuildCostsL1[index];
                parts[index] = $"{cost.Resource}:{cost.Amount}";
            }

            return string.Join("  ", parts);
        }

        private static string GetIconLetter(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "?";

            for (var index = 0; index < id.Length; index++)
            {
                var c = id[index];
                if (c >= 'a' && c <= 'z')
                    return char.ToUpperInvariant(c).ToString();
                if (c >= 'A' && c <= 'Z')
                    return c.ToString();
            }

            return "?";
        }

        private readonly struct BuildAvailability
        {
            private BuildAvailability(bool canBuild, string statusText)
            {
                CanBuild = canBuild;
                StatusText = statusText;
            }

            public bool CanBuild { get; }
            public string StatusText { get; }

            public static BuildAvailability Enabled() => new(true, string.Empty);
            public static BuildAvailability Disabled(string statusText) => new(false, statusText ?? string.Empty);
        }
    }
}
