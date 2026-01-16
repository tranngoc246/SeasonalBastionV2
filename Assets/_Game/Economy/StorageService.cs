// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class StorageService : IStorageService
    {
        private readonly IWorldState _w;
        private readonly IDataRegistry _data;
        private readonly IEventBus _bus;

        public StorageService(IWorldState w, IDataRegistry data, IEventBus bus)
        { _w = w; _data = data; _bus = bus; }

        public StorageSnapshot GetStorage(BuildingId building) { throw new System.NotImplementedException(); }

        public bool CanStore(BuildingId building, ResourceType type)
        {
            // TODO:
            // - warehouse/hq forbid ammo
            // - check def flags + caps
            return true;
        }

        public int GetAmount(BuildingId building, ResourceType type) { throw new System.NotImplementedException(); }
        public int GetCap(BuildingId building, ResourceType type) { throw new System.NotImplementedException(); }

        public int Add(BuildingId building, ResourceType type, int amount)
        {
            // TODO: clamp to cap, return added, publish ResourceDeliveredEvent if dest is warehouse/hq
            throw new System.NotImplementedException();
        }

        public int Remove(BuildingId building, ResourceType type, int amount) { throw new System.NotImplementedException(); }

        public int GetTotal(ResourceType type) { throw new System.NotImplementedException(); }
    }
}
