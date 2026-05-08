using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class DefaultHarvestTargetSelector : IHarvestTargetSelector
    {
        public bool TryPickBestHarvestTarget(
            ResourcePatchService resourcePatchService,
            IPathfinderRuntime pathfinder,
            IWorldState world,
            ResourceType resourceType,
            CellPos origin,
            int workplaceId,
            int slot,
            out CellPos zoneCell)
        {
            return HarvestTargetSelectionHelper.TryPickBestHarvestTarget(
                resourcePatchService,
                pathfinder,
                world,
                resourceType,
                origin,
                workplaceId,
                slot,
                out zoneCell);
        }
    }
}
