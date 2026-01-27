using SeasonalBastion;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion.DebugTools
{
public sealed class DebugWorldIndexHUD : MonoBehaviour
{
    [SerializeField] private GameBootstrap _bootstrap;

    private IWorldIndex _index;
    private IWorldState _world;

    [SerializeField] private bool _hubControlled;
    public void SetHubControlled(bool v) => _hubControlled = v;

    private void Awake()
    {
        if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
    }

    private void TryResolve()
    {
        if (_bootstrap == null) return;
        _index ??= _bootstrap.Services?.WorldIndex;
        _world ??= _bootstrap.Services?.WorldState;
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

        // VS2 Day18: Build Sites progress (delivery gate)
        if (_world != null && _world.Sites != null)
        {
            GUILayout.Space(6);
            GUILayout.Label($"Build Sites: {_world.Sites.Count}");

            // Best-effort: if store exposes Ids enumeration, show first 10
            try
            {
                int shown = 0;
                foreach (var sid in _world.Sites.Ids)
                {
                    if (shown >= 10) break;
                    var st = _world.Sites.Get(sid);
                    GUILayout.Label($"- Site {sid.Value}: {st.BuildingDefId} @ ({st.Anchor.X},{st.Anchor.Y}) {(st.IsReadyToWork ? "[READY]" : "[WAIT_COST]")}");
                    GUILayout.Label("  " + FormatCostProgress(st));
                    shown++;
                }
                if (_world.Sites.Count > 10) GUILayout.Label("  ...");
            }
            catch
            {
                // If current IEntityStore doesn't expose Ids, we still show count.
            }
        }
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

    // VS2 Day18: Build Site cost progress formatting
    private static string FormatCostProgress(BuildSiteState st)
    {
        // totals = delivered + remaining (no need to store TotalCosts)
        int[] delivered = new int[5];
        int[] remaining = new int[5];

        if (st.DeliveredSoFar != null) Accum(st.DeliveredSoFar, delivered);
        if (st.RemainingCosts != null) Accum(st.RemainingCosts, remaining);

        return $"Cost: W {delivered[0]}/{delivered[0] + remaining[0]} | F {delivered[1]}/{delivered[1] + remaining[1]} | S {delivered[2]}/{delivered[2] + remaining[2]} | I {delivered[3]}/{delivered[3] + remaining[3]} | A {delivered[4]}/{delivered[4] + remaining[4]}";
    }

    private static void Accum(System.Collections.Generic.List<CostDef> list, int[] arr)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var c = list[i];
            if (c == null) continue;

            int idx = (int)c.Resource;
            if (idx < 0 || idx >= arr.Length) continue;

            arr[idx] += c.Amount;
        }
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
}
