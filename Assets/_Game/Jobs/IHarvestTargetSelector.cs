using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public interface IHarvestTargetSelector
    {
        bool TryPickBestHarvestTarget(
            ResourcePatchService resourcePatchService,
            IPathfinderRuntime pathfinder,
            IWorldState world,
            ResourceType resourceType,
            CellPos origin,
            int workplaceId,
            int slot,
            out CellPos zoneCell);
    }
}
