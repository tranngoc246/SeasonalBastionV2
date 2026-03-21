using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    public sealed partial class DataRegistry
    {
        internal void LoadWavesInternal(TextAsset ta)
        {
            if (ta == null)
            {
                Debug.LogWarning("[DataRegistry] Waves TextAsset is null (DefsCatalog.Waves). Using empty Wave defs.");
                return;
            }

            var json = ta.text;
            if (string.IsNullOrWhiteSpace(json))
            {
                _loadErrors.Add($"Waves TextAsset '{ta.name}' is empty");
                return;
            }

            WavesRoot root;
            try { root = JsonUtility.FromJson<WavesRoot>(json); }
            catch (Exception e)
            {
                _loadErrors.Add($"Waves parse failed: {e.Message}");
                return;
            }

            var arr = root != null ? root.waves : null;
            if (arr == null || arr.Length == 0)
            {
                _loadErrors.Add("Waves JSON missing/empty 'waves' array");
                return;
            }

            int added = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < arr.Length; i++)
            {
                var wj = arr[i];
                if (wj == null) continue;

                var id = (wj.id ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(id)) { _loadErrors.Add($"Waves[{i}] has empty id"); continue; }
                if (!seen.Add(id)) _loadErrors.Add($"Duplicate Wave id '{id}' in Waves.json");

                var entries = ToWaveEntries(wj.entries, ctx: $"Wave '{id}' entries");

                var def = new WaveDef
                {
                    DefId = id,
                    WaveIndex = wj.waveIndex,
                    Year = Mathf.Max(1, wj.year),
                    Season = ParseSeason(wj.season, ctx: $"Wave '{id}' season"),
                    Day = Mathf.Max(1, wj.day),
                    IsBoss = wj.isBoss,
                    Entries = entries
                };

                _waves[id] = def;
                added++;
            }

            Debug.Log($"[DataRegistry] Loaded Waves: {added} (TextAsset: {ta.name})");
        }
    }
}
