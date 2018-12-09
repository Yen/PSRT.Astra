using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT.Astra
{
    public partial class MainWindowViewModel
    {
        public bool IsPSO2Running { get; set; } = true;

        private CancellationTokenSource _GameWatcherToken = new CancellationTokenSource();

        private void _InitializeGameWatcher()
        {
            Task.Factory.StartNew(_GameWatcherLoopAsync, TaskCreationOptions.LongRunning);
        }

        private async Task _GameWatcherLoopAsync()
        {
            while (!_GameWatcherToken.IsCancellationRequested)
            {
                var processes = Process.GetProcessesByName("pso2");
                IsPSO2Running = processes.Length != 0;

                await Task.Delay(1000);
            }
        }

        private void _DestroyGameWatcher()
        {
            _GameWatcherToken.Cancel();
        }
    }
}
