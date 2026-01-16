// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class ResourceFlowService : IResourceFlowService
    {
        private readonly IWorldState _w;
        private readonly IWorldIndex _index;
        private readonly IStorageService _storage;

        public ResourceFlowService(IWorldState w, IWorldIndex index, IStorageService storage)
        { _w = w; _index = index; _storage = storage; }

        public bool TryPickSource(CellPos from, ResourceType type, int minAmount, out StoragePick pick)
        {
            // TODO: deterministic nearest; tie-break by id
            pick = default;
            return false;
        }

        public bool TryPickDest(CellPos from, ResourceType type, int minSpace, out StoragePick pick)
        {
            pick = default;
            return false;
        }

        public int Transfer(BuildingId src, BuildingId dst, ResourceType type, int amount)
        {
            // TODO: remove then add; ensure atomic semantics or revert if add fails
            throw new System.NotImplementedException();
        }
    }
}
