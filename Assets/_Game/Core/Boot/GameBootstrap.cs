namespace SeasonalBastion
{
    using UnityEngine;

    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private DefsCatalog _defsCatalog; // ScriptableObject listing all defs roots (optional)
        [SerializeField] private bool _autoStartRun = true;

        private GameServices _services;
        private GameLoop _loop;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            _services = GameServicesFactory.Create(_defsCatalog);
            _loop = new GameLoop(_services);

            if (_autoStartRun)
                _loop.StartNewRun(seed: 12345); // TODO: seed source UI
        }

        private void Update()
        {
            _loop.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            _loop.Dispose();
        }
    }
}
