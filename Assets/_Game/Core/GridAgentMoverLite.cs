using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    /// <summary>
    /// Simple deterministic mover (v0.1).
    /// - Move N cells per second toward target (Manhattan).
    /// - Deterministic: X first then Y.
    /// - Ignores obstacles (v0.1).
    /// </summary>
    public sealed class GridAgentMoverLite
    {
        private readonly IGridMap _grid;

        // Tốc độ: số cell / giây (gợi ý 1.2f ~ 2.0f)
        private float _cellsPerSecond = 1.5f;

        // Accumulator theo từng NPC để không phụ thuộc FPS
        private readonly Dictionary<int, float> _accum = new();

        public GridAgentMoverLite(IGridMap grid)
        {
            _grid = grid;
        }

        public void SetCellsPerSecond(float v)
        {
            if (v < 0.1f) v = 0.1f;
            _cellsPerSecond = v;
        }

        /// <returns>true when arrived at target (after possible steps)</returns>
        public bool StepToward(ref NpcState st, CellPos target, float dt)
        {
            var cur = st.Cell;
            if (cur.X == target.X && cur.Y == target.Y)
                return true;

            // NpcId thường nằm trong st.Id; nếu dự án bạn tên khác thì đổi lại key.
            // Nếu NpcState không có Id, bạn có thể truyền npcId.Value từ caller thay cho key.
            int key = st.Id.Value;

            if (!_accum.TryGetValue(key, out var a)) a = 0f;
            a += dt * _cellsPerSecond;

            // Mỗi khi đủ 1.0 => đi 1 cell
            int steps = (int)a;
            if (steps <= 0)
            {
                _accum[key] = a;
                return false;
            }

            a -= steps; // giữ phần dư
            _accum[key] = a;

            // Clamp steps để tránh dt lớn làm “teleport”
            if (steps > 4) steps = 4;

            for (int i = 0; i < steps; i++)
            {
                if (!StepOneCell(ref st, target))
                    return false;

                var c = st.Cell;
                if (c.X == target.X && c.Y == target.Y)
                    return true;
            }

            return false;
        }

        private bool StepOneCell(ref NpcState st, CellPos target)
        {
            var cur = st.Cell;
            if (cur.X == target.X && cur.Y == target.Y)
                return true;

            int nx = cur.X;
            int ny = cur.Y;

            if (cur.X != target.X) nx += (target.X > cur.X) ? 1 : -1;
            else ny += (target.Y > cur.Y) ? 1 : -1;

            var next = new CellPos(nx, ny);

            if (_grid != null && !_grid.IsInside(next))
                return false;

            st.Cell = next;
            return true;
        }

        public void ClearAll()
        {
            _accum.Clear();
        }
    }
}
