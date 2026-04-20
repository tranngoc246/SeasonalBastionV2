using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class SaveAutosaveService
    {
        private readonly GameServices _s;

        public SaveAutosaveService(GameServices s)
        {
            _s = s;
            _s?.EventBus?.Subscribe<SeasonChangedEvent>(OnSeasonChanged);
        }

        private void OnSeasonChanged(SeasonChangedEvent ev)
        {
            if (_s?.SaveService == null || _s?.WorldState == null || _s?.RunClock == null)
                return;

            var res = _s.SaveService.SaveRunToSlot(_s.WorldState, _s.RunClock, 1, autosave: true);
            if (res.Code == SaveResultCode.Ok)
            {
                _s.NotificationService?.Push(
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
