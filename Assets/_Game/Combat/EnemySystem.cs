using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
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
        private readonly EnemyTargetResolver _targetResolver;
        private readonly EnemyAttackResolver _attackResolver;
        private readonly EnemyLifecycleResolver _lifecycleResolver;
        private readonly EnemyMovementResolver _movementResolver;

        // Attack timing (v0.1): constant, can be moved to EnemyDef later if needed
        private const float DefaultAttackIntervalSec = 1.0f;

        // Runtime per-enemy cooldown (key = EnemyId.Value)
        private readonly Dictionary<int, float> _attackCd = new();

        // Day34: path fail streak -> fallback step to avoid stuck
        private readonly Dictionary<int, int> _pathFailStreak = new();

        // Day34 tuning
        private const int PathFailThreshold = 6;   // N lần fail liên tiếp mới bật fallback mạnh
        private const int LocalBfsRadius = 6;      // BFS radius nhỏ

        // Temp list for deterministic iteration
        private readonly List<EnemyId> _ids = new(64);

        // Day44: reusable buffers (avoid GC alloc)
        private readonly List<BuildingId> _tmpBuildingIds = new(64);
        private readonly List<int> _tmpEnemyKeys = new(128);

        public EnemySystem(GameServices s)
        {
            _s = s;
            _targetResolver = new EnemyTargetResolver(s.WorldState, s.DataRegistry, s.RunStartRuntime, _tmpBuildingIds);
            _attackResolver = new EnemyAttackResolver(s.WorldState, s.GridMap, s.DataRegistry, s.RunOutcomeService, _targetResolver, GetYearIndexOr1, DefaultAttackIntervalSec);
            _lifecycleResolver = new EnemyLifecycleResolver(s.WorldState, _attackCd, _pathFailStreak, _tmpEnemyKeys);
            _movementResolver = new EnemyMovementResolver(s.GridMap, LocalBfsRadius);
        }

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
            _targetResolver.EnsureHqCached();

            // Ensure BFS buffers
            _movementResolver.EnsureBfsBuffers(grid.Width, grid.Height);

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
                    _lifecycleResolver.CleanupEnemy(id);
                    continue;
                }

                EnemyDef def;
                if (!data.TryGetEnemy(st.DefId, out def) || def == null)
                {
                    // fallback safe defaults
                    def = new EnemyDef { DefId = st.DefId, MaxHp = Mathf.Max(1, st.Hp), MoveSpeed = 1f, DamageToHQ = 1, DamageToBuildings = 1, Range = 0f };
                }

                // Resolve lane dir (from SpawnGates) and compute HQ target cell
                if (!_targetResolver.TryResolveLaneTarget(st.Lane, out var hqTargetCell, out var laneDir))
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
                if (EnemyMovementResolver.CellsEqual(st.Cell, hqTargetCell))
                {
                    _attackResolver.TryAttackHQ(ref st, def, ref cd);
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
                    if (!_movementResolver.TryFindNextStep(st.Cell, hqTargetCell, out var next))
                    {
                        // Day34: path fail streak -> fallback step (dirToHQ / local BFS radius)
                        int streak = 0;
                        _pathFailStreak.TryGetValue(key, out streak);
                        streak++;

                        bool recovered = false;

                        // Khi fail đủ N lần: cố gắng "đẩy" enemy đi theo dirToHQ hoặc local BFS để thoát kẹt
                        if (streak >= PathFailThreshold)
                        {
                            if (_movementResolver.TryFallbackNextStep(st.Cell, hqTargetCell, laneDir, out var fbNext))
                            {
                                next = fbNext;
                                recovered = true;
                                streak = 0; // reset sau khi recover
                            }
                        }

                        _pathFailStreak[key] = streak;

                        if (!recovered)
                        {
                            // Không recover được -> hành vi cũ: cố gắng đập công trình đang chặn
                            _attackResolver.TryAttackAdjacentBlockingBuilding(ref st, def, ref cd);
                            break;
                        }
                    }

                    // If next is blocked by building: attack building instead of moving
                    var occ = grid.Get(next);
                    if (occ.Kind == CellOccupancyKind.Building && occ.Building.Value != 0)
                    {
                        int year = GetYearIndexOr1();
                        float mul = YearScaling.EnemyDamageMul(year);
                        int dmgB = Mathf.Max(0, Mathf.RoundToInt(def.DamageToBuildings * mul));
                        _attackResolver.TryAttackBuilding(occ.Building, dmgB, ref cd);
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
                        MoveProgress01 = 0f,
                        WaveId = st.WaveId,
                        WaveYear = st.WaveYear,
                        WaveSeason = st.WaveSeason,
                        WaveDay = st.WaveDay,
                    };

                    // Day34: moved successfully => reset path fail streak
                    _pathFailStreak[key] = 0;

                    progress -= 1f;

                    // Reached target: can attack immediately if remaining progress
                    if (EnemyMovementResolver.CellsEqual(st.Cell, hqTargetCell))
                    {
                        _attackResolver.TryAttackHQ(ref st, def, ref cd);
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
                    MoveProgress01 = Mathf.Clamp01(progress),
                    WaveId = st.WaveId,
                    WaveYear = st.WaveYear,
                    WaveSeason = st.WaveSeason,
                    WaveDay = st.WaveDay,
                };

                _attackCd[key] = cd;
                w.Enemies.Set(id, st);
            }

            // Day44: prune per-enemy dictionaries to avoid unbounded growth / resize spikes
            _lifecycleResolver.PruneEnemyCaches(dt);
        }

        private int GetYearIndexOr1()
        {
            if (_s.RunClock is RunClockService rc) return Mathf.Max(1, rc.YearIndex);
            return 1;
        }

    }
}
