using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using SeasonalBastion.RunStart;
using UnityEngine;

namespace SeasonalBastion
{
    /// <summary>
    /// Day29 — Enemy move + attack HQ/buildings (v0.1)
    /// - Deterministic: iterate enemies by id asc
    /// - Move: 4-dir grid, BFS fallback if greedy fails
    /// - Attack: cooldown per enemy, damage HQ/buildings, defeat when HQ HP <= 0
    /// Notes:
    /// - Building destroy/clearing occupancy is NOT fully implemented in v0.1 (only HP reduced).
    /// </summary>
    public sealed class EnemySystem
    {
        private readonly GameServices _s;

        // Attack timing (v0.1): constant, can be moved to EnemyDef later if needed
        private const float DefaultAttackIntervalSec = 1.0f;

        // Runtime per-enemy cooldown (key = EnemyId.Value)
        private readonly Dictionary<int, float> _attackCd = new();

        // Cached HQ building id (smallest id that matches HQ)
        private BuildingId _hqId;
        private bool _hqCached;

        // BFS buffers (lazy allocated)
        private int _gridW, _gridH, _gridN;
        private int[] _bfsQueue;
        private int[] _bfsPrev;      // store prev index (or -1)
        private int[] _bfsVisited;   // token-based visited marks
        private int _visitToken = 1;

        // Temp list for deterministic iteration
        private readonly List<EnemyId> _ids = new(64);

        public EnemySystem(GameServices s) { _s = s; }

        public void Tick(float dt)
        {
            var w = _s.WorldState;
            var grid = _s.GridMap;
            var data = _s.DataRegistry;
            var clock = _s.RunClock;

            if (w == null || grid == null || data == null || clock == null) return;
            if (w.Enemies == null || w.Enemies.Count <= 0) return;

            // Pause/speed aware
            float ts = clock.TimeScale;
            if (ts <= 0f) return;

            float simDt = dt * ts;
            if (simDt <= 0f) return;

            // Ensure HQ cached (or try refresh if invalid)
            EnsureHqCached();

            // Ensure BFS buffers
            EnsureBfsBuffers(grid.Width, grid.Height);

            // Deterministic iteration: enemy ids sorted asc
            _ids.Clear();
            foreach (var id in w.Enemies.Ids) _ids.Add(id);
            _ids.Sort((a, b) => a.Value.CompareTo(b.Value));

            // Tick each enemy
            for (int i = 0; i < _ids.Count; i++)
            {
                var id = _ids[i];
                if (!w.Enemies.Exists(id)) continue;

                var st = w.Enemies.Get(id);

                // Cleanup dead
                if (st.Hp <= 0)
                {
                    CleanupEnemy(id);
                    continue;
                }

                EnemyDef def;
                try { def = data.GetEnemy(st.DefId); }
                catch
                {
                    // fallback safe defaults
                    def = new EnemyDef { DefId = st.DefId, MaxHp = Mathf.Max(1, st.Hp), MoveSpeed = 1f, DamageToHQ = 1, DamageToBuildings = 1, Range = 0f };
                }

                // Resolve lane dir (from SpawnGates) and compute HQ target cell
                if (!TryResolveLaneTarget(st.Lane, out var hqTargetCell, out var laneDir))
                {
                    // Không có lane runtime -> không biết target -> skip tick enemy (safe)
                    // (hoặc có thể fallback target map center nếu bạn muốn)
                    w.Enemies.Set(id, st);
                    continue;
                }

                // Attack cooldown update
                int key = id.Value;
                if (_attackCd.TryGetValue(key, out float cd))
                {
                    cd -= simDt;
                    _attackCd[key] = cd;
                }
                else
                {
                    _attackCd[key] = 0f;
                    cd = 0f;
                }

                // If already at target cell: attack HQ
                if (CellsEqual(st.Cell, hqTargetCell))
                {
                    TryAttackHQ(ref st, def, ref cd);
                    _attackCd[key] = cd;
                    w.Enemies.Set(id, st);
                    continue;
                }

                // Movement: accumulate "cell steps" by MoveSpeed
                float spd = Mathf.Max(0.01f, def.MoveSpeed);
                float progress = st.MoveProgress01 + simDt * spd;

                // Limit steps per tick to keep stable
                int stepsLeft = 8;

                while (progress >= 1f && stepsLeft-- > 0)
                {
                    // Decide next cell toward target
                    if (!TryFindNextStep(st.Cell, hqTargetCell, out var next))
                    {
                        // No path => try attack adjacent blocking building (v0.1)
                        TryAttackAdjacentBlockingBuilding(ref st, def, ref cd);
                        break;
                    }

                    // If next is blocked by building: attack building instead of moving
                    var occ = grid.Get(next);
                    if (occ.Kind == CellOccupancyKind.Building && occ.Building.Value != 0)
                    {
                        TryAttackBuilding(occ.Building, def.DamageToBuildings, ref cd);
                        break;
                    }

                    // Move into next cell
                    st = new EnemyState
                    {
                        Id = st.Id,
                        DefId = st.DefId,
                        Cell = next,
                        Hp = st.Hp,
                        Lane = st.Lane,
                        MoveProgress01 = 0f
                    };

                    progress -= 1f;

                    // Reached target: can attack immediately if remaining progress
                    if (CellsEqual(st.Cell, hqTargetCell))
                    {
                        TryAttackHQ(ref st, def, ref cd);
                        break;
                    }
                }

                // Store leftover progress (0..1)
                st = new EnemyState
                {
                    Id = st.Id,
                    DefId = st.DefId,
                    Cell = st.Cell,
                    Hp = st.Hp,
                    Lane = st.Lane,
                    MoveProgress01 = Mathf.Clamp01(progress)
                };

                _attackCd[key] = cd;
                w.Enemies.Set(id, st);
            }
        }

