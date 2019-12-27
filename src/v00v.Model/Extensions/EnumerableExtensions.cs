using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace v00v.Model.Extensions
{
    public static class EnumerableExtensions
    {
        #region Static Methods

        public static void AddRange<T>(this ConcurrentBag<T> bag, IEnumerable<T> toAdd)
        {
            toAdd.AsParallel().ForAll(bag.Add);
        }

        public static IEnumerable<T> OrderBySequence<T, TId>(this IEnumerable<T> source, IEnumerable<TId> order, Func<T, TId> idSelector)
        {
            var lookup = source.ToLookup(idSelector, t => t);
            foreach (var id in order)
            {
                foreach (var t in lookup[id])
                {
                    yield return t;
                }
            }
        }

        public static IEnumerable<IEnumerable<T>> Split<T>(this List<T> array, int size = 50)
        {
            for (var i = 0; i < (float)array.Count / size; i++)
            {
                yield return array.Skip(i * size).Take(size);
            }
        }

        #endregion
    }
}
