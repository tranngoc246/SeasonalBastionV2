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
    public interface IStorageService
    {
        StorageSnapshot GetStorage(BuildingId building);

        bool CanStore(BuildingId building, ResourceType type);
        int GetAmount(BuildingId building, ResourceType type);
        int GetCap(BuildingId building, ResourceType type);

        int Add(BuildingId building, ResourceType type, int amount);     // returns actually added
        int Remove(BuildingId building, ResourceType type, int amount);  // returns actually removed

        int GetTotal(ResourceType type); // across allowed storages (ammo: only armory/forge local if you count)
    }
}
