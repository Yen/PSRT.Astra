using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using GalaSoft.MvvmLight.Command;
using Microsoft.Win32;
using Newtonsoft.Json;
using PropertyChanged;
using PSRT.Astra.Models;
using PSRT.Astra.Models.ArksLayer;
using SharpCompress.Archives.Rar;

namespace PSRT.Astra
{
    [AddINotifyPropertyChangedInterface]
    public partial class MainWindowViewModel
    {
        public RelayCommand VerifyGameFilesCommand => new RelayCommand(async () => await VerifyGameFilesAsync());
        public RelayCommand LaunchCommand => new RelayCommand(async () => await LaunchAsync());
        public RelayCommand ResetGameGuardCommand => new RelayCommand(async () => await ResetGameGuardAsync());

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

        public bool ArksLayerEnglishPatchEnabled { get; set; } = Properties.Settings.Default.EnglishPatchEnabled;
        public bool ArksLayerTelepipeProxyEnabled { get; set; } = Properties.Settings.Default.TelepipeProxyEnabled;

        public string LaunchPSO2ButtonLocaleKey => IsPSO2Running ? "MainWindow_PSO2Running" : "MainWindow_LaunchPSO2";

        private int _ActivityCount { get; set; } = 0;
        public bool Ready => _ActivityCount == 0 && DownloadConfiguration != null && !IsPSO2Running;

        //

        public MainWindowViewModel(string pso2BinDirectory)
        {
            InstallConfiguration = new InstallConfiguration(pso2BinDirectory);
        }

        public async Task InitializeAsync()
        {
            _ActivityCount += 1;

            Log("Astra", $"Game directory set to {Properties.Settings.Default.LastSelectedInstallLocation}");

            // start update in the background
            _CheckForUpdate();

            _InitializeGameWatcher();

            await _CreateKeyDirectoriesAsync();

            Log("Init", "Fetching download configuration");
            DownloadConfiguration = await DownloadConfiguration.CreateDefaultAsync();

            Log("Init", "Connecting to patch cache database");
            PatchCache = await PatchCache.CreateAsync(InstallConfiguration);

            _ActivityCount -= 1;
        }

        public Task DestroyAsync()
        {
            _DestroyGameWatcher();

            return Task.CompletedTask;
        }

        public async Task<bool> CanOpenSettingsAsync()
        {
            _ActivityCount += 1;

            var userFileExists = await Task.Run(() => File.Exists(InstallConfiguration.PSO2DocumentsUserFile));
            if (!userFileExists)
                Log("Error", "User settings file does not exists, please run the game once to generate it");

            _ActivityCount -= 1;
            return userFileExists;
        }

        public async Task VerifyGameFilesAsync()
        {
            _ActivityCount += 1;

            var logSource = "Verify PSO2";

            Log(logSource, "Downloading patch list");

            var patches = await PatchInfo.FetchPatchInfosAsync(InstallConfiguration, DownloadConfiguration);

            await VerifyAsync(logSource, patches);

            Log(logSource, "All files verified");

            _ActivityCount -= 1;
        }

