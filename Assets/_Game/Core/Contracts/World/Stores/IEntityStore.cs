using System.Collections.Generic;

namespace SeasonalBastion.Contracts
{
    public interface IEntityStore<TId,TState>
    {
        bool Exists(TId id);
        TState Get(TId id);
        void Set(TId id, TState state);      // overwrite
        TId Create(TState state);
        void Destroy(TId id);

        int Count { get; }
        IEnumerable<TId> Ids { get; }
    }
}
