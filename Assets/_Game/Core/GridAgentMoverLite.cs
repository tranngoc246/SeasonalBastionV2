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
        private readonly IDataRegistry _data;
        private readonly BalanceService _bal;

        // fallback nếu npc def thiếu
        private readonly float _fallbackBase;
        private readonly float _fallbackRoadMul;

        private readonly Dictionary<int, float> _accum = new();

        public GridAgentMoverLite(IGridMap grid, IDataRegistry data, BalanceService bal)
        {
            _grid = grid;
            _data = data;
            _bal = bal;

            _fallbackBase = bal != null ? bal.DefaultMoveSpeed : 1f;
            _fallbackRoadMul = bal != null ? bal.DefaultRoadMult : 1.3f;
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
                catch { }
            }

            bool onRoad = _grid != null && _grid.IsRoad(st.Cell);
            float spd = onRoad ? (baseSpd * roadMul) : baseSpd;

            a += dt * spd;

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
