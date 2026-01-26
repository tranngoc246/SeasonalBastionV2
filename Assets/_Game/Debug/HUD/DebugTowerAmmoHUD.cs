using UnityEngine;
using UnityEngine.InputSystem;
using SeasonalBastion.Contracts;

namespace SeasonalBastion.DebugTools
{
    public sealed class DebugTowerAmmoHUD : MonoBehaviour
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private Key _toggleKey = Key.T;     // show/hide HUD
        [SerializeField] private Key _devHookKey = Key.Y;    // toggle dev hook

        private GameServices _gs;

        private void Awake()
        {
            _gs = FindObjectOfType<GameBootstrap>()?.Services;
        }

        private void Update()
        {
            TryResolveServices();

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb[_toggleKey].wasPressedThisFrame)
                _enabled = !_enabled;

            if (!_enabled) return;

            if (kb[_devHookKey].wasPressedThisFrame)
            {
                if (_gs != null && _gs.AmmoService is AmmoService a)
                {
                    a.DevHook_Enabled = !a.DevHook_Enabled;
                }
            }
        }

        private void TryResolveServices()
        {
            if (_gs != null) return;

            var bootstrap = FindObjectOfType<GameBootstrap>();
            if (bootstrap != null)
                _gs = bootstrap.Services;
        }

        private void OnGUI()
        {
            if (!_enabled) return;

            GUILayout.BeginArea(new Rect(10, 740, 620, 200), GUI.skin.box);
            GUILayout.Label("DebugTowerAmmoHUD (Day25)");
            GUILayout.Label($"Toggle HUD: {_toggleKey} | Toggle DevHook: {_devHookKey}");

            if (_gs == null || _gs.WorldState == null || _gs.WorldIndex == null || _gs.AmmoService == null)
            {
                GUILayout.Label("GameServices/WorldState/WorldIndex/AmmoService = null");
                GUILayout.EndArea();
                return;
            }

            bool devHook = false;
            if (_gs.AmmoService is AmmoService a) devHook = a.DevHook_Enabled;

            GUILayout.Label($"PendingRequests = {_gs.AmmoService.PendingRequests} | DevHook = {devHook}");

            var towers = _gs.WorldIndex.Towers;
            if (towers == null || towers.Count == 0)
            {
                GUILayout.Label("No towers indexed.");
                GUILayout.EndArea();
                return;
            }

            GUILayout.Space(8);
            GUILayout.Label("Towers low/empty (<=25%):");

            int shown = 0;
            for (int i = 0; i < towers.Count; i++)
            {
                var tid = towers[i];
                if (!_gs.WorldState.Towers.Exists(tid)) continue;

                var ts = _gs.WorldState.Towers.Get(tid);
                if (ts.AmmoCap <= 0) continue;

                int thr = (ts.AmmoCap * 25 + 99) / 100;
                if (thr < 1) thr = 1;

                bool empty = ts.Ammo <= 0;
                bool low = (ts.Ammo > 0 && ts.Ammo <= thr);

                if (!empty && !low) continue;

                string tag = empty ? "EMPTY" : "LOW";
                GUILayout.Label($"Tower {tid.Value}: {tag}  Ammo {ts.Ammo}/{ts.AmmoCap}");

                shown++;
                if (shown >= 10) break; // avoid huge HUD
            }

            if (shown == 0)
                GUILayout.Label("(none)");

            GUILayout.EndArea();
        }
    }
}
