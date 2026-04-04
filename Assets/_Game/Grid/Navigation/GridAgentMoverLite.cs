using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// Deterministic NPC mover with cached weighted paths.
    /// - Follows weighted paths from NpcPathfinder instead of Manhattan X-then-Y.
    /// - Repaths lazily when target changes, roads change, or path becomes invalid.
    /// - Traversal preference:
    ///   - Road: preferred (lower cost)
    ///   - Empty ground: fallback
    ///   - Building/Site/OOB: blocked
    /// - Stop reservation:
    ///   - Moving NPCs do not block each other.
    ///   - Final target stop cell is unique; if occupied by another NPC, wait/retry.
    /// </summary>
    public sealed class GridAgentMoverLite : IAgentMoverRuntime
    {
        private sealed class RouteState
        {
            public CellPos Target;
            public List<CellPos> Path;
            public int PathIndex;
            public int RoadsVersion;
            public CellPos LastCell;
            public int NoProgressTicks;
            public CellPos? WaitCell;
        }

        private readonly IGridMap _grid;
        private readonly IDataRegistry _data;
        private readonly BalanceService _bal;
        private readonly NpcPathfinder _pathfinder;

        private readonly float _fallbackBase;
        private readonly float _fallbackRoadMul;

        private readonly Dictionary<int, float> _accum = new();
        private readonly Dictionary<int, RouteState> _routes = new();
        private readonly Dictionary<int, int> _reservedStopOwnerByPackedCell = new();
        private readonly Dictionary<int, CellPos> _reservedStopByNpc = new();
        private int _roadsVersion;

        public GridAgentMoverLite(IGridMap grid, IDataRegistry data, BalanceService bal)
        {
            _grid = grid;
            _data = data;
            _bal = bal;
            _pathfinder = new NpcPathfinder(grid);

            _fallbackBase = bal != null ? bal.DefaultMoveSpeed : 1f;
            _fallbackRoadMul = bal != null ? bal.DefaultRoadMult : 1.3f;
        }

        /// <returns>true when arrived at target (after possible steps)</returns>
        public bool StepToward(ref NpcState st, CellPos target, float dt)
        {
            var cur = st.Cell;
            int key = st.Id.Value;

            if (cur.X == target.X && cur.Y == target.Y)
            {
                return TryAcquireOrConfirmStopCell(key, target);
            }

            if (_grid == null || !_grid.IsInside(cur) || !_grid.IsInside(target))
                return false;

            ReleaseStopIfNpcMovedAway(key, cur);

            if (!_accum.TryGetValue(key, out var a)) a = 0f;

            float baseSpd = _fallbackBase;
            float roadMul = _fallbackRoadMul;

            if (_data != null && !string.IsNullOrEmpty(st.DefId))
            {
                try
                {
                    var def = _data.GetNpc(st.DefId);
                    if (def != null)
                    {
                        if (def.BaseMoveSpeed > 0f) baseSpd = def.BaseMoveSpeed;
                        if (def.RoadSpeedMultiplier > 0f) roadMul = def.RoadSpeedMultiplier;
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[GridAgentMoverLite] Failed to resolve NPC move stats for '{st.DefId}': {ex}");
                }
            }

            bool onRoad = _grid.IsRoad(st.Cell);
            float rewardMoveMult = _bal != null ? _bal.RewardNpcMoveSpeedMultiplier : 1f;

            float spd = (onRoad ? (baseSpd * roadMul) : baseSpd) * rewardMoveMult;

            a += dt * spd;

            int steps = (int)a;
            if (steps <= 0)
            {
                _accum[key] = a;
                return false;
            }

            a -= steps;
            _accum[key] = a;

            if (steps > 4) steps = 4;

            var route = GetOrCreateRoute(key);
            if (!EnsureValidRoute(route, st.Cell, target))
                return false;

            for (int i = 0; i < steps; i++)
            {
                if (!EnsureValidRoute(route, st.Cell, target))
                    return false;

                bool isFinalStep = route.Path != null
                    && route.PathIndex >= 0
                    && route.PathIndex == route.Path.Count - 1;

                if (isFinalStep)
                {
                    var finalCell = route.Path[route.PathIndex];
                    if (!TryAcquireOrConfirmStopCell(key, finalCell))
                    {
                        if (TryMoveToNearbyWaitCell(ref st, route, target, finalCell))
                            continue;
                        return false;
                    }
                }

                if (!StepOnePathCell(ref st, route))
                {
                    route.NoProgressTicks++;
                    if (route.NoProgressTicks >= 2)
                    {
                        InvalidateRoute(route, target);
                        if (!EnsureValidRoute(route, st.Cell, target))
                            return false;

                        bool isFinalRetryStep = route.Path != null
                            && route.PathIndex >= 0
                            && route.PathIndex == route.Path.Count - 1;

                        if (isFinalRetryStep)
                        {
                            var finalRetryCell = route.Path[route.PathIndex];
                            if (!TryAcquireOrConfirmStopCell(key, finalRetryCell))
                            {
                                if (TryMoveToNearbyWaitCell(ref st, route, target, finalRetryCell))
                                    continue;
                                return false;
                            }
                        }

                        if (!StepOnePathCell(ref st, route))
                            return false;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    route.NoProgressTicks = 0;
                    route.LastCell = st.Cell;
                }

                if (st.Cell.X == target.X && st.Cell.Y == target.Y)
                    return TryAcquireOrConfirmStopCell(key, target);
            }

            return st.Cell.X == target.X && st.Cell.Y == target.Y && TryAcquireOrConfirmStopCell(key, target);
        }

        public void NotifyRoadsDirty()
        {
            _roadsVersion++;
        }

        public void ClearAll()
        {
            _accum.Clear();
            _routes.Clear();
            _reservedStopByNpc.Clear();
            _reservedStopOwnerByPackedCell.Clear();
            _roadsVersion = 0;
        }

        private RouteState GetOrCreateRoute(int key)
        {
            if (!_routes.TryGetValue(key, out var route))
            {
                route = new RouteState();
                _routes[key] = route;
            }

            return route;
        }

        private bool EnsureValidRoute(RouteState route, CellPos from, CellPos target)
        {
            if (NeedsRepath(route, from, target))
            {
                if (!_pathfinder.TryFindPath(from, target, out var path))
                    return false;

                route.Target = target;
                route.Path = path;
                route.PathIndex = 0;
                route.RoadsVersion = _roadsVersion;
                route.LastCell = from;
                route.NoProgressTicks = 0;
                route.WaitCell = null;
            }

            return true;
        }

        private bool NeedsRepath(RouteState route, CellPos from, CellPos target)
        {
            if (route.Path == null) return true;
            if (route.RoadsVersion != _roadsVersion) return true;
            if (route.Target.X != target.X || route.Target.Y != target.Y) return true;

            if (route.PathIndex < 0 || route.PathIndex > route.Path.Count)
                return true;

            if (route.PathIndex < route.Path.Count)
            {
                var next = route.Path[route.PathIndex];
                if (!_grid.IsInside(next) || _grid.IsBlocked(next))
                    return true;
            }

            if (from.X == target.X && from.Y == target.Y)
                return false;

            if (route.PathIndex >= route.Path.Count)
                return !(from.X == target.X && from.Y == target.Y);

            if (route.LastCell.X != from.X || route.LastCell.Y != from.Y)
            {
                bool matchedPrev = false;
                if (route.PathIndex > 0)
                {
                    var prev = route.Path[route.PathIndex - 1];
                    matchedPrev = prev.X == from.X && prev.Y == from.Y;
                }

                bool matchedCur = false;
                if (route.PathIndex < route.Path.Count)
                {
                    var cur = route.Path[route.PathIndex];
                    matchedCur = cur.X == from.X && cur.Y == from.Y;
                }

                if (!matchedPrev && !matchedCur)
                    return true;
            }

            return false;
        }

        private bool StepOnePathCell(ref NpcState st, RouteState route)
        {
            if (route.Path == null) return false;
            if (route.PathIndex < 0 || route.PathIndex >= route.Path.Count) return false;

            var next = route.Path[route.PathIndex];
            if (!_grid.IsInside(next) || _grid.IsBlocked(next))
                return false;

            st.Cell = next;
            route.PathIndex++;
            return true;
        }

        private bool TryAcquireOrConfirmStopCell(int npcId, CellPos cell)
        {
            int packed = PackCell(cell);

            if (_reservedStopByNpc.TryGetValue(npcId, out var existing))
            {
                if (existing.X == cell.X && existing.Y == cell.Y)
                {
                    return _reservedStopOwnerByPackedCell.TryGetValue(packed, out var owner) && owner == npcId;
                }

                ReleaseStopCell(npcId);
            }

            if (_reservedStopOwnerByPackedCell.TryGetValue(packed, out var heldBy) && heldBy != npcId)
                return false;

            _reservedStopOwnerByPackedCell[packed] = npcId;
            _reservedStopByNpc[npcId] = cell;
            return true;
        }

        private void ReleaseStopIfNpcMovedAway(int npcId, CellPos currentCell)
        {
            if (_reservedStopByNpc.TryGetValue(npcId, out var reserved))
            {
                if (reserved.X != currentCell.X || reserved.Y != currentCell.Y)
                    ReleaseStopCell(npcId);
            }
        }

        private void ReleaseStopCell(int npcId)
        {
            if (_reservedStopByNpc.TryGetValue(npcId, out var cell))
            {
                int packed = PackCell(cell);
                if (_reservedStopOwnerByPackedCell.TryGetValue(packed, out var owner) && owner == npcId)
                    _reservedStopOwnerByPackedCell.Remove(packed);
                _reservedStopByNpc.Remove(npcId);
            }
        }

        private int PackCell(CellPos c)
        {
            return c.Y * _grid.Width + c.X;
        }

        private bool TryMoveToNearbyWaitCell(ref NpcState st, RouteState route, CellPos target, CellPos blockedFinalCell)
        {
            var waitCell = FindNearbyWaitCell(st.Cell, target, blockedFinalCell);
            if (!waitCell.HasValue)
                return false;

            if (!TryBuildWaitRoute(st.Cell, waitCell.Value, route))
                return false;

            if (!StepOnePathCell(ref st, route))
                return false;

            route.NoProgressTicks = 0;
            route.LastCell = st.Cell;
            route.WaitCell = waitCell;
            return true;
        }

        private bool TryBuildWaitRoute(CellPos from, CellPos waitCell, RouteState route)
        {
            if (!_pathfinder.TryFindPath(from, waitCell, out var waitPath))
                return false;

            route.Target = waitCell;
            route.Path = waitPath;
            route.PathIndex = 0;
            route.RoadsVersion = _roadsVersion;
            route.LastCell = from;
            route.NoProgressTicks = 0;
            route.WaitCell = waitCell;
            return true;
        }

        private CellPos? FindNearbyWaitCell(CellPos from, CellPos target, CellPos blockedFinalCell)
        {
            CellPos? best = null;
            int bestScore = int.MaxValue;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if ((dx == 0 && dy == 0) || (dx != 0 && dy != 0))
                        continue;

                    var c = new CellPos(target.X + dx, target.Y + dy);
                    if (!_grid.IsInside(c) || _grid.IsBlocked(c))
                        continue;

                    if (c.X == blockedFinalCell.X && c.Y == blockedFinalCell.Y)
                        continue;

                    int score = Manhattan(c, from);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = c;
                    }
                }
            }

            return best;
        }

        private static int Manhattan(CellPos a, CellPos b)
        {
            int dx = a.X - b.X; if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
            return dx + dy;
        }

        private static void InvalidateRoute(RouteState route, CellPos target)
        {
            route.Target = target;
            route.Path = null;
            route.PathIndex = 0;
            route.WaitCell = null;
        }
    }
}
