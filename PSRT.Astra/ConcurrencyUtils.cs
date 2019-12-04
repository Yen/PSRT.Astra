using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT.Astra
{
    public static class ConcurrencyUtils
    {
        public static Task RunOnDedicatedThreadAsync(Action action, string threadName = null)
        {
            var source = new TaskCompletionSource<bool>();
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                    source.SetResult(true);
                }
                catch (OperationCanceledException)
                {
                    source.SetCanceled();
                }
                catch (Exception ex)
                {
                    source.SetException(ex);
                }
            });
            if (!string.IsNullOrWhiteSpace(threadName))
                thread.Name = threadName;
            thread.Start();
            return source.Task;
        }
    }
}
