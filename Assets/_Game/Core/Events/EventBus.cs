// PATCH v0.1.3 — Minimal typed event bus
using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public void Publish<T>(T ev) where T : struct
        {
            if (_handlers.TryGetValue(typeof(T), out var list))
            {
                // copy to avoid modification during iteration
                var tmp = list.ToArray();
                for (int i = 0; i < tmp.Length; i++)
                    ((Action<T>)tmp[i])?.Invoke(ev);
            }
        }

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            var t = typeof(T);
            if (!_handlers.TryGetValue(t, out var list))
            {
                list = new List<Delegate>();
                _handlers[t] = list;
            }
            list.Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            var t = typeof(T);
            if (_handlers.TryGetValue(t, out var list))
                list.Remove(handler);
        }
    }
}
