// AUTO-GENERATED TEMPLATE from PART 25 (LOCKED v0.1)
// Source: PART25_Technical_InterfacesPack_Services_Events_DTOs_LOCKED_SPEC_v0.1.md
// Notes:
// - Contracts only: interfaces/enums/structs/DTO/events.
// - Do not put runtime logic here.
// - Namespace kept unified to minimize cross-namespace friction.

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

        event System.Action NotificationsChanged;
        System.Collections.Generic.IReadOnlyList<NotificationViewModel> GetVisible();
    }
}
