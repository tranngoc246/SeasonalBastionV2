using System;
using System.IO;
using UnityEngine;
using SeasonalBastion.Contracts;
using System.Collections;

namespace SeasonalBastion
{
    public sealed class QaSoakRunner : MonoBehaviour
    {
        [Serializable]
        public struct SoakConfig
        {
            public int durationMinutes;          // default 30
            public float buildTimeScale;         // default 5
            public float actionIntervalSec;      // default 75 (place road/build attempt)
            public float quickSaveLoadInterval;  // default 180
            public float logIntervalSec;         // default 60
            public int maxBuildAttemptsPerAction;// default 1

            public bool combatStress;             // bật stress combat
            public float combatSpawnIntervalSec;  // vd 150s
            public int combatSpawnCount;          // vd 5
            public string combatEnemyDefId;       // vd "Swarmling"
            public int combatLaneId;              // -1 = rotate lanes; >=0 = lane cố định
            public int combatMaxEnemiesAlive;     // cap để không phình vô hạn (vd 30)
            public float forceDefendAfterMinutes; // ép sang Autumn để chắc chắn có combat (vd 5 phút)
        }

        [Serializable]
        public struct SoakReport
        {
            public string schema;
            public string utcStart;
            public string utcEnd;
            public string unity;
            public string appVersion;
            public string platform;

            public bool passed;
            public string failReason;

            public int errors;
            public string firstError;

            public int finalBuildings;
            public int finalSites;
            public int finalNpcs;
            public int finalEnemies;
            public int finalRoads;

            public string reportPath;
        }

        private GameServices _s;
        private SoakConfig _cfg;
        private bool _running;

        private float _startUnscaled;
        private float _nextAction;
        private float _nextSaveLoad;
        private float _nextLog;

        private int _errors;
        private string _firstError;

        private float _nextCombatSpawn;
        private bool _combatForcedDefend;
        private bool _combatSpawnedAtLeastOnce;
        private int _combatLaneCursor;

        // temp lane ids (deterministic)
        private readonly System.Collections.Generic.List<int> _laneIdsTmp = new(8);

        private SoakReport _report;

        public bool IsRunning => _running;
        public string LastSummary { get; private set; } = "";

        public static QaSoakRunner Ensure()
        {
            var go = GameObject.Find("QA_SoakRunner");
            if (go == null)
            {
                go = new GameObject("QA_SoakRunner");
                DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.DontSave;
                go.AddComponent<QaSoakRunner>();
            }
            return go.GetComponent<QaSoakRunner>();
        }

        public void StartSoak(GameServices s, SoakConfig cfg)
        {
            if (_running) return;

            _s = s;
            _cfg = ApplyDefaults(cfg);
            _running = true;

            _errors = 0;
            _firstError = null;

            _report = new SoakReport
            {
                schema = "qa_soak_v0.1",
                utcStart = DateTime.UtcNow.ToString("o"),
                utcEnd = "",
                unity = Application.unityVersion,
                appVersion = Application.version,
                platform = Application.platform.ToString(),
                passed = false,
                failReason = "",
                errors = 0,
                firstError = "",
                finalBuildings = 0,
                finalSites = 0,
                finalNpcs = 0,
                finalEnemies = 0,
                finalRoads = 0,
                reportPath = ""
            };

            Application.logMessageReceived += OnLog;

            _startUnscaled = Time.unscaledTime;
            _nextAction = _startUnscaled + 1f;
            _nextSaveLoad = _startUnscaled + Mathf.Max(10f, _cfg.quickSaveLoadInterval);
            _nextLog = _startUnscaled + 2f;

            _nextCombatSpawn = Time.unscaledTime + 10f;
            _combatForcedDefend = false;
            _combatSpawnedAtLeastOnce = false;
            _combatLaneCursor = 0;

            LastSummary = "Soak START";

            // Start fresh run (deterministic)
            if (!TryStartFreshRun(_s, out var bootErr))
            {
                Fail("StartFreshRun failed: " + bootErr);
                return;
            }

            StartCoroutine(RunLoop());
        }

