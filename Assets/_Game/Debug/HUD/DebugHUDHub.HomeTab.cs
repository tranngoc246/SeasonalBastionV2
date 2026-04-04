using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion.DebugTools
{
    public sealed partial class DebugHUDHub
    {
        private void ValidateDataNow()
        {
            _dataErrors.Clear();

            if (_gs == null)
            {
                _dataLastOk = false;
                _dataLastSummary = "GameServices is null";
                _dataErrors.Add(_dataLastSummary);
                return;
            }

            var validator = _gs.DataValidator;
            var registry = _gs.DataRegistry;
            if (validator == null || registry == null)
            {
                _dataLastOk = false;
                _dataLastSummary = "Missing DataValidator/DataRegistry";
                _dataErrors.Add(_dataLastSummary);
                return;
            }

            _dataLastOk = validator.ValidateAll(registry, _dataErrors);
            _dataLastSummary = _dataLastOk ? "OK" : $"FAIL ({_dataErrors.Count} errors)";

            if (!_dataLastOk)
            {
                try
                {
                    _gs.NotificationService?.Push("data_invalid", "Data INVALID", _dataLastSummary, NotificationSeverity.Error, default, cooldownSeconds: 0f, dedupeByKey: true);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[DebugHUDHub] Failed to push data validation notification: {ex}");
                }
            }
        }

        private void DrawHome()
        {
            GUILayout.Label("Active Modules:");
            GUILayout.Label($"BuildTool: {(_buildTool != null ? "OK" : "missing")}  | RoadTool: {(_roadTool != null ? "OK" : "missing")}  | NpcTool: {(_npcTool != null ? "OK" : "missing")}");
            GUILayout.Label($"NotiHUD: {(_notiHud != null ? "OK" : "missing")}  | WorldIndexHUD: {(_worldIndexHud != null ? "OK" : "missing")}  | StorageHUD: {(_storageHud != null ? "OK" : "missing")}");

            GUILayout.Space(6);
            GUILayout.Label("Notes:");
            GUILayout.Label("- All old standalone toggle keys are disabled (B/R/N/H/I/S).");
            GUILayout.Label("- Tools only respond to inputs when their mode is active.");

            GUILayout.Space(8);
            if (_gs != null && _gs.JobBoard is JobBoard jb)
                GUILayout.Label($"HaulBasic jobs active: {jb.CountActiveJobs(JobArchetype.HaulBasic)}");

            if (_mode == DebugHubMode.Build && _gs != null && _buildTool != null && _gs.UnlockService != null)
            {
                GUILayout.Space(8);
                GUILayout.Label("Build Slots (Unlocked only)");
                _buildTool.GetBuildSlotDefs(_buildSlotsTmp);

                bool any = false;
                for (int i = 0; i < _buildSlotsTmp.Count; i++)
                {
                    var defId = _buildSlotsTmp[i];
                    if (string.IsNullOrEmpty(defId)) continue;

                    if (_gs.UnlockService.IsUnlocked(defId))
                    {
                        any = true;
                        GUILayout.Label($"{i + 1}: {defId}");
                    }
                }

                if (!any)
                    GUILayout.Label("(none unlocked in current time)");
            }

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            _homeShowData = GUILayout.Toggle(_homeShowData, "Data", GUILayout.Width(60));
            _homeShowRunClock = GUILayout.Toggle(_homeShowRunClock, "Clock", GUILayout.Width(70));
            _homeShowLanes = GUILayout.Toggle(_homeShowLanes, "Lanes", GUILayout.Width(70));
            _homeShowSaveLoad = GUILayout.Toggle(_homeShowSaveLoad, "Save", GUILayout.Width(60));
            _homeShowWave = GUILayout.Toggle(_homeShowWave, "Wave", GUILayout.Width(70));
            GUILayout.EndHorizontal();

            _homeScroll = GUILayout.BeginScrollView(_homeScroll, GUILayout.ExpandHeight(true));

            if (_homeShowData)
            {
                GUILayout.Space(10);
                GUILayout.Label("Day17: Data Validator");

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Validate Data", GUILayout.Width(140)))
                    ValidateDataNow();
                GUILayout.Label($"Result: {_dataLastSummary}");
                GUILayout.EndHorizontal();

                if (!_dataLastOk)
                {
                    GUILayout.Label("Errors:");
                    _dataScroll = GUILayout.BeginScrollView(_dataScroll, GUILayout.Height(240));
                    int show = Mathf.Min(_dataErrors.Count, 50);
                    for (int i = 0; i < show; i++)
                        GUILayout.Label("- " + _dataErrors[i]);
                    if (_dataErrors.Count > show)
                        GUILayout.Label($"...({_dataErrors.Count - show} more)");
                    GUILayout.EndScrollView();
                }

                GUILayout.Space(6);
            }

            if (_homeShowRewards)
            {
                GUILayout.Space(10);
                GUILayout.Label("EndSeasonRewardRequested (Reward placeholder)");

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Bound: {_rewardListenerBound}   Count: {_endSeasonRewardReqCount}");
                if (GUILayout.Button("Clear", GUILayout.Width(80)))
                {
                    _endSeasonRewardReqCount = 0;
                    _hasLastEndSeasonRewardReq = false;
                    _lastEndSeasonRewardRealtime = 0f;
                }
                GUILayout.EndHorizontal();

                if (_hasLastEndSeasonRewardReq)
                {
                    float dt = Time.realtimeSinceStartup - _lastEndSeasonRewardRealtime;
                    GUILayout.Label($"Last: Season={_lastEndSeasonRewardReq.Season}  Year={_lastEndSeasonRewardReq.YearIndex}  Day={_lastEndSeasonRewardReq.DayIndex}   ({dt:0.00}s ago)");
                }
                else
                {
                    GUILayout.Label("Last: (none yet)");
                }

                GUILayout.Space(6);
            }

            if (_homeShowHints)
            {
                GUILayout.Space(10);
                GUILayout.Label("Day41: Tutorial Hints");

                if (_gs == null || _gs.TutorialHints == null)
                {
                    GUILayout.Label("TutorialHintsService: missing");
                }
                else
                {
                    var h = _gs.TutorialHints;
                    GUILayout.Label($"ActiveWindow: 10min | RunAge(sim): {h.RunAge:0.0}s");
                    GUILayout.Label($"Counts: UnassignedNPC={h.HintNpcUnassignedCount} | ProducerFull={h.HintProducerFullCount} | OutOfAmmo={h.HintOutOfAmmoCount} | WaveIncoming={h.HintWaveIncomingCount}");
                    GUILayout.Label($"LastHintRealtime: {h.LastHintRealtime:0.00}s (Time.realtimeSinceStartup)");
                }

                GUILayout.Space(6);
            }

            if (_homeShowRunClock)
            {
                GUILayout.Space(10);
                if (_runClockHud != null)
                    _runClockHud.DrawHubGUI();
                else
                    GUILayout.Label("DebugRunClockHUD: missing (add component to scene if you want clock controls)");

                GUILayout.Space(6);
            }

            if (_homeShowLanes)
            {
                GUILayout.Space(10);
                if (_combatLaneHud != null)
                    _combatLaneHud.DrawHubGUI();
                else
                    GUILayout.Label("DebugCombatLaneHUD: missing (add component to scene if you want lane spawn debug)");

                GUILayout.Space(6);
            }

            if (_homeShowSaveLoad)
            {
                GUILayout.Space(10);
                GUILayout.Label("Advanced Save/Load / QA");
                if (_gs != null) _saveLoadHUD.Draw(_gs);
                else GUILayout.Label("SaveLoadHUD: GameServices is null");

                GUILayout.Space(6);
            }

            if (_homeShowWave)
            {
                GUILayout.Space(10);
                if (_waveHud != null)
                    _waveHud.DrawHubGUI();
                else
                    GUILayout.Label("DebugWaveHUD: missing (add component to scene if you want wave counters)");

                GUILayout.Space(6);
            }

            GUILayout.EndScrollView();
        }
    }
}
