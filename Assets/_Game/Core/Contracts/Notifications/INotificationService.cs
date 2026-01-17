using System;
using System.Collections.Generic;

namespace SeasonalBastion.Contracts
{
    public interface INotificationService
    {
        int MaxVisible { get; } // 3
        NotificationId Push(string key, string title, string body,
                            NotificationSeverity severity,
                            NotificationPayload payload,
                            float cooldownSeconds = 3f,
                            bool dedupeByKey = true);

        void Dismiss(NotificationId id);
        void ClearAll();

        event Action NotificationsChanged;
        IReadOnlyList<NotificationViewModel> GetVisible();
    }
}
