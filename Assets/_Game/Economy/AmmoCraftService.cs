using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class AmmoCraftService
    {
        private readonly AmmoService _owner;
        private readonly GameServices _s;
        private readonly AmmoRecipeProvider _recipeProvider;

        internal AmmoCraftService(AmmoService owner, AmmoRecipeProvider recipeProvider)
        {
            _owner = owner;
            _s = owner.Services;
            _recipeProvider = recipeProvider;
        }

        internal bool TryStartCraft(BuildingId forge)
        {
            if (_s.WorldState == null || _s.StorageService == null || _s.JobBoard == null || _s.DataRegistry == null) return false;
            if (!_s.WorldState.Buildings.Exists(forge)) return false;

            var bs = _s.WorldState.Buildings.Get(forge);
            if (!bs.IsConstructed) return false;

            if (!_recipeProvider.TryGetAmmoRecipe(out var recipe))
                return false;

            _owner.RebuildWorkplaceHasNpcSet_Core();
            if (!_owner.WorkplacesWithNpc.Contains(forge.Value)) return false;

            int outCap = _s.StorageService.GetCap(forge, recipe.OutputType);
            int outCur = _s.StorageService.GetAmount(forge, recipe.OutputType);
            if (outCap <= 0 || (outCap - outCur) < recipe.OutputAmount) return false;

            int inCur = _s.StorageService.GetAmount(forge, recipe.InputType);
            if (inCur < recipe.InputAmount) return false;

            var extras = recipe.ExtraInputs;
            if (extras != null && extras.Length > 0)
            {
                for (int i = 0; i < extras.Length; i++)
                {
                    var c = extras[i];
                    if (c == null || c.Amount <= 0) continue;
                    int cur = _s.StorageService.GetAmount(forge, c.Resource);
                    if (cur < c.Amount) return false;
                }
            }

            if (_owner.CraftJobByForge.TryGetValue(forge.Value, out var oldId))
            {
                if (_s.JobBoard.TryGet(oldId, out var old) && !AmmoService.IsTerminal(old.Status))
                    return false;
            }

            var j = new Job
            {
                Archetype = JobArchetype.CraftAmmo,
                Status = JobStatus.Created,
                Workplace = forge,
                SourceBuilding = forge,
                DestBuilding = default,
                ResourceType = recipe.OutputType,
                Amount = recipe.OutputAmount,
                TargetCell = bs.Anchor,
                CreatedAt = 0
            };

            var id = _s.JobBoard.Enqueue(j);
            _owner.CraftJobByForge[forge.Value] = id;
            return true;
        }
    }
}
