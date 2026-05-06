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
        public string OriginKind;
        public string GenerationBucket;
        public string SourceLabel;

        public bool IsStarterLike => GenerationBucket == "starter-generated";
    }
}
