// _Game/Debug/HUD/DebugSaveLoadHUD.cs
using System;
using UnityEngine;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class DebugSaveLoadHUD
    {
        private string _last = "";

        private struct Checkpoint
        {
            public string Name;
            public int Year;
            public Season Season;
            public int Day;
            public float DayTimer;
            public bool MidWave;
        }

        public void Draw(GameServices s)
        {
            if (s == null || s.SaveService == null)
            {
                GUILayout.Label("SaveLoadHUD: SaveService = null");
                return;
            }

            GUILayout.Space(8);
            GUILayout.Label("=== Save/Load (Day33) ===");
            GUILayout.Label(_last);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Run", GUILayout.Width(140)))
            {
                var r = s.SaveService.SaveRun(s.WorldState, s.RunClock);
                _last = $"Save: {r.Code} | {r.Message}";
                Debug.Log($"[Save] {r.Code} {r.Message}");
            }

            if (GUILayout.Button("Load + Apply", GUILayout.Width(140)))
            {
                LoadApplyOnce(s);
            }

            if (GUILayout.Button("Delete Save", GUILayout.Width(140)))
            {
                s.SaveService.DeleteRunSave();
                _last = "Deleted run save.";
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            if (GUILayout.Button("Run Regression (6 checkpoints + mid-wave)", GUILayout.Width(320)))
            {
                RunRegression(s);
            }

            if (GUILayout.Button("Run QA Save/Load Matrix (8 checkpoints)", GUILayout.Width(320)))
            {
                if (QaSaveLoadScenario8.Run(s, out var sum))
                    _last = sum;
                else
                    _last = "FAIL: " + sum;
            }

            GUILayout.Label($"HasRunSave: {s.SaveService.HasRunSave()}");
        }

        private void LoadApplyOnce(GameServices s)
        {
            var r = s.SaveService.LoadRun(out var dto);
            if (r.Code != SaveResultCode.Ok || dto == null)
            {
                _last = $"Load: {r.Code} | {r.Message}";
                Debug.Log($"[Load] {r.Code} {r.Message}");
                return;
            }

            if (SaveLoadApplier.TryApply(s, dto, out var err))
            {
                _last = "Load+Apply: OK";
                Debug.Log("[Load] Apply OK");
            }
            else
            {
                _last = $"Load+Apply: FAIL {err}";
                Debug.LogError($"[Load] Apply FAIL: {err}");
            }
        }

        private void RunRegression(GameServices s)
        {
            if (s.RunClock is not RunClockService rc)
            {
                _last = "Regression FAIL: RunClock is not RunClockService";
                return;
            }

            var cps = new[]
            {
                new Checkpoint{ Name="Spring Y1 D1", Year=1, Season=Season.Spring, Day=1, DayTimer=0f, MidWave=false },
                new Checkpoint{ Name="Spring Y1 D6", Year=1, Season=Season.Spring, Day=6, DayTimer=10f, MidWave=false },
                new Checkpoint{ Name="Summer Y1 D1", Year=1, Season=Season.Summer, Day=1, DayTimer=0f, MidWave=false },
                new Checkpoint{ Name="Summer Y1 D6", Year=1, Season=Season.Summer, Day=6, DayTimer=25f, MidWave=false },
                new Checkpoint{ Name="Autumn Y1 D1 (Defend)", Year=1, Season=Season.Autumn, Day=1, DayTimer=2f, MidWave=false },
                new Checkpoint{ Name="Winter Y2 D1 (Defend)", Year=2, Season=Season.Winter, Day=1, DayTimer=2f, MidWave=false },

                // Mid-wave checkpoint: force spawn a few enemies, then Save/Load/Apply and verify enemies persist.
                new Checkpoint{ Name="MidWave: Autumn Y1 D1", Year=1, Season=Season.Autumn, Day=1, DayTimer=5f, MidWave=true },
            };

            int pass = 0;
            int fail = 0;

            for (int i = 0; i < cps.Length; i++)
            {
                if (RunOneCheckpoint(s, rc, cps[i], out var msg))
                {
                    pass++;
                    Debug.Log($"[Regression PASS] {cps[i].Name} :: {msg}");
                }
                else
                {
                    fail++;
                    Debug.LogError($"[Regression FAIL] {cps[i].Name} :: {msg}");
                }
            }

            _last = $"Regression done. PASS={pass} FAIL={fail}. Check Console for details.";
        }

        private bool RunOneCheckpoint(GameServices s, RunClockService rc, Checkpoint cp, out string msg)
        {
            msg = "";

            // Jump clock snapshot
            rc.LoadSnapshot(
                yearIndex: cp.Year,
                seasonText: cp.Season.ToString(),
                dayIndex: cp.Day,
                dayTimerSeconds: Mathf.Max(0f, cp.DayTimer),
                timeScale: 1f
            );

            // If mid-wave: start defend and tick a bit to spawn enemies
            int enemiesBeforeSave = 0;
            if (cp.MidWave)
            {
                if (s.CombatService is CombatService cs)
                {
                    cs.ResetAfterLoad(new CombatDTO { IsDefendActive = true, CurrentWaveIndex = 0 });

                    // tick vài lần để WaveDirector spawn được 1 vài con (không cần coroutine)
                    for (int k = 0; k < 20; k++)
                        cs.Tick(0.2f);
                }

                enemiesBeforeSave = s.WorldState?.Enemies?.Count ?? 0;
                if (enemiesBeforeSave <= 0)
                {
                    msg = "MidWave could not spawn any enemy (lanes missing?)";
                    return false;
                }
            }

            // Record expected snapshot
            int expYear = rc.YearIndex;
            var expSeason = rc.CurrentSeason;
            int expDay = rc.DayIndex;
            float expTimer = rc.DayTimerSeconds;
            float expScale = rc.TimeScale;

            // Record world counts
            int bCount = s.WorldState?.Buildings?.Count ?? 0;
            int stCount = s.WorldState?.Sites?.Count ?? 0;
            int nCount = s.WorldState?.Npcs?.Count ?? 0;
            int tCount = s.WorldState?.Towers?.Count ?? 0;
            int eCount = s.WorldState?.Enemies?.Count ?? 0;
            int roadCount = CountRoads(s.GridMap);

            // Save
            var sr = s.SaveService.SaveRun(s.WorldState, s.RunClock);
            if (sr.Code != SaveResultCode.Ok)
            {
                msg = $"Save failed: {sr.Code} {sr.Message}";
                return false;
            }

            // Load + apply
            var lr = s.SaveService.LoadRun(out var dto);
            if (lr.Code != SaveResultCode.Ok || dto == null)
            {
                msg = $"Load failed: {lr.Code} {lr.Message}";
                return false;
            }

            if (!SaveLoadApplier.TryApply(s, dto, out var err))
            {
                msg = $"Apply failed: {err}";
                return false;
            }

            // Validate snapshot after apply
            if (s.RunClock is not RunClockService rc2)
            {
                msg = "RunClock not RunClockService after apply";
                return false;
            }

            if (rc2.YearIndex != expYear || rc2.CurrentSeason != expSeason || rc2.DayIndex != expDay)
            {
                msg = $"Clock mismatch: exp Y{expYear} {expSeason} D{expDay} got Y{rc2.YearIndex} {rc2.CurrentSeason} D{rc2.DayIndex}";
                return false;
            }

            if (Mathf.Abs(rc2.DayTimerSeconds - expTimer) > 0.05f)
            {
                msg = $"DayTimer mismatch: exp {expTimer:0.00} got {rc2.DayTimerSeconds:0.00}";
                return false;
            }

            if (Mathf.Abs(rc2.TimeScale - expScale) > 0.01f)
            {
                msg = $"TimeScale mismatch: exp {expScale:0.00} got {rc2.TimeScale:0.00}";
                return false;
            }

            // Validate counts preserved
            if ((s.WorldState?.Buildings?.Count ?? 0) != bCount ||
                (s.WorldState?.Sites?.Count ?? 0) != stCount ||
                (s.WorldState?.Npcs?.Count ?? 0) != nCount ||
                (s.WorldState?.Towers?.Count ?? 0) != tCount ||
                (s.WorldState?.Enemies?.Count ?? 0) != eCount)
            {
                msg = $"Counts mismatch: B {bCount}->{(s.WorldState?.Buildings?.Count ?? 0)} | " +
                      $"S {stCount}->{(s.WorldState?.Sites?.Count ?? 0)} | " +
                      $"N {nCount}->{(s.WorldState?.Npcs?.Count ?? 0)} | " +
                      $"T {tCount}->{(s.WorldState?.Towers?.Count ?? 0)} | " +
                      $"E {eCount}->{(s.WorldState?.Enemies?.Count ?? 0)}";
                return false;
            }

            int road2 = CountRoads(s.GridMap);
            if (road2 != roadCount)
            {
                msg = $"Road count mismatch: exp {roadCount} got {road2}";
                return false;
            }

            // Mid-wave specific: ensure enemies persisted
            if (cp.MidWave)
            {
                int enemiesAfter = s.WorldState?.Enemies?.Count ?? 0;
                if (enemiesAfter != enemiesBeforeSave)
                {
                    msg = $"Enemies not preserved on mid-wave: beforeSave {enemiesBeforeSave} afterLoad {enemiesAfter}";
                    return false;
                }
            }

            msg = "OK";
            return true;
        }

        private int CountRoads(IGridMap grid)
        {
            if (grid == null) return 0;
            int c = 0;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (grid.IsRoad(new CellPos(x, y))) c++;
            return c;
        }
    }
}
