using System;
using System.Collections.Generic;

namespace Homm.Client
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<Tuple<T, T>> GetBigramms<T>(this IEnumerable<T> items)
        {
            var prevItem = default(T);
            var firstItemTaken = false;
            foreach (var item in items)
            {
                if (!firstItemTaken)
                {
                    prevItem = item;
                    firstItemTaken = true;
                    continue;
                }

                yield return Tuple.Create(prevItem, item);
                prevItem = item;
            }
        }
    }
}
