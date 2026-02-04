using System.Collections.Generic;

namespace SeasonalBastion.Contracts
{
    public interface IZoneStore
    {
        IReadOnlyList<ZoneState> Zones { get; }
        ZoneState GetByResource(ResourceType rt); 
        CellPos PickCell(ResourceType rt, CellPos preferNear);
        void Clear();
        void Add(ZoneState z);
    }
}
