using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace SeasonalBastion
{
    public sealed class NotificationService : INotificationService, ITickable
    {
        public int MaxVisible => 3;

        private const float DefaultTtlInfo = 3f;
        private const float DefaultTtlWarnOrError = 4.0f;

        private readonly IEventBus _bus;
        private readonly List<NotificationViewModel> _visible = new();
        private readonly Dictionary<string, float> _cooldownUntilByKey = new();

        private float _cooldownPruneAcc;
        private int _nextId = 1;

        public event Action NotificationsChanged;

        public IReadOnlyList<NotificationViewModel> GetVisible() => _visible;

        public NotificationService(IEventBus bus) { _bus = bus; }

        public NotificationId Push(string key, string title, string body, NotificationSeverity severity,
                                   NotificationPayload payload, float cooldownSeconds = 3f, bool dedupeByKey = true)
        {
            var now = Time.realtimeSinceStartup;

            // Cooldown gate (per key)
            if (!string.IsNullOrEmpty(key) && cooldownSeconds > 0f)
            {
                if (_cooldownUntilByKey.TryGetValue(key, out var until) && now < until)
                    return default;

                _cooldownUntilByKey[key] = now + cooldownSeconds;
            }

            // Dedupe: update + move-to-top
            if (dedupeByKey && !string.IsNullOrEmpty(key))
            {
                for (int i = 0; i < _visible.Count; i++)
                {
                    var existing = _visible[i];
                    if (existing != null && existing.Key == key)
                    {
                        existing.Title = title;
                        existing.Body = body;
                        existing.Severity = severity;
                        existing.Payload = payload;

                        // Refresh TTL (auto-dismiss)
                        float ttl = ComputeDefaultTtl(severity);
                        existing.ExpiresAt = ttl > 0f ? (now + ttl) : 0f;

                        // Move to top (newest-first)
                        if (i != 0)
                        {
                            _visible.RemoveAt(i);
                            _visible.Insert(0, existing);
                        }

                        NotificationsChanged?.Invoke();
                        return existing.Id;
                    }
                }
            }

            // New notification
            var id = new NotificationId(_nextId++);
            float ttlNew = ComputeDefaultTtl(severity);

            var vm = new NotificationViewModel
            {
                Id = id,
                Key = key,
                Title = title,
                Body = body,
                Severity = severity,
                Payload = payload,
                CreatedAt = now,
                ExpiresAt = ttlNew > 0f ? (now + ttlNew) : 0f
            };

            _visible.Insert(0, vm);

            // Cap visible list
            while (_visible.Count > MaxVisible)
                _visible.RemoveAt(_visible.Count - 1);

            NotificationsChanged?.Invoke();
            return id;
        }

        public void Dismiss(NotificationId id)
        {
            if (id.Value == 0) return;

            for (int i = 0; i < _visible.Count; i++)
            {
                if (_visible[i] != null && _visible[i].Id.Value == id.Value)
                {
                    _visible.RemoveAt(i);
                    NotificationsChanged?.Invoke();
                    return;
                }
            }
        }

        public void ClearAll()
        {
            if (_visible.Count == 0) return;
            _visible.Clear();
            NotificationsChanged?.Invoke();
        }

        public void Tick(float dt)
        {
            if (_visible.Count == 0) return;

            float now = Time.realtimeSinceStartup;
            bool changed = false;

            for (int i = _visible.Count - 1; i >= 0; i--)
            {
                var vm = _visible[i];
                if (vm == null) continue;

                if (vm.ExpiresAt > 0f && now >= vm.ExpiresAt)
                {
                    _visible.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
                NotificationsChanged?.Invoke();

            // Day41: prune cooldown table occasionally to avoid unbounded growth
            _cooldownPruneAcc += dt;
            if (_cooldownPruneAcc >= 10f && _cooldownUntilByKey.Count > 64)
            {
                _cooldownPruneAcc = 0f;

                float now2 = Time.realtimeSinceStartup;
                // remove entries that are already expired for a while
                var toRemove = ListPool<string>.Get();
                foreach (var kv in _cooldownUntilByKey)
                {
                    if (kv.Value <= now2 - 1f)
                        toRemove.Add(kv.Key);
                }
                for (int i = 0; i < toRemove.Count; i++)
                    _cooldownUntilByKey.Remove(toRemove[i]);
                ListPool<string>.Release(toRemove);
            }
        }

        private static float ComputeDefaultTtl(NotificationSeverity severity)
        {
            return severity >= NotificationSeverity.Warning ? DefaultTtlWarnOrError : DefaultTtlInfo;
        }

        internal static class ListPool<T>
        {
            private static readonly Stack<List<T>> _pool = new Stack<List<T>>(8);

            public static List<T> Get()
            {
                if (_pool.Count > 0)
                {
                    var l = _pool.Pop();
                    l.Clear();
                    return l;
                }
                return new List<T>(16);
            }

            public static void Release(List<T> list)
            {
                if (list == null) return;
                if (_pool.Count >= 16) return;
                list.Clear();
                _pool.Push(list);
            }
        }
    }
}
