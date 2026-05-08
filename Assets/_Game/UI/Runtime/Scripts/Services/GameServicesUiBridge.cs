using SeasonalBastion.UI.Services;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion.UI.Runtime
{
    /// <summary>
    /// Attach vào GameBootstrap hoặc UIRoot.
    /// - Provide GameServices cho UiBootstrap
    /// - Provide pause/resume (modal stack) bằng RunClock.SetTimeScale
    /// </summary>
    public sealed class GameServicesUiBridge : MonoBehaviour, IUiServicesProvider, IUiPauseController
    {
        [SerializeField] private GameBootstrap _bootstrap;

        private GameServices _s;
        private float _prev = 1f;

        private void Awake()
        {
            if (_bootstrap == null) _bootstrap = FindAnyObjectByType<GameBootstrap>();
            _s = _bootstrap != null ? _bootstrap.Services : null;
        }

        public object GetServices()
        {
            if (_s == null)
            {
                if (_bootstrap == null) _bootstrap = FindAnyObjectByType<GameBootstrap>();
                _s = _bootstrap != null ? _bootstrap.Services : null;
            }
            return _s;
        }

        public void PauseUI()
        {
            var services = EnsureServices();
            if (services?.RunClock == null) return;

            float cur = services.RunClock.TimeScale;
            _prev = cur <= 0.01f ? Mathf.Max(1f, AppSettings.DefaultSpeed) : cur;
            services.RunClock.SetTimeScale(0f);
        }

        public void ResumeUI()
        {
            var services = EnsureServices();
            if (services?.RunClock == null) return;

            float target = _prev <= 0.01f ? Mathf.Max(1f, AppSettings.DefaultSpeed) : _prev;

            // If Build phase, apply app default speed; else clamp to 1 unless unlocked
            if (services.RunClock.CurrentPhase == Phase.Build)
                target = Mathf.Clamp(AppSettings.DefaultSpeed, 1, 3);
            else
                target = services.RunClock.DefendSpeedUnlocked ? Mathf.Clamp(target, 0, 3) : 1f;

            services.RunClock.SetTimeScale(target);
        }

        private GameServices EnsureServices()
        {
            if (_s == null)
            {
                if (_bootstrap == null) _bootstrap = FindAnyObjectByType<GameBootstrap>();
                _s = _bootstrap != null ? _bootstrap.Services : null;
            }

            return _s;
        }
    }
}
