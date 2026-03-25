using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SeasonalBastion
{
    /// <summary>
    /// App-level scene flow controller (Menu <-> Game).
    /// - Persistent singleton (DontDestroyOnLoad)
    /// - Defers NewGame/Continue until Game scene is loaded and GameBootstrap exists
    /// </summary>
    public sealed class GameAppController : MonoBehaviour
    {
        public const string SceneMainMenu = "MainMenu";
        public const string SceneGame = "Game";

        private static GameAppController _instance;
        public static GameAppController Instance => _instance;

        private enum PendingAction { None, NewGame, Continue }
        private PendingAction _pending = PendingAction.None;

        private int _pendingSeed;
        private bool _pendingWipeSave;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureExists()
        {
            if (_instance != null) return;

            var go = new GameObject("GameAppController");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GameAppController>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == SceneGame)
                TryExecutePendingInGameScene();
        }

        public void GoToMainMenu()
        {
            _pending = PendingAction.None;
            SceneManager.LoadScene(SceneMainMenu);
        }

        public void RequestNewGame(int seed, bool wipeExistingSave = true)
        {
            _pending = PendingAction.NewGame;
            _pendingSeed = seed;
            _pendingWipeSave = wipeExistingSave;

            SceneManager.LoadScene(SceneGame);
        }

        public void RequestContinue()
        {
            if (!HasRunSaveFile())
            {
                Debug.LogWarning("[App] Continue requested but no run_save.json exists.");
                _pending = PendingAction.None;
                return;
            }

            _pending = PendingAction.Continue;
            _pendingSeed = 0;
            _pendingWipeSave = false;

            SceneManager.LoadScene(SceneGame);
        }

        public void Quit()
        {
            Application.Quit();
        }

        private void TryExecutePendingInGameScene()
        {
            if (_pending == PendingAction.None) return;

            var boot = FindBootstrap();
            if (boot == null)
            {
                Debug.LogError("[App] Game scene loaded but GameBootstrap not found.");
                return;
            }

            string err;
            switch (_pending)
            {
                case PendingAction.NewGame:
                    {
                        int seed = _pendingSeed != 0 ? _pendingSeed : MakeSeed();
                        if (!boot.TryStartNewRun(seed, startMapConfigOverride: null, wipeExistingSave: _pendingWipeSave, out err))
                            Debug.LogError("[App] NewGame failed: " + err);
                        break;
                    }
                case PendingAction.Continue:
                    {
                        if (!boot.TryContinueLatest(out err))
                            Debug.LogWarning("[App] Continue failed: " + err);
                        break;
                    }
            }

            _pending = PendingAction.None;
            _pendingSeed = 0;
            _pendingWipeSave = false;
        }

        private static bool HasRunSaveFile()
        {
            try
            {
                return File.Exists(Path.Combine(Application.persistentDataPath, "run_save.json"));
            }
            catch
            {
                return false;
            }
        }

        private static int MakeSeed()
        {
            unchecked
            {
                long t = DateTime.UtcNow.Ticks;
                return (int)(t ^ (t >> 32));
            }
        }

        private static GameBootstrap FindBootstrap()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
#else
            return UnityEngine.Object.FindObjectOfType<GameBootstrap>();
#endif
        }
    }
}
