using SeasonalBastion;
using SeasonalBastion.Contracts;
using UnityEngine;

public sealed class DebugWorldIndexHUD : MonoBehaviour
{
    [SerializeField] private GameBootstrap _bootstrap;

    private IWorldIndex _index;

    [SerializeField] private bool _hubControlled;
    public void SetHubControlled(bool v) => _hubControlled = v;

    private void Awake()
    {
        if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
    }

    private void TryResolve()
    {
        _index ??= _bootstrap != null ? _bootstrap.Services?.WorldIndex : null;
    }

    public void DrawHubGUI()
    {
        TryResolve();
        if (_index == null)
        {
            GUILayout.Label("WorldIndex = null");
            return;
        }

        GUILayout.Label("WorldIndex (derived lists)");
        GUILayout.Label($"Warehouses: {_index.Warehouses.Count} | Producers: {_index.Producers.Count} | Houses: {_index.Houses.Count} | Forges: {_index.Forges.Count} | Armories: {_index.Armories.Count} | Towers: {_index.Towers.Count}");

        DrawList("Warehouses", _index.Warehouses);
        DrawList("Producers", _index.Producers);
        DrawList("Houses", _index.Houses);
    }

    private static void DrawList(string label, System.Collections.Generic.IReadOnlyList<BuildingId> list)
    {
        if (list == null || list.Count == 0) return;

        int max = list.Count < 10 ? list.Count : 10;
        string s = "";
        for (int i = 0; i < max; i++)
        {
            s += list[i].Value;
            if (i != max - 1) s += ", ";
        }
        if (list.Count > max) s += " ...";
        GUILayout.Label($"{label}: [{s}]");
    }

    private void OnGUI()
    {
        if (SeasonalBastion.DebugTools.DebugHubState.Enabled || _hubControlled) return;

        // Standalone fallback (rarely used)
        GUILayout.BeginArea(new Rect(10, 10, 520, 180), GUI.skin.box);
        DrawHubGUI();
        GUILayout.EndArea();
    }
}
