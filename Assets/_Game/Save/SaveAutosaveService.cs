using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class SaveAutosaveService
    {
        private readonly IEventBus _eventBus;
        private readonly ISaveService _saveService;
        private readonly IWorldState _worldState;
        private readonly IRunClock _runClock;
        private readonly INotificationService _notificationService;

        public SaveAutosaveService(
            IEventBus eventBus,
            ISaveService saveService,
            IWorldState worldState,
            IRunClock runClock,
            INotificationService notificationService)
        {
            _eventBus = eventBus;
            _saveService = saveService;
            _worldState = worldState;
            _runClock = runClock;
            _notificationService = notificationService;
            _eventBus?.Subscribe<SeasonChangedEvent>(OnSeasonChanged);
        }

        private void OnSeasonChanged(SeasonChangedEvent ev)
        {
            if (_saveService == null || _worldState == null || _runClock == null)
                return;

            var res = _saveService.SaveRunToSlot(_worldState, _runClock, 1, autosave: true);
            if (res.Code == SaveResultCode.Ok)
            {
                _notificationService?.Push(
                    key: "autosave.season",
                    title: "Tự động lưu",
                    body: "Đã tự động lưu khi sang mùa mới.",
                    severity: NotificationSeverity.Info,
                    payload: default,
                    cooldownSeconds: 45f,
                    dedupeByKey: true);
            }
        }
    }
}
