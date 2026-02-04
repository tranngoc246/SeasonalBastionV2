using System.Collections.Generic;

namespace SeasonalBastion.Contracts
{
    public sealed class ZoneState
    {
        public int Id;
        public ResourceType Resource; // Wood/Food
        public List<CellPos> Cells = new();
    }
}
