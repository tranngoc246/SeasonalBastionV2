using System;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion.RunStart
{
    internal static class RunStartWorldBuilder
    {
        internal static bool ApplyWorld(GameServices s, StartMapConfigDto cfg, RunStartBuildContext ctx, out string error)
        {
            error = null;

            if (cfg.roads != null)
            {
                for (int i = 0; i < cfg.roads.Length; i++)
                {
                    var c = cfg.roads[i];
                    if (c == null) continue;

                    var roadCell = new CellPos(c.x, c.y);
                    if (s.TerrainMap != null && !TerrainRules.IsWalkableTerrain(s.TerrainMap.Get(roadCell)))
                    {
                        error = $"RunStart road cannot be placed on non-walkable terrain at ({roadCell.X},{roadCell.Y}).";
                        return false;
                    }

                    s.GridMap.SetRoad(roadCell, true);
                }
            }

            if (cfg.initialBuildings == null) return true;

            for (int i = 0; i < cfg.initialBuildings.Length; i++)
            {
                var b = cfg.initialBuildings[i];
                if (b == null || b.anchor == null || string.IsNullOrEmpty(b.defId)) continue;

                string defId = RunStartPlacementHelper.ResolveBuildingDefIdOrNull(s, b.defId);
                if (string.IsNullOrEmpty(defId))
                {
                    if (RunStartTowerInitializer.IsArrowTowerLike(b.defId))
                    {
                        var desired = new CellPos(b.anchor.x, b.anchor.y);
                        RunStartTowerInitializer.TryCreateArrowTowerStandalone(s, desired, b);
                        continue;
                    }

                    error = $"BuildingDef not found: {b.defId}";
                    return false;
                }

                var def = s.DataRegistry.GetBuilding(defId);
                int w = Math.Max(1, def.SizeX);
                int h = Math.Max(1, def.SizeY);

                var rot = RunStartPlacementHelper.ParseDir4(b.rotation);
                var desiredAnchor = new CellPos(b.anchor.x, b.anchor.y);

                if (!RunStartPlacementHelper.TryPickValidAnchor(s, defId, desiredAnchor, w, h, rot, out var finalAnchor))
                {
                    if (RunStartTowerInitializer.IsArrowTowerLike(defId)) continue;

                    error = $"RunStart: cannot place '{defId}' near anchor ({desiredAnchor.X},{desiredAnchor.Y})";
                    return false;
                }

                if (s.TerrainMap != null)
                {
                    for (int dy = 0; dy < h; dy++)
                    {
                        for (int dx = 0; dx < w; dx++)
                        {
                            var cell = new CellPos(finalAnchor.X + dx, finalAnchor.Y + dy);
                            if (!TerrainRules.IsBuildableTerrain(s.TerrainMap.Get(cell)))
                            {
                                error = $"RunStart building '{defId}' footprint hits non-buildable terrain at ({cell.X},{cell.Y}).";
                                return false;
                            }
                        }
                    }
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
                    st.MaxHP = hpMax;
                    st.HP = hpMax;
                }
                catch
                {
                    st.MaxHP = 1;
                    st.HP = 1;
                }

                var id = s.WorldState.Buildings.Create(st);
                st.Id = id;
                s.WorldState.Buildings.Set(id, st);

                for (int dy = 0; dy < h; dy++)
                    for (int dx = 0; dx < w; dx++)
                        s.GridMap.SetBuilding(new CellPos(st.Anchor.X + dx, st.Anchor.Y + dy), id);

                RunStartPlacementHelper.PromoteRunStartEntryRoads(s, st, w, h);

                try { s.WorldIndex?.OnBuildingCreated(id); } catch (Exception ex) { UnityEngine.Debug.LogError($"[RunStartWorldBuilder] Failed to register created building {id.Value} ({defId}) in WorldIndex: {ex}"); }

                if (!ctx.DefIdToBuildingId.ContainsKey(defId))
                    ctx.DefIdToBuildingId[defId] = id;

                if (RunStartTowerInitializer.IsArrowTowerLike(defId))
                    RunStartTowerInitializer.TryCreateArrowTowerState(s, st, b);
            }

            return true;
        }
    }
}
