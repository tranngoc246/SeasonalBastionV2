using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SeasonalBastion
{
    /// <summary>
    /// Enforce "New Input System only" at runtime.
    /// - Ensure EventSystem exists
    /// - Remove legacy StandaloneInputModule
    /// - Ensure InputSystemUIInputModule exists (via reflection to avoid hard asmdef dependency)
    /// </summary>
    public static class InputSystemOnlyGuard
    {
        private const string InputSystemUiModuleType =
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem";

        public static void EnsureEventSystem_NewInputOnly()
        {
            // Ensure EventSystem exists
            if (EventSystem.current == null)
            {
                var go = new GameObject("EventSystem");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<EventSystem>();
            }

            var es = EventSystem.current;
            if (es == null)
            {
                Debug.LogError("[Input] EventSystem could not be created.");
                return;
            }

            // Remove legacy module if present
            var legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null)
                UnityEngine.Object.Destroy(legacy);

            // Ensure InputSystemUIInputModule exists
            if (!HasInputSystemUiModule(es.gameObject))
            {
                var t = Type.GetType(InputSystemUiModuleType);
                if (t == null)
                {
                    Debug.LogError(
                        "[Input] InputSystemUIInputModule not found. " +
                        "Install/enable Unity Input System package and set Player > Active Input Handling = Input System Package (New).");
                    return;
                }

                es.gameObject.AddComponent(t);
            }
        }

        private static bool HasInputSystemUiModule(GameObject go)
        {
            var all = go.GetComponents<MonoBehaviour>();
            for (int i = 0; i < all.Length; i++)
            {
                var mb = all[i];
                if (mb == null) continue;

                if (mb.GetType().FullName == "UnityEngine.InputSystem.UI.InputSystemUIInputModule")
                    return true;
            }
            return false;
        }
    }
}
