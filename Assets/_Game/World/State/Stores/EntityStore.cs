using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    using System.Collections.Generic;

    public abstract class EntityStore<TId, TState> : IEntityStore<TId, TState>
    {
        protected readonly Dictionary<int, TState> _map = new();
        protected readonly List<int> _ids = new();
        protected int _nextId = 1;

        public abstract int ToInt(TId id);
        public abstract TId FromInt(int v);

        public bool Exists(TId id) => _map.ContainsKey(ToInt(id));
        public TState Get(TId id) => _map[ToInt(id)];

        public void Set(TId id, TState state) => _map[ToInt(id)] = state;

        public TId Create(TState state)
        {
            var id = FromInt(_nextId++);
            var key = ToInt(id);
            _map[key] = state;

            _ids.Add(key);
            return id;
        }

        /// <summary>
        /// Create an entity with a specific id (used by Save/Load).
        /// Keeps deterministic order if caller inserts in sorted id order.
        /// Also updates _nextId to avoid collisions after load.
        /// </summary>
        public TId CreateWithId(TId id, TState state, bool overwriteIfExists = true)
        {
            var key = ToInt(id);

            if (!overwriteIfExists && _map.ContainsKey(key))
                return id;

            _map[key] = state;

            // keep insertion order deterministic; caller should insert sorted.
            if (!_ids.Contains(key))
                _ids.Add(key);

            // ensure next Create() won't collide
            if (key >= _nextId)
                _nextId = key + 1;

            return id;
        }

        public void Destroy(TId id)
        {
            var key = ToInt(id);
            if (_map.Remove(key))
                _ids.Remove(key);
        }

        public virtual void ClearAll()
        {
            _map.Clear();
            _ids.Clear();
            _nextId = 1;
        }

        public int Count => _map.Count;

        public IEnumerable<TId> Ids
        {
            get
            {
                for (int i = 0; i < _ids.Count; i++)
                    yield return FromInt(_ids[i]);
            }
        }
    }
}
