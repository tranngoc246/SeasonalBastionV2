using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion.RunStart
{
    internal enum RunStartIssueSeverity { Error, Warning }

    internal readonly struct RunStartValidationIssue
    {
        public readonly RunStartIssueSeverity Severity;
        public readonly string Code;
        public readonly string Message;

        public RunStartValidationIssue(RunStartIssueSeverity sev, string code, string msg)
        {
            Severity = sev;
            Code = code ?? "";
            Message = msg ?? "";
        }

        public override string ToString() => $"{Code}: {Message}";
    }

    internal static class RunStartValidator
    {
        // Main entry used by RunStart facade and Debug HUD.
        internal static void CollectRuntimeIssues(GameServices s, List<RunStartValidationIssue> issues)
        {
            if (issues == null) return;
            if (s == null)
            {
                issues.Add(new RunStartValidationIssue(RunStartIssueSeverity.Error, "NULL_SERVICES", "GameServices is null."));
                return;
            }

            if (s.GridMap == null || s.WorldState == null || s.DataRegistry == null)
            {
                issues.Add(new RunStartValidationIssue(RunStartIssueSeverity.Error, "MISSING_DEPS", "Missing GridMap / WorldState / DataRegistry."));
                return;
            }

            // 1) Road graph + components
            BuildRoadComponents(s.GridMap, out var compId, out int roadCount, out int visited, out CellPos firstRoad);
            if (roadCount <= 0)
            {
                issues.Add(new RunStartValidationIssue(RunStartIssueSeverity.Error, "ROAD_NONE", "No road cells found."));
            }
            else if (visited != roadCount)
            {
                issues.Add(new RunStartValidationIssue(
                    RunStartIssueSeverity.Error,
                    "ROAD_DISCONNECTED",
                    $"Road graph disconnected: visited {visited}/{roadCount} from start ({firstRoad.X},{firstRoad.Y})."));
            }

            // Determine reference component for "connected" checks
            int refComp = -1;
            if (roadCount > 0 && s.GridMap.IsInside(firstRoad))
                refComp = compId[firstRoad.Y * s.GridMap.Width + firstRoad.X];

            // 2) Building gap >= 1 cell (8-neighborhood)
            ValidateBuildingGap8(s, issues);

            // 3) Driveway / entry rule (based on RunStartApplier.PromoteRunStartEntryRoads logic)
            int hqEntryComp = ValidateBuildingEntriesAndGetHQEntryComp(s, compId, issues);
            if (hqEntryComp >= 0) refComp = hqEntryComp;

            // 4) Spawn gates: in-bounds + road + connected
            ValidateSpawnGates(s, compId, refComp, issues);
        }

        internal static bool ContainsErrors(List<RunStartValidationIssue> issues)
        {
            if (issues == null) return false;
            for (int i = 0; i < issues.Count; i++)
                if (issues[i].Severity == RunStartIssueSeverity.Error) return true;
            return false;
        }

        internal static string BuildSummary(List<RunStartValidationIssue> issues, int maxLines = 8)
        {
            if (issues == null || issues.Count == 0) return "RunStart validation: OK";
            int shown = 0;
            var sb = new System.Text.StringBuilder(256);

            // header
            int err = 0, warn = 0;
            for (int i = 0; i < issues.Count; i++)
                if (issues[i].Severity == RunStartIssueSeverity.Error) err++; else warn++;

            sb.Append($"RunStart INVALID: {err} error(s), {warn} warning(s).");

            for (int i = 0; i < issues.Count && shown < maxLines; i++)
            {
                if (issues[i].Severity != RunStartIssueSeverity.Error) continue;
                sb.Append("\n- ").Append(issues[i].ToString());
                shown++;
            }

            if (err > shown) sb.Append($"\n... and {err - shown} more error(s).");
            return sb.ToString();
        }

        // -----------------------------
        // Checks
        // -----------------------------

        private static void ValidateSpawnGates(GameServices s, int[] compId, int refComp, List<RunStartValidationIssue> issues)
        {
            var rt = s.RunStartRuntime;
            if (rt == null || rt.SpawnGates == null || rt.SpawnGates.Count == 0)
            {
                issues.Add(new RunStartValidationIssue(RunStartIssueSeverity.Warning, "GATE_NONE", "No spawn gates found in RunStartRuntime."));
                return;
            }

            for (int i = 0; i < rt.SpawnGates.Count; i++)
            {
                var g = rt.SpawnGates[i];
                var c = g.Cell;

                if (!s.GridMap.IsInside(c))
                {
                    issues.Add(new RunStartValidationIssue(RunStartIssueSeverity.Error, "GATE_OOB",
                        $"SpawnGate lane={g.Lane} out of bounds at ({c.X},{c.Y})."));
                    continue;
                }

                if (!s.GridMap.IsRoad(c))
                {
                    issues.Add(new RunStartValidationIssue(RunStartIssueSeverity.Error, "GATE_NOT_ROAD",
                        $"SpawnGate lane={g.Lane} cell ({c.X},{c.Y}) is not a road."));
                    continue;
                }

                if (refComp >= 0)
                {
                    int idx = c.Y * s.GridMap.Width + c.X;
                    int cc = compId[idx];
                    if (cc != refComp)
                    {
                        issues.Add(new RunStartValidationIssue(RunStartIssueSeverity.Error, "GATE_NOT_CONNECTED",
                            $"SpawnGate lane={g.Lane} at ({c.X},{c.Y}) not connected to main road component."));
                    }
                }
            }
        }

        private static void ValidateBuildingGap8(GameServices s, List<RunStartValidationIssue> issues)
        {
            int w = s.GridMap.Width;
            int h = s.GridMap.Height;

            // Check adjacency in 8-neighborhood by scanning occupancy buffer (no allocations).
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var o = s.GridMap.Get(new CellPos(x, y));
                    if (o.Kind != CellOccupancyKind.Building) continue;

                    var id = o.Building;
                    // 8 neighbors
                    for (int ny = y - 1; ny <= y + 1; ny++)
                    {
                        for (int nx = x - 1; nx <= x + 1; nx++)
                        {
                            if (nx == x && ny == y) continue;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;

                            var o2 = s.GridMap.Get(new CellPos(nx, ny));
                            if (o2.Kind != CellOccupancyKind.Building) continue;

                            if (!o2.Building.Equals(id))
                            {
                                issues.Add(new RunStartValidationIssue(
                                    RunStartIssueSeverity.Error,
                                    "BUILDING_GAP_8",
                                    $"Buildings too close (need gap>=1 cell 8-neighborhood): cell ({x},{y}) adjacent to ({nx},{ny})."));
                                return; // fail-fast: one clear error is enough
                            }
                        }
                    }
                }
            }
        }

        private const string HQ_DEFID = "bld_hq_t1";

        private static int ValidateBuildingEntriesAndGetHQEntryComp(GameServices s, int[] compId, List<RunStartValidationIssue> issues)
        {
            int w = s.GridMap.Width;
            int h = s.GridMap.Height;

            int hqEntryComp = -1;

            foreach (var bid in s.WorldState.Buildings.Ids)
            {
                var b = s.WorldState.Buildings.Get(bid);
                if (string.IsNullOrEmpty(b.DefId) || !b.IsConstructed) continue;

                BuildingDef def = null;
                try { def = s.DataRegistry.GetBuilding(b.DefId); }
                catch
                {
                    issues.Add(new RunStartValidationIssue(RunStartIssueSeverity.Error, "BUILD_DEF_MISSING",
                        $"BuildingDef missing for '{b.DefId}' (buildingId={bid.Value})."));
                    continue;
                }

                int sx = Mathf.Max(1, def.SizeX);
                int sy = Mathf.Max(1, def.SizeY);

                if (string.Equals(b.DefId, HQ_DEFID, StringComparison.OrdinalIgnoreCase))
                {
                    // HQ expects 4 entry roads
                    var eN = ComputeEntryOutsideFootprint(b.Anchor, sx, sy, Dir4.N);
                    var eS = ComputeEntryOutsideFootprint(b.Anchor, sx, sy, Dir4.S);
                    var eE = ComputeEntryOutsideFootprint(b.Anchor, sx, sy, Dir4.E);
                    var eW = ComputeEntryOutsideFootprint(b.Anchor, sx, sy, Dir4.W);

                    CheckEntryCell(s, compId, eN, "HQ_N", issues, ref hqEntryComp);
                    CheckEntryCell(s, compId, eS, "HQ_S", issues, ref hqEntryComp);
                    CheckEntryCell(s, compId, eE, "HQ_E", issues, ref hqEntryComp);
                    CheckEntryCell(s, compId, eW, "HQ_W", issues, ref hqEntryComp);
                }
                else
                {
                    // Normal building: single entry based on rotation
                    var entry = ComputeEntryOutsideFootprint(b.Anchor, sx, sy, b.Rotation);
                    CheckEntryCell(s, compId, entry, b.DefId, issues, ref hqEntryComp, isHQ: false);
                }
            }

            return hqEntryComp;
        }

        private static void CheckEntryCell(GameServices s, int[] compId, CellPos entry, string tag, List<RunStartValidationIssue> issues, ref int hqEntryComp, bool isHQ = true)
        {
            if (!s.GridMap.IsInside(entry))
            {
                issues.Add(new RunStartValidationIssue(RunStartIssueSeverity.Error, "ENTRY_OOB",
                    $"Entry/driveway cell out of bounds for '{tag}' at ({entry.X},{entry.Y})."));
                return;
            }

            if (!s.GridMap.IsRoad(entry))
            {
                issues.Add(new RunStartValidationIssue(RunStartIssueSeverity.Error, "ENTRY_NOT_ROAD",
                    $"Entry/driveway cell is not a road for '{tag}' at ({entry.X},{entry.Y})."));
                return;
            }

            // Capture a reference component (prefer HQ entry comp)
            int idx = entry.Y * s.GridMap.Width + entry.X;
            int cc = compId[idx];
            if (isHQ && hqEntryComp < 0) hqEntryComp = cc;
        }

        // Same logic as RunStartApplier.ComputeEntryOutsideFootprint (private there).
        private static CellPos ComputeEntryOutsideFootprint(CellPos anchor, int w, int h, Dir4 rot)
        {
            int cx = w / 2;
            int cy = h / 2;

            return rot switch
            {
                Dir4.N => new CellPos(anchor.X + cx, anchor.Y + h),
                Dir4.S => new CellPos(anchor.X + cx, anchor.Y - 1),
                Dir4.E => new CellPos(anchor.X + w, anchor.Y + cy),
                Dir4.W => new CellPos(anchor.X - 1, anchor.Y + cy),
                _ => new CellPos(anchor.X + cx, anchor.Y + h),
            };
        }

        // Build components for road cells using 4-neighborhood.
        private static void BuildRoadComponents(IGridMap grid, out int[] compId, out int roadCount, out int visited, out CellPos firstRoad)
        {
            int w = grid.Width, h = grid.Height;
            int n = w * h;

            compId = new int[n];
            for (int i = 0; i < n; i++) compId[i] = -1;

            roadCount = 0;
            firstRoad = default;

            // find first road and count roads
            bool found = false;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (grid.IsRoad(new CellPos(x, y)))
                    {
                        roadCount++;
                        if (!found)
                        {
                            found = true;
                            firstRoad = new CellPos(x, y);
                        }
                    }
                }
            }

            if (!found)
            {
                visited = 0;
                return;
            }

            // BFS from firstRoad (single component reachability check)
            var q = new int[n];
            int head = 0, tail = 0;

            int startIdx = firstRoad.Y * w + firstRoad.X;
            compId[startIdx] = 0;
            q[tail++] = startIdx;

            visited = 0;

            while (head < tail)
            {
                int idx = q[head++];
                visited++;

                int x = idx % w;
                int y = idx / w;

                // neighbor: (x+1, y)
                int nx = x + 1, ny = y;
                if (nx >= 0 && ny >= 0 && nx < w && ny < h)
                {
                    var c = new CellPos(nx, ny);
                    if (grid.IsRoad(c))
                    {
                        int nidx = ny * w + nx;
                        if (compId[nidx] < 0)
                        {
                            compId[nidx] = 0;
                            q[tail++] = nidx;
                        }
                    }
                }

                // neighbor: (x-1, y)
                nx = x - 1; ny = y;
                if (nx >= 0 && ny >= 0 && nx < w && ny < h)
                {
                    var c = new CellPos(nx, ny);
                    if (grid.IsRoad(c))
                    {
                        int nidx = ny * w + nx;
                        if (compId[nidx] < 0)
                        {
                            compId[nidx] = 0;
                            q[tail++] = nidx;
                        }
                    }
                }

                // neighbor: (x, y+1)
                nx = x; ny = y + 1;
                if (nx >= 0 && ny >= 0 && nx < w && ny < h)
                {
                    var c = new CellPos(nx, ny);
                    if (grid.IsRoad(c))
                    {
                        int nidx = ny * w + nx;
                        if (compId[nidx] < 0)
                        {
                            compId[nidx] = 0;
                            q[tail++] = nidx;
                        }
                    }
                }

                // neighbor: (x, y-1)
                nx = x; ny = y - 1;
                if (nx >= 0 && ny >= 0 && nx < w && ny < h)
                {
                    var c = new CellPos(nx, ny);
                    if (grid.IsRoad(c))
                    {
                        int nidx = ny * w + nx;
                        if (compId[nidx] < 0)
                        {
                            compId[nidx] = 0;
                            q[tail++] = nidx;
                        }
                    }
                }
            }
        }

    }
}
