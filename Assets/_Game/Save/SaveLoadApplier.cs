using log4net;
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

                    for (int i = 0; i < dto.build.Sites.Count; i++)
                    {
                        var site = dto.build.Sites[i];
                        var created = s.WorldState.Sites.Create(site);
                        // must match to keep references stable
                        if (created.Value != site.Id.Value)
                        {
                            // if mismatch, still set but warn
                            // (v0.1 minimal – assume no gaps)
                        }

                        site.Id = created;
                        s.WorldState.Sites.Set(created, site);

                        // occupy footprint
                        var def = s.DataRegistry.GetBuilding(site.BuildingDefId);
                        int w = Math.Max(1, def.SizeX);
                        int h = Math.Max(1, def.SizeY);

                        for (int dy = 0; dy < h; dy++)
                            for (int dx = 0; dx < w; dx++)
                                s.GridMap.SetSite(new CellPos(site.Anchor.X + dx, site.Anchor.Y + dy), created);
                    }
                }

                // 4) Restore buildings (constructed => occupy Building; if not constructed and site exists => footprint already occupied)
                if (dto.world.Buildings != null)
                {
                    dto.world.Buildings.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));

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
                        var created = s.WorldState.Buildings.Create(b);
                        if (created.Value != b.Id.Value)
                        {
                            // assume stable in v0.1
                        }

                        b.Id = created;
                        s.WorldState.Buildings.Set(created, b);

                        bool shouldOccupyAsBuilding = b.IsConstructed;
                        if (!shouldOccupyAsBuilding)
                        {
                            long key = Pack(b.Anchor.X, b.Anchor.Y, b.DefId);
                            if (!hasSiteAt.Contains(key))
                                shouldOccupyAsBuilding = true;
                        }

                        if (shouldOccupyAsBuilding)
                        {
                            var def = s.DataRegistry.GetBuilding(b.DefId);
                            int w = Math.Max(1, def.SizeX);
                            int h = Math.Max(1, def.SizeY);

                            for (int dy = 0; dy < h; dy++)
                                for (int dx = 0; dx < w; dx++)
                                    s.GridMap.SetBuilding(new CellPos(b.Anchor.X + dx, b.Anchor.Y + dy), created);
                        }
                    }
                }

                // 5) Restore towers
                if (dto.world.Towers != null)
                {
                    dto.world.Towers.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));

                    for (int i = 0; i < dto.world.Towers.Count; i++)
                    {
                        var t = dto.world.Towers[i];
                        var created = s.WorldState.Towers.Create(t);
                        t.Id = created;
                        s.WorldState.Towers.Set(created, t);
                    }
                }

                // 6) Restore npcs
                if (dto.world.Npcs != null)
                {
                    dto.world.Npcs.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));

                    for (int i = 0; i < dto.world.Npcs.Count; i++)
                    {
                        var n = dto.world.Npcs[i];
                        var created = s.WorldState.Npcs.Create(n);
                        n.Id = created;
                        n.CurrentJob = default;
                        n.IsIdle = true;
                        s.WorldState.Npcs.Set(created, n);
                    }
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
