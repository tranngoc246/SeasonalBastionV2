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
            var world = _s.WorldState;
            var grid = _s.GridMap;
            var data = _s.DataRegistry;
            var clock = _s.RunClock;

            if (!CanTick(world, grid, data, clock, dt, out float simDt))
                return;

            PrepareTick(world, grid);

            for (int i = 0; i < _ids.Count; i++)
                TickEnemy(world, grid, data, _ids[i], simDt);

            _lifecycleResolver.PruneEnemyCaches(dt);
        }

        private bool CanTick(IWorldState world, IGridMap grid, IDataRegistry data, IRunClock clock, float dt, out float simDt)
        {
            simDt = 0f;
            if (world == null || grid == null || data == null || clock == null)
                return false;
            if (world.Enemies == null || world.Enemies.Count <= 0)
                return false;

            float timeScale = clock.TimeScale;
            if (timeScale <= 0f)
                return false;

            simDt = dt * timeScale;
            return simDt > 0f;
        }

        private void PrepareTick(IWorldState world, IGridMap grid)
        {
            _targetResolver.EnsureHqCached();
            _movementResolver.EnsureBfsBuffers(grid.Width, grid.Height);

            _ids.Clear();
            foreach (var id in world.Enemies.Ids)
                _ids.Add(id);
            _ids.Sort((a, b) => a.Value.CompareTo(b.Value));
        }

        private void TickEnemy(IWorldState world, IGridMap grid, IDataRegistry data, EnemyId id, float simDt)
        {
            if (!world.Enemies.Exists(id))
                return;

            var state = world.Enemies.Get(id);
            if (state.Hp <= 0)
            {
                _lifecycleResolver.CleanupEnemy(id);
                return;
            }

            EnemyDef def = ResolveEnemyDef(data, state);
            if (!_targetResolver.TryResolveLaneTarget(state.Lane, out var hqTargetCell, out var laneDir))
            {
                world.Enemies.Set(id, state);
                return;
            }

            int key = id.Value;
            float cooldown = AdvanceAttackCooldown(key, simDt);
            if (EnemyMovementResolver.CellsEqual(state.Cell, hqTargetCell))
            {
                _attackResolver.TryAttackHQ(ref state, def, ref cooldown);
                _attackCd[key] = cooldown;
                world.Enemies.Set(id, state);
                return;
            }

            float progress = state.MoveProgress01 + (simDt * Mathf.Max(0.01f, def.MoveSpeed));
            progress = TickMovement(grid, def, laneDir, hqTargetCell, key, ref state, ref cooldown, progress);

            state = new EnemyState
            {
                Id = state.Id,
                DefId = state.DefId,
                Cell = state.Cell,
                Hp = state.Hp,
                Lane = state.Lane,
                MoveProgress01 = Mathf.Clamp01(progress),
                WaveId = state.WaveId,
                WaveYear = state.WaveYear,
                WaveSeason = state.WaveSeason,
                WaveDay = state.WaveDay,
            };

            _attackCd[key] = cooldown;
            world.Enemies.Set(id, state);
        }

        private EnemyDef ResolveEnemyDef(IDataRegistry data, EnemyState state)
        {
            if (data.TryGetEnemy(state.DefId, out var def) && def != null)
                return def;

            return new EnemyDef
            {
                DefId = state.DefId,
                MaxHp = Mathf.Max(1, state.Hp),
                MoveSpeed = 1f,
                DamageToHQ = 1,
                DamageToBuildings = 1,
                Range = 0f,
            };
        }

        private float AdvanceAttackCooldown(int key, float simDt)
        {
            if (_attackCd.TryGetValue(key, out float cooldown))
            {
                cooldown -= simDt;
                _attackCd[key] = cooldown;
                return cooldown;
            }

            _attackCd[key] = 0f;
            return 0f;
        }

        private float TickMovement(IGridMap grid, EnemyDef def, Dir4 laneDir, CellPos hqTargetCell, int key, ref EnemyState state, ref float cooldown, float progress)
        {
            int stepsLeft = 8;
            while (progress >= 1f && stepsLeft-- > 0)
            {
                if (!TryResolveNextStep(state, hqTargetCell, laneDir, key, def, ref cooldown, out var next))
                    break;

                var occupancy = grid.Get(next);
                if (occupancy.Kind == CellOccupancyKind.Building && occupancy.Building.Value != 0)
                {
                    int year = GetYearIndexOr1();
                    float multiplier = YearScaling.EnemyDamageMul(year);
                    int buildingDamage = Mathf.Max(0, Mathf.RoundToInt(def.DamageToBuildings * multiplier));
                    _attackResolver.TryAttackBuilding(occupancy.Building, buildingDamage, ref cooldown);
                    break;
                }

                state = MoveEnemyTo(state, next);
                _pathFailStreak[key] = 0;
                progress -= 1f;

                if (EnemyMovementResolver.CellsEqual(state.Cell, hqTargetCell))
                {
                    _attackResolver.TryAttackHQ(ref state, def, ref cooldown);
                    break;
                }
            }

            return progress;
        }

        private bool TryResolveNextStep(EnemyState state, CellPos hqTargetCell, Dir4 laneDir, int key, EnemyDef def, ref float cooldown, out CellPos next)
        {
            if (_movementResolver.TryFindNextStep(state.Cell, hqTargetCell, out next))
                return true;

            int streak = 0;
            _pathFailStreak.TryGetValue(key, out streak);
            streak++;

            bool recovered = false;
            if (streak >= PathFailThreshold && _movementResolver.TryFallbackNextStep(state.Cell, hqTargetCell, laneDir, out var fallbackNext))
            {
                next = fallbackNext;
                recovered = true;
                streak = 0;
            }

            _pathFailStreak[key] = streak;
            if (recovered)
                return true;

            _attackResolver.TryAttackAdjacentBlockingBuilding(ref state, def, ref cooldown);
            next = state.Cell;
            return false;
        }

        private static EnemyState MoveEnemyTo(EnemyState state, CellPos next)
        {
            return new EnemyState
            {
                Id = state.Id,
                DefId = state.DefId,
                Cell = next,
                Hp = state.Hp,
                Lane = state.Lane,
                MoveProgress01 = 0f,
                WaveId = state.WaveId,
                WaveYear = state.WaveYear,
                WaveSeason = state.WaveSeason,
                WaveDay = state.WaveDay,
            };
        }

        private int GetYearIndexOr1()
        {
            if (_s.RunClock is RunClockService rc) return Mathf.Max(1, rc.YearIndex);
            return 1;
        }

    }
}
