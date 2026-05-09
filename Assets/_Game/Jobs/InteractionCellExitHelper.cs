using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public static class InteractionCellExitHelper
    {
        private struct PendingStepOff
        {
            public CellPos OriginCell;
            public CellPos WaitCell;
        }

        private static readonly Dictionary<int, PendingStepOff> _pendingByNpc = new();
        public static bool TryStepOffBuildingEntry(
            GameServices s,
            ref NpcState npcState,
            in BuildingState building,
            float dt,
            CellPos? preferredFrom = null)
        {
            return TryStepOffBuildingEntry(s?.DataRegistry, s?.GridMap, s?.AgentMover, ref npcState, building, dt, preferredFrom);
        }

        public static bool TryStepOffBuildingEntry(
            IDataRegistry dataRegistry,
            IGridMap gridMap,
            IAgentMoverRuntime agentMover,
            ref NpcState npcState,
            in BuildingState building,
            float dt,
            CellPos? preferredFrom = null)
        {
            if (dataRegistry == null || gridMap == null || agentMover == null)
                return false;

            var from = preferredFrom ?? npcState.Cell;
            var entry = EntryCellUtil.GetApproachCellForBuilding(dataRegistry, gridMap, building, from);
            return TryStepOffCell(gridMap, agentMover, ref npcState, entry, dt, from);
        }

        public static bool TryStepOffSiteEntry(
            GameServices s,
            ref NpcState npcState,
            in BuildSiteState site,
            float dt,
            CellPos? preferredFrom = null)
        {
            return TryStepOffSiteEntry(s?.DataRegistry, s?.GridMap, s?.AgentMover, ref npcState, site, dt, preferredFrom);
        }

        public static bool TryStepOffSiteEntry(
            IDataRegistry dataRegistry,
            IGridMap gridMap,
            IAgentMoverRuntime agentMover,
            ref NpcState npcState,
            in BuildSiteState site,
            float dt,
            CellPos? preferredFrom = null)
        {
            if (dataRegistry == null || gridMap == null || agentMover == null)
                return false;

            var from = preferredFrom ?? npcState.Cell;
            var entry = EntryCellUtil.GetApproachCellForSite(dataRegistry, gridMap, site, from);
            return TryStepOffCell(gridMap, agentMover, ref npcState, entry, dt, from);
        }

        public static bool TryStepOffCell(
            GameServices s,
            ref NpcState npcState,
            CellPos interactionCell,
            float dt,
            CellPos? preferredBias = null)
        {
            return TryStepOffCell(s?.GridMap, s?.AgentMover, ref npcState, interactionCell, dt, preferredBias);
        }

        public static bool TryStepOffCell(
            IGridMap gridMap,
            IAgentMoverRuntime agentMover,
            ref NpcState npcState,
            CellPos interactionCell,
            float dt,
            CellPos? preferredBias = null)
        {
            if (agentMover == null || gridMap == null)
                return false;

            if (npcState.Cell.X != interactionCell.X || npcState.Cell.Y != interactionCell.Y)
                return false;

            var bias = preferredBias ?? npcState.Cell;
            if (!TryFindNearbyWaitCell(gridMap, interactionCell, bias, out var waitCell))
                return false;

            _pendingByNpc[npcState.Id.Value] = new PendingStepOff
            {
                OriginCell = interactionCell,
                WaitCell = waitCell
            };
            return ContinuePendingStepOff(agentMover, gridMap, ref npcState, dt);
        }

        public static bool ContinuePendingStepOff(GameServices s, ref NpcState npcState, float dt)
        {
            return ContinuePendingStepOff(s?.AgentMover, s?.GridMap, ref npcState, dt);
        }

        public static bool ContinuePendingStepOff(IAgentMoverRuntime agentMover, IGridMap gridMap, ref NpcState npcState, float dt)
        {
            if (agentMover == null || gridMap == null)
                return false;

            if (!_pendingByNpc.TryGetValue(npcState.Id.Value, out var pending))
                return false;

            if (npcState.Cell.X != pending.OriginCell.X || npcState.Cell.Y != pending.OriginCell.Y)
            {
                _pendingByNpc.Remove(npcState.Id.Value);
                return false;
            }

            var waitCell = pending.WaitCell;
            if (!IsWalkableWaitCell(gridMap, waitCell))
            {
                if (!TryFindNearbyWaitCell(gridMap, pending.OriginCell, npcState.Cell, out waitCell))
                {
                    _pendingByNpc.Remove(npcState.Id.Value);
                    return false;
                }

                pending.WaitCell = waitCell;
                _pendingByNpc[npcState.Id.Value] = pending;
            }

            int stepDist = Manhattan(pending.OriginCell, waitCell);
            if (stepDist == 1)
            {
                npcState.Cell = waitCell;
                _pendingByNpc.Remove(npcState.Id.Value);
                return true;
            }

            bool arrived = agentMover.StepToward(ref npcState, waitCell, dt);
            if (npcState.Cell.X != pending.OriginCell.X || npcState.Cell.Y != pending.OriginCell.Y)
            {
                _pendingByNpc.Remove(npcState.Id.Value);
                return true;
            }

            if (arrived || (npcState.Cell.X == waitCell.X && npcState.Cell.Y == waitCell.Y))
            {
                _pendingByNpc.Remove(npcState.Id.Value);
                return true;
            }

            if (!TryFindNearbyWaitCell(gridMap, pending.OriginCell, npcState.Cell, out waitCell))
                return true;

            pending.WaitCell = waitCell;
            _pendingByNpc[npcState.Id.Value] = pending;
            return true;
        }

        public static bool HasPendingStepOff(NpcId npc)
        {
            return _pendingByNpc.ContainsKey(npc.Value);
        }

        public static void ClearPendingStepOff(NpcId npc)
        {
            _pendingByNpc.Remove(npc.Value);
        }

        private static bool TryFindNearbyWaitCell(
            GameServices s,
            CellPos interactionCell,
            CellPos biasFrom,
            out CellPos waitCell)
        {
            return TryFindNearbyWaitCell(s?.GridMap, interactionCell, biasFrom, out waitCell);
        }

        private static bool TryFindNearbyWaitCell(
            IGridMap gridMap,
            CellPos interactionCell,
            CellPos biasFrom,
            out CellPos waitCell)
        {
            waitCell = default;
            int bestScore = int.MaxValue;
            bool found = false;

            for (int r = 1; r <= 3; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    int ax = r - System.Math.Abs(dy);
                    int dx1 = -ax;
                    int dx2 = ax;

                    var c1 = new CellPos(interactionCell.X + dx1, interactionCell.Y + dy);
                    TryConsiderCandidate(gridMap, c1, interactionCell, biasFrom, ref found, ref bestScore, ref waitCell);

                    if (dx2 != dx1)
                    {
                        var c2 = new CellPos(interactionCell.X + dx2, interactionCell.Y + dy);
                        TryConsiderCandidate(gridMap, c2, interactionCell, biasFrom, ref found, ref bestScore, ref waitCell);
                    }
                }

                if (found)
                    return true;
            }

            return false;
        }

        private static void TryConsiderCandidate(
            IGridMap gridMap,
            CellPos candidate,
            CellPos interactionCell,
            CellPos biasFrom,
            ref bool found,
            ref int bestScore,
            ref CellPos best)
        {
            if (!IsWalkableWaitCell(gridMap, candidate))
                return;

            if (candidate.X == interactionCell.X && candidate.Y == interactionCell.Y)
                return;

            int score = Manhattan(candidate, biasFrom);
            if (!found || score < bestScore || (score == bestScore && CompareCell(candidate, best) < 0))
            {
                found = true;
                bestScore = score;
                best = candidate;
            }
        }

        private static bool IsWalkableWaitCell(GameServices s, CellPos cell)
        {
            return IsWalkableWaitCell(s?.GridMap, cell);
        }

        private static bool IsWalkableWaitCell(IGridMap gridMap, CellPos cell)
        {
            if (gridMap == null || !gridMap.IsInside(cell))
                return false;

            var kind = gridMap.Get(cell).Kind;
            return kind == CellOccupancyKind.Empty || kind == CellOccupancyKind.Road;
        }

        private static int Manhattan(CellPos a, CellPos b)
        {
            int dx = a.X - b.X; if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
            return dx + dy;
        }

        private static int CompareCell(CellPos a, CellPos b)
        {
            int cmp = a.Y.CompareTo(b.Y);
            if (cmp != 0) return cmp;
            return a.X.CompareTo(b.X);
        }
    }
}
