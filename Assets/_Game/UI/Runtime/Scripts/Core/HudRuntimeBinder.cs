using SeasonalBastion.Contracts;
using SeasonalBastion.UI.Presenters;

namespace SeasonalBastion.UI
{
    /// <summary>
    /// Runtime-only orchestration for HUD data refresh.
    /// Keeps HudPresenter as a pure view presenter.
    /// </summary>
    public sealed class HudRuntimeBinder
    {
        private const string ResourceItemWood = "ResourceItemWood";
        private const string ResourceItemStone = "ResourceItemStone";
        private const string ResourceItemIron = "ResourceItemIron";
        private const string ResourceItemFood = "ResourceItemFood";
        private const string ResourceItemAmmo = "ResourceItemAmmo";

        private readonly HudPresenter _hudPresenter;
        private readonly GameServices _services;

        public HudRuntimeBinder(HudPresenter hudPresenter, object services)
        {
            _hudPresenter = hudPresenter;
            _services = services as GameServices;
        }

        public void Bind()
        {
            if (_services?.EventBus == null)
            {
                RefreshResourceValues();
                return;
            }

            _services.EventBus.Subscribe<ResourceDeliveredEvent>(OnResourceChanged);
            _services.EventBus.Subscribe<ResourceSpentEvent>(OnResourceChanged);
            _services.EventBus.Subscribe<AmmoUsedEvent>(OnAmmoChanged);

            RefreshResourceValues();
        }

        public void Unbind()
        {
            if (_services?.EventBus == null)
                return;

            _services.EventBus.Unsubscribe<ResourceDeliveredEvent>(OnResourceChanged);
            _services.EventBus.Unsubscribe<ResourceSpentEvent>(OnResourceChanged);
            _services.EventBus.Unsubscribe<AmmoUsedEvent>(OnAmmoChanged);
        }

        public void Refresh()
        {
            RefreshResourceValues();
        }

        private void RefreshResourceValues()
        {
            var storageService = _services?.StorageService;
            if (storageService == null || _hudPresenter == null)
                return;

            _hudPresenter.SetResourceValue(ResourceItemWood, storageService.GetTotal(ResourceType.Wood).ToString());
            _hudPresenter.SetResourceValue(ResourceItemStone, storageService.GetTotal(ResourceType.Stone).ToString());
            _hudPresenter.SetResourceValue(ResourceItemIron, storageService.GetTotal(ResourceType.Iron).ToString());
            _hudPresenter.SetResourceValue(ResourceItemFood, storageService.GetTotal(ResourceType.Food).ToString());
            _hudPresenter.SetResourceValue(ResourceItemAmmo, storageService.GetTotal(ResourceType.Ammo).ToString());
        }

        private void OnResourceChanged(ResourceDeliveredEvent _) => RefreshResourceValues();
        private void OnResourceChanged(ResourceSpentEvent _) => RefreshResourceValues();
        private void OnAmmoChanged(AmmoUsedEvent _) => RefreshResourceValues();
    }
}
