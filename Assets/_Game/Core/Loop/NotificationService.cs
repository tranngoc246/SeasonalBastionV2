using SeasonalBastion.Contracts;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace SeasonalBastion
{
    /// <summary>
    /// NotificationService v2:
    /// - Toast (visible) capped to 3 (HUD)
    /// - Inbox (active/unread) keeps all until user marks read (panel)
    /// - Dedupe per (key + building + tower + extra)
    /// - Toast TTL uses Time.realtimeSinceStartup (unscaled)
    /// </summary>
    public sealed class NotificationService : INotificationService, ITickable
    {
        public int MaxVisible => 3;

        private const float DefaultTtlInfo = 3f;
        private const float DefaultTtlWarnOrError = 4.0f;

        private bool _muted = true;
        private float _muteUntilRealtime;

        private readonly IEventBus _bus;

        // HUD toast list (max 3)
        private readonly List<NotificationViewModel> _visible = new();

        // Cooldown gate per key
        private readonly Dictionary<string, float> _cooldownUntilByKey = new();
        private float _cooldownPruneAcc;

        // Inbox (active/unread list) + fast lookup by dedupeKey
        private sealed class InboxEntry
        {
            public string DedupeKey;
            public NotificationViewModel Vm;
            public float LastUpdatedAt;
        }

        private readonly List<InboxEntry> _inbox = new(); // newest-first
        private readonly Dictionary<string, InboxEntry> _inboxByKey = new();

        // cached view for UI
        private readonly List<NotificationViewModel> _inboxVmView = new();

        private int _nextId = 1;

        public event Action NotificationsChanged;

        public NotificationService(IEventBus bus)
        {
            _bus = bus;
            _muted = false;
            _muteUntilRealtime = 0f;
        }

        // ===== INotificationService (toast) =====
        public IReadOnlyList<NotificationViewModel> GetVisible() => _visible;

        // ===== Extra API (panel) =====
        public int GetInboxCount() => _inbox.Count;

        public IReadOnlyList<NotificationViewModel> GetInbox()
        {
            // Keep a stable list reference for UI; rebuild on demand (small list).
            _inboxVmView.Clear();
            for (int i = 0; i < _inbox.Count; i++)
                _inboxVmView.Add(_inbox[i].Vm);
            return _inboxVmView;
        }

        public void MarkRead(NotificationId id)
        {
            if (id.Value == 0) return;

            // remove from inbox
            for (int i = 0; i < _inbox.Count; i++)
            {
                var e = _inbox[i];
                if (e?.Vm != null && e.Vm.Id.Value == id.Value)
                {
                    _inbox.RemoveAt(i);
                    if (!string.IsNullOrEmpty(e.DedupeKey))
                        _inboxByKey.Remove(e.DedupeKey);
                    break;
                }
            }

            // also remove from toast if present
            for (int i = _visible.Count - 1; i >= 0; i--)
            {
                var vm = _visible[i];
                if (vm != null && vm.Id.Value == id.Value)
                    _visible.RemoveAt(i);
            }

            NotificationsChanged?.Invoke();
        }

        public void ClearInbox()
        {
            if (_inbox.Count == 0 && _visible.Count == 0) return;

            _inbox.Clear();
            _inboxByKey.Clear();
            _visible.Clear();

            NotificationsChanged?.Invoke();
        }

        // ===== Push =====
        public NotificationId Push(
            string key,
            string title,
            string body,
            NotificationSeverity severity,
            NotificationPayload payload,
            float cooldownSeconds = 3f,
            bool dedupeByKey = true)
        {
            float now = Time.realtimeSinceStartup;

            // Boot mute window
            if (_muted)
            {
                if (_muteUntilRealtime > 0f && now >= _muteUntilRealtime)
                {
                    _muted = false;
                }
                else
                {
                    // allow Errors during boot; swallow Info/Warning
                    if (severity < NotificationSeverity.Error)
                        return default;
                }
            }

            // Cooldown gate (per key) - keep original behavior
            if (!string.IsNullOrEmpty(key) && cooldownSeconds > 0f)
            {
                if (_cooldownUntilByKey.TryGetValue(key, out var until) && now < until)
                    return default;

                _cooldownUntilByKey[key] = now + cooldownSeconds;
            }

            string dedupeKey = dedupeByKey ? ComposeDedupeKey(key, payload) : null;

            // Dedupe in inbox (authoritative): update + move-to-top.
            // Fallback by raw key so a later deduped push can update an earlier non-deduped entry,
            // which is the legacy behavior expected by stability tests.
            InboxEntry existingEntry = null;
            if (!string.IsNullOrEmpty(dedupeKey))
            {
                _inboxByKey.TryGetValue(dedupeKey, out existingEntry);
                if (existingEntry == null)
                {
                    for (int i = 0; i < _inbox.Count; i++)
                    {
                        var e = _inbox[i];
                        if (e?.Vm != null && string.Equals(e.Vm.Key, key, StringComparison.Ordinal))
                        {
                            existingEntry = e;
                            break;
                        }
                    }
                }
            }

            if (existingEntry?.Vm != null)
            {
                var vm = existingEntry.Vm;
                vm.Key = key;
                vm.Title = title;
                vm.Body = body;
                vm.Severity = severity;
                vm.Payload = payload;

                // refresh toast TTL
                float ttl = ComputeDefaultTtl(severity);
                vm.ExpiresAt = ttl > 0f ? (now + ttl) : 0f;

                existingEntry.LastUpdatedAt = now;

                // move entry to top (newest-first)
                int idx = _inbox.IndexOf(existingEntry);
                if (idx > 0)
                {
                    _inbox.RemoveAt(idx);
                    _inbox.Insert(0, existingEntry);
                }

                // ensure it is shown as toast when panel hidden (toast list is just "latest 3")
                PromoteToToast(vm);

                NotificationsChanged?.Invoke();
                return vm.Id;
            }

            // New notification
            var id = new NotificationId(_nextId++);
            float ttlNew = ComputeDefaultTtl(severity);

            var vmNew = new NotificationViewModel
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

            // inbox entry
            var entry = new InboxEntry
            {
                DedupeKey = string.IsNullOrEmpty(dedupeKey) ? ("_id_" + id.Value) : dedupeKey,
                Vm = vmNew,
                LastUpdatedAt = now
            };

            _inbox.Insert(0, entry);
            _inboxByKey[entry.DedupeKey] = entry;

            // toast
            PromoteToToast(vmNew);

            NotificationsChanged?.Invoke();
            return id;
        }

        // ===== dismiss/clear for toast (interface) =====
        public void Dismiss(NotificationId id)
        {
            if (id.Value == 0) return;

            // Dismiss only toast (old behavior)
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
            // keep original meaning: clear toast only
            if (_visible.Count == 0) return;
            _visible.Clear();
            NotificationsChanged?.Invoke();
        }

        // ===== tick =====
        public void Tick(float dt)
        {
            // Toast expiry only (inbox does NOT auto-expire)
            if (_visible.Count > 0)
            {
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
            }

            // Prune cooldown table occasionally
            _cooldownPruneAcc += dt;
            if (_cooldownPruneAcc >= 10f && _cooldownUntilByKey.Count > 64)
            {
                _cooldownPruneAcc = 0f;

                float now2 = Time.realtimeSinceStartup;
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

        // ===== helpers =====

        private void PromoteToToast(NotificationViewModel vm)
        {
            // remove if already exists
            for (int i = 0; i < _visible.Count; i++)
            {
                if (_visible[i] != null && _visible[i].Id.Value == vm.Id.Value)
                {
                    if (i != 0)
                    {
                        _visible.RemoveAt(i);
                        _visible.Insert(0, vm);
                    }
                    CapToasts();
                    return;
                }
            }

            _visible.Insert(0, vm);
            CapToasts();
        }

        private void CapToasts()
        {
            while (_visible.Count > MaxVisible)
                _visible.RemoveAt(_visible.Count - 1);
        }

        private static string ComposeDedupeKey(string key, NotificationPayload payload)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            int b = payload.Building.Value;
            int t = payload.Tower.Value;

            // Global notifications (no building/tower) like "Can't place":
            // dedupe strictly by key to prevent spam.
            if (b == 0 && t == 0)
                return $"{key}|global";

            // Building/tower scoped notifications: include extra when present
            string x = payload.Extra ?? string.Empty;
            return $"{key}|b={b}|t={t}|x={x}";
        }

        private static float ComputeDefaultTtl(NotificationSeverity severity)
        {
            return severity >= NotificationSeverity.Warning ? DefaultTtlWarnOrError : DefaultTtlInfo;
        }

        public void SetMuted(bool muted)
        {
            _muted = muted;
            if (!muted) _muteUntilRealtime = 0f;
        }

        public void MuteForSeconds(float seconds)
        {
            _muted = true;
            _muteUntilRealtime = Time.realtimeSinceStartup + Mathf.Max(0f, seconds);
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
