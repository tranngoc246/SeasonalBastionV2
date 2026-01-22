using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeasonalBastion.RunStart
{
    /// <summary>
    /// Applies StartMapConfig JSON into runtime world/grid.
    /// Deterministic: apply in JSON order; ids assigned by stores.
    /// </summary>
    public static class RunStartApplier
    {
        // Optional backwards compat (old defs) — only used if direct id not found.
        private static readonly Dictionary<string, string> DefRemap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "bld_hq_t1", "HQ" },
            { "bld_house_t1", "House" },
            { "bld_farmhouse_t1", "Farm" },
            { "bld_lumbercamp_t1", "Lumber" },
            { "bld_tower_arrow_t1", "TowerArrow" },
        };

        public static bool TryApply(GameServices s, string jsonOrMarkdown, out string error)
        {
            error = null;
            if (s == null) { error = "services=null"; return false; }

            string json = ExtractJsonIfMarkdown(jsonOrMarkdown);
            if (string.IsNullOrWhiteSpace(json)) { error = "StartMapConfig json is empty"; return false; }

            StartMapConfigRootDto cfg;
            try
            {
                cfg = JsonUtility.FromJson<StartMapConfigRootDto>(json);
            }
            catch (Exception ex)
            {
                error = "Parse StartMapConfig failed (JsonUtility): " + ex.Message;
                return false;
            }

            if (cfg == null || cfg.map == null)
            {
                error = "StartMapConfig missing map";
                return false;
            }

            // Basic grid size check
            if (s.GridMap != null)
            {
                if (cfg.map.width != s.GridMap.Width || cfg.map.height != s.GridMap.Height)
                {
                    error = $"StartMapConfig map size {cfg.map.width}x{cfg.map.height} != GridMap {s.GridMap.Width}x{s.GridMap.Height}";
                    return false;
                }
            }

            // Cache run-start metadata (optional)
            if (s.RunStartRuntime != null)
            {
                s.RunStartRuntime.MapWidth = cfg.map.width;
                s.RunStartRuntime.MapHeight = cfg.map.height;

                if (cfg.map.buildableRect != null)
                    s.RunStartRuntime.BuildableRect = new IntRect(
                        cfg.map.buildableRect.xMin,
                        cfg.map.buildableRect.yMin,
                        cfg.map.buildableRect.xMax,
                        cfg.map.buildableRect.yMax
                    );

                s.RunStartRuntime.SpawnGates.Clear();
                s.RunStartRuntime.Zones.Clear();

                if (cfg.spawnGates != null)
                {
                    for (int i = 0; i < cfg.spawnGates.Length; i++)
                    {
                        var g = cfg.spawnGates[i];
                        if (g == null || g.cell == null) continue;
                        s.RunStartRuntime.SpawnGates.Add(new SpawnGate(g.lane, new CellPos(g.cell.x, g.cell.y), ParseDir4(g.dirToHQ)));
                    }
                }

                if (cfg.zones != null)
                {
                    for (int i = 0; i < cfg.zones.Length; i++)
                    {
                        var z = cfg.zones[i];
                        if (z == null || z.cellsRect == null || string.IsNullOrEmpty(z.zoneId)) continue;
                        var rect = new IntRect(z.cellsRect.xMin, z.cellsRect.yMin, z.cellsRect.xMax, z.cellsRect.yMax);
                        s.RunStartRuntime.Zones[z.zoneId] = new ZoneRect(z.zoneId, z.type, z.ownerBuildingHint, rect, z.cellCount);
                    }
                }
            }

            // 1) Roads
            if (cfg.roads != null)
            {
                for (int i = 0; i < cfg.roads.Length; i++)
                {
                    var c = cfg.roads[i];
                    if (c == null) continue;
                    s.GridMap.SetRoad(new CellPos(c.x, c.y), true);
                }
            }

            // 2) Buildings
            var defIdToBuildingId = new Dictionary<string, BuildingId>(StringComparer.OrdinalIgnoreCase);

            if (cfg.initialBuildings != null)
            {
                for (int i = 0; i < cfg.initialBuildings.Length; i++)
                {
                    var b = cfg.initialBuildings[i];
                    if (b == null || b.anchor == null || string.IsNullOrEmpty(b.defId)) continue;

                    string resolvedDefId = ResolveBuildingDefId(s, b.defId);
                    if (string.IsNullOrEmpty(resolvedDefId))
                    {
                        // Best-effort for optional tower in StartMapConfig (common during incremental defs work)
                        if (IsArrowTowerLike(b.defId, null))
                            continue;

                        error = $"BuildingDef not found: {b.defId}";
                        return false;
                    }

                    var def = s.DataRegistry.GetBuilding(resolvedDefId);
                    int w = Math.Max(1, def.SizeX);
                    int h = Math.Max(1, def.SizeY);

                    var rot = ParseDir4(b.rotation);
                    var desiredAnchor = new CellPos(b.anchor.x, b.anchor.y);

                    // IMPORTANT: StartMapConfig anchors were authored against older smaller footprints.
                    // We re-validate placement with current Buildings.json and pick the nearest valid anchor deterministically.
                    if (!TryPickValidAnchor(s, resolvedDefId, desiredAnchor, w, h, rot, out var finalAnchor))
                    {
                        // Optional tower: don't fail run-start if invalid.
                        if (IsArrowTowerLike(b.defId, resolvedDefId))
                            continue;

                        error = $"RunStart: cannot place '{resolvedDefId}' near anchor ({desiredAnchor.X},{desiredAnchor.Y})";
                        return false;
                    }

                    var st = new BuildingState
                    {
                        DefId = resolvedDefId,
                        Anchor = finalAnchor,
                        Rotation = rot,
                        Level = Math.Max(1, def.BaseLevel),
                        IsConstructed = true
                    };

                    var id = s.WorldState.Buildings.Create(st);
                    st.Id = id;
                    s.WorldState.Buildings.Set(id, st);

                    // Occupy footprint
                    for (int dy = 0; dy < h; dy++)
                        for (int dx = 0; dx < w; dx++)
                            s.GridMap.SetBuilding(new CellPos(st.Anchor.X + dx, st.Anchor.Y + dy), id);

                    // WorldIndex bookkeeping
                    try { s.WorldIndex?.OnBuildingCreated(id); } catch { }

                    // keep first by defId for workplace mapping
                    if (!defIdToBuildingId.ContainsKey(b.defId))
                        defIdToBuildingId[b.defId] = id;
                    if (!defIdToBuildingId.ContainsKey(resolvedDefId))
                        defIdToBuildingId[resolvedDefId] = id;

                    // Tower init (optional): Arrow tower from balance table
                    if (IsArrowTowerLike(b.defId, resolvedDefId))
                        TryCreateArrowTowerState(s, st, b);
                }
            }

            // 3) Starting storage (Deliverable_B 2.3)
            ApplyStartingStorage(s);

            // 4) NPCs
            if (cfg.initialNpcs != null)
            {
                for (int i = 0; i < cfg.initialNpcs.Length; i++)
                {
                    var n = cfg.initialNpcs[i];
                    if (n == null || n.spawnCell == null || string.IsNullOrEmpty(n.npcDefId)) continue;

                    BuildingId workplace = default;
                    if (!string.IsNullOrEmpty(n.assignedWorkplaceDefId))
                    {
                        defIdToBuildingId.TryGetValue(n.assignedWorkplaceDefId, out workplace);
                        if (workplace.Value == 0)
                        {
                            // Try remapped id as well
                            var resolvedWp = ResolveBuildingDefId(s, n.assignedWorkplaceDefId);
                            if (!string.IsNullOrEmpty(resolvedWp))
                                defIdToBuildingId.TryGetValue(resolvedWp, out workplace);
                        }
                    }

                    var st = new NpcState
                    {
                        DefId = n.npcDefId,
                        Cell = new CellPos(n.spawnCell.x, n.spawnCell.y),
                        Workplace = workplace,
                        CurrentJob = default,
                        IsIdle = true
                    };

                    var id = s.WorldState.Npcs.Create(st);
                    st.Id = id;
                    s.WorldState.Npcs.Set(id, st);
                }
            }

            return true;
        }

        private static string ExtractJsonIfMarkdown(string jsonOrMd)
        {
            if (string.IsNullOrEmpty(jsonOrMd)) return jsonOrMd;

            // Fast path: starts with '{' => already json
            for (int i = 0; i < jsonOrMd.Length; i++)
            {
                char ch = jsonOrMd[i];
                if (char.IsWhiteSpace(ch)) continue;
                if (ch == '{') return jsonOrMd;
                break;
            }

            // Markdown: find ```json ... ```
            int fence = jsonOrMd.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
            if (fence < 0) return jsonOrMd;

            int start = jsonOrMd.IndexOf('\n', fence);
            if (start < 0) return jsonOrMd;
            start++;

            int end = jsonOrMd.IndexOf("```", start, StringComparison.OrdinalIgnoreCase);
            if (end < 0) return jsonOrMd;

            return jsonOrMd.Substring(start, end - start);
        }

        private static string ResolveBuildingDefId(GameServices s, string defId)
        {
            if (string.IsNullOrEmpty(defId) || s?.DataRegistry == null) return null;

            // 1) direct
            if (HasBuildingDef(s, defId)) return defId;

            // 2) remap
            if (DefRemap.TryGetValue(defId, out var mapped) && HasBuildingDef(s, mapped))
                return mapped;

            return null;
        }

        /// <summary>
        /// StartMapConfig anchors were authored for an earlier footprint set.
        /// We must place using current BuildingDef sizes from Buildings.json.
        /// Strategy: try desired anchor first, then search nearest (Manhattan ring) for the first valid placement.
        /// Deterministic: fixed scan order.
        /// </summary>
        private static bool TryPickValidAnchor(
            GameServices s,
            string buildingDefId,
            CellPos desiredAnchor,
            int w,
            int h,
            Dir4 rot,
            out CellPos finalAnchor)
        {
            finalAnchor = desiredAnchor;
            if (s == null || s.GridMap == null || string.IsNullOrEmpty(buildingDefId)) return false;

            var placement = s.PlacementService;
            if (placement == null)
            {
                // Should not happen in VS1/VS2, but keep graceful.
                return true;
            }

            bool hasBuildableRect = s.RunStartRuntime != null && (s.RunStartRuntime.BuildableRect.XMax != 0 || s.RunStartRuntime.BuildableRect.YMax != 0);
            var rect = hasBuildableRect ? s.RunStartRuntime.BuildableRect : default;

            bool IsFootprintInBuildable(CellPos a)
            {
                if (!hasBuildableRect) return true;
                if (!rect.Contains(a)) return false;
                return rect.Contains(new CellPos(a.X + w - 1, a.Y + h - 1));
            }

            bool IsCandidateOk(CellPos a)
            {
                if (!IsFootprintInBuildable(a)) return false;
                var pr = placement.ValidateBuilding(buildingDefId, a, rot);
                return pr.Ok;
            }

            // 0) desired anchor
            if (IsCandidateOk(desiredAnchor))
            {
                finalAnchor = desiredAnchor;
                return true;
            }

            // 1) search around (Manhattan rings)
            const int maxR = 24; // enough to resolve footprint drift while staying deterministic
            for (int r = 1; r <= maxR; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    int ax = r - Math.Abs(dy);
                    int dx1 = -ax;
                    int dx2 = ax;

                    var c1 = new CellPos(desiredAnchor.X + dx1, desiredAnchor.Y + dy);
                    if (IsCandidateOk(c1)) { finalAnchor = c1; return true; }

                    if (dx2 != dx1)
                    {
                        var c2 = new CellPos(desiredAnchor.X + dx2, desiredAnchor.Y + dy);
                        if (IsCandidateOk(c2)) { finalAnchor = c2; return true; }
                    }
                }
            }

            return false;
        }

        private static bool HasBuildingDef(GameServices s, string id)
        {
            try { s.DataRegistry.GetBuilding(id); return true; }
            catch { return false; }
        }

        private static Dir4 ParseDir4(string s)
        {
            if (string.IsNullOrEmpty(s)) return Dir4.N;
            char c = char.ToUpperInvariant(s[0]);
            return c switch
            {
                'N' => Dir4.N,
                'E' => Dir4.E,
                'S' => Dir4.S,
                'W' => Dir4.W,
                _ => Dir4.N
            };
        }

        private static bool IsArrowTowerLike(string rawDefId, string resolvedDefId)
        {
            if (!string.IsNullOrEmpty(rawDefId) && rawDefId.IndexOf("tower", StringComparison.OrdinalIgnoreCase) >= 0)
                return rawDefId.IndexOf("arrow", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!string.IsNullOrEmpty(resolvedDefId) && resolvedDefId.IndexOf("tower", StringComparison.OrdinalIgnoreCase) >= 0)
                return resolvedDefId.IndexOf("arrow", StringComparison.OrdinalIgnoreCase) >= 0;
            return false;
        }

        private static void TryCreateArrowTowerState(GameServices s, in BuildingState building, InitialBuildingDto b)
        {
            if (s.WorldState?.Towers == null) return;

            // Arrow Tower L1 (Deliverable_C 6.2)
            const int hpMax = 260;
            const int ammoMax = 90;

            int ammo = ammoMax;
            if (b != null && b.initialStateOverrides != null)
            {
                if (!string.IsNullOrEmpty(b.initialStateOverrides.ammo)
                    && b.initialStateOverrides.ammo.Equals("FULL", StringComparison.OrdinalIgnoreCase))
                    ammo = ammoMax;
                else if (b.initialStateOverrides.ammoPercent > 0f)
                    ammo = ClampToInt(b.initialStateOverrides.ammoPercent * ammoMax, 0, ammoMax);
            }

            var st = new TowerState
            {
                Cell = building.Anchor,
                Hp = hpMax,
                HpMax = hpMax,
                Ammo = ammo,
                AmmoCap = ammoMax,
            };

            var id = s.WorldState.Towers.Create(st);
            st.Id = id;
            s.WorldState.Towers.Set(id, st);

            // Also mirror ammo into building state for UI/debug convenience
            try
            {
                var bs = s.WorldState.Buildings.Get(building.Id);
                bs.Ammo = ammo;
                s.WorldState.Buildings.Set(building.Id, bs);
            }
            catch { }
        }

        private static int ClampToInt(float v, int min, int max)
        {
            int x = (int)Mathf.Round(v);
            if (x < min) return min;
            if (x > max) return max;
            return x;
        }

        private static void ApplyStartingStorage(GameServices s)
        {
            if (s.WorldState == null || s.DataRegistry == null || s.StorageService == null) return;

            BuildingId hq = default;

            // Prefer IsHQ tag
            foreach (var bid in s.WorldState.Buildings.Ids)
            {
                var st = s.WorldState.Buildings.Get(bid);
                try
                {
                    var def = s.DataRegistry.GetBuilding(st.DefId);
                    if (def.IsHQ)
                    {
                        hq = bid;
                        break;
                    }
                }
                catch { }
            }

            // Fallback: first building
            if (hq.Value == 0)
            {
                foreach (var bid in s.WorldState.Buildings.Ids) { hq = bid; break; }
            }

            if (hq.Value == 0) return;

            // Deliverable_B 2.3
            s.StorageService.Add(hq, ResourceType.Wood, 30);
            s.StorageService.Add(hq, ResourceType.Stone, 20);
            s.StorageService.Add(hq, ResourceType.Food, 10);
            s.StorageService.Add(hq, ResourceType.Iron, 0);
            s.StorageService.Add(hq, ResourceType.Ammo, 0);
        }

        // =========================
        // DTO (JsonUtility-friendly)
        // =========================

        [Serializable]
        private class StartMapConfigRootDto
        {
            public int schemaVersion;
            public CoordSystemDto coordSystem;
            public MapDto map;

            public RoadCellDto[] roads;
            public SpawnGateDto[] spawnGates;
            public ZoneDto[] zones;

            public InitialBuildingDto[] initialBuildings;
            public InitialNpcDto[] initialNpcs;

            public StartHintDto[] startHints;
            public string[] lockedInvariants;
        }

        [Serializable] private class CoordSystemDto { public string origin; public string indexing; public string notes; }

        [Serializable]
        private class MapDto
        {
            public int width;
            public int height;
            public RectMinMaxDto buildableRect;
        }

        [Serializable] private class RectMinMaxDto { public int xMin; public int yMin; public int xMax; public int yMax; }

        [Serializable] private class CellDto { public int x; public int y; }

        [Serializable] private class RoadCellDto { public int x; public int y; }

        [Serializable]
        private class SpawnGateDto
        {
            public int lane;
            public CellDto cell;
            public string dirToHQ;
        }

        [Serializable]
        private class ZoneDto
        {
            public string zoneId;
            public string type;
            public string ownerBuildingHint;
            public RectMinMaxDto cellsRect;
            public int cellCount;
        }

        [Serializable]
        private class InitialBuildingDto
        {
            public string defId;
            public CellDto anchor;
            public string rotation;
            public InitialBuildingOverridesDto initialStateOverrides;
            public string notes;
        }

        [Serializable]
        private class InitialBuildingOverridesDto
        {
            public string ammo;       // "FULL"
            public float ammoPercent; // 1.0
        }

        [Serializable]
        private class InitialNpcDto
        {
            public string npcDefId;
            public CellDto spawnCell;
            public string assignedWorkplaceDefId;
            public string jobProfile;
            public string notes;
        }

        [Serializable]
        private class StartHintDto
        {
            public string hintId;
            public string trigger;
            public string title;
            public string body;
            public string notificationKey;
        }
    }
}
