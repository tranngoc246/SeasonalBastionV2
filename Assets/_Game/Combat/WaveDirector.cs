using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    /// <summary>
    /// Day28 + Day34:
    /// - Spawn theo schedule.
    /// - Resolve rule (Day34): wave ends only when spawn done AND aliveCount==0
    /// - Timeout safety: log warn + force resolve to avoid softlock.
    /// - Debug counters: alive/spawned/planned/spawnDone/resolveTimer.
    /// </summary>
    public sealed class WaveDirector
    {
        private readonly GameServices _s;

        public event Action<string> WaveStarted;
        public event Action<string> WaveEnded;

        // Tuning (v0.1)
        private const float SpawnIntervalSec = 0.35f;
        private const float GroupGapSec = 1.0f;
        private const float InterWaveGapSec = 2.0f;

        // Day34: safety resolve timeout (sim seconds)
        private const float ResolveTimeoutSecConst = 120f;

        // Today wave queue
        private readonly List<WaveDef> _today = new(4);
        private int _waveCursor = -1;
        private WaveDef _active;

        // Spawn progression
        private int _entryIndex;
        private int _spawnedInEntry;
        private float _cooldown;

        // Day34: resolve state
        private bool _spawnDone;
        private float _resolveElapsed;
        private bool _pendingNextWave;

        // Day34: debug counters
        private int _activePlanned;
        private int _activeSpawned;

        // Lane RR
        private readonly List<int> _laneIdsSorted = new(8);
        private int _laneRR;

        private readonly Dictionary<int, List<WaveDef>> _calendarCache = new();

        public WaveDirector(GameServices s) { _s = s; }

        // ---- Debug getters (used by CombatService/Debug HUD) ----
        public bool HasActiveWave => _active != null;
        public string ActiveWaveId => _active != null ? _active.DefId : null;
        public int ActivePlanned => _activePlanned;
        public int ActiveSpawned => _activeSpawned;
        public bool SpawnDone => _spawnDone;
        public float ResolveElapsedSec => _resolveElapsed;
        public float ResolveTimeoutSec => ResolveTimeoutSecConst;
        public int AliveCount => _s.WorldState?.Enemies?.Count ?? 0;

        public bool ActiveIsBoss => _active != null && _active.IsBoss;

        public void StartDayWaves(int dayIndex)
        {
            var season = _s.RunClock.CurrentSeason;
            var day = _s.RunClock.DayIndex;
            var year = GetYearIndexOr1();

            _today.Clear();
            _waveCursor = -1;
            _active = null;

            _entryIndex = 0;
            _spawnedInEntry = 0;
            _cooldown = 0f;

            _spawnDone = false;
            _resolveElapsed = 0f;
            _pendingNextWave = false;

            _activePlanned = 0;
            _activeSpawned = 0;

            BuildLaneIds();

            var waves = ResolveWavesForCalendar(year, season, day);
            if (waves == null || waves.Count == 0)
                return;

            for (int i = 0; i < waves.Count; i++)
                _today.Add(waves[i]);

            StartNextWave();
        }

        public void Tick(float dt)
        {
            var ts = _s.RunClock.TimeScale;
            if (ts <= 0f) return;

            float simDt = dt * ts;
            if (simDt <= 0f) return;

            _cooldown -= simDt;

            // If no active wave, maybe waiting inter-wave gap
            if (_active == null)
            {
                if (_pendingNextWave && _cooldown <= 0f)
                {
                    _pendingNextWave = false;
                    StartNextWave();
                }
                return;
            }

            // Spawn phase
            if (!_spawnDone)
            {
                while (_cooldown <= 0f && _active != null && !_spawnDone)
                {
                    if (!TrySpawnNextStep())
                        break;
                }
            }

            // Resolve phase (Day34): spawn done + aliveCount==0
            if (_active != null && _spawnDone)
            {
                _resolveElapsed += simDt;

                int alive = AliveCount;
                if (alive <= 0)
                {
                    EndActiveWaveNow("cleared");
                    return;
                }

                if (_resolveElapsed >= ResolveTimeoutSecConst)
                {
                    Debug.LogWarning($"[WaveDirector] Resolve timeout. Force end wave '{_active.DefId}'. alive={alive}");
                    EndActiveWaveNow("timeout");
                    return;
                }
            }
        }

        // -------------------------
        // Internals
        // -------------------------

        public void DebugStartSingleWave(WaveDef def)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (def == null) return;

            _today.Clear();
            _waveCursor = -1;
            _active = null;

            _entryIndex = 0;
            _spawnedInEntry = 0;
            _cooldown = 0f;

            _spawnDone = false;
            _resolveElapsed = 0f;
            _pendingNextWave = false;

            _activePlanned = 0;
            _activeSpawned = 0;

            BuildLaneIds();

            _today.Add(def);
            StartNextWave();
#endif
        }

        public void ForceResolveActiveWave()
        {
            if (_active == null) return;
            Debug.LogWarning($"[WaveDirector] ForceResolveActiveWave '{_active.DefId}'");
            EndActiveWaveNow("force");
        }

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
            _cooldown = 0f;

            _spawnDone = false;
            _resolveElapsed = 0f;

            _activeSpawned = 0;
            _activePlanned = ComputePlannedCount(_active);

            WaveStarted?.Invoke(_active.DefId);

            // Nếu không có lane -> không spawn được. Để tránh softlock, kết thúc ngay (log warn).
            if (_laneIdsSorted.Count == 0)
            {
                Debug.LogWarning($"[WaveDirector] No lanes available. End wave immediately: '{_active.DefId}'.");
                EndActiveWaveNow("no-lanes");
            }
        }

        private int ComputePlannedCount(WaveDef w)
        {
            int planned = 0;
            var entries = w?.Entries;
            if (entries == null) return 0;

            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                var enemyId = (e.EnemyId ?? string.Empty).Trim();
                int cnt = Math.Max(0, e.Count);
                if (string.IsNullOrEmpty(enemyId) || cnt <= 0) continue;
                planned += cnt;
            }
            return planned;
        }

        private bool TrySpawnNextStep()
        {
            if (_active == null) return false;

            var entries = _active.Entries;
            if (entries == null || entries.Length == 0)
            {
                // spawn done immediately, then resolve will end when alive==0 (or timeout)
                _spawnDone = true;
                return false;
            }

            // Finished all entries => mark spawn done (do NOT end immediately)
            if (_entryIndex >= entries.Length)
            {
                _spawnDone = true;
                _cooldown = 0f;
                return false;
            }

            var e = entries[_entryIndex];
            var enemyId = (e.EnemyId ?? string.Empty).Trim();
            var count = Math.Max(0, e.Count);

            if (string.IsNullOrEmpty(enemyId) || count <= 0)
            {
                _entryIndex++;
                _spawnedInEntry = 0;
                _cooldown += 0f;
                return true;
            }

            // Spawn 1 enemy
            bool spawned = TrySpawnEnemy(enemyId);
            if (spawned) _activeSpawned++;

            _spawnedInEntry++;
            if (_spawnedInEntry >= count)
            {
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

        private void EndActiveWaveNow(string reason)
        {
            if (_active == null) return;

            var endedId = _active.DefId;
            WaveEnded?.Invoke(endedId);

            _active = null;
            _spawnDone = false;
            _resolveElapsed = 0f;

            // schedule next wave (if any) after inter gap
            if (_waveCursor + 1 < _today.Count)
            {
                _pendingNextWave = true;
                _cooldown = InterWaveGapSec;
            }
            else
            {
                _pendingNextWave = false;
            }
        }

        private bool TrySpawnEnemy(string enemyDefId)
        {
            if (_s.WorldState == null || _s.DataRegistry == null) return false;
            if (_laneIdsSorted.Count == 0) return false;

            int laneId = _laneIdsSorted[_laneRR % _laneIdsSorted.Count];
            _laneRR++;

            if (!TryGetLaneStartCell(laneId, out var spawnCell))
                return false;

            int hp = 1;
            try
            {
                var def = _s.DataRegistry.GetEnemy(enemyDefId);
                int baseHp = Math.Max(1, def.MaxHp);

                int year = GetYearIndexOr1();
                float mul = YearScaling.EnemyHpMul(year);

                hp = Math.Max(1, Mathf.RoundToInt(baseHp * mul));
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
            return true;
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
            }
            else if (rs != null && rs.SpawnGates != null && rs.SpawnGates.Count > 0)
            {
                for (int i = 0; i < rs.SpawnGates.Count; i++)
                    _laneIdsSorted.Add(rs.SpawnGates[i].Lane);
            }

            _laneIdsSorted.Sort();

            ApplyLanePolicy(_s.RunClock.CurrentSeason, _s.RunClock.DayIndex);
        }

        private void ApplyLanePolicy(Season season, int day)
        {
            if (_laneIdsSorted.Count <= 1) return;

            // v0.1: Autumn ramps lanes by day (D1:1 lane, D2:2 lanes, D3+:all)
            if (season == Season.Autumn)
            {
                int keep = day;
                if (keep < 1) keep = 1;
                if (keep > _laneIdsSorted.Count) keep = _laneIdsSorted.Count;

                if (keep < _laneIdsSorted.Count)
                {
                    // deterministic: keep smallest lane ids
                    _laneIdsSorted.RemoveRange(keep, _laneIdsSorted.Count - keep);
                }

                _laneRR = 0;
                return;
            }

            // Winter: keep all lanes (default behavior)
        }

        private List<WaveDef> ResolveWavesForCalendar(int year, Season season, int day)
        {
            // cache by hash key
            int key = (year * 100000) + ((int)season * 1000) + day;

            if (_calendarCache.TryGetValue(key, out var cached))
                return cached;

            var resolver = _s.WaveCalendarResolver;
            if (resolver == null)
            {
                _calendarCache[key] = null;
                return null;
            }

            // Interface chuẩn trong project: Resolve(year, season, day)
            var ro = resolver.Resolve(year, season, day);
            List<WaveDef> list = null;

            if (ro != null && ro.Count > 0)
            {
                // copy sang List để cache + dùng thống nhất
                list = new List<WaveDef>(ro.Count);
                for (int i = 0; i < ro.Count; i++)
                    list.Add(ro[i]);
            }

            _calendarCache[key] = list;
            return list;
        }

        private int GetYearIndexOr1()
        {
            if (_s.RunClock is RunClockService rc) return Mathf.Max(1, rc.YearIndex);
            return 1;
        }
    }
}
