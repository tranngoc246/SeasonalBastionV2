using SeasonalBastion;
using SeasonalBastion.Contracts;
using SeasonalBastion.RunStart;
using UnityEngine;

public sealed class DebugCombatLaneHUD : MonoBehaviour
{
    [SerializeField] private GameBootstrap _bootstrap;

    [Header("Debug Spawn")]
    [SerializeField] private string _enemyDefId = "Swarmling";
    [SerializeField] private int _laneId = 0;

    [SerializeField] private bool _hubControlled;
    public void SetHubControlled(bool v) => _hubControlled = v;

    private GameServices _gs;

    private void Awake()
    {
        if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
    }

    private void TryResolve()
    {
        if (_bootstrap == null) return;
        _gs ??= _bootstrap.Services;
    }

    public void DrawHubGUI()
    {
        TryResolve();
        if (_gs == null)
        {
            GUILayout.Label("GameServices = null");
            return;
        }

        var rs = _gs.RunStartRuntime;
        if (rs == null)
        {
            GUILayout.Label("RunStartRuntime = null");
            return;
        }

        GUILayout.Space(10);
        GUILayout.Label("Day27: Combat Lanes (from StartMapConfig)");

        if (rs.Lanes == null || rs.Lanes.Count == 0)
        {
            GUILayout.Label("Lane table: EMPTY (did RunStartApplier build lanes?)");
        }
        else
        {
            GUILayout.Label($"Lane count: {rs.Lanes.Count}");
            foreach (var kv in rs.Lanes)
            {
                var ln = kv.Value;
                GUILayout.Label($"- Lane {ln.LaneId}: start=({ln.StartCell.X},{ln.StartCell.Y}) dir={ln.DirToHQ} targetHQ=({ln.TargetHQ.X},{ln.TargetHQ.Y})");
            }
        }

        GUILayout.Space(8);
        GUILayout.Label("Spawn 1 enemy by lane (debug)");

        GUILayout.BeginHorizontal();
        GUILayout.Label("EnemyDefId:", GUILayout.Width(90));
        _enemyDefId = GUILayout.TextField(_enemyDefId, GUILayout.Width(140));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("LaneId:", GUILayout.Width(90));
        var laneStr = GUILayout.TextField(_laneId.ToString(), GUILayout.Width(60));
        if (int.TryParse(laneStr, out var parsedLane)) _laneId = parsedLane;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn", GUILayout.Width(120)))
            TrySpawnEnemy(_enemyDefId, _laneId);
        GUILayout.EndHorizontal();
    }

    private void TrySpawnEnemy(string enemyDefId, int laneId)
    {
        if (_gs == null || _gs.WorldState == null || _gs.DataRegistry == null) return;
        var rs = _gs.RunStartRuntime;
        if (rs == null || rs.Lanes == null) return;

        if (!rs.Lanes.TryGetValue(laneId, out var lane))
        {
            Debug.LogWarning($"[DebugCombatLaneHUD] Lane {laneId} not found in lane table.");
            return;
        }

        EnemyDef def;
        try { def = _gs.DataRegistry.GetEnemy(enemyDefId); }
        catch
        {
            Debug.LogWarning($"[DebugCombatLaneHUD] EnemyDef not found: '{enemyDefId}'");
            return;
        }

        // Spawn EnemyState directly (so HP is correct)
        var st = new EnemyState
        {
            DefId = enemyDefId,
            Cell = lane.StartCell,
            Hp = def.MaxHp,
            Lane = laneId,
            MoveProgress01 = 0f
        };

        var id = _gs.WorldState.Enemies.Create(st);
        st.Id = id;
        _gs.WorldState.Enemies.Set(id, st);

        Debug.Log($"[DebugCombatLaneHUD] Spawned enemy '{enemyDefId}' id={id.Value} lane={laneId} spawn=({lane.StartCell.X},{lane.StartCell.Y}) targetHQ=({lane.TargetHQ.X},{lane.TargetHQ.Y}) dir={lane.DirToHQ}");
    }

    private void OnGUI()
    {
        if (SeasonalBastion.DebugTools.DebugHubState.Enabled || _hubControlled) return;

        // Standalone fallback (rarely used)
        GUILayout.BeginArea(new Rect(10, 10, 520, 240), GUI.skin.box);
        DrawHubGUI();
        GUILayout.EndArea();
    }
}