        public void StopSoak(string reason = "Stopped by user")
        {
            if (!_running) return;
            Fail(reason);
        }

        private SoakConfig ApplyDefaults(SoakConfig cfg)
        {
            if (cfg.durationMinutes <= 0) cfg.durationMinutes = 30;
            if (cfg.buildTimeScale <= 0f) cfg.buildTimeScale = 5f;
            if (cfg.actionIntervalSec <= 0f) cfg.actionIntervalSec = 75f;
            if (cfg.quickSaveLoadInterval <= 0f) cfg.quickSaveLoadInterval = 180f;
            if (cfg.logIntervalSec <= 0f) cfg.logIntervalSec = 60f;
            if (cfg.maxBuildAttemptsPerAction <= 0) cfg.maxBuildAttemptsPerAction = 1;
            if (cfg.combatStress)
            {
                if (cfg.combatSpawnIntervalSec <= 0f) cfg.combatSpawnIntervalSec = 150f;
                if (cfg.combatSpawnCount <= 0) cfg.combatSpawnCount = 5;
                if (string.IsNullOrEmpty(cfg.combatEnemyDefId)) cfg.combatEnemyDefId = "Swarmling";
                if (cfg.combatMaxEnemiesAlive <= 0) cfg.combatMaxEnemiesAlive = 30;

                // nếu bật combatStress mà chưa set thì ép defend sau 5 phút để chắc chắn stress được
                if (cfg.forceDefendAfterMinutes <= 0f) cfg.forceDefendAfterMinutes = 5f;
            }
            return cfg;
        }

        private IEnumerator RunLoop()
        {
            float durationSec = _cfg.durationMinutes * 60f;

            while (_running)
            {
                float elapsed = Time.unscaledTime - _startUnscaled;

                if (elapsed >= durationSec)
                {
                    if (_cfg.combatStress && !_combatSpawnedAtLeastOnce)
                    {
                        Fail("CombatStress enabled but no enemy was spawned (check forceDefendAfterMinutes / lanes / enemyDefId).");
                        yield break;
                    }

                    Pass();
                    yield break;
                }

                // Keep timescale policy
                ApplyTimeScalePolicy();

                if (Time.unscaledTime >= _nextAction)
                {
                    _nextAction = Time.unscaledTime + _cfg.actionIntervalSec;
                    TryDoActionBatch();
                }

                if (Time.unscaledTime >= _nextSaveLoad)
                {
                    _nextSaveLoad = Time.unscaledTime + _cfg.quickSaveLoadInterval;
                    if (!QuickSaveLoad(out var err))
                    {
                        Fail("QuickSaveLoad failed: " + err);
                        yield break;
                    }
                }

                if (Time.unscaledTime >= _nextLog)
                {
                    _nextLog = Time.unscaledTime + _cfg.logIntervalSec;
                    LogSnapshot(elapsed, durationSec);
                }

                // Combat stress: ensure we reach defend, then spawn enemies periodically
                if (_cfg.combatStress)
                {
                    // Force defend after X minutes if still in build
                    if (!_combatForcedDefend && _cfg.forceDefendAfterMinutes > 0f)
                    {
                        if (elapsed >= _cfg.forceDefendAfterMinutes * 60f && _s.RunClock.CurrentPhase == Phase.Build)
                        {
                            ForceDefendNow();
                            _combatForcedDefend = true;
                        }
                    }

                    if (Time.unscaledTime >= _nextCombatSpawn)
                    {
                        _nextCombatSpawn = Time.unscaledTime + _cfg.combatSpawnIntervalSec;

                        if (_s.RunClock.CurrentPhase == Phase.Defend)
                        {
                            if (!TrySpawnCombatEnemies(out var spawnErr))
                            {
                                Fail("Combat spawn failed: " + spawnErr);
                                yield break;
                            }
                        }
                    }
                }

                // Hard fail if exceptions occurred
                if (_errors > 0)
                {
                    Fail("Exception/Error detected: " + _firstError);
                    yield break;
                }

                yield return null;
            }
        }

