using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        private unsafe void _CheckGameWatcher()
        {
            uint bufferSize = 1024 * 1024 * 50;
            fixed (byte* buffer = new byte[bufferSize])
            {
                var processInfo = (NtBindings.SYSTEM_PROCESS_INFO*)buffer;

                // SEGA is hiding pso2 from the process list now by hooking
                // the system calls but they did a bad job
                if (!NtBindings.NT_SUCCESS(NtBindings.NtQuerySystemInformation(NtBindings.SYSTEM_INFORMATION_CLASS.SystemExtendedProcessInformation, processInfo, bufferSize, null)))
                    throw new Exception("Failed to query system information");

                while (processInfo->NextEntryOffset != 0)
                {
                    if (processInfo->ImageName.Buffer != null)
                    {
                        var processName = new string(processInfo->ImageName.Buffer, 0, processInfo->ImageName.Length / 2);
                        if (processName == "pso2.exe")
                        {
                            IsPSO2Running = true;
                            return;
                        }
                    }
                    processInfo = (NtBindings.SYSTEM_PROCESS_INFO*)((byte*)processInfo + processInfo->NextEntryOffset);
                }
                // set IsChangelogVisible to true if the state was changed
                // from running to not running
                if (IsPSO2Running)
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
