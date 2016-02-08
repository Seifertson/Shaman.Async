using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

#if SMALL_LIB_AWDEE
namespace Shaman.Runtime
#else
namespace Xamasoft
#endif
{
    /// <summary>
    /// Represents a context that only allows one asynchronous operation to run at a certain time, invalidating the previous calls.
    /// </summary>
    public class SingleTaskContext : IDisposable
    {
        private CancellationTokenSource cts;
        private bool disposed;

        /// <summary>
        /// Signals the start of a new operation, and invalidates the previously returned cancellation tokens.
        /// </summary>
        /// <returns>A cancellation token for the newly created operation.</returns>
        public CancellationToken StartNew()
        {
            if (disposed) throw new ObjectDisposedException("SingleTaskContext");
            CancelCurrent();
            cts = new CancellationTokenSource();
            return cts.Token;
        }

        /// <summary>
        /// Cancels the current operation.
        /// </summary>
        public void CancelCurrent()
        {
            if (cts != null) cts.Cancel();
        }

        /// <summary>
        /// Cancels the current operation and disposes the object.
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                if (cts != null) cts.Cancel();
                disposed = true;
            }
        }

    }
}
