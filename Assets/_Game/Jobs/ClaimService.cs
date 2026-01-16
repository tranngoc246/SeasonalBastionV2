// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class ClaimService : IClaimService
    {
        private readonly System.Collections.Generic.Dictionary<ClaimKey, NpcId> _map = new();

        public bool TryAcquire(ClaimKey key, NpcId owner)
        {
            if (_map.TryGetValue(key, out var o)) return o.Value == owner.Value;
            _map[key] = owner;
            return true;
        }

        public bool IsOwnedBy(ClaimKey key, NpcId owner) =>
            _map.TryGetValue(key, out var o) && o.Value == owner.Value;

        public void Release(ClaimKey key, NpcId owner)
        {
            if (IsOwnedBy(key, owner)) _map.Remove(key);
        }

        public void ReleaseAll(NpcId owner)
        {
            // TODO: iterate safely (copy keys) remove owned
            throw new System.NotImplementedException();
        }

        public int ActiveClaimsCount => _map.Count;
    }
}
