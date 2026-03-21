using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    public sealed partial class DataRegistry
    {
        internal void LoadEnemiesInternal(TextAsset ta)
        {
            if (ta == null)
            {
                Debug.LogWarning("[DataRegistry] Enemies TextAsset is null (DefsCatalog.Enemies). Using empty Enemy defs.");
                return;
            }

            var json = ta.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                _loadErrors.Add($"Enemies TextAsset '{ta.name}' is empty");
                return;
            }

            EnemiesRoot root;
            try { root = JsonUtility.FromJson<EnemiesRoot>(json); }
            catch (Exception e)
            {
                _loadErrors.Add($"Enemies parse failed: {e.Message}");
                return;
            }

            var arr = root != null ? root.enemies : null;
            if (arr == null || arr.Length == 0)
            {
                _loadErrors.Add("Enemies JSON missing/empty 'enemies' array");
                return;
            }

            int added = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < arr.Length; i++)
            {
                var ej = arr[i];
                if (ej == null) continue;

                var id = (ej.id ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id)) { _loadErrors.Add($"Enemies[{i}] has empty id"); continue; }
                if (!seen.Add(id)) _loadErrors.Add($"Duplicate Enemy id '{id}' in Enemies.json");

                var def = new EnemyDef
                {
                    DefId = id,
                    MaxHp = Mathf.Max(1, ej.maxHp),
                    MoveSpeed = Mathf.Max(0.01f, ej.moveSpeed),
                    DamageToHQ = Mathf.Max(0, ej.damageToHQ),
                    DamageToBuildings = Mathf.Max(0, ej.damageToBuildings),
                    Range = Mathf.Max(0f, ej.range),
                    IsBoss = ej.isBoss,
                    BossYear = Mathf.Max(0, ej.year),
                    BossSeason = ParseSeason(ej.season, ctx: $"Enemy '{id}' season"),
                    BossDay = Mathf.Max(0, ej.day),
                    AuraSlowRofPct = Mathf.Max(0f, ej.auraSlowRofPct)
                };

                _enemies[id] = def;
                added++;
            }

            Debug.Log($"[DataRegistry] Loaded Enemies: {added} (TextAsset: {ta.name})");
        }
    }
}
