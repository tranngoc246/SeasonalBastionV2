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
