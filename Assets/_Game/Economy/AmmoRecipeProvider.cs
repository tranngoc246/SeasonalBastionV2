using System;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    internal sealed class AmmoRecipeProvider
    {
        private readonly IDataRegistry _dataRegistry;
        private readonly Func<string> _getAmmoRecipeId;
        private RecipeDef _cachedAmmoRecipe;
        private string _cachedAmmoRecipeId;

        internal AmmoRecipeProvider(IDataRegistry dataRegistry, Func<string> getAmmoRecipeId)
        {
            _dataRegistry = dataRegistry;
            _getAmmoRecipeId = getAmmoRecipeId;
        }

        internal bool TryGetAmmoRecipe(out RecipeDef recipe)
        {
            recipe = null;

            string recipeId = _getAmmoRecipeId?.Invoke();
            if (string.IsNullOrEmpty(recipeId))
                recipeId = "ForgeAmmo";

            if (!string.Equals(_cachedAmmoRecipeId, recipeId, StringComparison.OrdinalIgnoreCase))
            {
                _cachedAmmoRecipeId = recipeId;
                _cachedAmmoRecipe = null;
            }

            if (_cachedAmmoRecipe != null)
            {
                recipe = _cachedAmmoRecipe;
                return true;
            }

            if (TryLoadRecipe(recipeId, out recipe))
            {
                _cachedAmmoRecipeId = recipeId;
                _cachedAmmoRecipe = recipe;
                return true;
            }

            Debug.LogWarning($"[AmmoService] Missing ammo recipe '{recipeId}'.");
            if (string.Equals(recipeId, "ForgeAmmo", StringComparison.OrdinalIgnoreCase))
                return false;

            Debug.LogWarning($"[AmmoService] Falling back to ammo recipe 'ForgeAmmo' after '{recipeId}' lookup failed.");
            if (!TryLoadRecipe("ForgeAmmo", out recipe))
            {
                Debug.LogWarning("[AmmoService] Failed to load fallback ammo recipe 'ForgeAmmo'.");
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
            if (_dataRegistry == null || string.IsNullOrWhiteSpace(recipeId))
                return false;

            try
            {
                recipe = _dataRegistry.GetRecipe(recipeId);
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
