using SeasonalBastion;
using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class DebugNotificationsHUD : MonoBehaviour
{
    [SerializeField] private GameBootstrap _bootstrap;

    private InputAction _pushFiveAction;   // N
    private InputAction _spamAction;       // M
    private InputAction _toggleHud;        // H

    private INotificationService _noti;
    private bool _show = true;
    private int _counter;

    private void Awake()
    {
        if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();

        // Runtime actions - không sửa asset
        _pushFiveAction = new InputAction("PushFive", InputActionType.Button, "<Keyboard>/n");
        _spamAction = new InputAction("Spam", InputActionType.Button, "<Keyboard>/m");
        _toggleHud = new InputAction("ToggleHud", InputActionType.Button, "<Keyboard>/h");
    }

    private void Start()
    {
        _noti = _bootstrap != null ? _bootstrap.Services?.NotificationService : null;
    }

    private void OnEnable()
    {
        _pushFiveAction.Enable();
        _spamAction.Enable();
        _toggleHud.Enable();

        _pushFiveAction.performed += OnPushFive;
        _spamAction.performed += OnSpam;
        _toggleHud.performed += OnToggleHud;
    }

    private void OnDisable()
    {
        _pushFiveAction.performed -= OnPushFive;
        _spamAction.performed -= OnSpam;
        _toggleHud.performed -= OnToggleHud;

        _pushFiveAction.Disable();
        _spamAction.Disable();
        _toggleHud.Disable();
    }

    private void OnToggleHud(InputAction.CallbackContext ctx)
    {
        _show = !_show;
        _noti ??= _bootstrap != null ? _bootstrap.Services?.NotificationService : null;

        _noti?.Push(
            key: "HUD_Toggle",
            title: "HUD",
            body: _show ? "Notifications HUD: ON (H)" : "Notifications HUD: OFF (H)",
            severity: NotificationSeverity.Info,
            payload: default,
            cooldownSeconds: 0.2f,
            dedupeByKey: true
        );
    }

    private void OnPushFive(InputAction.CallbackContext ctx)
    {
        _noti ??= _bootstrap != null ? _bootstrap.Services?.NotificationService : null;
        if (_noti == null) return;

        for (int i = 0; i < 5; i++)
        {
            _counter++;
            _noti.Push(
                key: "test_" + _counter,      // unique so you can see max3 behavior clearly
                title: "TEST",
                body: "Msg #" + _counter,
                severity: NotificationSeverity.Info,
                payload: default,
                cooldownSeconds: 0f,
                dedupeByKey: false
            );
        }
    }

    private void OnSpam(InputAction.CallbackContext ctx)
    {
        _noti ??= _bootstrap != null ? _bootstrap.Services?.NotificationService : null;
        if (_noti == null) return;

        _noti.Push(
            key: "spam",
            title: "SPAM",
            body: "Try spam",
            severity: NotificationSeverity.Warning,
            payload: default,
            cooldownSeconds: 3f,
            dedupeByKey: true
        );
    }

    private void OnGUI()
    {
        if (!_show) return;

        _noti ??= _bootstrap != null ? _bootstrap.Services?.NotificationService : null;
        if (_noti == null) return;

        var list = _noti.GetVisible();
        GUILayout.BeginArea(new Rect(10, 10, 520, 320), GUI.skin.box);
        GUILayout.Label("Notifications (max 3, newest-first)");
        GUILayout.Label("Press N: push 5 | Press M: spam key cooldown | Press H: toggle HUD");

        for (int i = 0; i < list.Count; i++)
        {
            var n = list[i];
            GUILayout.Label($"[{i}] {n.Severity} | {n.Title} | {n.Body} | key={n.Key}");
        }

        GUILayout.EndArea();
    }
}
