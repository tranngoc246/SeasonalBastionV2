using SeasonalBastion;
using SeasonalBastion.Contracts;
using UnityEngine;

namespace SeasonalBastion.DebugTools
{
public sealed class DebugNotificationsHUD : MonoBehaviour
{
    [SerializeField] private GameBootstrap _bootstrap;

    private INotificationService _noti;
    private int _counter;

    [SerializeField] private bool _hubControlled;

    public void SetHubControlled(bool v) => _hubControlled = v;

    private void Awake()
    {
        if (_bootstrap == null) _bootstrap = FindObjectOfType<GameBootstrap>();
    }

    private void TryResolve()
    {
        _noti ??= _bootstrap != null ? _bootstrap.Services?.NotificationService : null;
    }

    // Hub buttons prove max3/cooldown behavior
    public void PushFive()
    {
        TryResolve();
        if (_noti == null) return;

        for (int i = 0; i < 5; i++)
        {
            _counter++;
            _noti.Push(
                key: "test_" + _counter,
                title: "TEST",
                body: "Msg #" + _counter,
                severity: NotificationSeverity.Info,
                payload: default,
                cooldownSeconds: 0f,
                dedupeByKey: false
            );
        }
    }

    public void Spam()
    {
        TryResolve();
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

    // Hub render
    public void DrawHubGUI()
    {
        TryResolve();
        if (_noti == null)
        {
            GUILayout.Label("NotificationService = null");
            return;
        }

        GUILayout.Label("Notifications (max 3, newest-first)");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Push 5 (test max3)", GUILayout.Width(180))) PushFive();
        if (GUILayout.Button("Spam (cooldown key)", GUILayout.Width(180))) Spam();
        GUILayout.EndHorizontal();

        var list = _noti.GetVisible();
        for (int i = 0; i < list.Count; i++)
        {
            var n = list[i];
            GUILayout.Label($"[{i}] {n.Severity} | {n.Title} | {n.Body} | key={n.Key}");
        }
    }

    // Standalone OnGUI disabled when hub enabled
    private void OnGUI()
    {
        if (SeasonalBastion.DebugTools.DebugHubState.Enabled || _hubControlled) return;
        DrawHubGUI();
    }
}
}
