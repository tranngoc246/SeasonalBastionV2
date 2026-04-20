using System.Collections.Generic;

namespace SeasonalBastion.Contracts
{
    public interface IClaimService
    {
        bool TryAcquire(ClaimKey key, NpcId owner);
        bool IsOwnedBy(ClaimKey key, NpcId owner);
        void Release(ClaimKey key, NpcId owner);
        void ReleaseAll(NpcId owner);

        void ClearAll();

        void CleanupInvalidOwners(INpcStore npcs);

        int ActiveClaimsCount { get; }
        IEnumerable<KeyValuePair<ClaimKey, NpcId>> EnumerateClaims();
    }
}
