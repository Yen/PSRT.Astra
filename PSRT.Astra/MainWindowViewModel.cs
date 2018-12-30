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
        public enum ApplicationState
        {
            Idle,
            Loading,
            Patching,
            GameRunning
        };

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

        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();

        public bool ArksLayerEnglishPatchEnabled { get; set; } = Properties.Settings.Default.EnglishPatchEnabled;
        public bool ArksLayerTelepipeProxyEnabled { get; set; } = Properties.Settings.Default.TelepipeProxyEnabled;

        private CancellationTokenSource _LaunchCancellationTokenSource { get; set; }

        //

        private int _ActivityCount { get; set; } = 0;

        private ApplicationState _ApplicationState
        {
            get
            {
                if (IsPSO2Running)
                    return ApplicationState.GameRunning;

                if (_LaunchCancellationTokenSource != null)
                    return ApplicationState.Patching;

                if (_ActivityCount > 0)
                    return ApplicationState.Loading;

                return ApplicationState.Idle;
            }
        }

        public bool LaunchPSO2ButtonEnabled
            => _ApplicationState == ApplicationState.Idle
            || _ApplicationState == ApplicationState.Patching;

        public bool ConfigButtonsEnabled
            => _ApplicationState == ApplicationState.Idle;

        public string LaunchPSO2ButtonLocaleKey
        {
            get
            {
                switch (_ApplicationState)
                {
                    case ApplicationState.Idle:
                        return "MainWindow_LaunchPSO2";
                    case ApplicationState.Loading:
                        return "MainWindow_Loading";
                    case ApplicationState.Patching:
                        return "MainWindow_Cancel";
                    case ApplicationState.GameRunning:
                        return "MainWindow_PSO2Running";
                }

                return null;
            }
        }

        //

        public MainWindowViewModel(string pso2BinDirectory)
        {
            InstallConfiguration = new InstallConfiguration(pso2BinDirectory);
        }

        public async Task InitializeAsync()
        {
            _ActivityCount += 1;

            try
            {
                Log("Astra", $"Game directory set to {Properties.Settings.Default.LastSelectedInstallLocation}");

                // start update in the background
                _CheckForUpdate();

                // manually check once in case the game was running
                // before the launcher was started and initialisation 
                // finishes before the async task detects it
                await Task.Run(() => _CheckGameWatcher());
                _InitializeGameWatcher();
            }
            finally
            {
                _ActivityCount -= 1;
            }
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
            if (_ApplicationState == ApplicationState.Patching)
            {
                _LaunchCancellationTokenSource?.Cancel();
                return;
            }

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

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Current.Logger.Info("Launch", $"Running {nameof(ModFilesPhase)}");
                        Log("Launch", $"Running {nameof(ModFilesPhase)}");
                        var modFilesPhase = new ModFilesPhase(InstallConfiguration);
                        await modFilesPhase.RunAsync(_LaunchCancellationTokenSource.Token);

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Current.Logger.Info("Launch", $"Running {nameof(DeleteCensorFilePhase)}");
                        Log("Launch", $"Running {nameof(DeleteCensorFilePhase)}");
                        var deleteCensorFilePhase = new DeleteCensorFilePhase(InstallConfiguration);
                        await deleteCensorFilePhase.RunAsync(_LaunchCancellationTokenSource.Token);

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Current.Logger.Info("Launch", "Fetching download configuration");
                        Log("Launch", "Fetching download configuration");
                        var downloadConfiguration = await DownloadConfiguration.CreateDefaultAsync(_LaunchCancellationTokenSource.Token);

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Current.Logger.Info("Launch", "Connecting to patch cache database");
                        Log("Launch", "Connecting to patch cache database");
                        var patchCache = await PatchCache.CreateAsync(InstallConfiguration);

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        // loop this block so files that were updated while a possibly long
                        // verify phase took place are not missed
                        while (true)
                        {
                            _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                            App.Current.Logger.Info("Launch", $"Running {nameof(ComparePhase)}");
                            Log("Launch", $"Running {nameof(ComparePhase)}");
                            var comparePhase = new ComparePhase(InstallConfiguration, downloadConfiguration, patchCache);
                            var toUpdate = await comparePhase.RunAsync(_LaunchCancellationTokenSource.Token);
                            if (toUpdate.Count == 0)
                                break;

                            _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                            App.Current.Logger.Info("Launch", $"Running {nameof(VerifyFilesPhase)}");
                            Log("Launch", $"Running {nameof(VerifyFilesPhase)}");
                            var verifyFilesPhase = new VerifyFilesPhase(InstallConfiguration, patchCache);
                            await verifyFilesPhase.RunAsync(toUpdate, _LaunchCancellationTokenSource.Token);
                        }

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Current.Logger.Info("Launch", "Fetching plugin info");
                        Log("Launch", "Fetching plugin info");
                        var pluginInfo = await PluginInfo.FetchAsync(_LaunchCancellationTokenSource.Token);

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Current.Logger.Info("Launch", $"Running {nameof(PSO2hPhase)}");
                        Log("Launch", $"Running {nameof(PSO2hPhase)}");
                        var pso2hPhase = new PSO2hPhase(InstallConfiguration, pluginInfo, ArksLayerEnglishPatchEnabled || ArksLayerTelepipeProxyEnabled);
                        await pso2hPhase.RunAsync(_LaunchCancellationTokenSource.Token);

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Current.Logger.Info("Launch", $"Running {nameof(TelepipeProxyPhase)}");
                        Log("Launch", $"Running {nameof(TelepipeProxyPhase)}");
                        var telepipeProxyPhase = new TelepipeProxyPhase(InstallConfiguration, pluginInfo, ArksLayerTelepipeProxyEnabled);
                        await telepipeProxyPhase.RunAsync(_LaunchCancellationTokenSource.Token);

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Current.Logger.Info("Launch", $"Running {nameof(EnglishPatchPhase)}");
                        Log("Launch", $"Running {nameof(EnglishPatchPhase)}");
                        var englishPatchPhase = new EnglishPatchPhase(InstallConfiguration, patchCache, pluginInfo, ArksLayerEnglishPatchEnabled);
                        await englishPatchPhase.RunAsync(_LaunchCancellationTokenSource.Token);

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        if (Properties.Settings.Default.LargeAddressAwareEnabled)
                        {
                            App.Current.Logger.Info("Launch", $"Running {nameof(LargeAddressAwarePhase)}");
                            Log("Launch", $"Running {nameof(LargeAddressAwarePhase)}");
                            var largeAddressAwarePhase = new LargeAddressAwarePhase(InstallConfiguration);
                            await largeAddressAwarePhase.RunAsync(_LaunchCancellationTokenSource.Token);
                        }
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

                _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                // cancellation no longer works after this point

                App.Current.Logger.Info("Launch", "Starting PSO2");
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

                App.Current.Logger.Info("Launch", "PSO2 launch process ended");
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

            try
            {

                App.Current.Logger.Error("GameGuard", "Removing GameGuard files and directories");
                Log("GameGuard", "Removing GameGuard files and directories");

                try
                {
                    await Task.Run(() =>
                    {
                        if (Directory.Exists(InstallConfiguration.GameGuardDirectory))
                            Directory.Delete(InstallConfiguration.GameGuardDirectory, true);

                        if (File.Exists(InstallConfiguration.GameGuardFile))
                            File.Delete(InstallConfiguration.GameGuardFile);

                        foreach (var file in InstallConfiguration.GameGuardSystemFiles)
                            if (File.Exists(file))
                                File.Delete(file);
                    });
                }
                catch (Exception ex)
                {
                    App.Current.Logger.Error("GameGuard", "Error deleting game guard files", ex);
                    Log("GameGuard", "Error. Could not delete all GameGuard files as GameGuard is still running, ensure PSO2 is closed and restart your PC");
                }

                App.Current.Logger.Error("GameGuard", "Removing GameGuard registries");
                Log("GameGuard", "Removing GameGuard registries");

                try
                {
                    await Task.Run(() =>
                    {
                        if (Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\npggsvc", true) != null)
                            Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services", true).DeleteSubKeyTree("npggsvc");
                    });
                }
                catch (Exception ex)
                {
                    App.Current.Logger.Error("GameGuard", "Unable to delete GameGuard registry files", ex);
                    Log("GameGuard", "Error. Unable to delete GameGuard registry files");
                }

            }
            finally
            {
                _ActivityCount -= 1;
            }
        }
    }
}