        private void ForceDefendNow()
        {
            if (_s?.RunClock is not RunClockService rc) return;

            // Jump to Autumn Y1 D1, tick 1 nhịp để phase cập nhật chắc chắn
            rc.LoadSnapshot(yearIndex: 1, seasonText: Season.Autumn.ToString(), dayIndex: 1, dayTimerSeconds: 0f, timeScale: 1f);
            rc.Tick(0.01f);

            Debug.Log("[QA-Soak] ForceDefendNow -> Autumn Y1 D1");
        }

        private bool TrySpawnCombatEnemies(out string err)
        {
            err = null;

            if (_s?.WorldState == null || _s.WorldState.Enemies == null)
            {
                err = "WorldState/Enemies store null";
                return false;
            }

            if (_s.DataRegistry == null)
            {
                err = "DataRegistry null";
                return false;
            }

            if (_s.RunStartRuntime == null || _s.RunStartRuntime.Lanes == null || _s.RunStartRuntime.Lanes.Count == 0)
            {
                err = "RunStartRuntime.Lanes missing/empty";
                return false;
            }

            EnemyDef def;
            try { def = _s.DataRegistry.GetEnemy(_cfg.combatEnemyDefId); }
            catch
            {
                err = $"EnemyDef not found: '{_cfg.combatEnemyDefId}'";
                return false;
            }

            int alive = _s.WorldState.Enemies.Count;
            int cap = Mathf.Max(1, _cfg.combatMaxEnemiesAlive);
            int toSpawn = Mathf.Min(_cfg.combatSpawnCount, Mathf.Max(0, cap - alive));

            if (toSpawn <= 0)
                return true; // cap reached, not an error

            int laneId = _cfg.combatLaneId;

            // lane rotate if -1
            if (laneId < 0)
            {
                _laneIdsTmp.Clear();
                foreach (var kv in _s.RunStartRuntime.Lanes) _laneIdsTmp.Add(kv.Key);
                _laneIdsTmp.Sort();

                if (_laneIdsTmp.Count == 0)
                {
                    err = "No lane ids available";
                    return false;
                }

                laneId = _laneIdsTmp[_combatLaneCursor % _laneIdsTmp.Count];
                _combatLaneCursor++;
            }

            // fallback: if laneId invalid, pick smallest
            if (!_s.RunStartRuntime.Lanes.TryGetValue(laneId, out var lane))
            {
                _laneIdsTmp.Clear();
                foreach (var kv in _s.RunStartRuntime.Lanes) _laneIdsTmp.Add(kv.Key);
                _laneIdsTmp.Sort();

                if (_laneIdsTmp.Count == 0)
                {
                    err = $"Lane {laneId} not found, and no fallback lanes";
                    return false;
                }

                laneId = _laneIdsTmp[0];
                lane = _s.RunStartRuntime.Lanes[laneId];
            }

            int spawned = 0;
            for (int i = 0; i < toSpawn; i++)
            {
                var st = new EnemyState
                {
                    DefId = _cfg.combatEnemyDefId,
                    Cell = lane.StartCell,
                    Hp = def.MaxHp,
                    Lane = laneId,
                    MoveProgress01 = 0f
                };

                var id = _s.WorldState.Enemies.Create(st);
                st.Id = id;
                _s.WorldState.Enemies.Set(id, st);
                spawned++;
            }

            _combatSpawnedAtLeastOnce |= (spawned > 0);

            Debug.Log($"[QA-Soak] CombatSpawn {spawned} '{_cfg.combatEnemyDefId}' lane {laneId} (alive={alive + spawned}/{cap})");
            return true;
        }

        private void ApplyTimeScalePolicy()
        {
            if (_s?.RunClock == null) return;

            // Build = allow cfg.buildTimeScale
            if (_s.RunClock.CurrentPhase == Phase.Build)
            {
                _s.RunClock.SetTimeScale(_cfg.buildTimeScale);
            }
            else
            {
                // Defend = always 1 (pacing lock)
                _s.RunClock.SetTimeScale(1f);
            }
        }

