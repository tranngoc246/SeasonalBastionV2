using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public struct ResourcePatchState
    {
        public int Id;
        public ResourceType Resource;
        public CellPos Anchor;
        public List<CellPos> Cells;
        public int TotalAmount;
        public int RemainingAmount;
    }
}
