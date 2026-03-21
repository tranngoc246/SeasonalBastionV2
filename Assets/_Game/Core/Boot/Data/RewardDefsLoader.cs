using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    public sealed partial class DataRegistry
    {
        internal void LoadRewardsInternal(TextAsset ta)
        {
            if (ta == null)
            {
                Debug.LogWarning("[DataRegistry] Rewards TextAsset is null (DefsCatalog.Rewards). Using empty Reward defs.");
                return;
            }

            var json = ta.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                _loadErrors.Add($"Rewards TextAsset '{ta.name}' is empty");
                return;
            }

            RewardsRoot root;
            try { root = JsonUtility.FromJson<RewardsRoot>(json); }
            catch (Exception e)
            {
                _loadErrors.Add($"Rewards parse failed: {e.Message}");
                return;
            }

            var arr = root != null ? root.rewards : null;
            if (arr == null || arr.Length == 0)
            {
                _loadErrors.Add("Rewards JSON missing/empty 'rewards' array");
                return;
            }

            int added = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < arr.Length; i++)
            {
                var rj = arr[i];
                if (rj == null) continue;

                var id = (rj.id ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id)) { _loadErrors.Add($"Rewards[{i}] has empty id"); continue; }
                if (!seen.Add(id)) _loadErrors.Add($"Duplicate Reward id '{id}' in Rewards.json");

                var def = new RewardDef
                {
                    DefId = id,
                    Title = (rj.title ?? string.Empty).Trim()
                };

                _rewards[id] = def;
                added++;
            }

            Debug.Log($"[DataRegistry] Loaded Rewards: {added} (TextAsset: {ta.name})");
        }
    }
}
