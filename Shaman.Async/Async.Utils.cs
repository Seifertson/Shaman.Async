using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#if NETFX_CORE
using Windows.Foundation;
#endif

#if SMALL_LIB_AWDEE
namespace Shaman
#else
namespace Xamasoft
#endif
{
    public static class AsyncExtensions
    {
        public static Task ForEachThrottledAsync<T>(this IEnumerable<T> data, Func<T, Task> taskFactory, int parallelismLevel)
        {
            return ForEachThrottledAsync(data, taskFactory, parallelismLevel, CancellationToken.None);
        }


#if SALTARELLE
        private class SemaphoreSlim : IDisposable
        {
            private int count;
            private Queue<Tuple<TaskCompletionSource<bool>, CancellationToken>> pendingRequests = new Queue<Tuple<TaskCompletionSource<bool>, CancellationToken>>();
            public SemaphoreSlim(int count)
            {
                this.count = count;
            }


            public void Dispose()
            {
                while (pendingRequests.Count != 0)
                {
                    var item = pendingRequests.Dequeue();
                    item.Item1.TrySetCanceled();
                }
            }

            public Task WaitAsync(CancellationToken ct)
            {
                var tcs = new TaskCompletionSource<bool>();
                pendingRequests.Enqueue(Tuple.Create(tcs, ct));
                return tcs.Task;
            }

            public void Release()
            {
                count++;
                while (pendingRequests.Count != 0)
                {
                    var item = pendingRequests.Dequeue();
                    if (item.Item2.IsCancellationRequested)
                    {
                        item.Item1.TrySetCanceled();
                        continue;
                    }
                    else
                    {
                        count--;
                        item.Item1.TrySetResult(true);
                        break;
                    }
                }
            }
        }

#endif



        public async static Task ForEachThrottledAsync<T>(this IEnumerable<T> data, Func<T, Task> taskFactory, int parallelismLevel, CancellationToken cancellationToken)
        {
            if (parallelismLevel == 1)
            {
                foreach (var item in data)
                {
                    await taskFactory(item);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                return;
            }
            var allTasks = new HashSet<Task>();
            Exception exception = null;
            using (var throttler = new SemaphoreSlim(parallelismLevel))
            {
                foreach (var item in data)
                {
#if NETFX_35 || ANDROID || NETFX_40
                    await TaskEx.Run(() => throttler.Wait(cancellationToken))
#else
                    await throttler.WaitAsync(cancellationToken)
#endif
#if !SALTARELLE
.ConfigureAwait(true)
#endif
;
                    if (exception != null) throw new AggregateException(new Exception[] { exception });
                    try
                    {
                        Task currentTask = null;
                        bool completedSynchronously = false;
                        currentTask = (new Func<Task>(async () =>
                        {
                            try
                            {
                                await taskFactory(item);
                                if (currentTask == null) completedSynchronously = true;
                                else allTasks.Remove(currentTask);
                            }
                            catch (Exception ex)
                            {
                                exception = ex;
                            }
                            finally
                            {
                                if (!cancellationToken.IsCancellationRequested)
                                {
                                    try
                                    {
                                        throttler.Release();
                                    }
                                    catch (ObjectDisposedException) { }
                                }
                                
                            }
                        }))();
                        if(!completedSynchronously) allTasks.Add(currentTask);
                    }
                    catch
                    {
                        throttler.Release();
                        throw;
                    }
                }
#if NETFX_35 || ANDROID || NETFX_40
                await TaskEx.WhenAll(allTasks);
#else
                await Task.WhenAll(allTasks);
#endif
            }
        }



        public static Task WhenAnyThrottled<TInput>(this IEnumerable<TInput> data, Func<TInput, Task> taskFactory, int parallelismLevel)
        {
            return WhenAnyThrottled(data, async (x, c) =>
            {
                await taskFactory(x);
                return true;
            }, parallelismLevel, CancellationToken.None);
        }

        public static Task WhenAnyThrottled<TInput>(this IEnumerable<TInput> data, Func<TInput, CancellationToken, Task> taskFactory, int parallelismLevel)
        {
            return WhenAnyThrottled(data, taskFactory, parallelismLevel, CancellationToken.None);
        }


        public static Task WhenAnyThrottled<TInput>(this IEnumerable<TInput> data, Func<TInput, CancellationToken, Task> taskFactory, int parallelismLevel, CancellationToken cancellationToken)
        {
            return WhenAnyThrottled<TInput, bool>(data, async (x, c) =>
            {
                await taskFactory(x, c);
                return true;
            }, parallelismLevel, cancellationToken);
        }


        public static Task<TResult> WhenAnyThrottled<TInput, TResult>(this IEnumerable<TInput> data, Func<TInput, Task<TResult>> taskFactory, int parallelismLevel)
        {
            return WhenAnyThrottled(data, (x, c) => taskFactory(x), parallelismLevel, CancellationToken.None);
        }

        public static Task<TResult> WhenAnyThrottled<TInput, TResult>(this IEnumerable<TInput> data, Func<TInput, CancellationToken, Task<TResult>> taskFactory, int parallelismLevel)
        {
            return WhenAnyThrottled(data, taskFactory, parallelismLevel, CancellationToken.None);
        }


        public static Task<TResult> WhenAnyThrottled<TInput, TResult>(this IEnumerable<TInput> data, Func<TInput, CancellationToken, Task<TResult>> taskFactory, int parallelismLevel, CancellationToken cancellationToken)
        {
            var cts = new CancellationTokenSource();
            if (cancellationToken.CanBeCanceled) cancellationToken.Register(() => cts.Cancel());
            var tcs = new TaskCompletionSource<TResult>();
            Exception exception = null;
            TResult result;
            bool hasResult = false;
            var token = cts.Token;
            var foreachTask = ForEachThrottledAsync(data, async x =>
            {
                try
                {
                    result = await taskFactory(x, token);
                    tcs.TrySetResult(result);
                    hasResult = true;
                    cts.Cancel();
                }
                catch (Exception ex)
                {
                    if (exception == null) exception = ex;
                }
            }, parallelismLevel, token);

            var foreachAwaiter = foreachTask.GetAwaiter();
            foreachAwaiter.OnCompleted(() =>
            {
                if (hasResult) return;
                try
                {
                    foreachAwaiter.GetResult();
                }
                catch (OperationCanceledException)
                {
                    tcs.TrySetCanceled();
                }
                if (exception != null) tcs.TrySetException(new AggregateException(new Exception[] { exception }));
            });

            return tcs.Task;
        }





    }
}
