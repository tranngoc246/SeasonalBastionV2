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
            ValidateBuildablesGraph(dr, errors);

            // 3) Cross references
            ValidateWaveEnemyRefs(dr, errors);

            // 2.5) Balance JSON required
            var bal = dr.GetBalanceOrNull();
            if (bal == null)
            {
                errors.Add("Balance JSON missing: DefsCatalog.Balance not assigned or parse failed.");
            }
            else
            {
                if (bal.build == null || bal.build.workChunkSec <= 0f) errors.Add("Balance.build.workChunkSec must be > 0");
                if (bal.repair == null || bal.repair.workChunkSec <= 0f) errors.Add("Balance.repair.workChunkSec must be > 0");
                if (bal.repair == null || bal.repair.healPctPerChunk <= 0f) errors.Add("Balance.repair.healPctPerChunk must be > 0");
                if (bal.carry == null) errors.Add("Balance.carry missing");
            }

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

        private static void ValidateBuildablesGraph(DataRegistry dr, List<string> errors)
        {
            var nodeIds = dr.GetAllBuildableNodeIds();
            var edgeIds = dr.GetAllUpgradeEdgeIds();

            bool hasAny = (nodeIds != null && nodeIds.Count > 0) || (edgeIds != null && edgeIds.Count > 0);
            if (!hasAny) return;

            // Node phải map được sang BuildingDef (BuildOrderService upgrade dùng GetBuilding)
            if (nodeIds != null)
            {
                foreach (var nid in nodeIds)
                {
                    if (string.IsNullOrWhiteSpace(nid)) continue;
                    try { dr.GetBuilding(nid); }
                    catch { errors.Add($"BuildablesGraph: node '{nid}' missing BuildingDef (Buildings.json)"); }
                }
            }

            if (edgeIds == null) return;

            foreach (var eid in edgeIds)
            {
                if (string.IsNullOrWhiteSpace(eid)) continue;
                if (!dr.TryGetUpgradeEdge(eid, out var e) || e == null)
                {
                    errors.Add($"BuildablesGraph: edge '{eid}' missing from registry");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(e.From) || string.IsNullOrWhiteSpace(e.To))
                {
                    errors.Add($"BuildablesGraph: edge '{eid}' has empty From/To");
                    continue;
                }

                if (!dr.TryGetBuildableNode(e.From, out _))
                    errors.Add($"BuildablesGraph: edge '{eid}' references missing node From '{e.From}'");
                if (!dr.TryGetBuildableNode(e.To, out _))
                    errors.Add($"BuildablesGraph: edge '{eid}' references missing node To '{e.To}'");

                BuildingDef fromB = null;
                BuildingDef toB = null;
                try { fromB = dr.GetBuilding(e.From); }
                catch { errors.Add($"BuildablesGraph: edge '{eid}' From '{e.From}' missing BuildingDef"); }
                try { toB = dr.GetBuilding(e.To); }
                catch { errors.Add($"BuildablesGraph: edge '{eid}' To '{e.To}' missing BuildingDef"); }

                if (fromB != null && toB != null)
                {
                    if (Math.Max(1, fromB.SizeX) != Math.Max(1, toB.SizeX) ||
                        Math.Max(1, fromB.SizeY) != Math.Max(1, toB.SizeY))
                        errors.Add($"BuildablesGraph: edge '{eid}' footprint mismatch {e.From}({fromB.SizeX}x{fromB.SizeY}) -> {e.To}({toB.SizeX}x{toB.SizeY})");

                    if (toB.BaseLevel <= fromB.BaseLevel)
                        errors.Add($"BuildablesGraph: edge '{eid}' non-increasing level {fromB.BaseLevel}->{toB.BaseLevel} ({e.From}->{e.To})");

                    if (toB.IsTower)
                    {
                        try { dr.GetTower(e.To); }
                        catch { errors.Add($"BuildablesGraph: tower upgrade target '{e.To}' missing TowerDef (Towers.json)"); }
                    }
                }

                if (e.Cost != null)
                {
                    for (int i = 0; i < e.Cost.Length; i++)
                    {
                        var c = e.Cost[i];
                        if (c == null) { errors.Add($"BuildablesGraph: edge '{eid}' cost[{i}] is null"); continue; }
                        if (c.Resource == ResourceType.None) errors.Add($"BuildablesGraph: edge '{eid}' cost[{i}] uses ResourceType.None");
                        if (c.Amount < 0) errors.Add($"BuildablesGraph: edge '{eid}' cost[{i}] amt must be >=0");
                    }
                }

                if (e.WorkChunks < 0)
                    errors.Add($"BuildablesGraph: edge '{eid}' workChunks must be >=0");
            }
        }
    }
}
