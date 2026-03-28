using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// Weighted grid pathfinder for NPC movement.
    /// - 4-direction only
    /// - Traversable: Empty, Road
    /// - Blocked: Building, Site, OOB
    /// - Road-first rule: if a valid road backbone exists, path must use it.
    /// - Fallback mixed weighted path only when no usable road route exists.
    /// Deterministic by fixed neighbor order and tie-breaks.
    /// </summary>
    public sealed class NpcPathfinder
    {
        public const int RoadCost = 10;
        public const int GroundCost = 30;

        private readonly IGridMap _grid;

        // Fixed deterministic neighbor order: N, E, S, W
        private static readonly int[] _dx = { 0, 1, 0, -1 };
        private static readonly int[] _dy = { 1, 0, -1, 0 };

        public NpcPathfinder(IGridMap grid)
        {
            _grid = grid;
        }

        public bool TryFindPath(CellPos from, CellPos target, out List<CellPos> path)
        {
            path = null;

            if (_grid == null) return false;
            if (!_grid.IsInside(from) || !_grid.IsInside(target)) return false;
            if (!IsWalkableMixed(from) || !IsWalkableMixed(target)) return false;

            if (from.X == target.X && from.Y == target.Y)
            {
                path = new List<CellPos>(0);
                return true;
            }

            if (TryFindRoadFirstPath(from, target, out path))
                return true;

            return TryFindPathCore(from, target, out path, IsWalkableMixed, GetMixedStepCost);
        }

        public bool TryEstimateCost(CellPos from, CellPos target, out int cost)
        {
            cost = 0;
            if (!TryFindPath(from, target, out var path)) return false;

            for (int i = 0; i < path.Count; i++)
                cost += GetMixedStepCost(path[i]);

            return true;
        }

        private const int MaxRoadCandidatesPerSide = 8;

        private bool TryFindRoadFirstPath(CellPos from, CellPos target, out List<CellPos> path)
        {
            path = null;

            var roadCells = CollectRoadCells();
            if (roadCells.Count == 0)
                return false;

            var startEntries = CollectReachableRoadEntries(from, roadCells, MaxRoadCandidatesPerSide);
            if (startEntries.Count == 0)
                return false;

            var targetExits = CollectReachableRoadEntries(target, roadCells, MaxRoadCandidatesPerSide);
            if (targetExits.Count == 0)
                return false;

            PathCandidate best = default;
            bool found = false;

            for (int i = 0; i < startEntries.Count; i++)
            {
                var entry = startEntries[i];
                for (int j = 0; j < targetExits.Count; j++)
                {
                    var exit = targetExits[j];
                    if (!TryFindPathCore(entry.RoadCell, exit.RoadCell, out var backbone, IsWalkableRoadOnly, GetRoadOnlyStepCost))
                        continue;

                    int backboneCost = ComputeRoadOnlyPathCost(backbone);
                    int totalCost = entry.Cost + backboneCost + exit.Cost;
                    int totalGroundSteps = entry.GroundStepsBeforeRoad + exit.GroundStepsBeforeRoad;
                    var candidate = new PathCandidate(entry.RoadCell, exit.RoadCell, totalCost, totalGroundSteps, backbone);
                    if (!found || IsBetterCandidate(candidate, best))
                    {
                        best = candidate;
                        found = true;
                    }
                }
            }

            if (!found)
                return false;

            if (!TryFindPathCore(from, best.Entry, out var startLeg, IsWalkableMixed, GetMixedStepCost))
                return false;

            if (!TryFindPathCore(best.Exit, target, out var endLeg, IsWalkableMixed, GetMixedStepCost))
                return false;

            path = CombineSegments(startLeg, best.Backbone, endLeg);
            return path != null;
        }

        private bool TryFindPathCore(CellPos from, CellPos target, out List<CellPos> path, Func<CellPos, bool> isWalkable, Func<CellPos, int> getStepCost)
        {
            path = null;

            if (_grid == null) return false;
            if (!_grid.IsInside(from) || !_grid.IsInside(target)) return false;
            if (!isWalkable(from) || !isWalkable(target)) return false;

            if (from.X == target.X && from.Y == target.Y)
            {
                path = new List<CellPos>(0);
                return true;
            }

            int w = _grid.Width;
            int h = _grid.Height;
            int n = w * h;

            var gScore = new int[n];
            var fScore = new int[n];
            var cameFrom = new int[n];
            var open = new bool[n];
            var closed = new bool[n];
            var openList = new List<int>(64);

            for (int i = 0; i < n; i++)
            {
                gScore[i] = int.MaxValue;
                fScore[i] = int.MaxValue;
                cameFrom[i] = -1;
            }

            int start = Idx(from, w);
            int goal = Idx(target, w);

            gScore[start] = 0;
            fScore[start] = Heuristic(from, target);
            open[start] = true;
            openList.Add(start);

            while (openList.Count > 0)
            {
                int current = PopBestOpen(openList, open, fScore, gScore);
                if (current < 0) break;

                if (current == goal)
                {
                    path = ReconstructPath(cameFrom, current, start, w);
                    return true;
                }

                open[current] = false;
                closed[current] = true;

                int cx = current % w;
                int cy = current / w;

                for (int i = 0; i < 4; i++)
                {
                    int nx = cx + _dx[i];
                    int ny = cy + _dy[i];

                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;

                    int ni = ny * w + nx;
                    if (closed[ni]) continue;

                    var nextCell = new CellPos(nx, ny);
                    if (!isWalkable(nextCell)) continue;

                    int stepCost = getStepCost(nextCell);
                    if (stepCost <= 0) continue;

                    int tentativeG = gScore[current] + stepCost;
                    if (tentativeG >= gScore[ni]) continue;

                    cameFrom[ni] = current;
                    gScore[ni] = tentativeG;
                    fScore[ni] = tentativeG + Heuristic(nextCell, target);

                    if (!open[ni])
                    {
                        open[ni] = true;
                        openList.Add(ni);
                    }
                }
            }

            return false;
        }

        private List<RoadEntryCandidate> CollectReachableRoadEntries(CellPos origin, List<CellPos> roadCells, int maxCount)
        {
            var entries = new List<RoadEntryCandidate>(roadCells.Count);
            for (int i = 0; i < roadCells.Count; i++)
            {
                var roadCell = roadCells[i];
                if (!TryFindPathCore(origin, roadCell, out var leg, IsWalkableMixed, GetMixedStepCost))
                    continue;

                int cost = ComputePathCost(leg);
                int groundStepsBeforeRoad = CountGroundStepsBeforeFirstRoad(leg);
                entries.Add(new RoadEntryCandidate(roadCell, cost, groundStepsBeforeRoad));
            }

            entries.Sort(CompareRoadEntryCandidates);
            if (entries.Count > maxCount)
                entries.RemoveRange(maxCount, entries.Count - maxCount);
            return entries;
        }

        private List<CellPos> CollectRoadCells()
        {
            var roads = new List<CellPos>();
            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    var c = new CellPos(x, y);
                    if (_grid.IsRoad(c))
                        roads.Add(c);
                }
            }
            return roads;
        }

        private static int CompareRoadEntryCandidates(RoadEntryCandidate a, RoadEntryCandidate b)
        {
            int cmp = a.GroundStepsBeforeRoad.CompareTo(b.GroundStepsBeforeRoad);
            if (cmp != 0) return cmp;
            cmp = a.Cost.CompareTo(b.Cost);
            if (cmp != 0) return cmp;
            cmp = a.RoadCell.Y.CompareTo(b.RoadCell.Y);
            if (cmp != 0) return cmp;
            return a.RoadCell.X.CompareTo(b.RoadCell.X);
        }

        private int CountGroundStepsBeforeFirstRoad(List<CellPos> path)
        {
            int count = 0;
            for (int i = 0; i < path.Count; i++)
            {
                if (_grid.IsRoad(path[i]))
                    break;
                count++;
            }
            return count;
        }

        private bool IsBetterCandidate(PathCandidate a, PathCandidate b)
        {
            if (a.TotalGroundSteps != b.TotalGroundSteps)
                return a.TotalGroundSteps < b.TotalGroundSteps;

            if (a.TotalCost != b.TotalCost)
                return a.TotalCost < b.TotalCost;

            if (a.Entry.Y != b.Entry.Y)
                return a.Entry.Y < b.Entry.Y;
            if (a.Entry.X != b.Entry.X)
                return a.Entry.X < b.Entry.X;
            if (a.Exit.Y != b.Exit.Y)
                return a.Exit.Y < b.Exit.Y;
            return a.Exit.X < b.Exit.X;
        }

        private static List<CellPos> CombineSegments(List<CellPos> first, List<CellPos> second, List<CellPos> third)
        {
            if (first == null || second == null || third == null)
                return null;

            var combined = new List<CellPos>(first.Count + second.Count + third.Count);
            AppendUnique(combined, first);
            AppendUnique(combined, second);
            AppendUnique(combined, third);
            return combined;
        }

        private static void AppendUnique(List<CellPos> into, List<CellPos> segment)
        {
            for (int i = 0; i < segment.Count; i++)
            {
                var cell = segment[i];
                if (into.Count > 0)
                {
                    var last = into[into.Count - 1];
                    if (last.X == cell.X && last.Y == cell.Y)
                        continue;
                }

                into.Add(cell);
            }
        }

        private int ComputePathCost(List<CellPos> path)
        {
            int cost = 0;
            for (int i = 0; i < path.Count; i++)
                cost += GetMixedStepCost(path[i]);
            return cost;
        }

        private int ComputeRoadOnlyPathCost(List<CellPos> path)
        {
            int cost = 0;
            for (int i = 0; i < path.Count; i++)
                cost += RoadCost;
            return cost;
        }

        private bool IsWalkableMixed(CellPos c)
        {
            if (!_grid.IsInside(c)) return false;

            var kind = _grid.Get(c).Kind;
            return kind == CellOccupancyKind.Empty || kind == CellOccupancyKind.Road;
        }

        private bool IsWalkableRoadOnly(CellPos c)
        {
            return _grid.IsInside(c) && _grid.IsRoad(c);
        }

        private int GetMixedStepCost(CellPos c)
        {
            return _grid.IsRoad(c) ? RoadCost : GroundCost;
        }

        private int GetRoadOnlyStepCost(CellPos c)
        {
            return _grid.IsRoad(c) ? RoadCost : int.MaxValue;
        }

        private static int Idx(CellPos c, int w) => c.Y * w + c.X;

        private static int Heuristic(CellPos a, CellPos b)
        {
            int dx = a.X - b.X; if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
            return (dx + dy) * RoadCost;
        }

        private static int PopBestOpen(List<int> openList, bool[] open, int[] fScore, int[] gScore)
        {
            int bestIdx = -1;
            int bestNode = -1;
            int bestF = int.MaxValue;
            int bestG = int.MaxValue;

            for (int i = 0; i < openList.Count; i++)
            {
                int node = openList[i];
                if (!open[node]) continue;

                int f = fScore[node];
                int g = gScore[node];
                if (f < bestF || (f == bestF && g < bestG) || (f == bestF && g == bestG && node < bestNode))
                {
                    bestIdx = i;
                    bestNode = node;
                    bestF = f;
                    bestG = g;
                }
            }

            if (bestIdx >= 0)
                openList.RemoveAt(bestIdx);

            return bestNode;
        }

        private static List<CellPos> ReconstructPath(int[] cameFrom, int current, int start, int w)
        {
            var rev = new List<CellPos>(16);
            while (current != start)
            {
                int x = current % w;
                int y = current / w;
                rev.Add(new CellPos(x, y));
                current = cameFrom[current];
                if (current < 0) break;
            }

            rev.Reverse();
            return rev;
        }

        private readonly struct RoadEntryCandidate
        {
            public readonly CellPos RoadCell;
            public readonly int Cost;
            public readonly int GroundStepsBeforeRoad;

            public RoadEntryCandidate(CellPos roadCell, int cost, int groundStepsBeforeRoad)
            {
                RoadCell = roadCell;
                Cost = cost;
                GroundStepsBeforeRoad = groundStepsBeforeRoad;
            }
        }

        private readonly struct PathCandidate
        {
            public readonly CellPos Entry;
            public readonly CellPos Exit;
            public readonly int TotalCost;
            public readonly int TotalGroundSteps;
            public readonly List<CellPos> Backbone;

            public PathCandidate(CellPos entry, CellPos exit, int totalCost, int totalGroundSteps, List<CellPos> backbone)
            {
                Entry = entry;
                Exit = exit;
                TotalCost = totalCost;
                TotalGroundSteps = totalGroundSteps;
                Backbone = backbone;
            }
        }
    }
}