        // -------------------------
        // HQ / Target resolution
        // -------------------------

        private void EnsureHqCached()
        {
            var w = _s.WorldState;
            var data = _s.DataRegistry;
            if (w == null || w.Buildings == null || data == null) return;

            if (_hqCached && _hqId.Value != 0 && w.Buildings.Exists(_hqId))
                return;

            _hqCached = true;
            _hqId = default;

            // Deterministic: pick smallest buildingId that is HQ
            var tmp = new List<BuildingId>(32);
            foreach (var bid in w.Buildings.Ids) tmp.Add(bid);
            tmp.Sort((a, b) => a.Value.CompareTo(b.Value));

            for (int i = 0; i < tmp.Count; i++)
            {
                var id = tmp[i];
                if (!w.Buildings.Exists(id)) continue;
                var st = w.Buildings.Get(id);

                if (!st.IsConstructed) continue;

                bool isHQ = string.Equals(st.DefId, "bld_hq_t1", StringComparison.OrdinalIgnoreCase);
                if (!isHQ)
                {
                    // If defs use tags, try them (safe)
                    try
                    {
                        var def = data.GetBuilding(st.DefId);
                        if (def != null && def.IsHQ) isHQ = true;
                    }
                    catch { }
                }

                if (isHQ)
                {
                    _hqId = id;
                    return;
                }
            }
        }

        private bool TryResolveLaneTarget(int laneId, out CellPos target, out Dir4 dirToHQ)
        {
            target = default;
            dirToHQ = Dir4.S;

            var rs = _s.RunStartRuntime;
            if (rs == null) return false;

            // Ưu tiên Lanes table (Day27 single source)
            if (rs.Lanes != null && rs.Lanes.TryGetValue(laneId, out var lane))
            {
                target = lane.TargetHQ;
                dirToHQ = lane.DirToHQ;
                return true;
            }

            // Fallback: nếu thiếu lane runtime thì dùng gate dir + tự tính (tạm)
            // (Có thể bỏ fallback nếu bạn muốn strict)
            if (rs.SpawnGates != null)
            {
                for (int i = 0; i < rs.SpawnGates.Count; i++)
                {
                    var g = rs.SpawnGates[i];
                    if (g.Lane != laneId) continue;
                    dirToHQ = g.DirToHQ;
                    break;
                }
            }

            // Không còn tự tính target theo footprint HQ nữa -> fail
            return false;
        }

