namespace SeasonalBastion
{
    using System;
    using System.Collections.Generic;
    using SeasonalBastion.Contracts;
    using SeasonalBastion.RunStart;
    using UnityEngine;

    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private DefsCatalog _defsCatalog; // ScriptableObject listing all defs roots (optional)

        // IMPORTANT (M0): Menu-driven boot => default OFF. (Still can be toggled in inspector for quick testing.)
        [SerializeField] private bool _autoStartRun = false;

        [SerializeField] private int _debugSeed = 12345;

        [Header("Run Start (optional)")]
        [SerializeField] private TextAsset _startMapConfigOverride;

        private GameServices _services;
        private GameLoop _loop;

        // Cached start map config (json or markdown)
        private string _cfg;

        public GameServices Services => _services;

        private void Awake()
        {
            Application.targetFrameRate = 60;

            _services = GameServicesFactory.Create(_defsCatalog);
            _services.ApplyRunStartConfig = ApplyRunStartConfigInternal;
            _loop = new GameLoop(_services);

            // Day 17: Validate data at boot (fail-fast)
            if (!ValidateDataAtBoot())
            {
                // If data invalid: stop auto-run to avoid starting with broken defs.
                _autoStartRun = false;
            }

            _cfg = ResolveStartMapConfig();

            if (_autoStartRun)
            {
                if (string.IsNullOrWhiteSpace(_cfg))
                {
                    NotifyError("RunStart config missing: cannot auto start run.");
                    return;
                }

                _loop.StartNewRun(seed: _debugSeed, startMapConfigJsonOrMarkdown: _cfg);

                // Apply app default speed (only if build phase)
                ApplyAppDefaultSpeedIfAllowed();
            }
        }

        private (bool ok, string error) ApplyRunStartConfigInternal(GameServices services, string cfg)
        {
            bool ok = RunStartFacade.TryApply(services, cfg, out var error);
            return (ok, error);
        }

        private void Update()
        {
            if (_loop == null) return;
            _loop.Tick(Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            if (_loop != null)
            {
                _loop.Dispose();
                _loop = null;
            }

            _services = null;
        }

        /// <summary>
        /// Menu-driven entrypoint.
        /// </summary>
        public bool TryStartNewRun(int seed, string startMapConfigOverride, bool wipeExistingSave, out string error)
        {
            error = null;

            if (_loop == null || _services == null)
            {
                error = "Bootstrap not initialized yet.";
                return false;
            }

            if (wipeExistingSave)
            {
                try { _services.SaveService?.DeleteRunSave(); }
                catch (Exception ex) { UnityEngine.Debug.LogWarning($"[GameBootstrap] Failed to delete existing run save before bootstrap: {ex}"); }
            }

            var cfg = !string.IsNullOrWhiteSpace(startMapConfigOverride) ? startMapConfigOverride : _cfg;
            if (string.IsNullOrWhiteSpace(cfg))
                cfg = ResolveStartMapConfig();

            if (string.IsNullOrWhiteSpace(cfg))
            {
                error = "RunStart config missing (Resources/RunStart/StartMapConfig_RunStart_64x64_v0.1).";
                NotifyError(error);
                return false;
            }

            _cfg = cfg;

            _loop.StartNewRun(seed, _cfg);

            // Apply app default speed (only if build phase)
            ApplyAppDefaultSpeedIfAllowed();

            return true;
        }

        public bool TryContinueLatest(out string error)
        {
            error = null;

            if (_loop == null || _services == null)
            {
                error = "Bootstrap not initialized yet.";
                return false;
            }

            if (_services.SaveService == null)
            {
                error = "SaveService missing.";
                return false;
            }

            if (!_services.SaveService.HasAnyRunSave())
            {
                error = "No run save found.";
                return false;
            }

            var res = _services.SaveService.LoadRun(out var dto);
            if (res.Code != SaveResultCode.Ok || dto == null)
            {
                error = "LoadRun failed: " + res.Message + " (retry or backup load available if present)";
                _services.NotificationService?.Push("load.failed", "Tải tiến trình thất bại", "Không thể tải tiến trình đã lưu. Hãy thử lại hoặc dùng bản lưu khác nếu có.", NotificationSeverity.Warning, default, 5f, true);
                return false;
            }

            if (!SaveLoadApplier.TryApply(_services, dto, out var applyErr))
            {
                error = "Apply save failed: " + applyErr;
                return false;
            }

            // Keep loaded timescale as-is (do NOT override by app setting)
            return true;
        }

        public bool TrySaveNow(out string error)
        {
            error = null;
            if (_services?.SaveService == null) { error = "SaveService missing."; return false; }

            var res = _services.SaveService.SaveRun(_services.WorldState, _services.RunClock);
            if (res.Code != SaveResultCode.Ok)
            {
                error = res.Message;
                return false;
            }

            return true;
        }

        private void ApplyAppDefaultSpeedIfAllowed()
        {
            var rc = _services?.RunClock;
            if (rc == null) return;

            // Only apply when not paused and not in Defend gating.
            if (rc.CurrentPhase == Phase.Build)
            {
                int s = AppSettings.DefaultSpeed;
                rc.SetTimeScale(s);
            }
        }

        // -------------------------
        // Day 17 helpers
        // -------------------------

        private bool ValidateDataAtBoot()
        {
            var validator = _services?.DataValidator;
            var reg = _services?.DataRegistry;

            if (validator == null || reg == null)
            {
                // If you don't have validator wired yet, don't block play.
                Debug.LogWarning("[GameBootstrap] DataValidator or DataRegistry missing - skip validation.");
                return true;
            }

            var errors = new List<string>(64);
            bool ok = validator.ValidateAll(reg, errors);

            if (ok) return true;

            // Fail-fast: show summary + log details
            NotifyError($"Data INVALID: {errors.Count} error(s). Check Console for details.");
            for (int i = 0; i < errors.Count; i++)
                Debug.LogError("[DataValidator] " + errors[i]);

            return false;
        }

        private string ResolveStartMapConfig()
        {
            // Prefer inspector override; fallback to Resources/RunStart/StartMapConfig_RunStart_64x64_v0.1
            if (_startMapConfigOverride != null)
                return _startMapConfigOverride.text;

            var ta = Resources.Load<TextAsset>("RunStart/StartMapConfig_RunStart_64x64_v0.1");
            if (ta != null) return ta.text;

            return null;
        }

        private void NotifyError(string msg)
        {
            Debug.LogError("[GameBootstrap] " + msg);

            try
            {
                _services?.NotificationService?.Push(
                    key: "BOOT_ERROR",
                    title: "Lỗi khởi tạo",
                    body: "Game gặp lỗi trong lúc khởi tạo. Hãy kiểm tra Console để biết thêm chi tiết.",
                    severity: NotificationSeverity.Error,
                    payload: default,
                    cooldownSeconds: 2f,
                    dedupeByKey: true
                );
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameBootstrap] Failed to push boot error notification. Boot continues with logged error only: {ex}");
            }
        }
    }
}
