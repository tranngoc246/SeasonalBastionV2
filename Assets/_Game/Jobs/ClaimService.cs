using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class ClaimService : IClaimService
    {
        private readonly Dictionary<ClaimKey, NpcId> _map = new();
        private readonly List<ClaimKey> _tmpToRemove = new(16);

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

            _tmpToRemove.Clear();

            foreach (var kv in _map)
            {
                if (kv.Value.Value != owner.Value) continue;
                _tmpToRemove.Add(kv.Key);
            }

            for (int i = 0; i < _tmpToRemove.Count; i++)
                _map.Remove(_tmpToRemove[i]);

            _tmpToRemove.Clear();
        }

        public void ClearAll()
        {
            _map.Clear();
        }

        public void CleanupInvalidOwners(INpcStore npcs)
        {
            if (npcs == null) return;
            if (_map.Count == 0) return;

            _tmpToRemove.Clear();

            foreach (var kv in _map)
            {
                var owner = kv.Value;
                if (owner.Value == 0) { _tmpToRemove.Add(kv.Key); continue; }
                if (!npcs.Exists(owner)) _tmpToRemove.Add(kv.Key);
            }

            for (int i = 0; i < _tmpToRemove.Count; i++)
                _map.Remove(_tmpToRemove[i]);

            _tmpToRemove.Clear();
        }

        public int ActiveClaimsCount => _map.Count;
    }
}
