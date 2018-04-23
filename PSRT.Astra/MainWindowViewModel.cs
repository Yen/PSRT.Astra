using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using GalaSoft.MvvmLight.Command;
using PropertyChanged;
using PSRT.Astra.Models;

namespace PSRT.Astra
{
    [AddINotifyPropertyChangedInterface]
    public class MainWindowViewModel
    {
        public RelayCommand VerifyCommand => new RelayCommand(async () => await VerifyAsync());
        public RelayCommand LaunchCommand => new RelayCommand(async () => await LaunchAsync());

        //

        [AddINotifyPropertyChangedInterface]
        public class LogEntry
        {
            public string Source { get; set; }
            public string Message { get; set; }
        }

        public InstallConfiguration InstallConfiguration { get; set; }
        public DownloadConfiguration DownloadConfiguration { get; set; }
        public PatchCache PatchCache { get; set; }

        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();

        private int _ActivityCount { get; set; } = 0;
        public bool Ready => _ActivityCount == 0 && DownloadConfiguration != null;

        //

        public MainWindowViewModel(string pso2BinDirectory)
        {
            InstallConfiguration = new InstallConfiguration(pso2BinDirectory);
        }

        public async Task InitializeAsync()
        {
            _ActivityCount += 1;

            await _CreateKeyDirectoriesAsync();

            Log("Init", "Fetching download configuration");
            DownloadConfiguration = await DownloadConfiguration.CreateDefaultAsync();

            Log("Init", "Connecting to patch cache database");
            PatchCache = await PatchCache.CreateAsync(InstallConfiguration);

            _ActivityCount -= 1;
        }

        public async Task VerifyAsync()
        {
            _ActivityCount += 1;

            await _CreateKeyDirectoriesAsync();

            Log("Verify", "Downloading patch list");

            var patches = await PatchInfo.FetchPatchInfosAsync(InstallConfiguration, DownloadConfiguration);

            Log("Verify", "Fetching patch cache data");

            var cacheData = await PatchCache.SelectAllAsync();

            Log("Verify", "Comparing game files");

            var toUpdate = new List<(string Name, (string Hash, Uri DownloadPath))>();

            await Task.Run(() =>
            {
                foreach (var patch in patches)
                {
                    var filePath = Path.Combine(InstallConfiguration.PSO2BinDirectory, patch.Key.Substring(0, patch.Key.Length - 4));
                    if (!File.Exists(filePath))
                    {
                        toUpdate.Add((patch.Key, patch.Value));
                        continue;
                    }

                    if (!cacheData.ContainsKey(patch.Key))
                    {
                        toUpdate.Add((patch.Key, patch.Value));
                        continue;
                    }

                    var cacheEntry = cacheData[patch.Key];
                    var info = new FileInfo(filePath);

                    if (info.LastWriteTimeUtc.ToFileTimeUtc() != cacheEntry.LastWriteTime)
                    {
                        toUpdate.Add((patch.Key, patch.Value));
                        continue;
                    }
                }
            });

            Log("Verify", $"{toUpdate.Count} files to update");
            if (toUpdate.Count == 0)
            {
                _DeleteCensorFile();

                Log("Verify", "All files verified");

                _ActivityCount -= 1;
                return;
            }

            //

            var atomicIndex = 0;
            var atomicProcessCount = Environment.ProcessorCount;
            var updateBucket = new ConcurrentQueue<List<PatchCacheEntry>>();

            //

            var processTasks = Enumerable.Range(0, atomicProcessCount).Select(i => Task.Run(async () =>
            {
                using (var md5 = MD5.Create())
                using (var client = new AquaHttpClient())
                {
                    var entries = new List<PatchCacheEntry>();
                    while (true)
                    {
                        await Task.Yield();

                        var index = Interlocked.Increment(ref atomicIndex) - 1;
                        if (index >= toUpdate.Count)
                        {
                            updateBucket.Enqueue(entries);
                            break;
                        }

                        if (entries.Count > 50)
                        {
                            updateBucket.Enqueue(entries);
                            entries = new List<PatchCacheEntry>();
                        }

                        var (name, info) = toUpdate[index];
                        var path = Path.Combine(InstallConfiguration.PSO2BinDirectory, name.Substring(0, name.Length - 4));

                        if (File.Exists(path))
                        {
                            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 4096, true))
                            {
                                var bytes = new byte[fs.Length];
                                await fs.ReadAsync(bytes, 0, bytes.Length);

                                var hashBytes = md5.ComputeHash(bytes);
                                var hashString = string.Concat(hashBytes.Select(b => b.ToString("X2")));

                                if (hashString == info.Hash)
                                {
                                    entries.Add(new PatchCacheEntry()
                                    {
                                        Name = name,
                                        Hash = info.Hash,
                                        LastWriteTime = new FileInfo(path).LastWriteTimeUtc.ToFileTimeUtc()
                                    });
                                    continue;
                                }
                            }
                        }

                        try
                        {
                            using (var responseStream = await client.GetStreamAsync(info.DownloadPath))
                            using (var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, true))
                                await responseStream.CopyToAsync(fs);
                        }
                        catch (Exception ex)
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() => Log("Verify Error", ex.Message));
                            continue;
                        }

                        entries.Add(new PatchCacheEntry()
                        {
                            Name = name,
                            Hash = info.Hash,
                            LastWriteTime = new FileInfo(path).LastWriteTimeUtc.ToFileTimeUtc()
                        });
                    }
                }

