using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    public sealed partial class DataRegistry
    {
        internal void LoadNpcsInternal(TextAsset ta)
        {
            if (ta == null)
            {
                Debug.LogWarning("[DataRegistry] Npcs TextAsset is null (DefsCatalog.Npcs). Using empty NPC defs.");
                return;
            }

            var json = ta.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                _loadErrors.Add($"Npcs TextAsset '{ta.name}' is empty");
                return;
            }

            NpcsRoot root;
            try { root = JsonUtility.FromJson<NpcsRoot>(json); }
            catch (Exception e)
            {
                _loadErrors.Add($"Npcs parse failed: {e.Message}");
                return;
            }

            var arr = root != null ? root.npcs : null;
            if (arr == null || arr.Length == 0)
            {
                _loadErrors.Add("Npcs JSON missing/empty 'npcs' array");
                return;
            }

            int added = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < arr.Length; i++)
            {
                var nj = arr[i];
                if (nj == null) continue;

                var id = (nj.id ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id)) { _loadErrors.Add($"Npcs[{i}] has empty id"); continue; }
                if (!seen.Add(id)) _loadErrors.Add($"Duplicate Npc id '{id}' in Npcs.json");

                var role = (nj.role ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(role)) _loadErrors.Add($"Npc '{id}' has empty role");

                var def = new NpcDef
                {
                    DefId = id,
                    Role = role,
                    BaseMoveSpeed = Mathf.Max(0.01f, nj.baseMoveSpeed),
                    RoadSpeedMultiplier = Mathf.Max(0.01f, nj.roadSpeedMultiplier),
                    CarryCore = ToCaps(nj.carryCore)
                };

                _npcs[id] = def;
                added++;
            }

            Debug.Log($"[DataRegistry] Loaded Npcs: {added} (TextAsset: {ta.name})");
        }
    }
}
