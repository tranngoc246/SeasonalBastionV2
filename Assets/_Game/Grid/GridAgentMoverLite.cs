using SeasonalBastion.Contracts;
using System.Numerics;

namespace SeasonalBastion
{
    /// <summary>
    /// Day14: Simple deterministic mover.
    /// - Move 1 cell per Tick toward target using Manhattan path
    /// - Deterministic: X first then Y
    /// - Ignores obstacles (optional but recommended in Part27)
    /// </summary>
    public sealed class GridAgentMoverLite
    {
        private readonly IGridMap _grid;

        public GridAgentMoverLite(IGridMap grid) => _grid = grid;

        public bool StepToward(ref NpcState st, CellPos target)
        {
            var cur = st.Cell;
            if (cur.X == target.X && cur.Y == target.Y) return true;

            Vector2 next = new(cur.X, cur.Y);

            // Deterministic: horizontal then vertical
            if (cur.X != target.X) next.X += (target.X > cur.X) ? 1 : -1;
            else next.Y += (target.Y > cur.Y) ? 1 : -1;

            CellPos nextPos = new((int)next.X, (int)next.Y);

            // Clamp inside grid (avoid out-of-bounds)
            if (_grid != null && !_grid.IsInside(nextPos))
            {
                // Can't move outside. Treat as arrived=false (executor can decide fail/cancel)
                return false;
            }

            st.Cell = nextPos;
            return (next.X == target.X && next.Y == target.Y);
        }
    }
}
