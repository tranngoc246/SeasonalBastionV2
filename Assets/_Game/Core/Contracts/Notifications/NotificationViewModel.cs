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
    public sealed class NotificationViewModel
    {
        public NotificationId Id;
        public string Key;
        public string Title;
        public string Body;
        public NotificationSeverity Severity;
        public NotificationPayload Payload;
        public float CreatedAt;
    }
}
