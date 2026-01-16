// PATCH v0.1.3 — Minimal notification service (no UI)
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class NotificationService : INotificationService
    {
        private readonly List<NotificationPayload> _stack = new();

        public int MaxShown => 3;

        public IReadOnlyList<NotificationPayload> Current => _stack;

        public void Push(NotificationPayload payload)
        {
            // newest first
            _stack.Insert(0, payload);
            if (_stack.Count > MaxShown) _stack.RemoveAt(_stack.Count - 1);
        }

        public void ClearAll() => _stack.Clear();
    }
}
