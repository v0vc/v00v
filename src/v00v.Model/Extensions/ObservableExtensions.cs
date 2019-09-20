using System;
using System.Reactive.Linq;

namespace v00v.Model.Extensions
{
    public static class ObservableExtensions
    {
        #region Static Methods

        /// <summary>
        ///     Like SelectMany but ordered
        /// </summary>
        public static IObservable<TResult> SelectSeq<T, TResult>(this IObservable<T> observable, Func<T, IObservable<TResult>> selector)
        {
            return observable.Select(selector).Concat();
        }

        #endregion
    }
}
