using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal static class HarvestTargetSelectionHelper
    {
        internal static bool TryPickBestHarvestTarget(
            ResourcePatchService resourcePatchService,
            IPathfinderRuntime pathfinder,
            IWorldState w,
            ResourceType rt,
            CellPos origin,
            int workplaceId,
            int slot,
            out CellPos zoneCell)
        {
            zoneCell = default;

            if (resourcePatchService == null || pathfinder == null)
                return false;

            CellPos bestCell = default;
            bool found = false;
            int bestScore = int.MaxValue;
            int patchCount = resourcePatchService.Patches.Count;

            for (int i = 0; i < patchCount; i++)
            {
                var patch = resourcePatchService.Patches[i];
                if (patch.Resource != rt || patch.RemainingAmount <= 0)
                    continue;

                int variationSeed = workplaceId * 37 + slot * 101 + (int)rt * 13 + patch.Id * 17;
                if (!resourcePatchService.TryPickCellInPatch(patch.Id, origin, variationSeed, out var candidateCell))
                    candidateCell = patch.Anchor;

                if (!pathfinder.TryEstimateCost(origin, candidateCell, out int cost))
                    continue;

                int score = cost - (patch.RemainingAmount > 200 ? 200 : patch.RemainingAmount);
                if (patch.IsStarterLike)
                    score -= 48;
                else if (string.Equals(patch.GenerationBucket, "bonus-generated", System.StringComparison.OrdinalIgnoreCase))
                    score += 24;

                if (!found || score < bestScore)
                {
                    found = true;
                    bestScore = score;
                    bestCell = candidateCell;
                }
            }

            if (!found)
            {
                for (int i = 0; i < patchCount; i++)
                {
                    var patch = resourcePatchService.Patches[i];
                    if (patch.Resource != rt || patch.RemainingAmount <= 0)
                        continue;

                    int variationSeed = workplaceId * 37 + slot * 101 + (int)rt * 13 + patch.Id * 17;
                    if (!resourcePatchService.TryPickCellInPatch(patch.Id, origin, variationSeed, out var relaxedCell))
                        relaxedCell = patch.Anchor;

                    if (patch.IsStarterLike)
                    {
                        zoneCell = relaxedCell;
                        return true;
                    }

                    if (!found)
                    {
                        found = true;
                        bestCell = relaxedCell;
                    }
                }

                if (!found)
                    return false;

                zoneCell = bestCell;
                return true;
            }

            zoneCell = bestCell;
            return true;
        }
    }
}
