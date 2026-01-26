// _Game/Debug/DebugSaveLoadHUD.cs
using UnityEngine;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class DebugSaveLoadHUD
    {
        private string _last = "";

        public void Draw(GameServices s)
        {
            if (s == null || s.SaveService == null)
            {
                GUILayout.Label("SaveLoadHUD: SaveService = null");
                return;
            }

            GUILayout.Space(8);
            GUILayout.Label("=== Save/Load (Day31) ===");
            GUILayout.Label(_last);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Run", GUILayout.Width(140)))
            {
                var r = s.SaveService.SaveRun(s.WorldState, s.RunClock);
                _last = $"Save: {r.Code} | {r.Message}";
                Debug.Log($"[Save] {r.Code} {r.Message}");
            }

            if (GUILayout.Button("Load + Apply", GUILayout.Width(140)))
            {
                var r = s.SaveService.LoadRun(out var dto);
                if (r.Code != SaveResultCode.Ok || dto == null)
                {
                    _last = $"Load: {r.Code} | {r.Message}";
                    Debug.Log($"[Load] {r.Code} {r.Message}");
                }
                else
                {
                    if (SaveLoadApplier.TryApply(s, dto, out var err))
                    {
                        _last = "Load+Apply: OK";
                        Debug.Log("[Load] Apply OK");
                    }
                    else
                    {
                        _last = $"Load+Apply: FAIL {err}";
                        Debug.LogError($"[Load] Apply FAIL: {err}");
                    }
                }
            }

            if (GUILayout.Button("Delete Save", GUILayout.Width(140)))
            {
                s.SaveService.DeleteRunSave();
                _last = "Deleted run save.";
            }
            GUILayout.EndHorizontal();

            GUILayout.Label($"HasRunSave: {s.SaveService.HasRunSave()}");
        }
    }
}
