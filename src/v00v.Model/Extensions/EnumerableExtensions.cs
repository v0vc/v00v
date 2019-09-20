using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace v00v.Model.Extensions
{
    public static class EnumerableExtensions
    {
        #region Static Methods

        public static async Task ForEachAsyncConcurrent<T>(this IEnumerable<T> enumerable,
            Func<T, Task> action,
            int? maxDegreeOfParallelism = null)
        {
            if (maxDegreeOfParallelism.HasValue)
            {
                using (var semaphoreSlim = new SemaphoreSlim(maxDegreeOfParallelism.Value, maxDegreeOfParallelism.Value))
                {
                    var tasksWithThrottler = new List<Task>();

                    foreach (var item in enumerable)
                    {
                        // Increment the number of currently running tasks and wait if they are more than limit.
                        await semaphoreSlim.WaitAsync();

                        tasksWithThrottler.Add(Task.Run(async () =>
                        {
                            await action(item).ContinueWith(res =>
                            {
                                // action is completed, so decrement the number of currently running tasks
                                semaphoreSlim.Release();
                            });
                        }));
                    }

                    // Wait for all tasks to complete.
                    await Task.WhenAll(tasksWithThrottler.ToArray());
                }
            }
            else
            {
                await Task.WhenAll(enumerable.Select(action));
            }
        }

        public static IEnumerable<List<string>> SplitList(this List<string> ids, int nSize = 50)
        {
            var list = new List<List<string>>();

            for (int i = 0; i < ids.Count; i += nSize)
            {
                list.Add(ids.GetRange(i, Math.Min(nSize, ids.Count - i)));
            }

            return list;
        }

        #endregion
    }
}
