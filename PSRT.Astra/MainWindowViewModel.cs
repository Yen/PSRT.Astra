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
using PSRT.Astra.Models.Phases;
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

        // phases

        private PSO2DirectoriesPhase _PSO2DirectoriesPhase;
        private ModFilesPhase _ModFilesPhase;
        private ComparePhase _ComparePhase;
        private DeleteCensorFilePhase _DeleteCensorFilePhase;
        private VerifyFilesPhase _VerifyFilesPhase;

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

            Log("Init", "Fetching download configuration");
            DownloadConfiguration = await DownloadConfiguration.CreateDefaultAsync();

            Log("Init", "Connecting to patch cache database");
            PatchCache = await PatchCache.CreateAsync(InstallConfiguration);

            _PSO2DirectoriesPhase = new PSO2DirectoriesPhase(InstallConfiguration);
            _ModFilesPhase = new ModFilesPhase(InstallConfiguration);
            _ComparePhase = new ComparePhase(InstallConfiguration, DownloadConfiguration, PatchCache);
            _DeleteCensorFilePhase = new DeleteCensorFilePhase(InstallConfiguration);
            _VerifyFilesPhase = new VerifyFilesPhase(InstallConfiguration, PatchCache);

            await VerifyGameFilesAsync();

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

            await VerifyAsync(logSource);

            Log(logSource, "All files verified");

            _ActivityCount -= 1;
        }

        public async Task VerifyAsync(string logSource)
        {
            _ActivityCount += 1;

            try
            {
                App.Current.Logger.Info("Verify", $"Running {nameof(PSO2DirectoriesPhase)}");
                Log(logSource, $"Running {nameof(PSO2DirectoriesPhase)}");
                await _PSO2DirectoriesPhase.RunAsync();

                App.Current.Logger.Info("Verify", $"Running {nameof(ModFilesPhase)}");
                Log(logSource, $"Running {nameof(ModFilesPhase)}");
                await _ModFilesPhase.RunAsync();

                App.Current.Logger.Info("Verify", $"Running {nameof(DeleteCensorFilePhase)}");
                Log(logSource, $"Running {nameof(DeleteCensorFilePhase)}");
                await _DeleteCensorFilePhase.RunAsync();

                App.Current.Logger.Info("Verify", $"Running {nameof(ComparePhase)}");
                Log(logSource, $"Running {nameof(ComparePhase)}");
                var toUpdate = await _ComparePhase.RunAsync();
                if (toUpdate.Count == 0)
                    return;

                App.Current.Logger.Info("Verify", $"Running {nameof(VerifyFilesPhase)}");
                Log(logSource, $"Running {nameof(VerifyFilesPhase)}");
                await _VerifyFilesPhase.RunAsync(toUpdate);

                // Rerun
                App.Current.Logger.Info("Verify", "Rerunning verify task to check for intermediate changes");
                Log(logSource, "Rerunning verify task to check for intermediate changes");
                await VerifyAsync(logSource);
            }
            finally
            {
                _ActivityCount -= 1;
            }
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
                    App.Current.Logger.Info(nameof(MainWindowViewModel), "Validating core components");

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
                    App.Current.Logger.Info(nameof(MainWindowViewModel), "Deleting core Arks-Layer components");

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
                App.Current.Logger.Error(nameof(MainWindowViewModel), "Error applying patches", ex);

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
