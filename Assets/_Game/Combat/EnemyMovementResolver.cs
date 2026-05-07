using System;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    internal sealed class EnemyMovementResolver
    {
        private readonly IGridMap _gridMap;
        private readonly int _localBfsRadius;

        private int _gridWidth;
        private int _gridHeight;
        private int _gridNodeCount;
        private int[] _bfsQueue;
        private int[] _bfsPrev;
        private int[] _bfsVisited;
        private int _visitToken = 1;

        public EnemyMovementResolver(IGridMap gridMap, int localBfsRadius)
        {
            _gridMap = gridMap;
            _localBfsRadius = localBfsRadius;
        }

        public void EnsureBfsBuffers(int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            if (_gridWidth == width && _gridHeight == height && _bfsQueue != null)
                return;

            _gridWidth = width;
            _gridHeight = height;
            _gridNodeCount = width * height;

            _bfsQueue = new int[_gridNodeCount];
            _bfsPrev = new int[_gridNodeCount];
            _bfsVisited = new int[_gridNodeCount];
            _visitToken = 1;
        }

        public bool TryFindNextStep(CellPos from, CellPos target, out CellPos next)
        {
            next = from;
            if (_gridMap == null)
                return false;

            int bestDistance = int.MaxValue;
            bool found = false;

            Span<CellPos> neighbors = stackalloc CellPos[4]
            {
                new CellPos(from.X, from.Y + 1),
                new CellPos(from.X + 1, from.Y),
                new CellPos(from.X, from.Y - 1),
                new CellPos(from.X - 1, from.Y),
            };

            for (int i = 0; i < 4; i++)
            {
                var cell = neighbors[i];
                if (!_gridMap.IsInside(cell))
                    continue;
                if (_gridMap.IsBlocked(cell))
                    continue;

                int distance = Manhattan(cell, target);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    next = cell;
                    found = true;
                }
            }

            if (found)
                return true;

            return TryBfsNextStep(from, target, out next);
        }

        public bool TryFallbackNextStep(CellPos from, CellPos target, Dir4 dirToHQ, out CellPos next)
        {
            next = from;
            if (_gridMap == null)
                return false;

            if (TryStepByDirPreference(from, dirToHQ, out next))
                return true;

            return TryLocalBfsEscape(from, target, _localBfsRadius, out next);
        }

        public static bool CellsEqual(CellPos a, CellPos b)
            => a.X == b.X && a.Y == b.Y;

        private bool TryBfsNextStep(CellPos from, CellPos target, out CellPos next)
        {
            next = from;
            if (_gridMap == null)
                return false;
            if (!_gridMap.IsInside(from) || !_gridMap.IsInside(target))
                return false;
            if (CellsEqual(from, target))
                return true;

            int start = ToIndex(from);
            int goal = ToIndex(target);

            _visitToken++;
            if (_visitToken == int.MaxValue)
            {
                Array.Clear(_bfsVisited, 0, _bfsVisited.Length);
                _visitToken = 1;
            }

            int queueHead = 0;
            int queueTail = 0;
            _bfsQueue[queueTail++] = start;
            _bfsVisited[start] = _visitToken;
            _bfsPrev[start] = -1;

            bool reached = false;

            while (queueHead < queueTail)
            {
                int current = _bfsQueue[queueHead++];
                if (current == goal)
                {
                    reached = true;
                    break;
                }

                var currentPos = FromIndex(current);
                Span<CellPos> neighbors = stackalloc CellPos[4]
                {
                    new CellPos(currentPos.X, currentPos.Y + 1),
                    new CellPos(currentPos.X + 1, currentPos.Y),
                    new CellPos(currentPos.X, currentPos.Y - 1),
                    new CellPos(currentPos.X - 1, currentPos.Y),
                };

                for (int i = 0; i < 4; i++)
                {
                    var cell = neighbors[i];
                    if (!_gridMap.IsInside(cell))
                        continue;
                    if (_gridMap.IsBlocked(cell))
                        continue;

                    int nextIndex = ToIndex(cell);
                    if (_bfsVisited[nextIndex] == _visitToken)
                        continue;

                    _bfsVisited[nextIndex] = _visitToken;
                    _bfsPrev[nextIndex] = current;
                    _bfsQueue[queueTail++] = nextIndex;

                    if (nextIndex == goal)
                    {
                        reached = true;
                        queueHead = queueTail;
                        break;
                    }
                }
            }

            if (!reached)
                return false;

            int step = goal;
            int previous = _bfsPrev[step];
            if (previous < 0)
                return false;

            while (previous != start && previous >= 0)
            {
                step = previous;
                previous = _bfsPrev[step];
            }

            next = FromIndex(step);
            return true;
        }

        private bool TryStepByDirPreference(CellPos from, Dir4 dirToHQ, out CellPos next)
        {
            next = from;
            if (_gridMap == null)
                return false;

            Span<Dir4> order = stackalloc Dir4[4]
            {
                dirToHQ,
                DirLeft(dirToHQ),
                DirRight(dirToHQ),
                DirOpposite(dirToHQ),
            };

            for (int i = 0; i < order.Length; i++)
            {
                var cell = Step(from, order[i]);
                if (!_gridMap.IsInside(cell))
                    continue;
                if (_gridMap.IsBlocked(cell))
                    continue;

                next = cell;
                return true;
            }

            return false;
        }

        private bool TryLocalBfsEscape(CellPos from, CellPos target, int radius, out CellPos next)
        {
            next = from;
            if (_gridMap == null)
                return false;

            int side = radius * 2 + 1;
            int maxNodes = side * side;
            int minX = from.X - radius;
            int minY = from.Y - radius;

            Span<byte> visited = stackalloc byte[maxNodes];
            Span<CellPos> nodes = stackalloc CellPos[maxNodes];
            Span<int> prev = stackalloc int[maxNodes];
            Span<byte> depth = stackalloc byte[maxNodes];
            Span<int> queue = stackalloc int[maxNodes];

            int nodeCount = 0;
            int queueHead = 0;
            int queueTail = 0;

            int ToLocalIndex(CellPos cell) => (cell.X - minX) + (cell.Y - minY) * side;

            nodes[nodeCount] = from;
            prev[nodeCount] = -1;
            depth[nodeCount] = 0;

            int startLocalIndex = ToLocalIndex(from);
            if (startLocalIndex < 0 || startLocalIndex >= maxNodes)
                return false;

            visited[startLocalIndex] = 1;
            queue[queueTail++] = nodeCount;
            nodeCount++;

            int bestNode = 0;
            int bestDistance = Manhattan(from, target);

            while (queueHead < queueTail)
            {
                int currentNode = queue[queueHead++];
                var current = nodes[currentNode];
                byte currentDepth = depth[currentNode];
                if (currentDepth >= radius)
                    continue;

                Span<CellPos> neighbors = stackalloc CellPos[4]
                {
                    new CellPos(current.X, current.Y + 1),
                    new CellPos(current.X + 1, current.Y),
                    new CellPos(current.X, current.Y - 1),
                    new CellPos(current.X - 1, current.Y),
                };

                for (int i = 0; i < 4; i++)
                {
                    var cell = neighbors[i];
                    if (!_gridMap.IsInside(cell))
                        continue;
                    if (_gridMap.IsBlocked(cell))
                        continue;

                    int localIndex = ToLocalIndex(cell);
                    if (localIndex < 0 || localIndex >= maxNodes)
                        continue;
                    if (visited[localIndex] != 0)
                        continue;

                    visited[localIndex] = 1;
                    if (nodeCount >= maxNodes)
                        continue;

                    nodes[nodeCount] = cell;
                    prev[nodeCount] = currentNode;
                    depth[nodeCount] = (byte)(currentDepth + 1);

                    int distance = Manhattan(cell, target);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestNode = nodeCount;
                    }

                    queue[queueTail++] = nodeCount;
                    nodeCount++;
                    if (nodeCount >= maxNodes)
                        break;
                }

                if (nodeCount >= maxNodes)
                    break;
            }

            if (bestNode == 0)
                return false;

            int step = bestNode;
            int previous = prev[step];
            while (previous > 0)
            {
                step = previous;
                previous = prev[step];
            }

            next = nodes[step];
            return !CellsEqual(next, from);
        }

        private int ToIndex(CellPos cell)
            => cell.Y * _gridWidth + cell.X;

        private CellPos FromIndex(int index)
        {
            int x = index % _gridWidth;
            int y = index / _gridWidth;
            return new CellPos(x, y);
        }

        private static int Manhattan(CellPos a, CellPos b)
        {
            int dx = a.X - b.X;
            if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y;
            if (dy < 0) dy = -dy;
            return dx + dy;
        }

        private static CellPos Step(CellPos cell, Dir4 dir)
        {
            return dir switch
            {
                Dir4.N => new CellPos(cell.X, cell.Y + 1),
                Dir4.E => new CellPos(cell.X + 1, cell.Y),
                Dir4.S => new CellPos(cell.X, cell.Y - 1),
                Dir4.W => new CellPos(cell.X - 1, cell.Y),
                _ => cell,
            };
        }

        private static Dir4 DirLeft(Dir4 dir) => dir switch
        {
            Dir4.N => Dir4.W,
            Dir4.E => Dir4.N,
            Dir4.S => Dir4.E,
            Dir4.W => Dir4.S,
            _ => Dir4.N,
        };

        private static Dir4 DirRight(Dir4 dir) => dir switch
        {
            Dir4.N => Dir4.E,
            Dir4.E => Dir4.S,
            Dir4.S => Dir4.W,
            Dir4.W => Dir4.N,
            _ => Dir4.S,
        };

        private static Dir4 DirOpposite(Dir4 dir) => dir switch
        {
            Dir4.N => Dir4.S,
            Dir4.E => Dir4.W,
            Dir4.S => Dir4.N,
            Dir4.W => Dir4.E,
            _ => Dir4.S,
        };
    }
}
