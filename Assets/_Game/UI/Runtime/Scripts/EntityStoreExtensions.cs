using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    internal static class EntityStoreExtensions
    {
        public static bool TryGet<TId, TState>(this IEntityStore<TId, TState> store, TId id, out TState state)
        {
            if (store != null && store.Exists(id))
            {
                state = store.Get(id);
                return true;
            }

            state = default;
            return false;
        }
    }
}
