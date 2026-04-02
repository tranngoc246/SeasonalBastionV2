using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public interface IHarvestTargetSelector
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
