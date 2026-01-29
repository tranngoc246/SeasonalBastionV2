namespace SeasonalBastion
{
    using System.Collections.Generic;
    using SeasonalBastion.Contracts;
    using UnityEngine;

    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private DefsCatalog _defsCatalog; // ScriptableObject listing all defs roots (optional)
        [SerializeField] private bool _autoStartRun = true;
        [SerializeField] private int _debugSeed = 12345;

        [Header("Run Start (optional)")]
        [SerializeField] private TextAsset _startMapConfigOverride;

        private GameServices _services;
        private GameLoop _loop;

        // Cached start map config (json or markdown)
        private string _cfg;

        // For debug tools and other systems to access game services
        public GameServices Services => _services;

        private void Awake()
        {
            Application.targetFrameRate = 60;

            _services = GameServicesFactory.Create(_defsCatalog);
            _loop = new GameLoop(_services);

            // Day40: auto attach season summary overlay (shipable UX)
            if (GetComponent<SeasonSummaryOverlay>() == null)
                gameObject.AddComponent<SeasonSummaryOverlay>();

            // Day 17: Validate data at boot (fail-fast)
            if (!ValidateDataAtBoot())
            {
                // If data invalid: stop auto-run to avoid starting with broken defs.
                _autoStartRun = false;
            }

            // Load cfg once (for both auto-run and debug start)
            _cfg = ResolveStartMapConfig();

            if (_autoStartRun)
            {
                if (string.IsNullOrWhiteSpace(_cfg))
                {
                    NotifyError("RunStart config missing: cannot auto start run.");
                    return;
                }

                _loop.StartNewRun(seed: _debugSeed, startMapConfigJsonOrMarkdown: _cfg);
            }
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

        // For debug tools: start a new run using cached cfg
        public void DebugStartNewRun(int seed)
        {
            if (_loop == null) return;

            if (string.IsNullOrWhiteSpace(_cfg))
                _cfg = ResolveStartMapConfig();

            if (string.IsNullOrWhiteSpace(_cfg))
            {
                NotifyError("RunStart config missing: cannot start new run.");
                return;
            }

            _loop.StartNewRun(seed, _cfg);
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
                // Use NotificationService if available (non-blocking)
                _services?.NotificationService?.Push(
                    key: "BOOT_ERROR",
                    title: "BOOT ERROR",
                    body: msg,
                    severity: NotificationSeverity.Error,
                    payload: default,
                    cooldownSeconds: 0f,
                    dedupeByKey: true
                );
            }
            catch
            {
                // ignore - boot should not crash
            }
        }
    }
}
