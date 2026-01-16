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
