using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class DefaultHarvestTargetSelector : IHarvestTargetSelector
    {
        public bool TryPickBestHarvestTarget(
            GameServices services,
            IWorldState world,
            ResourceType resourceType,
            CellPos origin,
            int workplaceId,
            int slot,
            out CellPos zoneCell)
        {
            return HarvestTargetSelectionHelper.TryPickBestHarvestTarget(
                services,
                world,
                resourceType,
                origin,
                workplaceId,
                slot,
                out zoneCell);
        }
    }
}
