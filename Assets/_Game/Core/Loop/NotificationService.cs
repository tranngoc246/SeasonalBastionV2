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
        private readonly List<NotificationViewModel> _visible = new();
        private readonly Dictionary<string, float> _cooldowns = new();
        private int _nextId = 1;

        public event Action NotificationsChanged;

        public NotificationService(IEventBus bus) { _bus = bus; }

        public NotificationId Push(string key, string title, string body, NotificationSeverity severity,
                                   NotificationPayload payload, float cooldownSeconds = 3f, bool dedupeByKey = true)
        {
            var now = Time.realtimeSinceStartup;

            // 1) Cooldown per key
            if (cooldownSeconds > 0f && _cooldowns.TryGetValue(key, out var lastAt))
            {
                if ((now - lastAt) < cooldownSeconds)
                {
                    return default;
                }
            }

            _cooldowns[key] = now;

            // 2) Dedupe by key: update notification and move to top
            if (dedupeByKey)
            {
                for (int i = 0; i < _visible.Count; i++)
                {
                    if (_visible[i].Key == key)
                    {
                        var existing = _visible[i];
                        existing.Title = title;
                        existing.Body = body;
                        existing.Severity = severity;
                        existing.Payload = payload;
                        existing.CreatedAt = now;

                        // move to top (newest-first)
                        _visible.RemoveAt(i);
                        _visible.Insert(0, existing);

                        NotificationsChanged?.Invoke();
                        return existing.Id;
                    }
                }
            }

            // 3) Create new
            var id = new NotificationId(_nextId++);
            var vm = new NotificationViewModel
            {
                Id = id,
                Key = key,
                Title = title,
                Body = body,
                Severity = severity,
                Payload = payload,
                CreatedAt = now
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

        public IReadOnlyList<NotificationViewModel> GetVisible() => _visible;
    }
}
