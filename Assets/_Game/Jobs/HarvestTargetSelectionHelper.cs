using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal static class HarvestTargetSelectionHelper
    {
        internal static bool TryPickBestHarvestTarget(
            GameServices s,
            IWorldState w,
            ResourceType rt,
            CellPos origin,
            int workplaceId,
            int slot,
            out CellPos zoneCell)
        {
            zoneCell = default;

            if (s?.ResourcePatchService == null || s.Pathfinder == null)
                return false;

            var pathfinder = s.Pathfinder;
            CellPos bestCell = default;
            bool found = false;
            int bestScore = int.MaxValue;
            int patchCount = s.ResourcePatchService.Patches.Count;

            for (int i = 0; i < patchCount; i++)
            {
                var patch = s.ResourcePatchService.Patches[i];
                if (patch.Resource != rt || patch.RemainingAmount <= 0)
                    continue;

                int variationSeed = workplaceId * 37 + slot * 101 + (int)rt * 13 + patch.Id * 17;
                if (!s.ResourcePatchService.TryPickCellInPatch(patch.Id, origin, variationSeed, out var candidateCell))
                    candidateCell = patch.Anchor;

                if (!pathfinder.TryEstimateCost(origin, candidateCell, out int cost))
                    continue;

                int score = cost - (patch.RemainingAmount > 200 ? 200 : patch.RemainingAmount);
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
                    var patch = s.ResourcePatchService.Patches[i];
                    if (patch.Resource != rt || patch.RemainingAmount <= 0)
                        continue;

                    int variationSeed = workplaceId * 37 + slot * 101 + (int)rt * 13 + patch.Id * 17;
                    if (!s.ResourcePatchService.TryPickCellInPatch(patch.Id, origin, variationSeed, out var relaxedCell))
                        relaxedCell = patch.Anchor;

                    zoneCell = relaxedCell;
                    return true;
                }

                return false;
            }

            zoneCell = bestCell;
            return true;
        }
    }
}
