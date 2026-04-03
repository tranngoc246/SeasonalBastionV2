using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal sealed class ArmoryBufferPlanner
    {
        private readonly AmmoService _owner;

        internal ArmoryBufferPlanner(AmmoService owner)
        {
            _owner = owner;
        }

        internal bool TryStartCraft(BuildingId forge) => _owner.TryStartCraft_Core(forge);
        internal bool TryGetAmmoRecipe(out RecipeDef recipe) => _owner.TryGetAmmoRecipe_Core(out recipe);
        internal bool HasCapForForgeInputs(BuildingId forge, RecipeDef recipe) => _owner.HasCapForForgeInputs_Core(forge, recipe);
        internal void EnsureForgeSupplyByRecipe(BuildingId forge, CellPos forgeAnchor, RecipeDef recipe) => _owner.EnsureForgeSupplyByRecipe_Core(forge, forgeAnchor, recipe);
        internal void EnsureArmoryAmmoBuffer() => _owner.EnsureArmoryAmmoBuffer_Core();
        internal bool TryPickForgeAmmoSource(CellPos refPos, out BuildingId bestForge, out int bestTakeable) => _owner.TryPickForgeAmmoSource_Core(refPos, out bestForge, out bestTakeable);
    }
}
