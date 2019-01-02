using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PSRT.Astra.Models.Phases
{
    public class ComparePhase
    {
        public CompareProgress Progress { get; } = new CompareProgress();

        private struct UpdateInfo
        {
            public PatchInfo PatchInfo;
            public bool ShouldUpdate;
        }

        private InstallConfiguration _InstallConfiguration;

        public ComparePhase(InstallConfiguration installConfiguration)
        {
            _InstallConfiguration = installConfiguration;
        }

        public async Task<PatchInfo[]> RunAsync(DownloadConfiguration downloadConfiguration, PatchCache patchCache, CancellationToken ct = default)
        {
            App.Current.Logger.Info(nameof(ComparePhase), "Fetching patches");
            var patches = await PatchInfo.FetchPatchInfosAsync(_InstallConfiguration, downloadConfiguration, ct);

            App.Current.Logger.Info(nameof(ComparePhase), "Fetching cache data");
            var cacheData = await patchCache.SelectAllAsync();

            var workingPatches = patches
                .Select(p => new UpdateInfo
                {
                    PatchInfo = p,
                    ShouldUpdate = false
                })
                .ToArray();

            var progressValueAtomic = 0;
            
            App.Current.Logger.Info(nameof(ComparePhase), "Comparing files");
            Progress.IsIndeterminate = false;
            var processTask = Task.Factory.StartNew(() =>
            {
                var parallelOptions = new ParallelOptions
                {
                    CancellationToken = ct
                };
                var parallelResult = Parallel.For(
                    0,
                    workingPatches.Length,
                    parallelOptions,
                    index =>
                    {
                        try
                        {
                            ref var patch = ref workingPatches[index];

                            ct.ThrowIfCancellationRequested();

                            var relativeFilePath = patch.PatchInfo.Name
                                .Substring(0, patch.PatchInfo.Name.Length - 4)
                                .Replace('/', Path.DirectorySeparatorChar)
                                .ToLower();
                            var filePath = Path.Combine(_InstallConfiguration.PSO2BinDirectory, relativeFilePath);

                            // skip file if a file with the same name exists in the mods folder
                            if (Path.GetDirectoryName(filePath).ToLower() == _InstallConfiguration.DataWin32Directory.ToLower())
                            {
                                var modFilePath = Path.Combine(_InstallConfiguration.ModsDirectory, Path.GetFileName(relativeFilePath));
                                if (File.Exists(modFilePath))
                                    return;
                            }

                            if (!File.Exists(filePath))
                            {
                                patch.ShouldUpdate = true;
                                return;
                            }

                            // skip pso2.exe if the file is large address aware patched
                            if (Path.GetFileName(relativeFilePath) == Path.GetFileName(_InstallConfiguration.PSO2Executable)
                                && Properties.Settings.Default.LargeAddressAwareEnabled
                                && LargeAddressAware.IsLargeAddressAwarePactchApplied(_InstallConfiguration, patch.PatchInfo.Hash))
                            {
                                return;
                            }

                            if (!cacheData.ContainsKey(patch.PatchInfo.Name))
                            {
                                patch.ShouldUpdate = true;
                                return;
                            }

                            var cacheEntry = cacheData[patch.PatchInfo.Name];

                            if (patch.PatchInfo.Hash != cacheEntry.Hash)
                            {
                                patch.ShouldUpdate = true;
                                return;
                            }

                            var info = new FileInfo(filePath);
                            if (info.LastWriteTimeUtc.ToFileTimeUtc() != cacheEntry.LastWriteTime)
                            {
                                patch.ShouldUpdate = true;
                                return;
                            }
                        }
                        finally
                        {
                            Interlocked.Increment(ref progressValueAtomic);
                        }
                    });
            }, TaskCreationOptions.LongRunning);

            await Task.Factory.StartNew(() =>
            {
                while (!processTask.Wait(100))
                    Progress.Progress = (progressValueAtomic / (double)workingPatches.Length);
                Progress.Progress = 100.0;
            }, TaskCreationOptions.LongRunning);


            return workingPatches
                .Where(p => p.ShouldUpdate)
                .Select(p => p.PatchInfo)
                .ToArray();
        }
    }
}
