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
                if (!ValidateSnapshotDeep(s, dto, out error))
                {
                    Debug.LogError("[SaveLoad] Snapshot validation failed: " + error);
                    return false;
                }

                var transaction = new ApplyTransaction(s);
                transaction.Begin();

                try
                {
                    RestoreClockSnapshot(s, dto);
                    RestoreRoads(s, dto.world);
                    RestoreWorldCollections(s, dto, out error);
                    if (error != null)
                        throw new InvalidOperationException(error);

                    RestoreBuildOrdersAfterLoad(s);
                    RebuildRuntimeCachesAndIndices(s);
                    RestoreCombatAfterLoad(s, dto.combat);
                    RestorePopulationAfterLoad(s, dto.population);
                    transaction.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    error = ex.Message;
                    Debug.LogError("[SaveLoad] Apply transaction failed: " + ex);
                    return false;
                }
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogError("[SaveLoad] TryApply failed: " + e);
                return false;
            }
        }

        public static bool ValidateSnapshotDeep(GameServices s, RunSaveDTO dto, out string error)
        {
            error = null;
            if (s.WorldState?.Buildings is not BuildingStore) { error = "WorldState.Buildings is not BuildingStore"; return false; }
            if (s.WorldState?.Sites is not BuildSiteStore) { error = "WorldState.Sites is not BuildSiteStore"; return false; }
            if (s.WorldState?.Towers is not TowerStore) { error = "WorldState.Towers is not TowerStore"; return false; }
            if (s.WorldState?.Npcs is not NpcStore) { error = "WorldState.Npcs is not NpcStore"; return false; }
            if (s.WorldState?.Enemies is not EnemyStore) { error = "WorldState.Enemies is not EnemyStore"; return false; }
            if (s.GridMap == null) { error = "GridMap null"; return false; }
            if (s.DataRegistry == null) { error = "DataRegistry null"; return false; }

            var buildingsById = new Dictionary<int, BuildingState>();
            var sitesById = new Dictionary<int, BuildSiteState>();
            var towersByCell = new Dictionary<long, TowerState>();
            var buildingCells = new Dictionary<long, int>();
            var siteCells = new Dictionary<long, int>();
            var roadCells = new HashSet<long>();

            if (dto.world?.Buildings != null)
            {
                for (int i = 0; i < dto.world.Buildings.Count; i++)
                {
                    var b = dto.world.Buildings[i];
                    if (string.IsNullOrWhiteSpace(b.DefId) || !s.DataRegistry.TryGetBuilding(b.DefId, out var def) || def == null)
                    {
                        error = $"Missing BuildingDef for building '{b.DefId}'";
                        return false;
                    }

                    if (buildingsById.ContainsKey(b.Id.Value))
                    {
                        error = $"Duplicate BuildingId {b.Id.Value}";
                        return false;
                    }

                    buildingsById.Add(b.Id.Value, b);

                    int w = Math.Max(1, def.SizeX);
                    int h = Math.Max(1, def.SizeY);
                    for (int dy = 0; dy < h; dy++)
                    for (int dx = 0; dx < w; dx++)
                    {
                        int x = b.Anchor.X + dx;
                        int y = b.Anchor.Y + dy;
                        if (!s.GridMap.IsInside(new CellPos(x, y)))
                        {
                            error = $"Building {b.Id.Value} out of bounds at ({x},{y})";
                            return false;
                        }

                        long key = PackCell(x, y);
                        if (buildingCells.TryGetValue(key, out var otherBuilding))
                        {
                            error = $"Grid building overlap between {otherBuilding} and {b.Id.Value} at ({x},{y})";
                            return false;
                        }

                        buildingCells.Add(key, b.Id.Value);
                    }
                }
            }

            if (dto.build?.Sites != null)
            {
                for (int i = 0; i < dto.build.Sites.Count; i++)
                {
                    var site = dto.build.Sites[i];
                    if (string.IsNullOrWhiteSpace(site.BuildingDefId) || !s.DataRegistry.TryGetBuilding(site.BuildingDefId, out var def) || def == null)
                    {
                        error = $"Missing BuildingDef for site '{site.BuildingDefId}'";
                        return false;
                    }

                    if (sitesById.ContainsKey(site.Id.Value))
                    {
                        error = $"Duplicate SiteId {site.Id.Value}";
                        return false;
                    }

                    if (site.TargetBuilding.Value != 0 && !buildingsById.ContainsKey(site.TargetBuilding.Value))
                    {
                        error = $"Site {site.Id.Value} references missing TargetBuildingId {site.TargetBuilding.Value}";
                        return false;
                    }

                    sitesById.Add(site.Id.Value, site);

                    int w = Math.Max(1, def.SizeX);
                    int h = Math.Max(1, def.SizeY);
                    for (int dy = 0; dy < h; dy++)
                    for (int dx = 0; dx < w; dx++)
                    {
                        int x = site.Anchor.X + dx;
                        int y = site.Anchor.Y + dy;
                        if (!s.GridMap.IsInside(new CellPos(x, y)))
                        {
                            error = $"Site {site.Id.Value} out of bounds at ({x},{y})";
                            return false;
                        }

                        long key = PackCell(x, y);
                        if (siteCells.TryGetValue(key, out var otherSite))
                        {
                            error = $"Grid site overlap between {otherSite} and {site.Id.Value} at ({x},{y})";
                            return false;
                        }

                        if (buildingCells.TryGetValue(key, out var otherBuilding) && site.Kind == 0 && site.TargetBuilding.Value != otherBuilding)
                        {
                            error = $"Grid occupancy mismatch at ({x},{y}): site {site.Id.Value} conflicts with building {otherBuilding}";
                            return false;
                        }

                        siteCells.Add(key, site.Id.Value);
                    }
                }
            }

            if (dto.world?.Npcs != null)
            {
                for (int i = 0; i < dto.world.Npcs.Count; i++)
                {
                    var n = dto.world.Npcs[i];
                    if (!string.IsNullOrWhiteSpace(n.DefId) && !s.DataRegistry.TryGetNpc(n.DefId, out _))
                    {
                        error = $"Missing NpcDef for npc '{n.DefId}'";
                        return false;
                    }

                    if (n.Workplace.Value != 0 && !buildingsById.ContainsKey(n.Workplace.Value))
                    {
                        error = $"Npc {n.Id.Value} references missing workplace building {n.Workplace.Value}";
                        return false;
                    }
                }
            }

            if (dto.world?.Towers != null)
            {
                for (int i = 0; i < dto.world.Towers.Count; i++)
                {
                    var t = dto.world.Towers[i];
                    if (string.IsNullOrWhiteSpace(t.DefId) || !s.DataRegistry.TryGetTower(t.DefId, out _))
                    {
                        error = $"Missing TowerDef for tower '{t.DefId}'";
                        return false;
                    }

                    long key = PackCell(t.Cell.X, t.Cell.Y);
                    if (towersByCell.ContainsKey(key))
                    {
                        error = $"Duplicate tower cell occupancy at ({t.Cell.X},{t.Cell.Y})";
                        return false;
                    }

                    towersByCell.Add(key, t);
                }
            }

            if (dto.world?.Enemies != null)
            {
                for (int i = 0; i < dto.world.Enemies.Count; i++)
                {
                    var e = dto.world.Enemies[i];
                    if (string.IsNullOrWhiteSpace(e.DefId) || !s.DataRegistry.TryGetEnemy(e.DefId, out _))
                    {
                        error = $"Missing EnemyDef for enemy '{e.DefId}'";
                        return false;
                    }
                }
            }

            if (dto.world?.Roads != null)
            {
                for (int i = 0; i < dto.world.Roads.Count; i++)
                {
                    var c = dto.world.Roads[i];
                    if (!s.GridMap.IsInside(new CellPos(c.x, c.y)))
                    {
                        error = $"Road out of bounds at ({c.x},{c.y})";
                        return false;
                    }

                    long key = PackCell(c.x, c.y);
                    if (!roadCells.Add(key))
                    {
                        error = $"Duplicate road cell at ({c.x},{c.y})";
                        return false;
                    }
                }
            }

            return true;
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
            catch (Exception ex)
            {
                Debug.LogError("[SaveLoad] Rebuild runtime caches failed: " + ex);
            }
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
            catch (Exception ex)
            {
                Debug.LogError("[SaveLoad] PublishResourceRefreshEvents failed: " + ex);
            }
        }

        private sealed class ApplyTransaction
        {
            private readonly GameServices _s;
            private readonly RuntimeSnapshot _snapshot;
            private bool _committed;

            public ApplyTransaction(GameServices s)
            {
                _s = s;
                _snapshot = RuntimeSnapshot.Capture(s);
            }

            public void Begin()
            {
                ClearCurrentRuntimeBeforeApply(_s);
            }

            public void Commit()
            {
                _committed = true;
            }

            public void Rollback()
            {
                if (_committed) return;
                _snapshot.Restore(_s);
            }
        }

        private sealed class RuntimeSnapshot
        {
            public readonly List<BuildingState> Buildings = new();
            public readonly List<BuildSiteState> Sites = new();
            public readonly List<NpcState> Npcs = new();
            public readonly List<TowerState> Towers = new();
            public readonly List<EnemyState> Enemies = new();
            public readonly List<CellPosI32> Roads = new();

            public string Season;
            public int DayIndex;
            public float TimeScale;
            public int YearIndex = 1;
            public float DayTimer;

            public static RuntimeSnapshot Capture(GameServices s)
            {
                var snap = new RuntimeSnapshot();
                if (s?.RunClock != null)
                {
                    snap.Season = s.RunClock.CurrentSeason.ToString();
                    snap.DayIndex = s.RunClock.DayIndex;
                    snap.TimeScale = s.RunClock.TimeScale;
                    if (s.RunClock is RunClockService rc)
                    {
                        snap.YearIndex = rc.YearIndex;
                        snap.DayTimer = rc.DayTimerSeconds;
                    }
                }

                if (s?.WorldState?.Buildings != null)
                    foreach (var id in s.WorldState.Buildings.Ids)
                        if (s.WorldState.Buildings.Exists(id)) snap.Buildings.Add(s.WorldState.Buildings.Get(id));

                if (s?.WorldState?.Sites != null)
                    foreach (var id in s.WorldState.Sites.Ids)
                        if (s.WorldState.Sites.Exists(id)) snap.Sites.Add(s.WorldState.Sites.Get(id));

                if (s?.WorldState?.Npcs != null)
                    foreach (var id in s.WorldState.Npcs.Ids)
                        if (s.WorldState.Npcs.Exists(id)) snap.Npcs.Add(s.WorldState.Npcs.Get(id));

                if (s?.WorldState?.Towers != null)
                    foreach (var id in s.WorldState.Towers.Ids)
                        if (s.WorldState.Towers.Exists(id)) snap.Towers.Add(s.WorldState.Towers.Get(id));

                if (s?.WorldState?.Enemies != null)
                    foreach (var id in s.WorldState.Enemies.Ids)
                        if (s.WorldState.Enemies.Exists(id)) snap.Enemies.Add(s.WorldState.Enemies.Get(id));

                if (s?.GridMap != null)
                {
                    for (int y = 0; y < s.GridMap.Height; y++)
                    for (int x = 0; x < s.GridMap.Width; x++)
                    {
                        var c = new CellPos(x, y);
                        if (s.GridMap.IsRoad(c)) snap.Roads.Add(new CellPosI32(x, y));
                    }
                }

                return snap;
            }

            public void Restore(GameServices s)
            {
                try
                {
                    ClearCurrentRuntimeBeforeApply(s);

                    var dto = new RunSaveDTO
                    {
                        season = Season,
                        dayIndex = DayIndex,
                        timeScale = TimeScale,
                        yearIndex = YearIndex,
                        dayTimer = DayTimer,
                        world = new WorldDTO
                        {
                            Buildings = new List<BuildingState>(Buildings),
                            Npcs = new List<NpcState>(Npcs),
                            Towers = new List<TowerState>(Towers),
                            Enemies = new List<EnemyState>(Enemies),
                            Roads = new List<CellPosI32>(Roads)
                        },
                        build = new BuildDTO { Sites = new List<BuildSiteState>(Sites) },
                        combat = new CombatDTO(),
                        population = new PopulationDTO()
                    };

                    RestoreClockSnapshot(s, dto);
                    RestoreRoads(s, dto.world);
                    RestoreWorldCollections(s, dto, out _);
                    RestoreBuildOrdersAfterLoad(s);
                    RebuildRuntimeCachesAndIndices(s);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[SaveLoad] Rollback restore failed: " + ex);
                }
            }
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

        private static long Pack(int x, int y, string defId)
        {
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

        private static long PackCell(int x, int y)
        {
            unchecked
            {
                return ((long)x << 32) ^ (uint)y;
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
