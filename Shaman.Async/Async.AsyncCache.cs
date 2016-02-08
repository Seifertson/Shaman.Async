using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Shaman.Runtime
{
    /// <summary>
    /// Provides a cache for asynchronous operations.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class AsyncCache<T>
    {

#if SALTARELLE
        private static readonly TimeSpan MaxValue = new TimeSpan(9223372036854775807L);
#else
        private static readonly TimeSpan MaxValue = TimeSpan.MaxValue;
#endif

        private DateTime _time;


        private TimeSpan _maxAge;
        private Task<T> _task;
        private Func<Task<T>> _function;

        /// <summary>
        /// Creates an asynchronous cache whose cached result expired after a certain time.
        /// </summary>
        /// <param name="func">The asynchronous function that calculates the value.</param>
        /// <param name="maxAge">The maximum age for the cached result.</param>
        public AsyncCache(Func<Task<T>> func, TimeSpan maxAge)
        {
            if (func == null) throw new ArgumentNullException("func");

            _function = func;
            _maxAge = maxAge;

        }


        /// <summary>
        /// Creates an asynchronous cache whose cached result expired after a certain time.
        /// </summary>
        /// <param name="func">The asynchronous function that calculates the value.</param>
        /// <param name="maxAgeMilliseconds">The maximum age for the cached result, in milliseconds.</param>
        public AsyncCache(Func<Task<T>> func, int maxAgeMilliseconds)
            : this(func, TimeSpan.FromMilliseconds(maxAgeMilliseconds))
        {
        }

        /// <summary>
        /// Creates an asynchronous that never expires once calculated, unless it is invalidated manually.
        /// </summary>
        /// <param name="func"></param>
        public AsyncCache(Func<Task<T>> func)
            : this(func, MaxValue)
        {
        }

        /// <summary>
        /// Invalidates the cache and starts preloading the new result.
        /// </summary>
        public void Reload()
        {
            _task = _function();
            _time = DateTime.UtcNow;
        }

        /// <summary>
        /// Starts preloading the result.
        /// </summary>
        public void Prefetch()
        {
            if (IsCached()) return;
            Reload();
        }

        /// <summary>
        /// Determines if the result is already available.
        /// </summary>
        /// <returns>Whether the result is already available or not.</returns>
        private bool IsCached()
        {
            if (_maxAge == MaxValue) return _task != null;

            if (_time.Ticks == 0) return false;

            var currentTime = DateTime.UtcNow;
            if (currentTime < _time) return false; // Date-time changed backwards, force update

            return currentTime - _time < _maxAge;

        }

        /// <summary>
        /// Asynchronously retrieves the result, potentially returning a cached version.
        /// </summary>
        /// <returns>The result.</returns>
        public Task<T> GetValueAsync()
        {
            if (!IsCached()) Reload();
            return _task;
        }

        /// <summary>
        /// Asynchronously retrieves the result, potentially returning a cached version. If the previous retrival failed, attempts a new one.
        /// </summary>
        /// <returns>The result.</returns>
        public Task<T> GetValueOrRetryAsync()
        {
            if ((_task != null && _task.IsFaulted) || !IsCached()) Reload();
            return _task;
        }

        /// <summary>
        /// Invalidates the cache.
        /// </summary>
        public void Invalidate()
        {
            _time = default(DateTime);
            _task = null;
        }


        /// <summary>
        /// Converts a cache to the underlying task, starting a new one if not already started.
        /// </summary>
        /// <param name="cache">The asynchronous cache.</param>
        /// <returns>The task.</returns>
        public static implicit operator Task<T>(AsyncCache<T> cache)
        {
            cache.Prefetch();
            return cache._task;
        }

    }
}
