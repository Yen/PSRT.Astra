using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.Phases
{
    public class ComparePhase
    {
        private InstallConfiguration _InstallConfiguration;
        private DownloadConfiguration _DownloadConfiguration;
        private PatchCache _PatchCache;

        public ComparePhase(InstallConfiguration installConfiguration, DownloadConfiguration downloadConfiguration, PatchCache patchCache)
        {
            _InstallConfiguration = installConfiguration;
            _DownloadConfiguration = downloadConfiguration;
            _PatchCache = patchCache;
        }

        public async Task<List<(string Name, (string Hash, Uri DownloadPath))>> RunAsync(CancellationToken ct = default)
        {
            App.Current.Logger.Info(nameof(ComparePhase), "Fetching patches");
            var patches = await PatchInfo.FetchPatchInfosAsync(_InstallConfiguration, _DownloadConfiguration, ct);

            App.Current.Logger.Info(nameof(ComparePhase), "Fetching cache data");
            var cacheData = await _PatchCache.SelectAllAsync();

            App.Current.Logger.Info(nameof(ComparePhase), "Comparing file");

            var toUpdate = new List<(string Name, (string Hash, Uri DownloadPath))>();

            await Task.Run(() =>
            {
                foreach (var patch in patches)
                {
                    ct.ThrowIfCancellationRequested();

                    var relativeFilePath = patch.Key
                        .Substring(0, patch.Key.Length - 4)
                        .Replace('/', Path.DirectorySeparatorChar)
                        .ToLower();
                    var filePath = Path.Combine(_InstallConfiguration.PSO2BinDirectory, relativeFilePath);

                    // skip file if a file with the same name exists in the mods folder
                    if (Path.GetDirectoryName(filePath).ToLower() == _InstallConfiguration.DataWin32Directory.ToLower())
                    {
                        var modFilePath = Path.Combine(_InstallConfiguration.ModsDirectory, Path.GetFileName(relativeFilePath));
                        if (File.Exists(modFilePath))
                            continue;
                    }

                    if (!File.Exists(filePath))
                    {
                        toUpdate.Add((patch.Key, patch.Value));
                        continue;
                    }

                    // skip pso2.exe if the file is large address aware patched
                    if (Path.GetFileName(relativeFilePath) == Path.GetFileName(_InstallConfiguration.PSO2Executable)
                        && Properties.Settings.Default.LargeAddressAwareEnabled
                        && LargeAddressAware.IsLargeAddressAwarePactchApplied(_InstallConfiguration, patch.Value.Hash))
                    {
                        continue;
                    }


                    if (!cacheData.ContainsKey(patch.Key))
                    {
                        toUpdate.Add((patch.Key, patch.Value));
                        continue;
                    }

                    var cacheEntry = cacheData[patch.Key];

                    if (patch.Value.Hash != cacheEntry.Hash)
                    {
                        toUpdate.Add((patch.Key, patch.Value));
                        continue;
                    }

                    var info = new FileInfo(filePath);
                    if (info.LastWriteTimeUtc.ToFileTimeUtc() != cacheEntry.LastWriteTime)
                    {
                        toUpdate.Add((patch.Key, patch.Value));
                        continue;
                    }
                }
            });

            return toUpdate;
        }
    }
}