        private void TryDoActionBatch()
        {
            // 1) Extend road a bit
            TryPlaceRoadExtension();

            // 2) Try place 1 cheap building (only if we can find valid placement + have resources)
            for (int i = 0; i < _cfg.maxBuildAttemptsPerAction; i++)
                if (TryPlaceCheapBuilding()) break;
        }

        private bool TryPlaceCheapBuilding()
        {
            if (_s == null || _s.BuildOrderService == null || _s.PlacementService == null || _s.GridMap == null || _s.DataRegistry == null)
                return false;

            // Short, stable list; skip locked defs
            string[] candidates =
            {
                "bld_woodcutter_t1",
                "bld_farm_t1",
                "bld_stonecutter_t1",
                "bld_house_t1",
                "bld_ironhut_t1",
                "bld_warehouse_t1",
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                string defId = candidates[i];

                try { _s.DataRegistry.GetBuilding(defId); }
                catch { continue; }

                if (_s.UnlockService != null && !_s.UnlockService.IsUnlocked(defId))
                    continue;

                if (!TryFindValidPlacement(defId, Dir4.N, out var anchor))
                    continue;

                int orderId = _s.BuildOrderService.CreatePlaceOrder(defId, anchor, Dir4.N);
                if (orderId > 0)
                {
                    Debug.Log($"[QA-Soak] Placed build site: {defId} at ({anchor.X},{anchor.Y}) orderId={orderId}");
                    return true;
                }
            }

            return false;
        }

        private bool TryFindValidPlacement(string defId, Dir4 rot, out CellPos anchor)
        {
            anchor = default;

            // Scan a small window first (fast), then full scan
            int w = _s.GridMap.Width;
            int h = _s.GridMap.Height;

            // small scan around center
            int cx = w / 2;
            int cy = h / 2;

            int radius = 10;
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = cx + dx;
                    int y = cy + dy;
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;

                    var a = new CellPos(x, y);
                    var vr = _s.PlacementService.ValidateBuilding(defId, a, rot);
                    if (vr.Ok) { anchor = a; return true; }
                }

            // fallback full scan
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var a = new CellPos(x, y);
                    var vr = _s.PlacementService.ValidateBuilding(defId, a, rot);
                    if (vr.Ok) { anchor = a; return true; }
                }

