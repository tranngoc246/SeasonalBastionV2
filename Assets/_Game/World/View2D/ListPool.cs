using System.Collections.Generic;

namespace SeasonalBastion.View2D
{
    internal static class ListPool<T>
    {
        private static readonly Stack<List<T>> _pool = new();

        public static List<T> Get()
        {
            return _pool.Count > 0 ? _pool.Pop() : new List<T>(64);
        }

        public static void Release(List<T> list)
        {
            if (list == null) return;
            list.Clear();
            _pool.Push(list);
        }
    }
}
