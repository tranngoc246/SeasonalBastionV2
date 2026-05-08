using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class PlacementActionController
    {
        private readonly IPlacementService _placement;
        private readonly INotificationService _notifications;
        private readonly IEventBus _bus;

        public PlacementActionController(
            IPlacementService placement,
            INotificationService notifications,
            IEventBus bus)
        {
            _placement = placement;
            _notifications = notifications;
            _bus = bus;
        }

        public bool TryCommitBuilding(string placeDefId, CellPos cell, Dir4 rotation)
        {
            var validation = _placement.ValidateBuilding(placeDefId, cell, rotation);
            if (!validation.Ok)
            {
                _notifications?.Push(
                    key: "place.fail",
                    title: "Place failed",
                    body: $"{validation.FailReason}",
                    severity: NotificationSeverity.Warning,
                    payload: new NotificationPayload(default, default, placeDefId),
                    cooldownSeconds: 0.2f,
                    dedupeByKey: false);
                return false;
            }

            var buildingId = _placement.CommitBuilding(placeDefId, cell, rotation);
            if (buildingId.Value == 0)
            {
                _notifications?.Push(
                    "place.commit.fail",
                    "Place failed",
                    "Commit returned default.",
                    NotificationSeverity.Error,
                    new NotificationPayload(default, default, placeDefId),
                    0.2f,
                    false);
                return false;
            }

            _notifications?.Push(
                "place.ok",
                "Building placed",
                $"Id={buildingId.Value}",
                NotificationSeverity.Info,
                new NotificationPayload(default, default, ""),
                0.2f,
                false);

            _bus?.Publish(new UiPlacementFinishedEvent(placeDefId, true));
            return true;
        }

        public void TryPlaceRoad(CellPos cell)
        {
            if (_placement.CanPlaceRoad(cell))
            {
                _placement.PlaceRoad(cell);
                return;
            }

            _notifications?.Push(
                "road.fail",
                "Road",
                "Cannot place road here (must connect to existing road).",
                NotificationSeverity.Warning,
                new NotificationPayload(default, default, ""),
                0.15f,
                true);
        }

        public void TryRemoveRoad(CellPos cell)
        {
            if (_placement.CanRemoveRoad(cell))
                _placement.RemoveRoad(cell);
        }
    }
}