        public async Task VerifyAsync(string logSource, Dictionary<string, (string Hash, Uri DownloadPath)> patches)
        {
            _ActivityCount += 1;

            await _CreateKeyDirectoriesAsync();

            var modFiles = await Task.Run(() => Directory.GetFiles(InstallConfiguration.ModsDirectory)
                .Select(p => Path.GetFileName(p))
                .ToArray());

            if (modFiles.Length > 0)
            {
                Log(logSource, $"Copying {modFiles.Length} mod file{(modFiles.Length == 1 ? string.Empty : "s")}");

                await Task.Run(() =>
                {
                    foreach (var file in modFiles)
                    {
                        var dataPath = Path.Combine(InstallConfiguration.DataWin32Directory, file);
                        File.Delete(dataPath);
                        File.Copy(Path.Combine(InstallConfiguration.ModsDirectory, file), dataPath, true);
                    }
                });
            }

            Log(logSource, "Fetching patch cache data");

            var cacheData = await PatchCache.SelectAllAsync();

            Log(logSource, "Comparing files");

            var toUpdate = new List<(string Name, (string Hash, Uri DownloadPath))>();

            await Task.Run(() =>
            {
                foreach (var patch in patches)
                {
                    var relativeFilePath = patch.Key
                        .Substring(0, patch.Key.Length - 4)
                        .Replace('/', Path.DirectorySeparatorChar)
                        .ToLower();
                    var filePath = Path.Combine(InstallConfiguration.PSO2BinDirectory, relativeFilePath);

                    // skip file if a file with the same name exists in the mods folder
                    if (Path.GetDirectoryName(filePath).ToLower() == InstallConfiguration.DataWin32Directory.ToLower())
                    {
                        var modFilePath = Path.Combine(InstallConfiguration.ModsDirectory, Path.GetFileName(relativeFilePath));
                        if (File.Exists(modFilePath))
                            continue;
                    }

                    if (!File.Exists(filePath))
                    {
                        toUpdate.Add((patch.Key, patch.Value));
                        continue;
                    }

                    // skip pso2.exe if the file is large address aware patched
                    if (Path.GetFileName(relativeFilePath) == Path.GetFileName(InstallConfiguration.PSO2Executable)
                        && Properties.Settings.Default.LargeAddressAwareEnabled
                        && LargeAddressAware.IsLargeAddressAwarePactchApplied(InstallConfiguration, patch.Value.Hash))
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

            Log(logSource, $"{toUpdate.Count} files to update");
            if (toUpdate.Count == 0)
            {
                _DeleteCensorFile();

                _ActivityCount -= 1;
                return;
            }

            //

            var atomicIndex = 0;
            var atomicProcessCount = Environment.ProcessorCount;
            var updateBucket = new ConcurrentQueue<List<PatchCacheEntry>>();

            //

            var processTasks = Enumerable.Range(0, atomicProcessCount).Select(i => Task.Factory.StartNew(async () =>
            {
                using (var md5 = MD5.Create())
                using (var client = new AquaHttpClient())
                {
                    const int streamingFileSize = 10 * 1024 * 1024; // 10MB
                    byte[] bufferBytes = new byte[streamingFileSize];

                    var entries = new List<PatchCacheEntry>();
                    while (true)
                    {
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
                            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                            {
                                // streaming the file into the hash is considerably slower than 
                                // reading into a byte array first. It does however avoid possible
                                // out of memory errors with giant files.
                                // As such we only stream the file if its bigger than the 
                                // specified file size
                                byte[] hashBytes;
                                if (fs.Length > streamingFileSize)
                                {
                                    hashBytes = md5.ComputeHash(fs);
                                }
                                else
                                {
                                    fs.Read(bufferBytes, 0, (int)fs.Length);
                                    hashBytes = md5.ComputeHash(bufferBytes, 0, (int)fs.Length);
                                }

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
                            Directory.CreateDirectory(Path.GetDirectoryName(path));
                            using (var responseStream = await client.GetStreamAsync(info.DownloadPath))
                            using (var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, true))
                                await responseStream.CopyToAsync(fs);
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() => Log(logSource, $"Error: {ex.Message}"));
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
            }, TaskCreationOptions.LongRunning));

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
                Source = logSource
            };

            async Task LogLoopAsync()
            {
                var perSecondQueue = new Queue<double>();

                var lastRemainingUpdates = toUpdate.Count;
                var lastTime = DateTime.UtcNow;

                var updateSpeed = 1.0;

                while (atomicProcessCount > 0)
                {
                    await Task.Delay(100);

                    var remainingUpdates = toUpdate.Count - atomicIndex;

                    if (DateTime.UtcNow - TimeSpan.FromSeconds(5) > lastTime)
                    {
                        var updateCount = lastRemainingUpdates - remainingUpdates;

                        if (updateCount > 0)
                            perSecondQueue.Enqueue(updateCount / (DateTime.UtcNow - lastTime).TotalSeconds);
                        lastRemainingUpdates = remainingUpdates;
                        lastTime = DateTime.UtcNow;

                        while (perSecondQueue.Count > 20)
                            perSecondQueue.Dequeue();

                        updateSpeed = perSecondQueue.Sum() / perSecondQueue.Count;
                    }

                    var estimatedSeconds = remainingUpdates / updateSpeed;
                    var estimatedMinutes = (int)Math.Round(TimeSpan.FromSeconds(estimatedSeconds).TotalMinutes);

                    var message = $"{Math.Max(0, remainingUpdates)} queued for update";
                    if (estimatedMinutes > 1)
                        message += $": ~{estimatedMinutes} minutes";

                    logEntry.Message = message;
                }
            }

            Log(logEntry);
            var logLoopTask = LogLoopAsync();
            var cacheInsertLoopTask = CacheInsertLoopAsync();

            await Task.WhenAll(processTasks);
            await logLoopTask;
            await cacheInsertLoopTask;

            // Rerun
            Log(logSource, "Rerunning verify task to check for intermediate changes");
            await VerifyAsync(logSource, patches);

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
                Directory.CreateDirectory(InstallConfiguration.ArksLayer.PluginsDirectory);
                Directory.CreateDirectory(InstallConfiguration.ArksLayer.PluginsDisabledDirectory);
                Directory.CreateDirectory(InstallConfiguration.ArksLayer.PatchesDirectory);
                Directory.CreateDirectory(InstallConfiguration.ModsDirectory);
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

            Log("Launch", "Saving client settings");
            await Task.Run(() =>
            {
                Properties.Settings.Default.EnglishPatchEnabled = ArksLayerEnglishPatchEnabled;
                Properties.Settings.Default.TelepipeProxyEnabled = ArksLayerTelepipeProxyEnabled;
                Properties.Settings.Default.Save();
            });

            if (await _PerformArksLayerPatches() == false)
            {
                Log("Launch", "Launch canceled due to error");
                _ActivityCount -= 1;
                return;
            }

            if (Properties.Settings.Default.LargeAddressAwareEnabled)
            {
                Log("Launch", "Applying large address aware patch");
                await Task.Run(() => LargeAddressAware.ApplyLargeAddressAwarePatch(InstallConfiguration));
            }

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

        private async Task<bool> _PerformArksLayerPatches()
        {
            _ActivityCount += 1;

            try
            {
                Log("ArksLayer", "Downloading plugin info");

                var pluginInfo = await PluginInfo.FetchAsync();

                async Task ValidateFileAsync(string filePath, PluginInfo.PluginEntry entry)
                {
                    if (File.Exists(filePath))
                    {
                        using (var md5 = MD5.Create())
                        using (var fs = File.OpenRead(filePath))
                        {
                            var hashBytes = md5.ComputeHash(fs);
                            var hash = string.Concat(hashBytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
                            if (hash == entry.Hash)
                                return;
                        }
                    }

                    File.Delete(filePath);
                    using (var client = new ArksLayerHttpClient())
                    using (var ns = await client.GetStreamAsync(new Uri(DownloadConfiguration.PluginsRoot, entry.FileName)))
                    using (var fs = File.Create(filePath, 4096, FileOptions.Asynchronous))
                    {
                        await ns.CopyToAsync(fs);
                    }
                }

                if (ArksLayerEnglishPatchEnabled || ArksLayerTelepipeProxyEnabled)
                {
                    Log("ArksLayer", "Validating core Arks-Layer components");
                    App.Current.Logger.Info("Validating core Arks-Layer components");

                    await ValidateFileAsync(InstallConfiguration.ArksLayer.DDrawDll, pluginInfo.DDrawDll);
                    await ValidateFileAsync(InstallConfiguration.ArksLayer.PSO2hDll, pluginInfo.PSO2hDll);

                    Log("ArksLayer", "Writing tweaker.bin file");

                    using (var fs = new FileStream(InstallConfiguration.ArksLayer.TweakerBin, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                    using (var writer = new StreamWriter(fs))
                    {
                        var magic = TweakerBin.GenerateFileContents(InstallConfiguration.PSO2BinDirectory);
                        await writer.WriteLineAsync(magic);
                    }
                }
                else
                {
                    App.Current.Logger.Info("Deleting core Arks-Layer components");

                    File.Delete(InstallConfiguration.ArksLayer.TweakerBin);
                    File.Delete(InstallConfiguration.ArksLayer.DDrawDll);
                    File.Delete(InstallConfiguration.ArksLayer.PSO2hDll);
                }

                if (ArksLayerTelepipeProxyEnabled)
                {
                    using (var client = new HttpClient())
                    {
                        var proxyUrl = Properties.Settings.Default.TelepipeProxyUrl;
                        bool isCustomProxy = !string.IsNullOrWhiteSpace(proxyUrl);
                        string url = isCustomProxy ? proxyUrl : "http://telepipe.io/config.json";

                        if (isCustomProxy)
                        {
                            Log("ArksLayer", $"Downloading proxy information from {proxyUrl}");
                        }
                        else
                        {
                            Log("ArksLayer", "Downloading Telepipe proxy information");
                        }
                        var configString = await client.GetStringAsync(url);
                        var config = JsonConvert.DeserializeObject<ProxyInfo>(configString);
                        var publicKey = await client.GetByteArrayAsync(config.PublicKeyUrl);

                        Log("ArksLayer", "Writing Telepipe proxy config files");
                        await Task.Run(() =>
                        {
                            File.WriteAllText(InstallConfiguration.ArksLayer.TelepipeProxyConfig, config.Host);
                            File.WriteAllBytes(InstallConfiguration.ArksLayer.TelepipeProxyPublicKey, publicKey);
                        });
                    }

                    await ValidateFileAsync(InstallConfiguration.ArksLayer.PluginTelepipeProxyDll, pluginInfo.TelepipeProxyDll);
                }
                else
                {
                    File.Delete(InstallConfiguration.ArksLayer.PluginTelepipeProxyDll);
                }

                if (ArksLayerEnglishPatchEnabled)
                {
                    Log("ArksLayer", "Downloading english translation information");
                    var translation = await TranslationInfo.FetchEnglishAsync();

                    Log("ArksLayer", "Verifying english patch files");
                    var cacheData = await PatchCache.SelectAllAsync();

                    string CreateRelativePath(string path)
                    {
                        var root = new Uri(InstallConfiguration.PSO2BinDirectory);
                        var relative = root.MakeRelativeUri(new Uri(path));
                        return relative.OriginalString;
                    }

                    bool Verify(string path, string hash)
                    {
                        var relative = CreateRelativePath(path);

                        var info = new FileInfo(path);
                        if (info.Exists == false)
                            return false;

                        if (cacheData.ContainsKey(relative) == false)
                            return false;

                        var data = cacheData[relative];

                        if (data.Hash != hash)
                            return false;

                        if (data.LastWriteTime != info.LastWriteTimeUtc.ToFileTimeUtc())
                            return false;

                        return true;
                    }

                    using (var client = new ArksLayerHttpClient())
                    {
                        async Task VerifyAndDownlodRar(string path, string downloadHash, Uri downloadPath)
                        {
                            if (Verify(path, downloadHash) == false)
                            {
                                Log("ArksLayer", $"Downloading \"{Path.GetFileName(downloadPath.LocalPath)}\"");
                                using (var response = await client.GetAsync(downloadPath))
                                using (var stream = await response.Content.ReadAsStreamAsync())
                                using (var archive = RarArchive.Open(stream))
                                {
                                    if (archive.Entries.Count > 0)
                                    {
                                        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                                        {
                                            await archive.Entries.First().OpenEntryStream().CopyToAsync(fs);
                                        }
                                        await PatchCache.InsertUnderTransactionAsync(new[] { new PatchCacheEntry()
                                        {
                                            Name = CreateRelativePath(path),
                                            Hash = downloadHash,
                                            LastWriteTime = new FileInfo(path).LastWriteTimeUtc.ToFileTimeUtc()
                                        }});
                                    }
                                }
                            }
                        }

                        await VerifyAndDownlodRar(InstallConfiguration.ArksLayer.EnglishBlockPatch, translation.BlockMD5, new Uri(translation.BlockPatch));
                        await VerifyAndDownlodRar(InstallConfiguration.ArksLayer.EnglishItemPatch, translation.ItemMD5, new Uri(translation.ItemPatch));
                        await VerifyAndDownlodRar(InstallConfiguration.ArksLayer.EnglishTextPatch, translation.TextMD5, new Uri(translation.TextPatch));
                        await VerifyAndDownlodRar(InstallConfiguration.ArksLayer.EnglishTitlePatch, translation.TitleMD5, new Uri(translation.TitlePatch));

                        if (Verify(InstallConfiguration.ArksLayer.EnglishRaiserPatch, translation.RaiserMD5) == false)
                        {
                            Log("ArksLayer", $"Downloading \"{Path.GetFileName(new Uri(translation.RaiserPatch).LocalPath)}\"");
                            using (var stream = await client.GetStreamAsync(translation.RaiserPatch))
                            using (var fs = new FileStream(InstallConfiguration.ArksLayer.EnglishRaiserPatch, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                await stream.CopyToAsync(fs);
                            }
                            await PatchCache.InsertUnderTransactionAsync(new[] { new PatchCacheEntry()
                            {
                                Name = CreateRelativePath(InstallConfiguration.ArksLayer.EnglishRaiserPatch),
                                Hash = translation.RaiserMD5,
                                LastWriteTime = new FileInfo(InstallConfiguration.ArksLayer.EnglishRaiserPatch).LastWriteTimeUtc.ToFileTimeUtc()
                            }});
                        }
                    }

                    Log("ArksLayer", "Verifying english patch plugins");

                    await ValidateFileAsync(InstallConfiguration.ArksLayer.PluginPSO2BlockRenameDll, pluginInfo.PSO2BlockRenameDll);
                    await ValidateFileAsync(InstallConfiguration.ArksLayer.PluginPSO2ItemTranslatorDll, pluginInfo.PSO2ItemTranslatorDll);
                    await ValidateFileAsync(InstallConfiguration.ArksLayer.PluginPSO2TitleTranslatorDll, pluginInfo.PSO2TitleTranslatorDll);
                    await ValidateFileAsync(InstallConfiguration.ArksLayer.PluginPSO2RAISERSystemDll, pluginInfo.PSO2RAISERSystemDll);
                }
                else
                {
                    File.Delete(InstallConfiguration.ArksLayer.PluginPSO2BlockRenameDll);
                    File.Delete(InstallConfiguration.ArksLayer.PluginPSO2ItemTranslatorDll);
                    File.Delete(InstallConfiguration.ArksLayer.PluginPSO2TitleTranslatorDll);
                    File.Delete(InstallConfiguration.ArksLayer.PluginPSO2RAISERSystemDll);
                }

            }
            catch (Exception ex)
            {
                App.Current.Logger.Error("Error applying Arks-layer patches", ex);

                Log("ArksLayer", "Error applying Arks-Layer patches");
                Log("ArksLayer", ex.Message);

                return false;
            }
            finally
            {
                _ActivityCount -= 1;
            }

            return true;
        }

        public async Task ResetGameGuardAsync()
        {
            _ActivityCount += 1;

            Log("GameGuard", "Removing GameGuard files and directories");

            try
            {
                if (Directory.Exists(InstallConfiguration.GameGuardDirectory))
                    Directory.Delete(InstallConfiguration.GameGuardDirectory, true);

                await Task.Yield();

                if (File.Exists(InstallConfiguration.GameGuardFile))
                    File.Delete(InstallConfiguration.GameGuardFile);

                await Task.Yield();

                foreach (var file in InstallConfiguration.GameGuardSystemFiles)
                    if (File.Exists(file))
                        File.Delete(file);
            }
            catch
            {
                Log("GameGuard", "Error. Could not delete all GameGuard files as GameGuard is still running, ensure PSO2 is closed and restart your PC");
            }

            await Task.Yield();

            Log("GameGuard", "Removing GameGuard registries");

            try
            {
                if (Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\npggsvc", true) != null)
                    Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services", true).DeleteSubKeyTree("npggsvc");
            }
            catch
            {
                Log("GameGuard", "Error. Unable to delete GameGuard registry files");
            }

            await VerifyGameFilesAsync();
            _ActivityCount -= 1;
        }
    }
}