        // -------------------------
        // Attack
        // -------------------------

        private void TryAttackHQ(ref EnemyState enemy, EnemyDef def, ref float cd)
        {
            if (cd > 0f) return;

            var w = _s.WorldState;
            if (w == null || w.Buildings == null) return;

            EnsureHqCached();
            if (_hqId.Value == 0 || !w.Buildings.Exists(_hqId)) return;

            int dmg = Mathf.Max(0, def.DamageToHQ);
            if (dmg <= 0) { cd = DefaultAttackIntervalSec; return; }

            var hq = w.Buildings.Get(_hqId);
            int hp = Mathf.Max(0, hq.HP - dmg);

            hq.HP = hp;
            w.Buildings.Set(_hqId, hq);

            // Defeat
            if (hp <= 0)
            {
                _s.RunOutcomeService?.Defeat();
            }

            cd = DefaultAttackIntervalSec;
        }

        private void TryAttackBuilding(BuildingId bid, int dmg, ref float cd)
        {
            if (cd > 0f) return;
            if (dmg <= 0) { cd = DefaultAttackIntervalSec; return; }

            var w = _s.WorldState;
            if (w == null || w.Buildings == null) return;
            if (bid.Value == 0 || !w.Buildings.Exists(bid)) return;

            var b = w.Buildings.Get(bid);
            if (!b.IsConstructed) { cd = DefaultAttackIntervalSec; return; }

            int hp = Mathf.Max(0, b.HP - dmg);
            b.HP = hp;
            w.Buildings.Set(bid, b);

            cd = DefaultAttackIntervalSec;
            Debug.Log($"[EnemySystem] Enemy hit Building {bid.Value} for {dmg}. HP={hp}");
        }

        private void TryAttackAdjacentBlockingBuilding(ref EnemyState enemy, EnemyDef def, ref float cd)
        {
            if (cd > 0f) return;

            var grid = _s.GridMap;
            if (grid == null) return;

            int dmg = Mathf.Max(0, def.DamageToBuildings);
            if (dmg <= 0) { cd = DefaultAttackIntervalSec; return; }

            // Check 4-neighbors for building
            var c = enemy.Cell;

            var n = new CellPos(c.X, c.Y + 1);
            var e = new CellPos(c.X + 1, c.Y);
            var s = new CellPos(c.X, c.Y - 1);
            var w = new CellPos(c.X - 1, c.Y);

            if (TryAttackIfBuildingAt(n, dmg, ref cd)) return;
            if (TryAttackIfBuildingAt(e, dmg, ref cd)) return;
            if (TryAttackIfBuildingAt(s, dmg, ref cd)) return;
            if (TryAttackIfBuildingAt(w, dmg, ref cd)) return;

            // nothing to hit => still consume interval to avoid hammering
            cd = DefaultAttackIntervalSec;
        }

        private bool TryAttackIfBuildingAt(CellPos cell, int dmg, ref float cd)
        {
            var grid = _s.GridMap;
            if (grid == null) return false;

            var occ = grid.Get(cell);
            if (occ.Kind != CellOccupancyKind.Building || occ.Building.Value == 0) return false;

            TryAttackBuilding(occ.Building, dmg, ref cd);
            return true;
        }

        // -------------------------
        // Movement (greedy + BFS)
        // -------------------------

