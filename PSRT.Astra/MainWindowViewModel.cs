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
using PSRT.Astra.Views;
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

        [AddINotifyPropertyChangedInterface]
        public class PhaseState
        {
            public string Title { get; set; }
            public PhaseControl.State State { get; set; } = PhaseControl.State.Queued;
            public TimeSpan Duration { get; set; } = TimeSpan.Zero;
            public UIElement Child { get; set; }
        }

        public ObservableCollection<PhaseState> Phases { get; set; }

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

        private async Task<T> _AttemptPhase<T>(PhaseState state, Func<Task<T>> phase) where T : class
        {
            state.State = PhaseControl.State.Running;
            T result;
            try
            {
                var start = DateTime.UtcNow;
                var task = phase();
                while (await Task.WhenAny(task, Task.Delay(100)) != task)
                    state.Duration = DateTime.UtcNow - start;
                result = await task;
                state.Duration = DateTime.UtcNow - start;
            }
            catch (OperationCanceledException)
            {
                state.State = PhaseControl.State.Canceled;
                throw;
            }
            catch
            {
                state.State = PhaseControl.State.Error;
                throw;
            }
            state.State = PhaseControl.State.Success;
            return result;
        }

        private async Task _AttemptPhase(PhaseState state, Func<Task> phase)
        {
            state.State = PhaseControl.State.Running;
            try
            {
                var start = DateTime.UtcNow;
                var task = phase();
                while (await Task.WhenAny(task, Task.Delay(100)) != task)
                    state.Duration = DateTime.UtcNow - start;
                state.Duration = DateTime.UtcNow - start;
            }
            catch (OperationCanceledException)
            {
                state.State = PhaseControl.State.Canceled;
                throw;
            }
            catch
            {
                state.State = PhaseControl.State.Error;
                throw;
            }
            state.State = PhaseControl.State.Success;
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
                        var newPhases = new ObservableCollection<PhaseState>();

                        var pso2DirectoriesPhase = new PSO2DirectoriesPhase(InstallConfiguration);
                        var pso2DirectoriesPhaseState = new PhaseState
                        {
                            Title = "PSO2 Directories"
                        };
                        newPhases.Add(pso2DirectoriesPhaseState);

                        var modFilesPhase = new ModFilesPhase(InstallConfiguration);
                        var modFilesPhaseState = new PhaseState
                        {
                            Title = "Copying mod files"
                        };
                        newPhases.Add(modFilesPhaseState);

                        var deleteCensorFilePhase = new DeleteCensorFilePhase(InstallConfiguration);
                        var deleteCensorFilePhaseState = new PhaseState
                        {
                            Title = "Deleting censor file"
                        };
                        newPhases.Add(deleteCensorFilePhaseState);

                        var downloadConfigurationState = new PhaseState
                        {
                            Title = "Downloading configuration file"
                        };
                        newPhases.Add(downloadConfigurationState);

                        var patchCacheState = new PhaseState
                        {
                            Title = "Connecting to patch cache database"
                        };
                        newPhases.Add(patchCacheState);

                        var comparePhase = new ComparePhase(InstallConfiguration);
                        var comparePhaseState = new PhaseState { Title = "Comparing files" };
                        newPhases.Add(comparePhaseState);
                        {
                            var compareProgressControl = new CompareProgressControl();
                            compareProgressControl.SetBinding(CompareProgressControl.ProgressProperty, new Binding
                            {
                                Source = comparePhase.Progress,
                                Path = new PropertyPath(nameof(comparePhase.Progress.Progress)),
                                Mode = BindingMode.OneWay
                            });
                            compareProgressControl.SetBinding(CompareProgressControl.IsIndeterminateProperty, new Binding
                            {
                                Source = comparePhase.Progress,
                                Path = new PropertyPath(nameof(comparePhase.Progress.IsIndeterminate)),
                                Mode = BindingMode.OneWay
                            });
                            comparePhaseState.Child = compareProgressControl;
                        }

                        var verifyFilesPhase = new VerifyFilesPhase(InstallConfiguration);
                        var verifyFilesPhaseState = new PhaseState
                        {
                            Title = "Verifying files"
                        };
                        newPhases.Add(verifyFilesPhaseState);

                        var pluginInfoState = new PhaseState
                        {
                            Title = "Downloading plugin info"
                        };
                        newPhases.Add(pluginInfoState);

                        var pso2hPhase = new PSO2hPhase(InstallConfiguration, ArksLayerEnglishPatchEnabled || ArksLayerTelepipeProxyEnabled);
                        var pso2hPhaseState = new PhaseState
                        {
                            Title = $"{(ArksLayerEnglishPatchEnabled || ArksLayerTelepipeProxyEnabled ? "Installing" : "Removing")} PSO2 hook"
                        };
                        newPhases.Add(pso2hPhaseState);

                        var telepipeProxyPhase = new TelepipeProxyPhase(InstallConfiguration, ArksLayerTelepipeProxyEnabled);
                        var telepipeProxyPhaseState = new PhaseState
                        {
                            Title = $"{(ArksLayerTelepipeProxyEnabled ? "Installing" : "Removing")} Telepipe"
                        };
                        newPhases.Add(telepipeProxyPhaseState);

                        var englishPatchPhase = new EnglishPatchPhase(InstallConfiguration, ArksLayerEnglishPatchEnabled);
                        var englishPatchPhaseState = new PhaseState
                        {
                            Title = $"{(ArksLayerEnglishPatchEnabled ? "Installing" : "Removing")} English patch"
                        };
                        newPhases.Add(englishPatchPhaseState);

                        LargeAddressAwarePhase largeAddressAwarePhase = null;
                        PhaseState largeAddressAwarePhaseState = null;
                        if (Properties.Settings.Default.LargeAddressAwareEnabled)
                        {
                            largeAddressAwarePhase = new LargeAddressAwarePhase(InstallConfiguration);
                            largeAddressAwarePhaseState = new PhaseState
                            {
                                Title = $"Installing large address aware patch"
                            };
                            newPhases.Add(largeAddressAwarePhaseState);
                        }

                        var launchPSO2State = new PhaseState
                        {
                            Title = "Launch Phantasy Star Online 2"
                        };
                        newPhases.Add(launchPSO2State);

                        Phases = newPhases;


                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Current.Logger.Info("Launch", $"Running {nameof(PSO2DirectoriesPhase)}");
                        await _AttemptPhase(pso2DirectoriesPhaseState,
                            () => pso2DirectoriesPhase.RunAsync(_LaunchCancellationTokenSource.Token));

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Current.Logger.Info("Launch", $"Running {nameof(ModFilesPhase)}");
                        await _AttemptPhase(modFilesPhaseState,
                            () => modFilesPhase.RunAsync(_LaunchCancellationTokenSource.Token));

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Current.Logger.Info("Launch", $"Running {nameof(DeleteCensorFilePhase)}");
                        await _AttemptPhase(deleteCensorFilePhaseState,
                            () => deleteCensorFilePhase.RunAsync(_LaunchCancellationTokenSource.Token));

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Current.Logger.Info("Launch", "Fetching download configuration");
                        var downloadConfiguration = await _AttemptPhase(downloadConfigurationState,
                            () => DownloadConfiguration.CreateDefaultAsync(_LaunchCancellationTokenSource.Token));

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Current.Logger.Info("Launch", "Connecting to patch cache database");
                        var patchCache = await _AttemptPhase(patchCacheState,
                            () => PatchCache.CreateAsync(InstallConfiguration));

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        // loop this block so files that were updated while a possibly long
                        // verify phase took place are not missed
                        while (true)
                        {
                            comparePhaseState.State = PhaseControl.State.Queued;
                            verifyFilesPhaseState.State = PhaseControl.State.Queued;

                            _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                            App.Current.Logger.Info("Launch", $"Running {nameof(ComparePhase)}");
                            var toUpdate = await _AttemptPhase(comparePhaseState,
                                () => comparePhase.RunAsync(downloadConfiguration, patchCache, _LaunchCancellationTokenSource.Token));
                            if (toUpdate.Length == 0)
                            {
                                verifyFilesPhaseState.State = PhaseControl.State.Success;
                                break;
                            }

                            _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                            App.Current.Logger.Info("Launch", $"Running {nameof(VerifyFilesPhase)}");
                            await _AttemptPhase(verifyFilesPhaseState,
                                () => verifyFilesPhase.RunAsync(toUpdate, patchCache, _LaunchCancellationTokenSource.Token));
                        }

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Current.Logger.Info("Launch", "Fetching plugin info");
                        var pluginInfo = await _AttemptPhase(pluginInfoState,
                            () => PluginInfo.FetchAsync(_LaunchCancellationTokenSource.Token));

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Current.Logger.Info("Launch", $"Running {nameof(PSO2hPhase)}");
                        await _AttemptPhase(pso2hPhaseState,
                            () => pso2hPhase.RunAsync(pluginInfo, _LaunchCancellationTokenSource.Token));

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Current.Logger.Info("Launch", $"Running {nameof(TelepipeProxyPhase)}");
                        await _AttemptPhase(telepipeProxyPhaseState,
                            () => telepipeProxyPhase.RunAsync(pluginInfo, _LaunchCancellationTokenSource.Token));

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Current.Logger.Info("Launch", $"Running {nameof(EnglishPatchPhase)}");
                        await _AttemptPhase(englishPatchPhaseState,
                            () => englishPatchPhase.RunAsync(patchCache, pluginInfo, _LaunchCancellationTokenSource.Token));

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        if (largeAddressAwarePhaseState != null && largeAddressAwarePhase != null)
                        {
                            App.Current.Logger.Info("Launch", $"Running {nameof(LargeAddressAwarePhase)}");
                            await _AttemptPhase(largeAddressAwarePhaseState,
                                () => largeAddressAwarePhase.RunAsync(_LaunchCancellationTokenSource.Token));
                        }

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        // cancellation no longer works after this point

                        App.Current.Logger.Info("Launch", "Starting PSO2");

                        await _AttemptPhase(launchPSO2State, async () =>
                        {
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
                        });

                        App.Current.Logger.Info("Launch", "PSO2 launch process ended");
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
                        Log("Launch", "Error during launch phases:");
                        Log("Launch", ex.Message);
                        Log("Launch", "Retrying...");
                        await Task.Delay(5000);
                        continue;
                    }

                    break;
                }
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
