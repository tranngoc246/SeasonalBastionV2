using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// PART27 Day14: Simple deterministic mover.
    /// - Move 1 cell per Tick toward target (Manhattan)
    /// - Deterministic: X first then Y
    /// - Ignores obstacles (v0.1)
    /// </summary>
    public sealed class GridAgentMoverLite
    {
        private readonly IGridMap _grid;

        public GridAgentMoverLite(IGridMap grid)
        {
            _grid = grid;
        }

        /// <returns>true when arrived at target after the step</returns>
        public bool StepToward(ref NpcState st, CellPos target)
        {
            var cur = st.Cell;
            if (cur.X == target.X && cur.Y == target.Y)
                return true;

            int nx = cur.X;
            int ny = cur.Y;

            // Deterministic: horizontal then vertical
            if (cur.X != target.X) nx += (target.X > cur.X) ? 1 : -1;
            else ny += (target.Y > cur.Y) ? 1 : -1;

            var next = new CellPos(nx, ny);

            // Safety clamp (avoid out-of-bounds)
            if (_grid != null && !_grid.IsInside(next))
                return false;

            st.Cell = next;
            return (next.X == target.X && next.Y == target.Y);
        }
    }
}
