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
using PSRT.Astra.Models.ArksLayer.Phases;
using PSRT.Astra.Models.Phases;
using SharpCompress.Archives.Rar;

namespace PSRT.Astra
{
    [AddINotifyPropertyChangedInterface]
    public partial class MainWindowViewModel
    {
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
        
        private CancellationTokenSource _LaunchCancellationTokenSource;
        
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

            try
            {
                _LaunchCancellationTokenSource = new CancellationTokenSource();

                Log("Launch", "Saving client settings");
                await Task.Run(() =>
                {
                    Properties.Settings.Default.EnglishPatchEnabled = ArksLayerEnglishPatchEnabled;
                    Properties.Settings.Default.TelepipeProxyEnabled = ArksLayerTelepipeProxyEnabled;
                    Properties.Settings.Default.Save();
                });

                while (true)
                {
                    try
                    {
                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();


                        App.Current.Logger.Info("Launch", $"Running {nameof(PSO2DirectoriesPhase)}");
                        Log("Launch", $"Running {nameof(PSO2DirectoriesPhase)}");
                        var pso2DirectoriesPhase = new PSO2DirectoriesPhase(InstallConfiguration);
                        await pso2DirectoriesPhase.RunAsync(_LaunchCancellationTokenSource.Token);

                        App.Current.Logger.Info("Launch", $"Running {nameof(ModFilesPhase)}");
                        Log("Launch", $"Running {nameof(ModFilesPhase)}");
                        var modFilesPhase = new ModFilesPhase(InstallConfiguration);
                        await modFilesPhase.RunAsync(_LaunchCancellationTokenSource.Token);

                        App.Current.Logger.Info("Launch", $"Running {nameof(DeleteCensorFilePhase)}");
                        Log("Launch", $"Running {nameof(DeleteCensorFilePhase)}");
                        var deleteCensorFilePhase = new DeleteCensorFilePhase(InstallConfiguration);
                        await deleteCensorFilePhase.RunAsync(_LaunchCancellationTokenSource.Token);

                        // loop this block so files that were updated while a possibly long
                        // verify phase took place are not missed
                        while (true)
                        {
                            App.Current.Logger.Info("Launch", $"Running {nameof(ComparePhase)}");
                            Log("Launch", $"Running {nameof(ComparePhase)}");
                            var comparePhase = new ComparePhase(InstallConfiguration, DownloadConfiguration, PatchCache);
                            var toUpdate = await comparePhase.RunAsync(_LaunchCancellationTokenSource.Token);
                            if (toUpdate.Count == 0)
                                break;

                            App.Current.Logger.Info("Launch", $"Running {nameof(VerifyFilesPhase)}");
                            Log("Launch", $"Running {nameof(VerifyFilesPhase)}");
                            var verifyFilesPhase = new VerifyFilesPhase(InstallConfiguration, PatchCache);
                            await verifyFilesPhase.RunAsync(toUpdate, _LaunchCancellationTokenSource.Token);
                        }

                        App.Current.Logger.Info("Launch", "Fetching plugin info");
                        Log("Launch", "Fetching plugin info");
                        var pluginInfo = await PluginInfo.FetchAsync(_LaunchCancellationTokenSource.Token);

                        App.Current.Logger.Info("Launch", $"Running {nameof(PSO2hPhase)}");
                        Log("Launch", $"Running {nameof(PSO2hPhase)}");
                        var pso2hPhase = new PSO2hPhase(InstallConfiguration, pluginInfo, ArksLayerEnglishPatchEnabled || ArksLayerTelepipeProxyEnabled);
                        await pso2hPhase.RunAsync(_LaunchCancellationTokenSource.Token);

                        App.Current.Logger.Info("Launch", $"Running {nameof(TelepipeProxyPhase)}");
                        Log("Launch", $"Running {nameof(TelepipeProxyPhase)}");
                        var telepipeProxyPhase = new TelepipeProxyPhase(InstallConfiguration, pluginInfo, ArksLayerTelepipeProxyEnabled);
                        await telepipeProxyPhase.RunAsync(_LaunchCancellationTokenSource.Token);

                        App.Current.Logger.Info("Launch", $"Running {nameof(EnglishPatchPhase)}");
                        Log("Launch", $"Running {nameof(EnglishPatchPhase)}");
                        var englishPatchPhase = new EnglishPatchPhase(InstallConfiguration, PatchCache, pluginInfo, ArksLayerEnglishPatchEnabled);
                        await englishPatchPhase.RunAsync(_LaunchCancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        App.Current.Logger.Info("Launch", "Launch cancelled");
                        Log("Launch", "Launch cancelled");
                        return;
                    }
                    catch (Exception ex)
                    {
                        App.Current.Logger.Info("Launch", "Error during launch phases", ex);
                        Log("Launch", "Error during launch phases, retrying");
                        await Task.Delay(5000);
                        continue;
                    }

                    break;
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

            }
            finally
            {
                _ActivityCount -= 1;
                _LaunchCancellationTokenSource = null;
            }
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

            _ActivityCount -= 1;
        }
    }
}
