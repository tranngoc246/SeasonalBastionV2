namespace SeasonalBastion.Contracts
{
    public enum NotificationSeverity { Info, Warning, Error }

    public readonly struct NotificationPayload
    {
        public readonly BuildingId Building;
        public readonly TowerId Tower;
        public readonly string Extra;
        public NotificationPayload(BuildingId b, TowerId t, string extra)
        { Building=b; Tower=t; Extra=extra; }
    }
}
