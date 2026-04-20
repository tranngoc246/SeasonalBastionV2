using System;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    internal sealed class AmmoRecipeProvider
    {
        private readonly AmmoService _owner;
        private readonly GameServices _s;
        private RecipeDef _cachedAmmoRecipe;
        private string _cachedAmmoRecipeId;

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

            if (!string.Equals(_cachedAmmoRecipeId, rid, StringComparison.OrdinalIgnoreCase))
            {
                _cachedAmmoRecipeId = rid;
                _cachedAmmoRecipe = null;
            }

            if (_cachedAmmoRecipe != null)
            {
                recipe = _cachedAmmoRecipe;
                return true;
            }

            if (TryLoadRecipe(rid, out recipe))
            {
                _cachedAmmoRecipeId = rid;
                _cachedAmmoRecipe = recipe;
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

            _cachedAmmoRecipeId = "ForgeAmmo";
            _cachedAmmoRecipe = recipe;
            return true;
        }

        internal void Clear()
        {
            _cachedAmmoRecipe = null;
            _cachedAmmoRecipeId = null;
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
