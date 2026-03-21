using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    public sealed partial class DataRegistry
    {
        internal void LoadRecipesInternal(TextAsset ta)
        {
            if (ta == null)
            {
                Debug.LogWarning("[DataRegistry] Recipes TextAsset is null (DefsCatalog.Recipes). Using empty Recipe defs.");
                return;
            }

            var json = ta.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                _loadErrors.Add($"Recipes TextAsset '{ta.name}' is empty");
                return;
            }

            RecipesRoot root;
            try { root = JsonUtility.FromJson<RecipesRoot>(json); }
            catch (Exception e)
            {
                _loadErrors.Add($"Recipes parse failed: {e.Message}");
                return;
            }

            var arr = root != null ? root.recipes : null;
            if (arr == null || arr.Length == 0)
            {
                _loadErrors.Add("Recipes JSON missing/empty 'recipes' array");
                return;
            }

            int added = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < arr.Length; i++)
            {
                var rj = arr[i];
                if (rj == null) continue;

                var id = (rj.id ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id)) { _loadErrors.Add($"Recipes[{i}] has empty id"); continue; }
                if (!seen.Add(id)) _loadErrors.Add($"Duplicate Recipe id '{id}' in Recipes.json");

                var def = new RecipeDef
                {
                    DefId = id,
                    InputType = ParseResourceType(rj.inputType, $"Recipe '{id}' inputType"),
                    InputAmount = Mathf.Max(1, rj.inputAmount),
                    OutputType = ParseResourceType(rj.outputType, $"Recipe '{id}' outputType"),
                    OutputAmount = Mathf.Max(1, rj.outputAmount),
                    ExtraInputs = ToCosts(rj.extraInputs, ctx: $"Recipe '{id}' extraInputs"),
                    CraftTimeSec = Mathf.Max(0f, rj.craftTimeSec)
                };

                _recipes[id] = def;
                added++;
            }

            Debug.Log($"[DataRegistry] Loaded Recipes: {added} (TextAsset: {ta.name})");
        }
    }
}
