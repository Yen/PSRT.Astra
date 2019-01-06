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

            var nextIndexAtomic = 0;
            var progressValueAtomic = 0;

            var errorTokenSource = new CancellationTokenSource();
            var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct, errorTokenSource.Token);

            App.Current.Logger.Info(nameof(ComparePhase), "Comparing files");
            Progress.IsIndeterminate = false;

            void ProcessLoop()
            {
                try
                {
                    while (true)
                    {
                        var index = Interlocked.Increment(ref nextIndexAtomic);
                        if (index >= workingPatches.Length)
                            return;

                        combinedTokenSource.Token.ThrowIfCancellationRequested();

                        try
                        {
                            ref var patch = ref workingPatches[index];

                            var relativeFilePath = patch.PatchInfo.Name
                                        .Substring(0, patch.PatchInfo.Name.Length - 4)
                                        .Replace('/', Path.DirectorySeparatorChar)
                                        .ToLower();
                            var filePath = Path.Combine(_InstallConfiguration.PSO2BinDirectory, relativeFilePath);

                            // skip this if mod files are not enabled so they are marked as invalid
                            if (Properties.Settings.Default.ModFilesEnabled)
                            {
                                // skip file if a file with the same name exists in the mods folder
                                if (Path.GetDirectoryName(filePath).ToLower() == _InstallConfiguration.DataWin32Directory.ToLower())
                                {
                                    var modFilePath = Path.Combine(_InstallConfiguration.ModsDirectory, Path.GetFileName(relativeFilePath));
                                    if (File.Exists(modFilePath))
                                        continue;
                                }
                            }

                            if (!File.Exists(filePath))
                            {
                                patch.ShouldUpdate = true;
                                continue;
                            }

                            // skip pso2.exe if the file is large address aware patched
                            if (Path.GetFileName(relativeFilePath) == Path.GetFileName(_InstallConfiguration.PSO2Executable)
                                && Properties.Settings.Default.LargeAddressAwareEnabled
                                && LargeAddressAware.IsLargeAddressAwarePactchApplied(_InstallConfiguration, patch.PatchInfo.Hash))
                            {
                                continue;
                            }

                            if (!cacheData.ContainsKey(patch.PatchInfo.Name))
                            {
                                patch.ShouldUpdate = true;
                                continue;
                            }

                            var cacheEntry = cacheData[patch.PatchInfo.Name];

                            if (patch.PatchInfo.Hash != cacheEntry.Hash)
                            {
                                patch.ShouldUpdate = true;
                                continue;
                            }

                            var info = new FileInfo(filePath);
                            if (info.LastWriteTimeUtc.ToFileTimeUtc() != cacheEntry.LastWriteTime)
                            {
                                patch.ShouldUpdate = true;
                                continue;
                            }
                        }
                        finally
                        {
                            Interlocked.Increment(ref progressValueAtomic);
                        }
                    }
                }
                catch
                {
                    errorTokenSource.Cancel();
                    throw;
                }
            }

            var tasks = Enumerable.Range(0, Environment.ProcessorCount)
                .Select(i => ConcurrencyUtils.RunOnDedicatedThreadAsync(ProcessLoop));
            var processTask = Task.WhenAll(tasks);

            while (await Task.WhenAny(processTask, Task.Delay(200)) != processTask)
                Progress.Progress = (progressValueAtomic / (double)workingPatches.Length);
            Progress.Progress = 1;

            await processTask;

            return workingPatches
                .Where(p => p.ShouldUpdate)
                .Select(p => p.PatchInfo)
                .ToArray();
        }
    }
}
