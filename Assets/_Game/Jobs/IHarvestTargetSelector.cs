using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal interface IHarvestTargetSelector
    {
        bool TryPickBestHarvestTarget(
            GameServices services,
            IWorldState world,
            ResourceType resourceType,
            CellPos origin,
            int workplaceId,
            int slot,
            out CellPos zoneCell);
    }
}
