using System.Collections.Generic;

namespace SeasonalBastion.Contracts
{
    public interface IResourcePileStore
    {
        IEnumerable<PileId> Ids { get; }

        bool Exists(PileId id);
        ResourcePileState Get(PileId id);
        void Set(PileId id, in ResourcePileState st);

        PileId AddOrIncrease(CellPos cell, ResourceType rt, int delta, BuildingId owner);

        bool TryTake(PileId id, int want, out int taken);
        bool TryFindNonEmpty(ResourceType rt, BuildingId owner, out PileId id);
    }
}
