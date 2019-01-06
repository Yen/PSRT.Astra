using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT.Astra.ViewModels
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
                _CheckGameWatcher();

                await Task.Delay(1000);
            }
        }

        private void _CheckGameWatcher()
        {
            var processes = Process.GetProcessesByName("pso2");
            if (processes.Length > 0)
            {
                IsPSO2Running = true;
            }
            else
            {
                // set IsChangelogVisible to true if the state was changed
                // from running to not running
                if (IsPSO2Running == true)
                    IsChangelogVisible = true;
                IsPSO2Running = false;
            }
        }

        private void _DestroyGameWatcher()
        {
            _GameWatcherToken.Cancel();
        }
    }
}
