namespace SeasonalBastion
{
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

        // For debug tools and other systems to access game services and loop
        public GameServices Services => _services;

        private void Awake()
        {
            Application.targetFrameRate = 60;

            _services = GameServicesFactory.Create(_defsCatalog);
            _loop = new GameLoop(_services);

            if (_autoStartRun)
            {
                // Prefer inspector override; fallback to Resources/RunStart/StartMapConfig_RunStart_64x64_v0.1
                string cfg = null;
                if (_startMapConfigOverride != null) cfg = _startMapConfigOverride.text;
                else
                {
                    var ta = Resources.Load<TextAsset>("RunStart/StartMapConfig_RunStart_64x64_v0.1");
                    if (ta != null) cfg = ta.text;
                }

                _loop.StartNewRun(seed: _debugSeed, startMapConfigJsonOrMarkdown: cfg); // TODO: seed source UI
            }
        }

        private void Update()
        {
            if (_loop == null) return;

            _loop.Tick(Time.deltaTime);
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
    }
}
