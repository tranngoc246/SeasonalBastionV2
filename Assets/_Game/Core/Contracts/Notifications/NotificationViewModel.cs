using SeasonalBastion.Contracts;

public sealed class NotificationViewModel
{
    public NotificationId Id;
    public string Key;
    public string Title;
    public string Body;
    public NotificationSeverity Severity;
    public NotificationPayload Payload;
    public float CreatedAt;
    public float ExpiresAt; // Time.realtimeSinceStartup timestamp, 0 = never
}
