using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SeasonalBastion
{
    public static class SaveLoadApplier
    {
        public static bool TryApply(GameServices s, RunSaveDTO dto, out string error)
        {
            error = null;
            if (s == null) { error = "GameServices null"; return false; }
            if (dto == null) { error = "RunSaveDTO null"; return false; }
            if (dto.world == null) { error = "dto.world null"; return false; }
            if (dto.build == null) dto.build = new BuildDTO();

            try
            {
                if (!ValidateSnapshotBeforeApply(s, dto, out error))
                    return false;

                ClearCurrentRuntimeBeforeApply(s);
                RestoreClockSnapshot(s, dto);
                RestoreRoads(s, dto.world);
                RestoreWorldCollections(s, dto, out error);
                if (error != null) return false;

                RestoreBuildOrdersAfterLoad(s);
                RebuildRuntimeCachesAndIndices(s);
                RestoreCombatAfterLoad(s, dto.combat);
                RestorePopulationAfterLoad(s, dto.population);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        private static bool ValidateSnapshotBeforeApply(GameServices s, RunSaveDTO dto, out string error)
        {
            error = null;
            if (s.WorldState?.Buildings is not BuildingStore) { error = "WorldState.Buildings is not BuildingStore"; return false; }
            if (s.WorldState?.Sites is not BuildSiteStore) { error = "WorldState.Sites is not BuildSiteStore"; return false; }
            if (s.WorldState?.Towers is not TowerStore) { error = "WorldState.Towers is not TowerStore"; return false; }
            if (s.WorldState?.Npcs is not NpcStore) { error = "WorldState.Npcs is not NpcStore"; return false; }
            if (s.WorldState?.Enemies is not EnemyStore) { error = "WorldState.Enemies is not EnemyStore"; return false; }

            if (dto.world.Buildings != null)
            {
                for (int i = 0; i < dto.world.Buildings.Count; i++)
                {
                    var b = dto.world.Buildings[i];
                    if (!s.DataRegistry.TryGetBuilding(b.DefId, out var def) || def == null)
                    {
                        error = $"Missing BuildingDef for building '{b.DefId}'";
                        return false;
                    }
                }
            }

            if (dto.build?.Sites != null)
            {
                for (int i = 0; i < dto.build.Sites.Count; i++)
                {
                    var site = dto.build.Sites[i];
                    if (!s.DataRegistry.TryGetBuilding(site.BuildingDefId, out var def) || def == null)
                    {
                        error = $"Missing BuildingDef for site '{site.BuildingDefId}'";
                        return false;
                    }
                }
            }

            return true;
        }

        private static void ClearCurrentRuntimeBeforeApply(GameServices s)
        {
            s.CombatService?.KillAllEnemies();

            s.WorldState?.Buildings?.ClearAll();
            s.WorldState?.Sites?.ClearAll();
            s.WorldState?.Npcs?.ClearAll();
            s.WorldState?.Towers?.ClearAll();
            s.WorldState?.Enemies?.ClearAll();

            s.GridMap?.ClearAll();
            s.NotificationService?.ClearAll();
            s.JobBoard?.ClearAll();
            s.ClaimService?.ClearAll();
            s.BuildOrderService?.ClearAll();
            (s.AmmoService as AmmoService)?.ClearAll();
        }

        private static void RestoreClockSnapshot(GameServices s, RunSaveDTO dto)
        {
            if (s.RunClock is RunClockService rc)
            {
                rc.LoadSnapshot(
                    yearIndex: Math.Max(1, dto.yearIndex),
                    seasonText: dto.season,
                    dayIndex: dto.dayIndex,
                    dayTimerSeconds: Math.Max(0f, dto.dayTimer),
                    timeScale: dto.timeScale
                );
            }
            else
            {
                s.RunClock?.ForceSeasonDay(ParseSeason(dto.season), dto.dayIndex);
                s.RunClock?.SetTimeScale(dto.timeScale);
            }
        }

        private static void RestoreRoads(GameServices s, WorldDTO world)
        {
            if (s.GridMap == null || world?.Roads == null) return;
            for (int i = 0; i < world.Roads.Count; i++)
            {
                var c = world.Roads[i];
                s.GridMap.SetRoad(new CellPos(c.x, c.y), true);
            }
        }

        private static void RestoreWorldCollections(GameServices s, RunSaveDTO dto, out string error)
        {
            error = null;
            RestoreSites(s, dto.build?.Sites);
            RestoreBuildings(s, dto.world.Buildings, dto.build?.Sites);
            RestoreTowers(s, dto.world.Towers);
            RestoreNpcs(s, dto.world.Npcs);
            RestoreEnemies(s, dto.world.Enemies);
        }

        private static void RestoreSites(GameServices s, List<BuildSiteState> sites)
        {
            if (sites == null) return;
            sites.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));

            var siteStore = (BuildSiteStore)s.WorldState.Sites;
            for (int i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                siteStore.CreateWithId(site.Id, site, overwriteIfExists: true);
                s.WorldState.Sites.Set(site.Id, site);

                if (site.Kind != 0) continue;
                s.DataRegistry.TryGetBuilding(site.BuildingDefId, out var def);
                int w = Math.Max(1, def.SizeX);
                int h = Math.Max(1, def.SizeY);
                for (int dy = 0; dy < h; dy++)
                    for (int dx = 0; dx < w; dx++)
                        s.GridMap.SetSite(new CellPos(site.Anchor.X + dx, site.Anchor.Y + dy), site.Id);
            }
        }

        private static void RestoreBuildings(GameServices s, List<BuildingState> buildings, List<BuildSiteState> sites)
        {
            if (buildings == null) return;
            buildings.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));

            var buildingStore = (BuildingStore)s.WorldState.Buildings;
            var hasSiteAt = new HashSet<long>();
            if (sites != null)
            {
                for (int i = 0; i < sites.Count; i++)
                {
                    var st = sites[i];
                    hasSiteAt.Add(Pack(st.Anchor.X, st.Anchor.Y, st.BuildingDefId));
                }
            }

            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                buildingStore.CreateWithId(b.Id, b, overwriteIfExists: true);
                s.WorldState.Buildings.Set(b.Id, b);

                bool shouldOccupyAsBuilding = b.IsConstructed;
                if (!shouldOccupyAsBuilding)
                {
                    long key = Pack(b.Anchor.X, b.Anchor.Y, b.DefId);
                    if (!hasSiteAt.Contains(key))
                        shouldOccupyAsBuilding = true;
                }

                if (!shouldOccupyAsBuilding) continue;

                s.DataRegistry.TryGetBuilding(b.DefId, out var def);
                int w = Math.Max(1, def.SizeX);
                int h = Math.Max(1, def.SizeY);
                for (int dy = 0; dy < h; dy++)
                    for (int dx = 0; dx < w; dx++)
                        s.GridMap.SetBuilding(new CellPos(b.Anchor.X + dx, b.Anchor.Y + dy), b.Id);
            }
        }

        private static void RestoreTowers(GameServices s, List<TowerState> towers)
        {
            if (towers == null) return;
            towers.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
            var towerStore = (TowerStore)s.WorldState.Towers;
            for (int i = 0; i < towers.Count; i++)
            {
                var t = towers[i];
                towerStore.CreateWithId(t.Id, t, overwriteIfExists: true);
                s.WorldState.Towers.Set(t.Id, t);
            }
        }

        private static void RestoreNpcs(GameServices s, List<NpcState> npcs)
        {
            if (npcs == null) return;
            npcs.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
            var npcStore = (NpcStore)s.WorldState.Npcs;
            for (int i = 0; i < npcs.Count; i++)
            {
                var n = npcs[i];
                n.CurrentJob = default;
                n.IsIdle = true;
                npcStore.CreateWithId(n.Id, n, overwriteIfExists: true);
                s.WorldState.Npcs.Set(n.Id, n);
            }
        }

        private static void RestoreEnemies(GameServices s, List<EnemyState> enemies)
        {
            if (enemies == null) return;
            enemies.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));
            var enemyStore = (EnemyStore)s.WorldState.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                enemyStore.CreateWithId(e.Id, e, overwriteIfExists: true);
                s.WorldState.Enemies.Set(e.Id, e);
            }
        }

        private static void RestoreBuildOrdersAfterLoad(GameServices s)
        {
            if (s.BuildOrderService is BuildOrderService bos)
                bos.RebuildActivePlaceOrdersFromSitesAfterLoad();
        }

        private static void RebuildRuntimeCachesAndIndices(GameServices s)
        {
            TryRebuildRunStartRuntimeCaches(s);
            s.EventBus?.Publish(new RoadsDirtyEvent());
            s.WorldIndex?.RebuildAll();
            PublishResourceRefreshEvents(s);
        }

        private static void RestoreCombatAfterLoad(GameServices s, CombatDTO combat)
        {
            if (s.CombatService is CombatService cs)
                cs.ResetAfterLoad(combat);
        }

        private static void RestorePopulationAfterLoad(GameServices s, PopulationDTO population)
        {
            if (s.PopulationService == null) return;

            if (population != null)
                s.PopulationService.LoadState(population.GrowthProgressDays, population.StarvationDays, population.StarvedToday);
            else
                s.PopulationService.Reset();

            s.PopulationService.RebuildDerivedState();
        }

        private static void TryRebuildRunStartRuntimeCaches(GameServices s)
        {
            try
            {
                var ta = Resources.Load<TextAsset>("RunStart/StartMapConfig_RunStart_64x64_v0.1");
                var cfgText = ta != null ? ta.text : null;
                if (string.IsNullOrWhiteSpace(cfgText)) return;

                var asm = typeof(GameServices).Assembly;
                var parserType = asm.GetType("SeasonalBastion.RunStart.RunStartInputParser");
                var validatorType = asm.GetType("SeasonalBastion.RunStart.RunStartConfigValidator");
                var cacheBuilderType = asm.GetType("SeasonalBastion.RunStart.RunStartRuntimeCacheBuilder");
                var hqResolverType = asm.GetType("SeasonalBastion.RunStart.RunStartHqResolver");
                if (parserType == null || validatorType == null || cacheBuilderType == null || hqResolverType == null) return;

                object[] parseArgs = { cfgText, null, null };
                var parse = parserType.GetMethod("TryParseConfig", BindingFlags.Static | BindingFlags.NonPublic);
                if (parse == null) return;
                var parseOk = parse.Invoke(null, parseArgs);
                if (parseOk is not bool ok || !ok) return;
                var cfg = parseArgs[1];
                if (cfg == null) return;

                object[] validateArgs = { s, cfg, null };
                var validate = validatorType.GetMethod("ValidateConfig", BindingFlags.Static | BindingFlags.NonPublic);
                if (validate == null) return;
                var validateOk = validate.Invoke(null, validateArgs);
                if (validateOk is not bool valid || !valid) return;

                cacheBuilderType.GetMethod("ApplyRuntimeMetadata", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, new[] { s, cfg });
                hqResolverType.GetMethod("BuildLanes", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, new[] { s, cfg });
            }
            catch { }
        }

        private static void PublishResourceRefreshEvents(GameServices s)
        {
            try
            {
                if (s?.EventBus == null || s?.WorldState?.Buildings == null) return;

                foreach (var bid in s.WorldState.Buildings.Ids)
                {
                    if (!s.WorldState.Buildings.Exists(bid)) continue;
                    var b = s.WorldState.Buildings.Get(bid);
                    if (!b.IsConstructed) continue;

                    if (b.Wood > 0) s.EventBus.Publish(new ResourceDeliveredEvent(ResourceType.Wood, 0, bid));
                    if (b.Food > 0) s.EventBus.Publish(new ResourceDeliveredEvent(ResourceType.Food, 0, bid));
                    if (b.Stone > 0) s.EventBus.Publish(new ResourceDeliveredEvent(ResourceType.Stone, 0, bid));
                    if (b.Iron > 0) s.EventBus.Publish(new ResourceDeliveredEvent(ResourceType.Iron, 0, bid));
                    if (b.Ammo > 0) s.EventBus.Publish(new ResourceDeliveredEvent(ResourceType.Ammo, 0, bid));
                }
            }
            catch { }
        }

        private static long Pack(int x, int y, string defId)
        {
            // deterministic cheap key
            unchecked
            {
                long h = 1469598103934665603L;
                h = (h ^ x) * 1099511628211L;
                h = (h ^ y) * 1099511628211L;
                if (!string.IsNullOrEmpty(defId))
                {
                    for (int i = 0; i < defId.Length; i++)
                        h = (h ^ defId[i]) * 1099511628211L;
                }
                return h;
            }
        }

        private static Season ParseSeason(string s)
        {
            if (string.IsNullOrEmpty(s)) return Season.Spring;
            if (Enum.TryParse<Season>(s, out var v)) return v;
            return Season.Spring;
        }
    }
}
