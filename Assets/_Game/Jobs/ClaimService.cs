using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class ClaimService : IClaimService
    {
        private readonly Dictionary<ClaimKey, NpcId> _map = new();

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
            if (_map.Count == 0) return;

            List<ClaimKey> toRemove = null;

            foreach (var kv in _map)
            {
                if (kv.Value.Value != owner.Value) continue;

                toRemove ??= new List<ClaimKey>(8);
                toRemove.Add(kv.Key);
            }

            if (toRemove == null) return;

            for (int i = 0; i < toRemove.Count; i++)
                _map.Remove(toRemove[i]);
        }

        public int ActiveClaimsCount => _map.Count;
    }
}