            return false;
        }

        private void TryPlaceRoadExtension()
        {
            if (_s == null || _s.PlacementService == null || _s.GridMap == null) return;

            if (!TryFindFirstRoad(out var from)) return;

            // Try 4-neighbors in deterministic order
            var cands = new[]
            {
                new CellPos(from.X, from.Y + 1),
                new CellPos(from.X + 1, from.Y),
                new CellPos(from.X, from.Y - 1),
                new CellPos(from.X - 1, from.Y),
            };

            for (int i = 0; i < cands.Length; i++)
            {
                var c = cands[i];
                if (_s.PlacementService.CanPlaceRoad(c))
                {
                    _s.PlacementService.PlaceRoad(c);
                    Debug.Log($"[QA-Soak] PlaceRoad at ({c.X},{c.Y})");
                    return;
                }
            }
        }

        private bool TryFindFirstRoad(out CellPos road)
        {
            road = default;
            var grid = _s.GridMap;
            if (grid == null) return false;

            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var c = new CellPos(x, y);
                    if (grid.IsRoad(c)) { road = c; return true; }
                }

            return false;
        }

        private bool QuickSaveLoad(out string err)
        {
            err = null;

            if (_s.SaveService == null) { err = "SaveService null"; return false; }

            var sr = _s.SaveService.SaveRun(_s.WorldState, _s.RunClock);
            if (sr.Code != SaveResultCode.Ok)
            {
                err = $"Save failed: {sr.Code} {sr.Message}";
                return false;
            }

            var lr = _s.SaveService.LoadRun(out var dto);
            if (lr.Code != SaveResultCode.Ok || dto == null)
            {
                err = $"Load failed: {lr.Code} {lr.Message}";
                return false;
            }

            if (!SaveLoadApplier.TryApply(_s, dto, out var applyErr))
            {
                err = $"Apply failed: {applyErr}";
                return false;
            }

            Debug.Log("[QA-Soak] QuickSaveLoad PASS");
            return true;
        }

        private void LogSnapshot(float elapsed, float duration)
        {
            int b = _s.WorldState?.Buildings?.Count ?? 0;
            int s = _s.WorldState?.Sites?.Count ?? 0;
            int n = _s.WorldState?.Npcs?.Count ?? 0;
            int e = _s.WorldState?.Enemies?.Count ?? 0;
            int r = CountRoads(_s.GridMap);

            Debug.Log($"[QA-Soak] t={elapsed:0}s/{duration:0}s | Phase={_s.RunClock.CurrentPhase} {_s.RunClock.CurrentSeason} D{_s.RunClock.DayIndex} | B={b} S={s} N={n} E={e} R={r} | ERR={_errors}");
        }

        private int CountRoads(IGridMap grid)
        {
            if (grid == null) return 0;
            int count = 0;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (grid.IsRoad(new CellPos(x, y))) count++;
            return count;
        }

        private void OnLog(string condition, string stacktrace, LogType type)
        {
            if (!_running) return;

            if (type == LogType.Error || type == LogType.Exception)
            {
                _errors++;
                if (string.IsNullOrEmpty(_firstError))
                    _firstError = condition;
            }
        }

        private void Pass()
        {
            _report.utcEnd = DateTime.UtcNow.ToString("o");
            _report.passed = true;
            _report.failReason = "";
            _report.errors = _errors;
            _report.firstError = _firstError ?? "";

            FillFinalCounts();
            _report.reportPath = TryWriteReportJson(ref _report);

            LastSummary = $"PASS. Report: {_report.reportPath}";
            Debug.Log("[QA-Soak] PASS. " + LastSummary);

            Cleanup();
        }

        private void Fail(string reason)
        {
            _report.utcEnd = DateTime.UtcNow.ToString("o");
            _report.passed = false;
            _report.failReason = reason;
            _report.errors = _errors;
            _report.firstError = _firstError ?? "";

            FillFinalCounts();
            _report.reportPath = TryWriteReportJson(ref _report);

            LastSummary = $"FAIL: {reason}\nReport: {_report.reportPath}";
            Debug.LogError("[QA-Soak] " + LastSummary);

            Cleanup();
        }

        private void FillFinalCounts()
        {
            _report.finalBuildings = _s.WorldState?.Buildings?.Count ?? 0;
            _report.finalSites = _s.WorldState?.Sites?.Count ?? 0;
            _report.finalNpcs = _s.WorldState?.Npcs?.Count ?? 0;
            _report.finalEnemies = _s.WorldState?.Enemies?.Count ?? 0;
            _report.finalRoads = CountRoads(_s.GridMap);
        }

        private void Cleanup()
        {
            if (_running)
            {
                _running = false;
                Application.logMessageReceived -= OnLog;
                StopAllCoroutines();
            }
        }

        private static string TryWriteReportJson(ref SoakReport r)
        {
            try
            {
                var dir = Path.Combine(Application.persistentDataPath, "qa_reports");
                Directory.CreateDirectory(dir);

                var file = $"qa_soak_{(r.passed ? "PASS" : "FAIL")}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_UTC.json";
                var path = Path.Combine(dir, file);

                r.reportPath = path; // IMPORTANT: set before serialize

                var json = JsonUtility.ToJson(r, prettyPrint: true);
                File.WriteAllText(path, json);

                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[QA-Soak] Failed to write report: " + e.Message);
                return "";
            }
        }

        private static bool TryStartFreshRun(GameServices s, out string error)
        {
            error = null;

            var boot = FindObjectOfType<GameBootstrap>();
            if (boot == null)
            {
                error = "GameBootstrap not found in scene.";
                return false;
            }

            // wipe existing save to avoid contamination
            if (!boot.TryStartNewRun(seed: 24680, startMapConfigOverride: null, wipeExistingSave: true, out var err))
            {
                error = err;
                return false;
            }

            return true;
        }
    }
}