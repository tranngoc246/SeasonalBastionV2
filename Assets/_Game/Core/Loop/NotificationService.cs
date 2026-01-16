// AUTO-GENERATED SKELETON TEMPLATE from PART 26 (LOCKED v0.1)
// Source: PART26_Concrete_Class_Skeletons_Scaffolds_LOCKED_SPEC_v0.1.md
// Notes: Runtime scaffolds only. Fill TODOs during implementation.

using UnityEngine;
using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class NotificationService : INotificationService
    {
        public int MaxVisible => 3;

        private readonly IEventBus _bus;
        private readonly System.Collections.Generic.List<NotificationViewModel> _visible = new();
        private readonly System.Collections.Generic.Dictionary<string, float> _cooldowns = new();
        private int _nextId = 1;

        public event System.Action NotificationsChanged;

        public NotificationService(IEventBus bus){ _bus = bus; }

        public NotificationId Push(string key, string title, string body, NotificationSeverity severity,
                                   NotificationPayload payload, float cooldownSeconds = 3f, bool dedupeByKey = true)
        {
            // TODO: cooldown + dedupe
            var id = new NotificationId(_nextId++);
            var vm = new NotificationViewModel{
                Id = id, Key = key, Title = title, Body = body, Severity = severity, Payload = payload,
                CreatedAt = UnityEngine.Time.realtimeSinceStartup
            };

            // newest first
            _visible.Insert(0, vm);
            if (_visible.Count > MaxVisible) _visible.RemoveAt(_visible.Count - 1);

            NotificationsChanged?.Invoke();
            return id;
        }

        public void Dismiss(NotificationId id)
        {
            _visible.RemoveAll(x => x.Id.Value == id.Value);
            NotificationsChanged?.Invoke();
        }

        public void ClearAll()
        {
            _visible.Clear();
            NotificationsChanged?.Invoke();
        }

        public System.Collections.Generic.IReadOnlyList<NotificationViewModel> GetVisible() => _visible;
    }
}
