using System.Collections.Generic;

namespace SeasonalBastion.Contracts
{
    public interface IResourcePileStore
    {
        IEnumerable<PileId> Ids { get; }

        bool Exists(PileId id);
        ResourcePileState Get(PileId id);
        void Set(PileId id, in ResourcePileState st);

        // Tạo pile hoặc cộng dồn lên pile cùng cell+type+owner
        PileId AddOrIncrease(CellPos cell, ResourceType rt, int delta, BuildingId owner);

        // Lấy bớt từ pile
        bool TryTake(PileId id, int want, out int taken);

        // Tìm pile có hàng để tạo Haul job
        bool TryFindNonEmpty(ResourceType rt, BuildingId owner, out PileId id);
    }
}
