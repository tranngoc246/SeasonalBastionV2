// PATCH v0.2 — Day17 real validator (schema + cross-ref) — no hardcode balance
using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class DataValidator : IDataValidator
    {
        public bool ValidateAll(IDataRegistry reg, List<string> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            errors.Clear();

            if (reg == null)
            {
                errors.Add("IDataRegistry is null");
                return false;
            }

            // We validate against our concrete runtime registry (v0.1 pipeline).
            // If you later replace with ScriptableObject registry, implement a parallel validator.
            var dr = reg as DataRegistry;
            if (dr == null)
                return true; // permissive fallback

            // 1) Loader errors (duplicates, parse failures, missing required fields)
            var loadErr = dr.GetLoadErrors();
            if (loadErr != null && loadErr.Count > 0)
                errors.AddRange(loadErr);

            // 2) Basic sanity checks (non-negative, required arrays)
            ValidateBuildings(dr, errors);
            ValidateEnemies(dr, errors);
            ValidateWaves(dr, errors);
            ValidateRewards(dr, errors);
            ValidateRecipes(dr, errors);
            ValidateNpcs(dr, errors);
            ValidateTowers(dr, errors);

            // 3) Cross references
            ValidateWaveEnemyRefs(dr, errors);

            return errors.Count == 0;
        }

        private static void ValidateBuildings(DataRegistry dr, List<string> errors)
        {
            foreach (var id in dr.GetAllBuildingIds())
            {
                var d = dr.GetBuilding(id);
                if (d == null) { errors.Add($"Building '{id}' is null"); continue; }

                if (string.IsNullOrWhiteSpace(d.DefId)) errors.Add("BuildingDef has empty DefId");
                if (d.SizeX < 1 || d.SizeY < 1) errors.Add($"Building '{id}': invalid size {d.SizeX}x{d.SizeY}");
                if (d.BaseLevel < 1) errors.Add($"Building '{id}': BaseLevel must be >=1");
            }
        }

        private static void ValidateEnemies(DataRegistry dr, List<string> errors)
        {
            foreach (var id in dr.GetAllEnemyIds())
            {
                var d = dr.GetEnemy(id);
                if (d == null) { errors.Add($"Enemy '{id}' is null"); continue; }

                if (d.MaxHp <= 0) errors.Add($"Enemy '{id}': maxHp must be >0");
                if (d.MoveSpeed <= 0f) errors.Add($"Enemy '{id}': moveSpeed must be >0");
                if (d.DamageToHQ < 0 || d.DamageToBuildings < 0) errors.Add($"Enemy '{id}': damage must be >=0");
                if (d.Range < 0f) errors.Add($"Enemy '{id}': range must be >=0");
                if (d.IsBoss)
                {
                    if (d.BossYear < 1) errors.Add($"Enemy '{id}': boss year must be >=1");
                    if (d.BossDay < 1) errors.Add($"Enemy '{id}': boss day must be >=1");
                }
            }
        }

        private static void ValidateWaves(DataRegistry dr, List<string> errors)
        {
            var waveIndexSet = new HashSet<int>();
            foreach (var id in dr.GetAllWaveIds())
            {
                var d = dr.GetWave(id);
                if (d == null) { errors.Add($"Wave '{id}' is null"); continue; }

                if (d.WaveIndex < 0) errors.Add($"Wave '{id}': waveIndex must be >=0");
                if (!waveIndexSet.Add(d.WaveIndex))
                    errors.Add($"Wave '{id}': duplicate waveIndex={d.WaveIndex}");

                if (d.Year < 1) errors.Add($"Wave '{id}': year must be >=1");
                if (d.Day < 1) errors.Add($"Wave '{id}': day must be >=1");

                if (d.Entries == null || d.Entries.Length == 0)
                    errors.Add($"Wave '{id}': entries must not be empty");
                else
                {
                    for (int i = 0; i < d.Entries.Length; i++)
                    {
                        var e = d.Entries[i];
                        if (string.IsNullOrWhiteSpace(e.EnemyId)) errors.Add($"Wave '{id}': entries[{i}] enemyId is empty");
                        if (e.Count <= 0) errors.Add($"Wave '{id}': entries[{i}] count must be >0");
                    }
                }
            }
        }

        private static void ValidateRewards(DataRegistry dr, List<string> errors)
        {
            foreach (var id in dr.GetAllRewardIds())
            {
                var d = dr.GetReward(id);
                if (d == null) { errors.Add($"Reward '{id}' is null"); continue; }
                if (string.IsNullOrWhiteSpace(d.DefId)) errors.Add("RewardDef has empty DefId");
                if (string.IsNullOrWhiteSpace(d.Title)) errors.Add($"Reward '{id}': title is empty");
            }
        }

        private static void ValidateRecipes(DataRegistry dr, List<string> errors)
        {
            foreach (var id in dr.GetAllRecipeIds())
            {
                var d = dr.GetRecipe(id);
                if (d == null) { errors.Add($"Recipe '{id}' is null"); continue; }
                if (d.InputAmount <= 0) errors.Add($"Recipe '{id}': inputAmount must be >0");
                if (d.OutputAmount <= 0) errors.Add($"Recipe '{id}': outputAmount must be >0");
                if (d.CraftTimeSec < 0f) errors.Add($"Recipe '{id}': craftTimeSec must be >=0");
                if (d.ExtraInputs != null)
                {
                    for (int i = 0; i < d.ExtraInputs.Length; i++)
                    {
                        var c = d.ExtraInputs[i];
                        if (c == null) { errors.Add($"Recipe '{id}': extraInputs[{i}] is null"); continue; }
                        if (c.Amount <= 0) errors.Add($"Recipe '{id}': extraInputs[{i}] amt must be >0");
                    }
                }
            }
        }

        private static void ValidateNpcs(DataRegistry dr, List<string> errors)
        {
            foreach (var id in dr.GetAllNpcIds())
            {
                var d = dr.GetNpc(id);
                if (d == null) { errors.Add($"Npc '{id}' is null"); continue; }
                if (string.IsNullOrWhiteSpace(d.Role)) errors.Add($"Npc '{id}': role is empty");
                if (d.BaseMoveSpeed <= 0f) errors.Add($"Npc '{id}': baseMoveSpeed must be >0");
                if (d.RoadSpeedMultiplier <= 0f) errors.Add($"Npc '{id}': roadSpeedMultiplier must be >0");
            }
        }

        private static void ValidateTowers(DataRegistry dr, List<string> errors)
        {
            foreach (var id in dr.GetAllTowerIds())
            {
                var d = dr.GetTower(id);
                if (d == null) { errors.Add($"Tower '{id}' is null"); continue; }

                if (d.MaxHp <= 0) errors.Add($"Tower '{id}': hp must be >0");
                if (d.Range <= 0f) errors.Add($"Tower '{id}': range must be >0");
                if (d.Rof <= 0f) errors.Add($"Tower '{id}': rof must be >0");
                if (d.Damage < 0) errors.Add($"Tower '{id}': damage must be >=0");
                if (d.AmmoPerShot <= 0) errors.Add($"Tower '{id}': ammoPerShot must be >0");
                if (d.BuildChunks <= 0) errors.Add($"Tower '{id}': buildChunks must be >0");

                if (d.NeedsAmmoThresholdPct < 0f || d.NeedsAmmoThresholdPct > 1f)
                    errors.Add($"Tower '{id}': needsAmmoThresholdPct must be 0..1");

                if (d.Unlock.Year < 1) errors.Add($"Tower '{id}': unlock.year must be >=1");
                if (d.Unlock.Day < 1) errors.Add($"Tower '{id}': unlock.day must be >=1");

                if (d.BuildCost == null || d.BuildCost.Length == 0)
                    errors.Add($"Tower '{id}': buildCost must not be empty");
                else
                {
                    for (int i = 0; i < d.BuildCost.Length; i++)
                    {
                        var c = d.BuildCost[i];
                        if (c == null) { errors.Add($"Tower '{id}': buildCost[{i}] is null"); continue; }
                        if (c.Amount < 0) errors.Add($"Tower '{id}': buildCost[{i}] amt must be >=0");
                    }
                }
            }
        }

        private static void ValidateWaveEnemyRefs(DataRegistry dr, List<string> errors)
        {
            foreach (var wid in dr.GetAllWaveIds())
            {
                var w = dr.GetWave(wid);
                if (w?.Entries == null) continue;

                for (int i = 0; i < w.Entries.Length; i++)
                {
                    var eid = w.Entries[i].EnemyId;
                    if (string.IsNullOrWhiteSpace(eid)) continue;
                    if (!dr.TryGetEnemy(eid, out _))
                        errors.Add($"Wave '{wid}': entries[{i}] references missing Enemy '{eid}'");
                }
            }
        }
    }
}
