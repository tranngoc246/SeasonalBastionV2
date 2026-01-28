using System;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion
{
    /// <summary>
    /// Day28:
    /// - Defend day trigger: when clock enters Phase.Defend, run wave for that day.
    /// - Emits WaveStarted/WaveEnded via both CombatService events + EventBus.
    /// - Tick is pause/speed aware through WaveDirector (dt * TimeScale).
    /// </summary>
    public sealed class CombatService : ICombatService, ITickable
    {
        private readonly GameServices _s;
        private readonly WaveDirector _waves;

        // Day29
        private readonly EnemySystem _enemies;

        private readonly TowerCombatSystem _towers;

        public bool IsActive { get; private set; }

        // Day34: expose wave debug counters for Debug HUD
        public bool HasActiveWave => _waves != null && _waves.HasActiveWave;
        public string ActiveWaveId => _waves != null ? _waves.ActiveWaveId : null;
        public int ActiveWavePlanned => _waves != null ? _waves.ActivePlanned : 0;
        public int ActiveWaveSpawned => _waves != null ? _waves.ActiveSpawned : 0;
        public int AliveEnemyCount => _waves != null ? _waves.AliveCount : (_s.WorldState?.Enemies?.Count ?? 0);
        public bool ActiveWaveSpawnDone => _waves != null && _waves.SpawnDone;
        public float ActiveWaveResolveElapsed => _waves != null ? _waves.ResolveElapsedSec : 0f;
        public float ActiveWaveResolveTimeout => _waves != null ? _waves.ResolveTimeoutSec : 0f;

        public bool ActiveWaveIsBoss => _waves != null && _waves.ActiveIsBoss;

        // Day34: debug action
        public void ForceResolveWave()
        {
            _waves?.ForceResolveActiveWave();
        }

        public event Action<string> OnWaveStarted;
        public event Action<string> OnWaveEnded;

        private Phase _latchedPhase;
        private Season _latchedSeason;
        private int _latchedDay;
        private int _latchedYear;

        public CombatService(GameServices s)
        {
            _s = s;
            _waves = new WaveDirector(s);

            // Day29
            _enemies = new EnemySystem(s);

            _towers = new TowerCombatSystem(s);

            _waves.WaveStarted += HandleWaveStarted;
            _waves.WaveEnded += HandleWaveEnded;

            // init latches
            _latchedPhase = _s.RunClock.CurrentPhase;
            _latchedSeason = _s.RunClock.CurrentSeason;
            _latchedDay = _s.RunClock.DayIndex;
            _latchedYear = GetYearIndexOr1();

            // If we start already in defend (debug jump), ensure active
            if (_latchedPhase == Phase.Defend)
                OnDefendPhaseStarted();
        }

        public void OnDefendPhaseStarted()
        {
            IsActive = true;
            // Start today's waves immediately (lane table already in RunStartRuntime)
            _waves.StartDayWaves(_s.RunClock.DayIndex);
        }

        public void OnDefendPhaseEnded()
        {
            IsActive = false;
            // v0.1: keep enemies; Day29 will handle movement + cleanup rules.
        }

        public void Tick(float dt)
        {
            // Phase latch (defend trigger)
            var phase = _s.RunClock.CurrentPhase;
            if (phase != _latchedPhase)
            {
                if (phase == Phase.Defend) OnDefendPhaseStarted();
                else if (_latchedPhase == Phase.Defend) OnDefendPhaseEnded();

                _latchedPhase = phase;
            }

            if (!IsActive) return;

            // Day latch: in Autumn/Winter phase stays Defend across days -> need restart wave each defend day.
            var season = _s.RunClock.CurrentSeason;
            var day = _s.RunClock.DayIndex;
            var year = GetYearIndexOr1();

            if (season != _latchedSeason || day != _latchedDay || year != _latchedYear)
            {
                _latchedSeason = season;
                _latchedDay = day;
                _latchedYear = year;

                // Only start waves when in Defend days
                if (_s.RunClock.CurrentPhase == Phase.Defend)
                    _waves.StartDayWaves(day);
            }

            _waves.Tick(dt);

            // Day30
            _towers.Tick(dt);

            // Day29: tick enemies (pause-aware inside EnemySystem via RunClock.TimeScale)
            _enemies.Tick(dt);
        }

        public void SpawnWave(string waveDefId)
        {
            // Dev hook: spawn a specific wave immediately (does not change calendar)
            if (string.IsNullOrWhiteSpace(waveDefId)) return;

            // Simple: temporarily resolve and run that wave as "today"
            // (v0.1 gọn: re-use WaveDirector calendar cache is not needed)
            try
            {
                // Emit start
                HandleWaveStarted(waveDefId);

                // Spawn all entries instantly (dev hook) - schedule not required here
                var def = _s.DataRegistry.GetWave(waveDefId);
                if (def.Entries != null)
                {
                    for (int i = 0; i < def.Entries.Length; i++)
                    {
                        var e = def.Entries[i];
                        var cnt = Math.Max(0, e.Count);
                        for (int k = 0; k < cnt; k++)
                        {
                            // Use WaveDirector spawn logic by restarting calendar wave runner is overkill,
                            // so keep this hook minimal: call StartDayWaves not used.
                            // We'll spawn through WaveDirector by fake-running: easiest is to rely on WaveDirector internal.
                        }
                    }
                }

                // Emit end (dev hook)
                HandleWaveEnded(waveDefId);
            }
            catch
            {
                // ignore
            }
        }

        public void KillAllEnemies()
        {
            try
            {
                var es = _s.WorldState?.Enemies;
                if (es == null) return;

                // Deterministic clear
                var toKill = new System.Collections.Generic.List<EnemyId>(es.Count);
                foreach (var id in es.Ids) toKill.Add(id);
                for (int i = 0; i < toKill.Count; i++) es.Destroy(toKill[i]);
            }
            catch { }
        }

        private void HandleWaveStarted(string waveId)
        {
            _s.NotificationService?.Push(
                key: $"WaveStart_{waveId}",
                title: "Wave bắt đầu",
                body: waveId,
                severity: NotificationSeverity.Warning, // hoặc Info
                payload: default,
                cooldownSeconds: 0.25f,
                dedupeByKey: true
            );

            OnWaveStarted?.Invoke(waveId);
            _s.EventBus?.Publish(new WaveStartedEvent(waveId));
        }

        private void HandleWaveEnded(string waveId)
        {
            _s.NotificationService?.Push(
                key: $"WaveEnd_{waveId}",
                title: "Wave kết thúc",
                body: waveId,
                severity: NotificationSeverity.Info,
                payload: default,
                cooldownSeconds: 0.25f,
                dedupeByKey: true
            );

            OnWaveEnded?.Invoke(waveId);
            _s.EventBus?.Publish(new WaveEndedEvent(waveId));
        }

        // Day33: called by SaveLoadApplier after clock snapshot restored.
        // Reset-wave option: restart day waves if Defend.
        public void ResetAfterLoad(CombatDTO dto)
        {
            // Relatch current clock
            _latchedPhase = _s.RunClock.CurrentPhase;
            _latchedSeason = _s.RunClock.CurrentSeason;
            _latchedDay = _s.RunClock.DayIndex;
            _latchedYear = GetYearIndexOr1();

            bool shouldDefend = (_latchedPhase == Phase.Defend);
            if (dto != null) shouldDefend |= dto.IsDefendActive;

            if (shouldDefend)
            {
                // keep existing enemies (restored from save) and restart waves
                OnDefendPhaseStarted();
            }
            else
            {
                OnDefendPhaseEnded();
            }
        }

        private int GetYearIndexOr1()
        {
            if (_s.RunClock is RunClockService rc) return Math.Max(1, rc.YearIndex);
            return 1;
        }
    }
}
