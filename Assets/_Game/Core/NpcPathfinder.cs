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
    /// - Road is preferred over ground by lower move cost
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
            if (!IsWalkable(from) || !IsWalkable(target)) return false;

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
                    if (!IsWalkable(nextCell)) continue;

                    int stepCost = GetStepCost(nextCell);
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

        public bool TryEstimateCost(CellPos from, CellPos target, out int cost)
        {
            cost = 0;
            if (!TryFindPath(from, target, out var path)) return false;

            var cur = from;
            for (int i = 0; i < path.Count; i++)
            {
                var step = path[i];
                cost += GetStepCost(step);
                cur = step;
            }

            return true;
        }

        private bool IsWalkable(CellPos c)
        {
            if (!_grid.IsInside(c)) return false;

            var kind = _grid.Get(c).Kind;
            return kind == CellOccupancyKind.Empty || kind == CellOccupancyKind.Road;
        }

        private int GetStepCost(CellPos c)
        {
            return _grid.IsRoad(c) ? RoadCost : GroundCost;
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
    }
}