        private bool TryFindNextStep(CellPos from, CellPos target, out CellPos next)
        {
            next = from;

            var grid = _s.GridMap;
            if (grid == null) return false;

            // Greedy: pick neighbor that reduces Manhattan and not blocked
            int bestD = int.MaxValue;
            bool found = false;

            // Deterministic neighbor order: N,E,S,W (or any fixed order)
            Span<CellPos> nb = stackalloc CellPos[4]
            {
                new CellPos(from.X, from.Y + 1),
                new CellPos(from.X + 1, from.Y),
                new CellPos(from.X, from.Y - 1),
                new CellPos(from.X - 1, from.Y),
            };

            for (int i = 0; i < 4; i++)
            {
                var c = nb[i];
                if (!grid.IsInside(c)) continue;
                if (grid.IsBlocked(c)) continue;

                int d = Manhattan(c, target);
                if (d < bestD)
                {
                    bestD = d;
                    next = c;
                    found = true;
                }
            }

            if (found) return true;

            // BFS fallback (if surrounded by blocked cells but path exists elsewhere)
            return TryBfsNextStep(from, target, out next);
        }

        private bool TryBfsNextStep(CellPos from, CellPos target, out CellPos next)
        {
            next = from;

            var grid = _s.GridMap;
            if (grid == null) return false;
            if (!grid.IsInside(from) || !grid.IsInside(target)) return false;
            if (CellsEqual(from, target)) return true;

            int start = ToIdx(from);
            int goal = ToIdx(target);

            _visitToken++;
            if (_visitToken == int.MaxValue) { Array.Clear(_bfsVisited, 0, _bfsVisited.Length); _visitToken = 1; }

            int qh = 0, qt = 0;
            _bfsQueue[qt++] = start;
            _bfsVisited[start] = _visitToken;
            _bfsPrev[start] = -1;

            bool reached = false;

            while (qh < qt)
            {
                int cur = _bfsQueue[qh++];
                if (cur == goal) { reached = true; break; }

                var curPos = FromIdx(cur);

                // N,E,S,W
                Span<CellPos> nb = stackalloc CellPos[4]
                {
                    new CellPos(curPos.X, curPos.Y + 1),
                    new CellPos(curPos.X + 1, curPos.Y),
                    new CellPos(curPos.X, curPos.Y - 1),
                    new CellPos(curPos.X - 1, curPos.Y),
                };

                for (int i = 0; i < 4; i++)
                {
                    var c = nb[i];
                    if (!grid.IsInside(c)) continue;

                    // treat blocked as wall
                    if (grid.IsBlocked(c)) continue;

                    int ni = ToIdx(c);
                    if (_bfsVisited[ni] == _visitToken) continue;

                    _bfsVisited[ni] = _visitToken;
                    _bfsPrev[ni] = cur;
                    _bfsQueue[qt++] = ni;

                    if (ni == goal) { reached = true; qh = qt; break; }
                }
            }

            if (!reached) return false;

            // Reconstruct first step: walk back from goal until prev == start
            int step = goal;
            int prev = _bfsPrev[step];
            if (prev < 0) return false;

            while (prev != start && prev >= 0)
            {
                step = prev;
                prev = _bfsPrev[step];
            }

            var stepPos = FromIdx(step);
            next = stepPos;
            return true;
        }

        // -------------------------
        // Buffers / helpers
        // -------------------------

        private void EnsureBfsBuffers(int w, int h)
        {
            w = Mathf.Max(1, w);
            h = Mathf.Max(1, h);

            if (_gridW == w && _gridH == h && _bfsQueue != null) return;

            _gridW = w;
            _gridH = h;
            _gridN = w * h;

            _bfsQueue = new int[_gridN];
            _bfsPrev = new int[_gridN];
            _bfsVisited = new int[_gridN];
            _visitToken = 1;
        }

        private int ToIdx(CellPos c) => c.Y * _gridW + c.X;

        private CellPos FromIdx(int idx)
        {
            int x = idx % _gridW;
            int y = idx / _gridW;
            return new CellPos(x, y);
        }

        private static int Manhattan(CellPos a, CellPos b)
        {
            int dx = a.X - b.X; if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
            return dx + dy;
        }

        private static bool CellsEqual(CellPos a, CellPos b) => a.X == b.X && a.Y == b.Y;

        private void CleanupEnemy(EnemyId id)
        {
            var w = _s.WorldState;
            if (w == null || w.Enemies == null) return;

            _attackCd.Remove(id.Value);
            w.Enemies.Destroy(id);
        }
    }
}
