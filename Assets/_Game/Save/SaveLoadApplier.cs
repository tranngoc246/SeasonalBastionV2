using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;

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
                // 1) Clear runtime state
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

                // 2) Restore roads
                if (s.GridMap != null && dto.world.Roads != null)
                {
                    for (int i = 0; i < dto.world.Roads.Count; i++)
                    {
                        var c = dto.world.Roads[i];
                        s.GridMap.SetRoad(new CellPos(c.x, c.y), true);
                    }
                }

                // 3) Restore sites first (occupy as Site)
                if (dto.build.Sites != null)
                {
                    // ensure deterministic order
                    dto.build.Sites.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));

                    var siteStore = s.WorldState.Sites as BuildSiteStore;
                    if (siteStore == null) { error = "WorldState.Sites is not BuildSiteStore"; return false; }

                    for (int i = 0; i < dto.build.Sites.Count; i++)
                    {
                        var site = dto.build.Sites[i];

                        // Create with fixed id to keep references stable
                        siteStore.CreateWithId(site.Id, site, overwriteIfExists: true);
                        s.WorldState.Sites.Set(site.Id, site);

                        // occupy footprint
                        if (!s.DataRegistry.TryGetBuilding(site.BuildingDefId, out var def) || def == null)
                        {
                            error = $"Missing BuildingDef for site '{site.BuildingDefId}'";
                            return false;
                        }

                        if (site.Kind == 0)
                        {
                            int w = Math.Max(1, def.SizeX);
                            int h = Math.Max(1, def.SizeY);

                            for (int dy = 0; dy < h; dy++)
                                for (int dx = 0; dx < w; dx++)
                                    s.GridMap.SetSite(new CellPos(site.Anchor.X + dx, site.Anchor.Y + dy), site.Id);
                        }
                    }
                }
                // 4) Restore buildings
                if (dto.world.Buildings != null)
                {
                    dto.world.Buildings.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));

                    var buildingStore = s.WorldState.Buildings as BuildingStore;
                    if (buildingStore == null) { error = "WorldState.Buildings is not BuildingStore"; return false; }

                    // quick lookup: active sites by anchor+def for deciding occupancy
                    var hasSiteAt = new HashSet<long>();
                    if (dto.build.Sites != null)
                    {
                        for (int i = 0; i < dto.build.Sites.Count; i++)
                        {
                            var st = dto.build.Sites[i];
                            long key = Pack(st.Anchor.X, st.Anchor.Y, st.BuildingDefId);
                            hasSiteAt.Add(key);
                        }
                    }

                    for (int i = 0; i < dto.world.Buildings.Count; i++)
                    {
                        var b = dto.world.Buildings[i];

                        // Create with fixed id
                        buildingStore.CreateWithId(b.Id, b, overwriteIfExists: true);
                        s.WorldState.Buildings.Set(b.Id, b);

                        bool shouldOccupyAsBuilding = b.IsConstructed;
                        if (!shouldOccupyAsBuilding)
                        {
                            long key = Pack(b.Anchor.X, b.Anchor.Y, b.DefId);
                            if (!hasSiteAt.Contains(key))
                                shouldOccupyAsBuilding = true;
                        }

                        if (shouldOccupyAsBuilding)
                        {
                            if (!s.DataRegistry.TryGetBuilding(b.DefId, out var def) || def == null)
                            {
                                error = $"Missing BuildingDef for building '{b.DefId}'";
                                return false;
                            }

                            int w = Math.Max(1, def.SizeX);
                            int h = Math.Max(1, def.SizeY);

                            for (int dy = 0; dy < h; dy++)
                                for (int dx = 0; dx < w; dx++)
                                    s.GridMap.SetBuilding(new CellPos(b.Anchor.X + dx, b.Anchor.Y + dy), b.Id);
                        }
                    }
                }

                // 5) Restore towers
                if (dto.world.Towers != null)
                {
                    dto.world.Towers.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));

                    var towerStore = s.WorldState.Towers as TowerStore;
                    if (towerStore == null) { error = "WorldState.Towers is not TowerStore"; return false; }

                    for (int i = 0; i < dto.world.Towers.Count; i++)
                    {
                        var t = dto.world.Towers[i];
                        towerStore.CreateWithId(t.Id, t, overwriteIfExists: true);
                        s.WorldState.Towers.Set(t.Id, t);
                    }
                }

                // 6) Restore npcs
                if (dto.world.Npcs != null)
                {
                    dto.world.Npcs.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));

                    var npcStore = s.WorldState.Npcs as NpcStore;
                    if (npcStore == null) { error = "WorldState.Npcs is not NpcStore"; return false; }

                    for (int i = 0; i < dto.world.Npcs.Count; i++)
                    {
                        var n = dto.world.Npcs[i];

                        // hard reset transient runtime state
                        n.CurrentJob = default;
                        n.IsIdle = true;

                        npcStore.CreateWithId(n.Id, n, overwriteIfExists: true);
                        s.WorldState.Npcs.Set(n.Id, n);
                    }
                }

                // 6.5) Restore enemies (Day33)
                if (dto.world.Enemies != null)
                {
                    dto.world.Enemies.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));

                    var enemyStore = s.WorldState.Enemies as EnemyStore;
                    if (enemyStore == null) { error = "WorldState.Enemies is not EnemyStore"; return false; }

                    for (int i = 0; i < dto.world.Enemies.Count; i++)
                    {
                        var e = dto.world.Enemies[i];
                        enemyStore.CreateWithId(e.Id, e, overwriteIfExists: true);
                        s.WorldState.Enemies.Set(e.Id, e);
                    }
                }

                // + ADD: rebuild build orders for active sites (so construction continues after load)
                if (s.BuildOrderService is BuildOrderService bos)
                {
                    bos.RebuildActivePlaceOrdersFromSitesAfterLoad();
                }

                // 7) Rebuild index
                s.WorldIndex?.RebuildAll();

                // 8) Restore clock snapshot (needs RunClockService.LoadSnapshot)
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
                    // fallback
                    s.RunClock?.ForceSeasonDay(ParseSeason(dto.season), dto.dayIndex);
                    s.RunClock?.SetTimeScale(dto.timeScale);
                }

                // 9) Day33: reset combat/wave state after load
                if (s.CombatService is CombatService cs)
                {
                    cs.ResetAfterLoad(dto.combat);
                }

                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
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
