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
        public static Task RunOnDedicatedThreadAsync(Action action)
        {
            var source = new TaskCompletionSource<bool>();
            new Thread(() =>
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
            }).Start();
            return source.Task;
        }
    }
}
