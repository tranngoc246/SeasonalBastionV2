using System;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    internal sealed class AmmoRecipeProvider
    {
        private readonly AmmoService _owner;
        private readonly GameServices _s;

        internal AmmoRecipeProvider(AmmoService owner)
        {
            _owner = owner;
            _s = owner.Services;
        }

        internal bool TryGetAmmoRecipe(out RecipeDef recipe)
        {
            recipe = null;

            string rid = _owner.AmmoRecipeIdValue;
            if (string.IsNullOrEmpty(rid))
                rid = "ForgeAmmo";

            if (!string.Equals(_owner.CachedAmmoRecipeId, rid, StringComparison.OrdinalIgnoreCase))
            {
                _owner.CachedAmmoRecipeId = rid;
                _owner.CachedAmmoRecipe = null;
            }

            if (_owner.CachedAmmoRecipe != null)
            {
                recipe = _owner.CachedAmmoRecipe;
                return true;
            }

            if (TryLoadRecipe(rid, out recipe))
            {
                _owner.CachedAmmoRecipeId = rid;
                _owner.CachedAmmoRecipe = recipe;
                return true;
            }

            Debug.LogWarning($"[AmmoService] Missing ammo recipe '{rid}'.");
            if (string.Equals(rid, "ForgeAmmo", StringComparison.OrdinalIgnoreCase))
                return false;

            Debug.LogWarning($"[AmmoService] Falling back to ammo recipe 'ForgeAmmo' after '{rid}' lookup failed.");
            if (!TryLoadRecipe("ForgeAmmo", out recipe))
            {
                Debug.LogWarning($"[AmmoService] Failed to load fallback ammo recipe 'ForgeAmmo'.");
                return false;
            }

            _owner.CachedAmmoRecipeId = "ForgeAmmo";
            _owner.CachedAmmoRecipe = recipe;
            return true;
        }

        private bool TryLoadRecipe(string recipeId, out RecipeDef recipe)
        {
            recipe = null;
            if (_s?.DataRegistry == null || string.IsNullOrWhiteSpace(recipeId))
                return false;

            try
            {
                recipe = _s.DataRegistry.GetRecipe(recipeId);
                return recipe != null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AmmoService] Failed to load ammo recipe '{recipeId}': {ex}");
                return false;
            }
        }
    }
}
