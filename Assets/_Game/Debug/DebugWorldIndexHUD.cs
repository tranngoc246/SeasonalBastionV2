using SeasonalBastion;
using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Day 6 (Part27): Debug overlay showing derived lists from WorldIndex.
/// Minimal OnGUI HUD.
/// </summary>
public sealed class DebugWorldIndexHUD : MonoBehaviour
{
    [SerializeField] private GameBootstrap _bootstrap;

    private InputAction _toggleHud; // I
    private bool _show = true;

    private IWorldIndex _index;

    private void Awake()
    {
        if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
        _toggleHud = new InputAction("ToggleWorldIndexHud", InputActionType.Button, "<Keyboard>/i");
    }

    private void Start()
    {
        _index = _bootstrap != null ? _bootstrap.Services?.WorldIndex : null;
    }

    private void OnEnable()
    {
        _toggleHud.Enable();
        _toggleHud.performed += OnToggle;
    }

    private void OnDisable()
    {
        _toggleHud.performed -= OnToggle;
        _toggleHud.Disable();
    }

    private void OnToggle(InputAction.CallbackContext ctx)
    {
        _show = !_show;

        var noti = _bootstrap != null ? _bootstrap.Services?.NotificationService : null;
        noti?.Push(
            key: "WorldIndexHUD_Toggle",
            title: "HUD",
            body: _show ? "WorldIndex HUD: ON (I)" : "WorldIndex HUD: OFF (I)",
            severity: NotificationSeverity.Info,
            payload: default,
            cooldownSeconds: 0.2f,
            dedupeByKey: true
        );
    }

    private void OnGUI()
    {
        if (!_show) return;
        _index ??= _bootstrap != null ? _bootstrap.Services?.WorldIndex : null;
        if (_index == null) return;

        GUILayout.BeginArea(new Rect(10, 340, 520, 240), GUI.skin.box);
        GUILayout.Label("WorldIndex (derived lists) — Press I to toggle");

        GUILayout.Label($"Warehouses: {_index.Warehouses.Count} | Producers: {_index.Producers.Count} | Houses: {_index.Houses.Count} | Forges: {_index.Forges.Count} | Armories: {_index.Armories.Count} | Towers: {_index.Towers.Count}");

        DrawList("Warehouses", _index.Warehouses);
        DrawList("Producers", _index.Producers);
        DrawList("Houses", _index.Houses);

        GUILayout.EndArea();
    }

    private static void DrawList(string label, System.Collections.Generic.IReadOnlyList<BuildingId> list)
    {
        if (list == null || list.Count == 0) return;

        // Keep HUD short (show first 6 IDs)
        int max = list.Count < 6 ? list.Count : 6;
        string s = "";
        for (int i = 0; i < max; i++)
        {
            s += list[i].Value;
            if (i != max - 1) s += ", ";
        }
        if (list.Count > max) s += " ...";

        GUILayout.Label($"{label}: [{s}]");
    }
}
