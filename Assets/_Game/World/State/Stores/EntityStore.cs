using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    using System.Collections.Generic;

    public abstract class EntityStore<TId, TState> : IEntityStore<TId, TState>
    {
        protected readonly Dictionary<int, TState> _map = new();
        protected int _nextId = 1;

        public abstract int ToInt(TId id);
        public abstract TId FromInt(int v);

        public bool Exists(TId id) => _map.ContainsKey(ToInt(id));
        public TState Get(TId id) => _map[ToInt(id)];

        public void Set(TId id, TState state) => _map[ToInt(id)] = state;

        public TId Create(TState state)
        {
            var id = FromInt(_nextId++);
            _map[ToInt(id)] = state;
            return id;
        }

        public void Destroy(TId id) => _map.Remove(ToInt(id));

        public virtual void ClearAll()
        {
            _map.Clear();
            _nextId = 1;
        }

        public int Count => _map.Count;

        public IEnumerable<TId> Ids
        {
            get { foreach (var k in _map.Keys) yield return FromInt(k); }
        }
    }
}
