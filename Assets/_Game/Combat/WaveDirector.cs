using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    /// <summary>
    /// Day28: wave schedule runner.
    /// - Resolve today's waves from DataRegistry by (Year, Season, Day).
    /// - Spawn enemies by interval (1 enemy each SpawnIntervalSec; gap between entries).
    /// - Timing follows RunClock.TimeScale (pause-aware).
    /// </summary>
    public sealed class WaveDirector
    {
        private readonly GameServices _s;

        // Events (CombatService will forward to bus + public events)
        public event Action<string> WaveStarted;
        public event Action<string> WaveEnded;

        // Tuning (v0.1)
        private const float SpawnIntervalSec = 0.35f; // 1 enemy / 0.35s
        private const float GroupGapSec = 1.0f;       // gap when switching entry type
        private const float InterWaveGapSec = 2.0f;   // gap between waves in same day (if multiple)

        // Today wave queue
        private readonly List<WaveDef> _today = new(4);
        private int _waveCursor = -1;
        private WaveDef _active;

        // Spawn progression within active wave
        private int _entryIndex;
        private int _spawnedInEntry;
        private float _cooldown;

        // Lane round-robin
        private readonly List<int> _laneIdsSorted = new(8);
        private int _laneRR;

        // Simple cache: (year, season, day) -> resolved wave defs (sorted)
        private readonly Dictionary<int, List<WaveDef>> _calendarCache = new();

        public WaveDirector(GameServices s) { _s = s; }

        public void StartDayWaves(int dayIndex)
        {
            // Resolve calendar from RunClock (dayIndex param kept for compatibility)
            var season = _s.RunClock.CurrentSeason;
            var day = _s.RunClock.DayIndex;
            var year = GetYearIndexOr1();

            _today.Clear();
            _waveCursor = -1;
            _active = null;

            // Build lane ids cache (deterministic order)
            BuildLaneIds();

            // If no lanes, still allow events but we cannot spawn.
            // We'll emit start+end immediately when starting waves (handled in StartNextWave).
            var waves = ResolveWavesForCalendar(year, season, day);
            if (waves == null || waves.Count == 0)
                return;

            for (int i = 0; i < waves.Count; i++)
                _today.Add(waves[i]);

            StartNextWave();
        }

        public void Tick(float dt)
        {
            if (_active == null) return;

            // Pause/speed handling: wave time follows RunClock.TimeScale
            var ts = _s.RunClock.TimeScale;
            if (ts <= 0f) return;

            var simDt = dt * ts;
            if (simDt <= 0f) return;

            _cooldown -= simDt;

            // Catch-up spawn (cap by schedule; deterministic)
            while (_cooldown <= 0f && _active != null)
            {
                if (!TrySpawnNextStep())
                    break;
            }
        }

        // -------------------------
        // Internals
        // -------------------------

        private void StartNextWave()
        {
            _waveCursor++;
            if (_waveCursor < 0 || _waveCursor >= _today.Count)
            {
                _active = null;
                return;
            }

            _active = _today[_waveCursor];
            _entryIndex = 0;
            _spawnedInEntry = 0;
            _cooldown = 0f; // spawn immediately

            // Emit started
            WaveStarted?.Invoke(_active.DefId);

            // If no lane available, end immediately (still emits end) to satisfy acceptance safety.
            if (_laneIdsSorted.Count == 0)
            {
                WaveEnded?.Invoke(_active.DefId);
                _active = null;
                return;
            }
        }

        private bool TrySpawnNextStep()
        {
            if (_active == null) return false;

            var entries = _active.Entries;
            if (entries == null || entries.Length == 0)
            {
                EndActiveWave();
                return false;
            }

            // Finished all entries => end wave, maybe schedule next wave
            if (_entryIndex >= entries.Length)
            {
                EndActiveWave();
                return false;
            }

            var e = entries[_entryIndex];
            var enemyId = (e.EnemyId ?? string.Empty).Trim();
            var count = Math.Max(0, e.Count);

            if (string.IsNullOrEmpty(enemyId) || count <= 0)
            {
                // Skip invalid entry deterministically
                _entryIndex++;
                _spawnedInEntry = 0;
                _cooldown += 0f;
                return true;
            }

            // Spawn 1 enemy
            TrySpawnEnemy(enemyId);

            _spawnedInEntry++;
            if (_spawnedInEntry >= count)
            {
                // Move to next entry type
                _entryIndex++;
                _spawnedInEntry = 0;
                _cooldown += GroupGapSec;
            }
            else
            {
                _cooldown += SpawnIntervalSec;
            }

            return true;
        }

        private void EndActiveWave()
        {
            if (_active == null) return;

            var endedId = _active.DefId;
            WaveEnded?.Invoke(endedId);

            // Next wave same day (if any) after inter gap
            if (_waveCursor + 1 < _today.Count)
            {
                _cooldown = InterWaveGapSec;
                StartNextWave();
            }
            else
            {
                _active = null;
            }
        }

        private void TrySpawnEnemy(string enemyDefId)
        {
            if (_s.WorldState == null || _s.DataRegistry == null) return;
            if (_laneIdsSorted.Count == 0) return;

            int laneId = _laneIdsSorted[_laneRR % _laneIdsSorted.Count];
            _laneRR++;

            if (!TryGetLaneStartCell(laneId, out var spawnCell))
                return;

            int hp = 1;
            try
            {
                var def = _s.DataRegistry.GetEnemy(enemyDefId);
                hp = Math.Max(1, def.MaxHp);
            }
            catch { /* keep hp=1 */ }

            var st = new EnemyState
            {
                DefId = enemyDefId,
                Cell = spawnCell,
                Hp = hp,
                Lane = laneId,
                MoveProgress01 = 0f
            };

            var id = _s.WorldState.Enemies.Create(st);
            st.Id = id;
            _s.WorldState.Enemies.Set(id, st);
        }

        private bool TryGetLaneStartCell(int laneId, out CellPos cell)
        {
            cell = default;

            var rs = _s.RunStartRuntime;
            if (rs != null && rs.Lanes != null && rs.Lanes.TryGetValue(laneId, out var lane))
            {
                cell = lane.StartCell;
                return true;
            }

            // Fallback: SpawnGates list
            if (rs != null && rs.SpawnGates != null)
            {
                for (int i = 0; i < rs.SpawnGates.Count; i++)
                {
                    var g = rs.SpawnGates[i];
                    if (g.Lane != laneId) continue;
                    cell = g.Cell;
                    return true;
                }
            }

            return false;
        }

        private void BuildLaneIds()
        {
            _laneIdsSorted.Clear();
            _laneRR = 0;

            var rs = _s.RunStartRuntime;
            if (rs != null && rs.Lanes != null && rs.Lanes.Count > 0)
            {
                foreach (var kv in rs.Lanes)
                    _laneIdsSorted.Add(kv.Key);

                _laneIdsSorted.Sort();
                return;
            }

            // Fallback from gates
            if (rs != null && rs.SpawnGates != null && rs.SpawnGates.Count > 0)
            {
                for (int i = 0; i < rs.SpawnGates.Count; i++)
                {
                    var lane = rs.SpawnGates[i].Lane;
                    if (!_laneIdsSorted.Contains(lane))
                        _laneIdsSorted.Add(lane);
                }
                _laneIdsSorted.Sort();
            }
        }

        private IReadOnlyList<WaveDef> ResolveWavesForCalendar(int year, Season season, int day)
        {
            var r = _s.WaveCalendarResolver;
            if (r == null) return Array.Empty<WaveDef>();
            return r.Resolve(year, season, day);
        }

        private int GetYearIndexOr1()
        {
            // RunClockService exposes YearIndex (runtime-only, not in contract)
            if (_s.RunClock is RunClockService rc) return Math.Max(1, rc.YearIndex);
            return 1;
        }
    }
}
