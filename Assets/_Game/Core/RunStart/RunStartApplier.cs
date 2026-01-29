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
        // Option 1 (canonical): StartMapConfig MUST use canonical DefIds from Buildings.json.
        // No alias/remap here. Single-source-of-truth = DataRegistry.Buildings.
        private const string TowerArrowDefId = "bld_tower_arrow_t1";

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

            // VS3 hardening: validate StartMapConfig header + locked invariants
            if (!ValidateStartMapHeader(cfg, out error))
                return false;

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
                s.RunStartRuntime.Lanes.Clear(); // Day27
                s.RunStartRuntime.LockedInvariants.Clear();

                if (cfg.lockedInvariants != null)
                {
                    for (int i = 0; i < cfg.lockedInvariants.Length; i++)
                    {
                        var t = cfg.lockedInvariants[i];
                        if (!string.IsNullOrEmpty(t))
                            s.RunStartRuntime.LockedInvariants.Add(t);
                    }
                }

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
            // Canonical only: key = canonical defId (same as Buildings.json)
            var defIdToBuildingId = new Dictionary<string, BuildingId>(StringComparer.OrdinalIgnoreCase);

            if (cfg.initialBuildings != null)
            {
                for (int i = 0; i < cfg.initialBuildings.Length; i++)
                {
                    var b = cfg.initialBuildings[i];
                    if (b == null || b.anchor == null || string.IsNullOrEmpty(b.defId)) continue;

                    string defId = ResolveBuildingDefIdOrNull(s, b.defId);
                    if (string.IsNullOrEmpty(defId))
                    {
                        // Tower is stored separately (TowerStore). If StartMapConfig includes TowerArrow but Buildings.json doesn't yet,
                        // still create TowerState so Day25+ debug can work.
                        if (IsArrowTowerLike(b.defId))
                        {
                            var desired = new CellPos(b.anchor.x, b.anchor.y);
                            TryCreateArrowTowerStandalone(s, desired, b);
                            continue;
                        }

                        error = $"BuildingDef not found: {b.defId}";
                        return false;
                    }

                    var def = s.DataRegistry.GetBuilding(defId);
                    int w = Math.Max(1, def.SizeX);
                    int h = Math.Max(1, def.SizeY);

                    var rot = ParseDir4(b.rotation);
                    var desiredAnchor = new CellPos(b.anchor.x, b.anchor.y);

                    // IMPORTANT: StartMapConfig anchors might be authored against older smaller footprints.
                    // We re-validate placement with current Buildings.json and pick the nearest valid anchor deterministically.
                    if (!TryPickValidAnchor(s, defId, desiredAnchor, w, h, rot, out var finalAnchor))
                    {
                        // Optional tower: don't fail run-start if invalid.
                        if (IsArrowTowerLike(defId)) continue;

                        error = $"RunStart: cannot place '{defId}' near anchor ({desiredAnchor.X},{desiredAnchor.Y})";
                        return false;
                    }

                    var st = new BuildingState
                    {
                        DefId = defId,
                        Anchor = finalAnchor,
                        Rotation = rot,
                        Level = Math.Max(1, def.BaseLevel),
                        IsConstructed = true
                    };

                    try
                    {
                        int hpMax = Mathf.Max(1, def.MaxHp);
                        st.HP = hpMax; 
                    }
                    catch
                    {
                        st.HP = 1;
                    }

                    var id = s.WorldState.Buildings.Create(st);
                    st.Id = id;
                    s.WorldState.Buildings.Set(id, st);

                    // Occupy footprint
                    for (int dy = 0; dy < h; dy++)
                        for (int dx = 0; dx < w; dx++)
                            s.GridMap.SetBuilding(new CellPos(st.Anchor.X + dx, st.Anchor.Y + dy), id);

                    PromoteRunStartEntryRoads(s, st, w, h);

                    // WorldIndex bookkeeping
                    try { s.WorldIndex?.OnBuildingCreated(id); } catch { }

                    // Canonical mapping for workplace lookup
                    if (!defIdToBuildingId.ContainsKey(defId))
                        defIdToBuildingId[defId] = id;

                    // Tower init (optional): Arrow tower from balance table
                    if (IsArrowTowerLike(defId))
                        TryCreateArrowTowerState(s, st, b);
                }
            }

            // Day27: Build lane table (laneId -> start cell -> dir -> target HQ)
            if (s.RunStartRuntime != null)
            {
                // Resolve HQ target cell (center of HQ footprint)
                if (TryResolveHQTargetCell(s, out var hqTarget))
                {
                    // Prefer cfg.spawnGates as source of truth (same as SpawnGates cache)
                    if (cfg.spawnGates != null)
                    {
                        for (int i = 0; i < cfg.spawnGates.Length; i++)
                        {
                            var g = cfg.spawnGates[i];
                            if (g == null || g.cell == null) continue;

                            int laneId = g.lane;
                            var start = new CellPos(g.cell.x, g.cell.y);
                            var dir = ParseDir4(g.dirToHQ);

                            if (TryResolveHQTargetCellAdjacent(s, dir, out var hqAdjTarget))
                            {
                                s.RunStartRuntime.Lanes[laneId] = new LaneRuntime(laneId, start, dir, hqAdjTarget);
                            }
                        }
                    }
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
                        // Canonical only
                        defIdToBuildingId.TryGetValue(n.assignedWorkplaceDefId, out workplace);
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

        // Day27: Resolve HQ target as an OUTSIDE cell adjacent to HQ footprint (4-neighbor).
        // Prefer the side implied by dirToHQ (enemy approaches HQ along dirToHQ).
        private static bool TryResolveHQTargetCellAdjacent(GameServices s, Dir4 dirToHQ, out CellPos target)
        {
            target = default;
            if (s == null || s.WorldState == null || s.DataRegistry == null || s.GridMap == null) return false;

            // 1) Find constructed HQ (deterministic by smallest BuildingId)
            BuildingId best = default;
            int bestId = int.MaxValue;

            foreach (var id in s.WorldState.Buildings.Ids)
            {
                if (!s.WorldState.Buildings.Exists(id)) continue;
                var st = s.WorldState.Buildings.Get(id);
                if (!st.IsConstructed) continue;

                bool isHQ = false;
                try
                {
                    var def = s.DataRegistry.GetBuilding(st.DefId);
                    isHQ = def.IsHQ;
                }
                catch { }

                if (!isHQ && !string.Equals(st.DefId, "bld_hq_t1", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                if (id.Value < bestId)
                {
                    best = id;
                    bestId = id.Value;
                }
            }

            if (best.Value == 0) return false;

            var hq = s.WorldState.Buildings.Get(best);

            int w = 1, h = 1;
            try
            {
                var def = s.DataRegistry.GetBuilding(hq.DefId);
                w = System.Math.Max(1, def.SizeX);
                h = System.Math.Max(1, def.SizeY);
            }
            catch { }

            // 2) Pick preferred adjacent cell based on approach direction:
            // If enemy moves N to reach HQ, it approaches HQ from the SOUTH side => target just below HQ footprint.
            int midX = (w - 1) / 2;
            int midY = (h - 1) / 2;

            CellPos pref;
            switch (dirToHQ)
            {
                case Dir4.N: pref = new CellPos(hq.Anchor.X + midX, hq.Anchor.Y - 1); break;          // south of HQ
                case Dir4.S: pref = new CellPos(hq.Anchor.X + midX, hq.Anchor.Y + h); break;          // north of HQ
                case Dir4.E: pref = new CellPos(hq.Anchor.X - 1, hq.Anchor.Y + midY); break;          // west of HQ
                case Dir4.W: pref = new CellPos(hq.Anchor.X + w, hq.Anchor.Y + midY); break;          // east of HQ
                default: pref = new CellPos(hq.Anchor.X + midX, hq.Anchor.Y - 1); break;
            }

            if (IsGoodTargetCell(s, pref))
            {
                target = pref;
                return true;
            }

            // 3) Fallback: any valid adjacent cell around footprint (deterministic order)
            // Order: South, West, North, East (you can change, but keep deterministic)
            var candidates = new CellPos[]
            {
        new CellPos(hq.Anchor.X + midX, hq.Anchor.Y - 1),
        new CellPos(hq.Anchor.X - 1, hq.Anchor.Y + midY),
        new CellPos(hq.Anchor.X + midX, hq.Anchor.Y + h),
        new CellPos(hq.Anchor.X + w, hq.Anchor.Y + midY),
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (!IsGoodTargetCell(s, candidates[i])) continue;
                target = candidates[i];
                return true;
            }

            return false;
        }

        private static bool IsGoodTargetCell(GameServices s, CellPos c)
        {
            var grid = s.GridMap;
            if (!grid.IsInside(c)) return false;

            // Không nằm trong footprint building nào
            var occ = grid.Get(c).Kind;
            if (occ == CellOccupancyKind.Building || occ == CellOccupancyKind.Site)
                return false;

            // Có thể cho phép đứng trên road hoặc ground đều OK. (Nếu bạn muốn bắt buộc road, bật check dưới)
            // if (!grid.IsRoad(c)) return false;

            return true;
        }

        // Day27: Resolve HQ target as center cell of HQ footprint (deterministic)
        private static bool TryResolveHQTargetCell(GameServices s, out CellPos target)
        {
            target = default;
            if (s == null || s.WorldState == null || s.DataRegistry == null) return false;

            BuildingId best = default;
            int bestId = int.MaxValue;

            // Find constructed HQ by tag, fallback by DefId == "HQ"
            foreach (var id in s.WorldState.Buildings.Ids)
            {
                if (!s.WorldState.Buildings.Exists(id)) continue;
                var st = s.WorldState.Buildings.Get(id);
                if (!st.IsConstructed) continue;

                bool isHQ = false;
                try
                {
                    var def = s.DataRegistry.GetBuilding(st.DefId);
                    isHQ = def.IsHQ;
                }
                catch
                {
                    // ignore
                }

                if (!isHQ && !string.Equals(st.DefId, "bld_hq_t1", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (id.Value < bestId)
                {
                    best = id;
                    bestId = id.Value;
                }
            }

            if (best.Value == 0) return false;

            var hq = s.WorldState.Buildings.Get(best);

            int w = 3, h = 3;
            try
            {
                var def = s.DataRegistry.GetBuilding(hq.DefId);
                w = Math.Max(1, def.SizeX);
                h = Math.Max(1, def.SizeY);
            }
            catch { }

            // center cell (integer)
            target = new CellPos(hq.Anchor.X + (w - 1) / 2, hq.Anchor.Y + (h - 1) / 2);
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

        /// <summary>
        /// Option 1: canonical only. Returns defId if exists, else null.
        /// </summary>
        private static string ResolveBuildingDefIdOrNull(GameServices s, string defId)
        {
            if (string.IsNullOrEmpty(defId) || s?.DataRegistry == null) return null;
            return HasBuildingDef(s, defId) ? defId : null;
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

        private const string HQ_DEFID = "bld_hq_t1";

        private static void PromoteRunStartEntryRoads(GameServices s, BuildingState b, int w, int h)
        {
            // Safety
            if (s == null || s.GridMap == null) return;

            // HQ wants 4 entry cells (N/E/S/W), regardless of rotation.
            if (string.Equals(b.DefId, HQ_DEFID, StringComparison.OrdinalIgnoreCase))
            {
                PromoteRoadIfPossible(s, ComputeEntryOutsideFootprint(b.Anchor, w, h, Dir4.N));
                PromoteRoadIfPossible(s, ComputeEntryOutsideFootprint(b.Anchor, w, h, Dir4.S));
                PromoteRoadIfPossible(s, ComputeEntryOutsideFootprint(b.Anchor, w, h, Dir4.E));
                PromoteRoadIfPossible(s, ComputeEntryOutsideFootprint(b.Anchor, w, h, Dir4.W));
                return;
            }

            // Normal buildings: single entry based on rotation
            PromoteRoadIfPossible(s, ComputeEntryOutsideFootprint(b.Anchor, w, h, b.Rotation));
        }

        private static void PromoteRoadIfPossible(GameServices s, CellPos cell)
        {
            // Only promote if inside and not occupied by building/site; if already road => keep.
            if (!s.GridMap.IsInside(cell)) return;

            // Don't overwrite building footprint
            var occ = s.GridMap.Get(cell);
            if (occ.Kind == CellOccupancyKind.Building) return;
            if (occ.Kind == CellOccupancyKind.Site) return;

            if (!s.GridMap.IsRoad(cell))
                s.GridMap.SetRoad(cell, true);
        }

        // Entry/Driveway = middle of the FRONT edge, 1 cell OUTSIDE footprint (deterministic).
        private static CellPos ComputeEntryOutsideFootprint(CellPos anchor, int w, int h, Dir4 rot)
        {
            int cx = w / 2;
            int cy = h / 2;

            return rot switch
            {
                Dir4.N => new CellPos(anchor.X + cx, anchor.Y + h),     // outside north
                Dir4.S => new CellPos(anchor.X + cx, anchor.Y - 1),     // outside south
                Dir4.E => new CellPos(anchor.X + w, anchor.Y + cy),    // outside east
                Dir4.W => new CellPos(anchor.X - 1, anchor.Y + cy),    // outside west
                _ => new CellPos(anchor.X + cx, anchor.Y + h),
            };
        }

        private static bool IsArrowTowerLike(string defId)
        {
            return !string.IsNullOrEmpty(defId) && defId.Equals(TowerArrowDefId, StringComparison.OrdinalIgnoreCase);
        }

        private static void TryCreateArrowTowerState(GameServices s, in BuildingState building, InitialBuildingDto b)
        {
            if (s.WorldState?.Towers == null) return;

            // Arrow Tower L1 (Deliverable_C 6.2) - prefer TowerDef if present
            int hpMax = 260;
            int ammoMax = 90;

            try
            {
                var tdef = s.DataRegistry.GetTower(TowerArrowDefId);
                if (tdef != null)
                {
                    hpMax = Mathf.Max(1, tdef.MaxHp);
                    ammoMax = Mathf.Max(0, tdef.AmmoMax);
                }
            }
            catch { /* keep fallback */ }

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

        private static void TryCreateArrowTowerStandalone(GameServices s, CellPos desiredCell, InitialBuildingDto b)
        {
            if (s == null || s.WorldState?.Towers == null) return;

            // Pick a valid cell (prefer desired anchor; avoid overlapping buildings/sites deterministically)
            if (!TryPickValidTowerCell(s, desiredCell, out var finalCell))
                return;

            // Prefer TowerDef from Towers.json (DataRegistry), fallback to balance constants
            int hpMax = 260;
            int ammoMax = 90;

            try
            {
                var tdef = s.DataRegistry?.GetTower(TowerArrowDefId);
                if (tdef != null)
                {
                    hpMax = Mathf.Max(1, tdef.MaxHp);
                    ammoMax = Mathf.Max(0, tdef.AmmoMax);
                }
            }
            catch { /* keep fallback */ }

            int ammo = ammoMax;
            if (b != null && b.initialStateOverrides != null)
            {
                if (!string.IsNullOrEmpty(b.initialStateOverrides.ammo)
                    && b.initialStateOverrides.ammo.Equals("FULL", StringComparison.OrdinalIgnoreCase))
                {
                    ammo = ammoMax;
                }
                else if (b.initialStateOverrides.ammoPercent > 0f)
                {
                    ammo = ClampToInt(b.initialStateOverrides.ammoPercent * ammoMax, 0, ammoMax);
                }
            }

            var st = new TowerState
            {
                Cell = finalCell,
                Hp = hpMax,
                HpMax = hpMax,
                Ammo = ammo,
                AmmoCap = ammoMax
            };

            var id = s.WorldState.Towers.Create(st);
            st.Id = id;
            s.WorldState.Towers.Set(id, st);

            // Rebuild index so debug gizmos/hud see it immediately
            try { s.WorldIndex?.RebuildAll(); } catch { }
        }

        /// <summary>
        /// Tower is a single-cell marker store. Avoid overlapping Building/Site cells.
        /// Deterministic: desired first, then ring scan.
        /// </summary>
        private static bool TryPickValidTowerCell(GameServices s, CellPos desired, out CellPos finalCell)
        {
            finalCell = desired;

            if (s?.GridMap == null) return false;

            bool IsOk(CellPos c)
            {
                if (!s.GridMap.IsInside(c)) return false;
                var occ = s.GridMap.Get(c);

                // Avoid placing tower marker on occupied building/site cells.
                // Road/Empty is fine for gizmo marker.
                return occ.Kind != CellOccupancyKind.Building && occ.Kind != CellOccupancyKind.Site;
            }

            if (IsOk(desired)) { finalCell = desired; return true; }

            // Manhattan ring search, deterministic order
            const int maxR = 8;
            for (int r = 1; r <= maxR; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    int dx1 = r - Mathf.Abs(dy);
                    int dx2 = -dx1;

                    var c1 = new CellPos(desired.X + dx1, desired.Y + dy);
                    if (IsOk(c1)) { finalCell = c1; return true; }

                    if (dx2 != dx1)
                    {
                        var c2 = new CellPos(desired.X + dx2, desired.Y + dy);
                        if (IsOk(c2)) { finalCell = c2; return true; }
                    }
                }
            }

            return false;
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

        private static bool ValidateStartMapHeader(StartMapConfigRootDto cfg, out string error)
        {
            error = null;

            if (cfg.schemaVersion != 1)
            {
                error = $"StartMapConfig schemaVersion={cfg.schemaVersion} unsupported (expect 1).";
                return false;
            }

            if (cfg.coordSystem == null)
            {
                error = "StartMapConfig missing coordSystem.";
                return false;
            }

            // Hard checks theo file StartMapConfig_RunStart_64x64_v0.1.json
            if (!string.Equals(cfg.coordSystem.origin, "bottom-left", StringComparison.OrdinalIgnoreCase))
            {
                error = $"coordSystem.origin='{cfg.coordSystem.origin}' (expect 'bottom-left').";
                return false;
            }

            if (!string.Equals(cfg.coordSystem.indexing, "0-based", StringComparison.OrdinalIgnoreCase))
            {
                error = $"coordSystem.indexing='{cfg.coordSystem.indexing}' (expect '0-based').";
                return false;
            }

            // lockedInvariants bắt buộc có (VS3 hardening)
            if (cfg.lockedInvariants == null || cfg.lockedInvariants.Length == 0)
            {
                error = "StartMapConfig missing lockedInvariants (expect non-empty).";
                return false;
            }

            return true;
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
