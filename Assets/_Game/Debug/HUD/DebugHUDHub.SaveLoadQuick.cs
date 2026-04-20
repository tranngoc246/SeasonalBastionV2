using SeasonalBastion.Contracts;

namespace SeasonalBastion.DebugTools
{
    public sealed partial class DebugHUDHub
    {
        private void Quick_SaveRun()
        {
            if (_gs?.SaveService == null)
            {
                _gs?.NotificationService?.Push("dbg_save_missing", "Debug", "SaveService missing.", NotificationSeverity.Warning, default, 0.25f, true);
                return;
            }

            var r = _gs.SaveService.SaveRun(_gs.WorldState, _gs.RunClock);
            var sev = r.Code == SaveResultCode.Ok ? NotificationSeverity.Info : NotificationSeverity.Warning;
            _gs.NotificationService?.Push("dbg_save_run", "Save/Load", $"Save: {r.Code} | {r.Message}", sev, default, 0.25f, false);
        }

        private void Quick_LoadApply()
        {
            if (_gs?.SaveService == null)
            {
                _gs?.NotificationService?.Push("dbg_load_missing", "Debug", "SaveService missing.", NotificationSeverity.Warning, default, 0.25f, true);
                return;
            }

            var r = _gs.SaveService.LoadRun(out var dto);
            if (r.Code != SaveResultCode.Ok || dto == null)
            {
                _gs.NotificationService?.Push("dbg_load_run", "Save/Load", $"Load: {r.Code} | {r.Message}", NotificationSeverity.Warning, default, 0.3f, false);
                return;
            }

            if (SaveLoadApplier.TryApply(_gs, dto, out var err))
            {
                TryResumeCombatAfterLoad();
                _gs.NotificationService?.Push("dbg_load_apply_ok", "Save/Load", "Load+Apply: OK", NotificationSeverity.Info, default, 0.25f, true);
            }
            else
            {
                _gs.NotificationService?.Push("dbg_load_apply_fail", "Save/Load", "Load+Apply FAIL: " + err, NotificationSeverity.Error, default, 0.5f, false);
            }
        }

        private void Quick_SaveLoadRoundTrip()
        {
            if (_gs?.SaveService == null)
            {
                _gs?.NotificationService?.Push("dbg_saveload_missing", "Debug", "SaveService missing.", NotificationSeverity.Warning, default, 0.25f, true);
                return;
            }

            var sr = _gs.SaveService.SaveRun(_gs.WorldState, _gs.RunClock);
            if (sr.Code != SaveResultCode.Ok)
            {
                _gs.NotificationService?.Push("dbg_saveload_save_fail", "Save/Load", $"Save failed: {sr.Code} | {sr.Message}", NotificationSeverity.Warning, default, 0.3f, false);
                return;
            }

            var lr = _gs.SaveService.LoadRun(out var dto);
            if (lr.Code != SaveResultCode.Ok || dto == null)
            {
                _gs.NotificationService?.Push("dbg_saveload_load_fail", "Save/Load", $"Load failed: {lr.Code} | {lr.Message}", NotificationSeverity.Warning, default, 0.3f, false);
                return;
            }

            if (SaveLoadApplier.TryApply(_gs, dto, out var err))
            {
                TryResumeCombatAfterLoad();
                _gs.NotificationService?.Push("dbg_saveload_ok", "Save/Load", "Quick Save+Load: OK", NotificationSeverity.Info, default, 0.25f, true);
            }
            else
            {
                _gs.NotificationService?.Push("dbg_saveload_apply_fail", "Save/Load", "Quick Save+Load FAIL: " + err, NotificationSeverity.Error, default, 0.5f, false);
            }
        }

        private void Quick_DeleteSave()
        {
            if (_gs?.SaveService == null)
            {
                _gs?.NotificationService?.Push("dbg_delete_save_missing", "Debug", "SaveService missing.", NotificationSeverity.Warning, default, 0.25f, true);
                return;
            }

            _gs.SaveService.DeleteRunSave();
            _gs.NotificationService?.Push("dbg_delete_save_ok", "Save/Load", "Deleted run save.", NotificationSeverity.Info, default, 0.2f, true);
        }

        private void TryResumeCombatAfterLoad()
        {
            if (_gs?.CombatService is not CombatService combat) return;
            if (_gs.WorldState?.Enemies == null) return;

            int enemyCount = _gs.WorldState.Enemies.Count;
            bool isDefendPhase = _gs.RunClock != null && _gs.RunClock.CurrentPhase == Phase.Defend;

            if (enemyCount > 0 || isDefendPhase)
                combat.OnDefendPhaseStarted();
        }

        private void Quick_RunSaveLoadMatrix()
        {
            if (_gs == null) return;
            bool ok = QaSaveLoadScenario8.Run(_gs, out var summary);
            _gs.NotificationService?.Push("dbg_saveload_matrix", "Save/Load", summary, ok ? NotificationSeverity.Info : NotificationSeverity.Warning, default, 0.5f, false);
        }

        private void Quick_RunInternalSaveLoadCi()
        {
            if (_gs == null) return;
            var rep = QaInternalCiRunner.RunB(_gs, writeReport: true);
            string body = (rep.passed ? "PASS: " : "FAIL: ") + rep.summary;
            if (!string.IsNullOrEmpty(rep.reportPath)) body += " | Report: " + rep.reportPath;
            _gs.NotificationService?.Push("dbg_saveload_ci", "Save/Load", body, rep.passed ? NotificationSeverity.Info : NotificationSeverity.Warning, default, 0.5f, false);
        }
    }
}
