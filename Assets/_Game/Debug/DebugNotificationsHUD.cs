using SeasonalBastion;
using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class DebugNotificationsHUD : MonoBehaviour
{
    [SerializeField] private GameBootstrap _bootstrap;

    private InputAction _pushFiveAction;   // N
    private InputAction _spamAction;       // M

    private void Awake()
    {
        if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();

        // Runtime actions - không sửa asset
        _pushFiveAction = new InputAction("PushFive", InputActionType.Button, "<Keyboard>/n");
        _spamAction = new InputAction("Spam", InputActionType.Button, "<Keyboard>/m");
    }

    private void OnEnable()
    {
        _pushFiveAction.Enable();
        _spamAction.Enable();

        _pushFiveAction.performed += OnPushFive;
        _spamAction.performed += OnSpam;
    }

    private void OnDisable()
    {
        _pushFiveAction.performed -= OnPushFive;
        _spamAction.performed -= OnSpam;

        _pushFiveAction.Disable();
        _spamAction.Disable();
    }

    private int _counter;

    private void OnPushFive(InputAction.CallbackContext ctx)
    {
        var noti = _bootstrap.Services.NotificationService;
        if (noti == null) return;

        for (int i = 0; i < 5; i++)
        {
            _counter++;
            noti.Push(
                key: "test",
                title: "TEST",
                body: "Msg #" + _counter,
                severity: NotificationSeverity.Info,
                payload: default,
                cooldownSeconds: 0f,     // test max3
                dedupeByKey: false
            );
        }
    }

    private void OnSpam(InputAction.CallbackContext ctx)
    {
        var noti = _bootstrap.Services.NotificationService;
        if (noti == null) return;

        noti.Push(
            key: "spam",
            title: "SPAM",
            body: "Try spam",
            severity: NotificationSeverity.Warning,
            payload: default,
            cooldownSeconds: 3f,        // test cooldown
            dedupeByKey: true
        );
    }

    private void OnGUI()
    {
        var noti = _bootstrap != null ? _bootstrap.Services?.NotificationService : null;
        if (noti == null) return;

        var list = noti.GetVisible();
        GUILayout.BeginArea(new Rect(10, 10, 520, 300), GUI.skin.box);
        GUILayout.Label("Notifications (max 3, newest-first)");
        GUILayout.Label("Press N: push 5 | Press M: spam key cooldown");
        for (int i = 0; i < list.Count; i++)
        {
            var n = list[i];
            GUILayout.Label($"[{i}] {n.Severity} | {n.Title} | {n.Body} | key={n.Key}");
        }
        GUILayout.EndArea();
    }
}
