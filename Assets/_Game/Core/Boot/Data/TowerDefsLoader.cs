using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    public sealed partial class DataRegistry
    {
        internal void LoadTowersInternal(TextAsset ta)
        {
            if (ta == null)
            {
                Debug.LogWarning("[DataRegistry] Towers TextAsset is null (DefsCatalog.Towers). Using empty Tower defs.");
                return;
            }

            var json = ta.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                _loadErrors.Add($"Towers TextAsset '{ta.name}' is empty");
                return;
            }

            TowersRoot root;
            try { root = JsonUtility.FromJson<TowersRoot>(json); }
            catch (Exception e)
            {
                _loadErrors.Add($"Towers parse failed: {e.Message}");
                return;
            }

            var arr = root != null ? root.towers : null;
            if (arr == null || arr.Length == 0)
            {
                _loadErrors.Add("Towers JSON missing/empty 'towers' array");
                return;
            }

            int added = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < arr.Length; i++)
            {
                var tj = arr[i];
                if (tj == null) continue;

                var id = (tj.id ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id)) { _loadErrors.Add($"Towers[{i}] has empty id"); continue; }
                if (!seen.Add(id)) _loadErrors.Add($"Duplicate Tower id '{id}' in Towers.json");

                int ammoMax = Mathf.Max(0, tj.ammoMax);
                int ammoPerShot = tj.ammoPerShot;

                if (ammoMax <= 0) ammoPerShot = 0;
                else
                {
                    if (ammoPerShot <= 0) ammoPerShot = 1;
                    if (ammoPerShot > ammoMax) ammoPerShot = ammoMax;
                }

                float thrPct = tj.needsAmmoThresholdPct;
                if (thrPct <= 0f) thrPct = 0.25f;
                thrPct = Mathf.Clamp01(thrPct);

                var def = new TowerDef
                {
                    DefId = id,
                    Tier = Mathf.Max(1, tj.tier),
                    MaxHp = Mathf.Max(1, tj.hp),
                    Range = Mathf.Max(0f, tj.range),
                    Rof = Mathf.Max(0.01f, tj.rof),
                    Damage = Mathf.Max(0, tj.damage),
                    SlowPct = Mathf.Max(0f, tj.slowPct),
                    SlowSec = Mathf.Max(0f, tj.slowSec),
                    Aoe = tj.aoe ?? string.Empty,
                    DotDps = Mathf.Max(0, tj.dotDps),
                    DotSec = Mathf.Max(0, tj.dotSec),
                    AmmoMax = ammoMax,
                    AmmoPerShot = ammoPerShot,
                    NeedsAmmoThresholdPct = thrPct,
                    BuildCost = ToCosts(tj.buildCost, ctx: $"Tower '{id}' buildCost"),
                    BuildChunks = Mathf.Max(1, tj.buildChunks),
                    Unlock = ToUnlock(tj.unlock, $"Tower '{id}' unlock")
                };

                _towers[id] = def;
                added++;
            }

            ValidateTowers();

            Debug.Log($"[DataRegistry] Loaded Towers: {added} (TextAsset: {ta.name})");
        }
    }
}
