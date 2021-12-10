using System;
using System.Threading;
using System.Threading.Tasks;

namespace CSharp.Parallelx.EventEx
{
    public static class EventUtils
    {
        public static Task ToTask (this WaitHandle waitHandle,
            CancellationToken cancelToken = default(CancellationToken), int timeout = -1)
        {
            var tcs = new TaskCompletionSource<object> ();

            RegisteredWaitHandle token = null;

            // There's a bug in RegisterWaitForSingleObject where calling token.Unregister on an AutoResetEvent can cause
            // subsequent invocations to trigger immediately. We'll leave it instead to the GC to do the job.

            var cancelTokenReg = cancelToken.Register (() => tcs.TrySetCanceled ());

            if (cancelToken.IsCancellationRequested) return tcs.Task;

            token = ThreadPool.RegisterWaitForSingleObject (
                waitHandle,
                (state, timedOut) =>
                {
                    cancelTokenReg.Dispose ();
                    if (timedOut) tcs.TrySetException (new TimeoutException ());
                    else tcs.TrySetResult (null);
                    GC.KeepAlive (token);
                },
                null,
                timeout,
                true);

            return tcs.Task;
        }
    }
}