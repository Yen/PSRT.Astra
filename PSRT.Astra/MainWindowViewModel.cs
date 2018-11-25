using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
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

        private int _ActivityCount { get; set; } = 0;
        public bool Ready => _ActivityCount == 0 && DownloadConfiguration != null;

        //

        public MainWindowViewModel(string pso2BinDirectory)
        {
            InstallConfiguration = new InstallConfiguration(pso2BinDirectory);
        }

        public async Task InitializeAsync()
        {
            // start update in the background
            _CheckForUpdate();

            _ActivityCount += 1;

            await _CreateKeyDirectoriesAsync();

            Log("Init", "Fetching download configuration");
            DownloadConfiguration = await DownloadConfiguration.CreateDefaultAsync();

            Log("Init", "Connecting to patch cache database");
            PatchCache = await PatchCache.CreateAsync(InstallConfiguration);

            _ActivityCount -= 1;
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

            _ActivityCount -= 1;
        }

        public async Task VerifyAsync(string logSource, Dictionary<string, (string Hash, Uri DownloadPath)> patches)
        {
            _ActivityCount += 1;

            await _CreateKeyDirectoriesAsync();

            Log(logSource, "Fetching patch cache data");

            var cacheData = await PatchCache.SelectAllAsync();

            Log(logSource, "Comparing files");

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

                Log(logSource, "All files verified");

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
                            await Application.Current.Dispatcher.InvokeAsync(() => Log(logSource, $"Error: {ex.Message}"));
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
                Source = logSource
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
            await VerifyAsync(logSource, patches);

            Log(logSource, "All files verified");

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
                Directory.CreateDirectory(InstallConfiguration.PluginsDirectory);
                Directory.CreateDirectory(InstallConfiguration.PluginsDisabledDirectory);
                Directory.CreateDirectory(InstallConfiguration.PatchesDirectory);
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

            Log("ArksLayer", "Deleting existing files");

            try
            {
                await Task.Run(() =>
                {
                    if (File.Exists(InstallConfiguration.TweakerBin))
                        File.Delete(InstallConfiguration.TweakerBin);

                    if (File.Exists(InstallConfiguration.DDrawDll))
                        File.Delete(InstallConfiguration.DDrawDll);
                    if (File.Exists(InstallConfiguration.PSO2hDll))
                        File.Delete(InstallConfiguration.PSO2hDll);

                    if (File.Exists(InstallConfiguration.PluginPSO2BlockRenameDll))
                        File.Delete(InstallConfiguration.PluginPSO2BlockRenameDll);
                    if (File.Exists(InstallConfiguration.PluginPSO2ItemTranslatorDll))
                        File.Delete(InstallConfiguration.PluginPSO2ItemTranslatorDll);
                    if (File.Exists(InstallConfiguration.PluginPSO2TitleTranslatorDll))
                        File.Delete(InstallConfiguration.PluginPSO2TitleTranslatorDll);
                    if (File.Exists(InstallConfiguration.PluginPSO2RAISERSystemDll))
                        File.Delete(InstallConfiguration.PluginPSO2RAISERSystemDll);
                    if (File.Exists(InstallConfiguration.PluginTelepipeProxyDll))
                        File.Delete(InstallConfiguration.PluginTelepipeProxyDll);

                    if (File.Exists(InstallConfiguration.TelepipeProxyConfig))
                        File.Delete(InstallConfiguration.TelepipeProxyConfig);
                    if (File.Exists(InstallConfiguration.TelepipeProxyPublicKey))
                        File.Delete(InstallConfiguration.TelepipeProxyPublicKey);
                });
            }
            catch
            {
                Log("ArksLayer", "Error deleting files");
                _ActivityCount -= 1;
                return false;
            }

            if (ArksLayerEnglishPatchEnabled == false && ArksLayerTelepipeProxyEnabled == false)
            {
                _ActivityCount -= 1;
                return true;
            }

            Log("ArksLayer", "Copying key files");
            await Task.Run(() =>
            {
                File.WriteAllBytes(InstallConfiguration.DDrawDll, Properties.Resources.DDrawDll);
                File.WriteAllBytes(InstallConfiguration.PSO2hDll, Properties.Resources.PSO2hDll);
            });

            try
            {
                if (ArksLayerEnglishPatchEnabled)
                {
                    Log("ArksLayer", "Downloading english translation information");
                    var translation = await TranslationInfo.FetchAsync();

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

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "ADragonIsFineToo");

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

                        await VerifyAndDownlodRar(InstallConfiguration.EnglishBlockPatch, translation.BlockMD5, new Uri(translation.BlockPatch));
                        await VerifyAndDownlodRar(InstallConfiguration.EnglishItemPatch, translation.ItemMD5, new Uri(translation.ItemPatch));
                        await VerifyAndDownlodRar(InstallConfiguration.EnglishTextPatch, translation.TextMD5, new Uri(translation.TextPatch));
                        await VerifyAndDownlodRar(InstallConfiguration.EnglishTitlePatch, translation.TitleMD5, new Uri(translation.TitlePatch));

                        if (Verify(InstallConfiguration.EnglishRaiserPatch, translation.RaiserMD5) == false)
                        {
                            Log("ArksLayer", $"Downloading \"{Path.GetFileName(new Uri(translation.RaiserPatch).LocalPath)}\"");
                            using (var stream = await client.GetStreamAsync(translation.RaiserPatch))
                            using (var fs = new FileStream(InstallConfiguration.EnglishRaiserPatch, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                await stream.CopyToAsync(fs);
                            }
                            await PatchCache.InsertUnderTransactionAsync(new[] { new PatchCacheEntry()
                            {
                                Name = CreateRelativePath(InstallConfiguration.EnglishRaiserPatch),
                                Hash = translation.RaiserMD5,
                                LastWriteTime = new FileInfo(InstallConfiguration.EnglishRaiserPatch).LastWriteTimeUtc.ToFileTimeUtc()
                            }});
                        }
                    }

                    Log("ArksLayer", "Copying english patch plugins");
                    await Task.Run(() =>
                    {
                        File.WriteAllBytes(InstallConfiguration.PluginPSO2BlockRenameDll, Properties.Resources.PSO2BlockRenameDll);
                        File.WriteAllBytes(InstallConfiguration.PluginPSO2ItemTranslatorDll, Properties.Resources.PSO2ItemTranslatorDll);
                        File.WriteAllBytes(InstallConfiguration.PluginPSO2TitleTranslatorDll, Properties.Resources.PSO2TitleTranslatorDll);
                        File.WriteAllBytes(InstallConfiguration.PluginPSO2RAISERSystemDll, Properties.Resources.PSO2RAISERSystemDll);
                    });
                }
            }
            catch
            {
                Log("ArksLayer", "Error installing english patch");
                _ActivityCount -= 1;
                return false;
            }

            if (ArksLayerTelepipeProxyEnabled)
            {
                try
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
                            File.WriteAllText(InstallConfiguration.TelepipeProxyConfig, config.Host);
                            File.WriteAllBytes(InstallConfiguration.TelepipeProxyPublicKey, publicKey);
                        });
                    }

                    Log("ArksLayer", "Copying Telepipe proxy plugin");
                    await Task.Run(() => File.WriteAllBytes(InstallConfiguration.PluginTelepipeProxyDll, Properties.Resources.TelepipeProxyDll));
                }
                catch
                {
                    Log("ArksLayer", "Error installing Telepipe proxy");
                    _ActivityCount -= 1;
                    return false;
                }
            }

            Log("ArksLayer", "Writing tweaker.bin file");

            using (var fs = new FileStream(InstallConfiguration.TweakerBin, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            using (var writer = new StreamWriter(fs))
            {
                var magic = _GenerateTweakerBin();
                await writer.WriteLineAsync(magic);
            }

            _ActivityCount -= 1;
            return true;
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
