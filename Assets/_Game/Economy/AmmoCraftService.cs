using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class AmmoCraftService
    {
        private readonly IWorldState _worldState;
        private readonly IStorageService _storageService;
        private readonly IJobBoard _jobBoard;
        private readonly AmmoRecipeProvider _recipeProvider;
        private readonly Dictionary<int, JobId> _craftJobByForge;
        private readonly Action _rebuildWorkplaceHasNpcSet;
        private readonly Func<HashSet<int>> _getWorkplacesWithNpc;

        internal AmmoCraftService(
            IWorldState worldState,
            IStorageService storageService,
            IJobBoard jobBoard,
            AmmoRecipeProvider recipeProvider,
            Dictionary<int, JobId> craftJobByForge,
            Action rebuildWorkplaceHasNpcSet,
            Func<HashSet<int>> getWorkplacesWithNpc)
        {
            _worldState = worldState;
            _storageService = storageService;
            _jobBoard = jobBoard;
            _recipeProvider = recipeProvider;
            _craftJobByForge = craftJobByForge;
            _rebuildWorkplaceHasNpcSet = rebuildWorkplaceHasNpcSet;
            _getWorkplacesWithNpc = getWorkplacesWithNpc;
        }

        internal bool TryStartCraft(BuildingId forge)
        {
            if (_worldState == null || _storageService == null || _jobBoard == null)
                return false;
            if (!_worldState.Buildings.Exists(forge))
                return false;

            var building = _worldState.Buildings.Get(forge);
            if (!building.IsConstructed)
                return false;

            if (!_recipeProvider.TryGetAmmoRecipe(out var recipe))
                return false;

            _rebuildWorkplaceHasNpcSet?.Invoke();
            var workplacesWithNpc = _getWorkplacesWithNpc?.Invoke();
            if (workplacesWithNpc == null || !workplacesWithNpc.Contains(forge.Value))
                return false;

            int outputCap = _storageService.GetCap(forge, recipe.OutputType);
            int outputCurrent = _storageService.GetAmount(forge, recipe.OutputType);
            if (outputCap <= 0 || (outputCap - outputCurrent) < recipe.OutputAmount)
                return false;

            int inputCurrent = _storageService.GetAmount(forge, recipe.InputType);
            if (inputCurrent < recipe.InputAmount)
                return false;

            var extras = recipe.ExtraInputs;
            if (extras != null && extras.Length > 0)
            {
                for (int i = 0; i < extras.Length; i++)
                {
                    var cost = extras[i];
                    if (cost == null || cost.Amount <= 0)
                        continue;

                    int current = _storageService.GetAmount(forge, cost.Resource);
                    if (current < cost.Amount)
                        return false;
                }
            }

            if (_craftJobByForge.TryGetValue(forge.Value, out var existingId))
            {
                if (_jobBoard.TryGet(existingId, out var existingJob) && !AmmoService.IsTerminal(existingJob.Status))
                    return false;
            }

            var job = new Job
            {
                Archetype = JobArchetype.CraftAmmo,
                Status = JobStatus.Created,
                Workplace = forge,
                SourceBuilding = forge,
                DestBuilding = default,
                ResourceType = recipe.OutputType,
                Amount = recipe.OutputAmount,
                TargetCell = building.Anchor,
                CreatedAt = 0
            };

            var jobId = _jobBoard.Enqueue(job);
            _craftJobByForge[forge.Value] = jobId;
            return true;
        }
    }
}