                Interlocked.Decrement(ref atomicProcessCount);
            }));

            //

            async Task CacheInsertLoopAsync()
            {
                while (atomicProcessCount > 0 || updateBucket.Count != 0)
                {
                    await Task.Delay(100);

                    if (updateBucket.Count == 0)
                        continue;

                    while (updateBucket.TryDequeue(out var list))
                    {
                        if (list.Count > 0)
                            await PatchCache.InsertUnderTransactionAsync(list);
                    }
                }
            }

            var logEntry = new LogEntry()
            {
                Source = "Verify"
            };

            async Task LogLoopAsync()
            {
                while (atomicProcessCount > 0)
                {
                    logEntry.Message = $"{Math.Max(0, toUpdate.Count - atomicIndex)} queued for update";
                    await Task.Delay(100);
                }
            }

            Log(logEntry);
            var logLoopTask = LogLoopAsync();
            var cacheInsertLoopTask = CacheInsertLoopAsync();

            await Task.WhenAll(processTasks);
            await logLoopTask;
            await cacheInsertLoopTask;

            // Rerun
            await VerifyAsync();

            Log("Verify", "All files verified");

            _ActivityCount -= 1;
        }

        private void _DeleteCensorFile()
        {
            _ActivityCount += 1;

            if (File.Exists(InstallConfiguration.CensorFile))
            {
                Log("Verify", "Removing censor file");
                File.Delete(InstallConfiguration.CensorFile);
            }

            _ActivityCount -= 1;
        }

        private async Task _CreateKeyDirectoriesAsync()
        {
            _ActivityCount += 1;

            Log("Info", "Creating key directories");

            await Task.Run(() =>
            {
                Directory.CreateDirectory(InstallConfiguration.PSO2BinDirectory);
                Directory.CreateDirectory(InstallConfiguration.DataDirectory);
                Directory.CreateDirectory(InstallConfiguration.DataLicenseDirectory);
                Directory.CreateDirectory(InstallConfiguration.DataWin32Directory);
                Directory.CreateDirectory(InstallConfiguration.DataWin32ScriptDirectory);
            });

            _ActivityCount -= 1;
        }

        public void Log(string source, string message)
        {
            Log(new LogEntry()
            {
                Source = source,
                Message = message
            });
        }

        public void Log(LogEntry entry)
        {
            LogEntries.Add(entry);
        }

        public async Task LaunchAsync()
        {
            _ActivityCount += 1;

            await _PerformArksLayerPatches();

            Log("Launch", "Starting PSO2");

            var startInfo = new ProcessStartInfo()
            {
                FileName = InstallConfiguration.PSO2Executable,
                Arguments = "+0x33aca2b9",
                UseShellExecute = false
            };
            startInfo.EnvironmentVariables["-pso2"] = "+0x01e3f1e9";

            await Task.Run(() =>
            {
                var process = new Process()
                {
                    StartInfo = startInfo
                };
                process.Start();

                process.WaitForExit();
            });

            Log("Launch", "PSO2 launch process ended");

            _ActivityCount -= 1;
        }

        private async Task _PerformArksLayerPatches()
        {
            _ActivityCount += 1;

            Log("ArksLayer", "Writing tweaker.bin file");

            using (var fs = new FileStream(InstallConfiguration.TweakerBin, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            using (var writer = new StreamWriter(fs))
            {
                var magic = _GenerateTweakerBin();
                await writer.WriteLineAsync(magic);
            }

            _ActivityCount -= 1;
        }

        private string _GenerateTweakerBin()
        {
            var key = "kW7eheKa7RMFXkbW7V5U";
            var hour = DateTime.Now.Hour.ToString(CultureInfo.InvariantCulture);
            var sanitizedDirectoryPath = InstallConfiguration.PSO2BinDirectory.Replace("://", ":/").Replace(@":\\", @":\");
            var directoryPathLength = sanitizedDirectoryPath.Length.ToString(CultureInfo.InvariantCulture);

            var combinedSeed = key + hour + directoryPathLength;
            var hashBytes = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(combinedSeed));

            var hexedStrings = hashBytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture));
            var hexed = string.Concat(hexedStrings);

            return hexed.ToLower(CultureInfo.InvariantCulture);
        }
    }
}
