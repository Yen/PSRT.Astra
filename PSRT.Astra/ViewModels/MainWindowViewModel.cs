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
using System.Reflection;
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

namespace PSRT.Astra.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public partial class MainWindowViewModel
    {
        public RelayCommand LaunchCommand => new RelayCommand(async () => await LaunchAsync());
        public RelayCommand UpdateCommand => new RelayCommand(async () => await LaunchAsync(false));
        public RelayCommand ResetGameGuardCommand => new RelayCommand(async () => await ResetGameGuardAsync());

        //

        public InstallConfiguration InstallConfiguration { get; set; }

        public bool ArksLayerEnglishPatchEnabled { get; set; } = Properties.Settings.Default.EnglishPatchEnabled;
        public bool ArksLayerTelepipeProxyEnabled { get; set; } = Properties.Settings.Default.TelepipeProxyEnabled;
        public bool ModFilesEnabled { get; set; } = Properties.Settings.Default.ModFilesEnabled;

        private CancellationTokenSource _LaunchCancellationTokenSource { get; set; }

        //

        public enum ApplicationState
        {
            Idle,
            Loading,
            Patching,
            GameRunning
        };

        private int _ActivityCount { get; set; } = 0;

        public ApplicationState State
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
            => State == ApplicationState.Idle
            || State == ApplicationState.Patching;

        public bool ConfigButtonsEnabled
            => State == ApplicationState.Idle;

        public bool IsLaunchingPSO2 { get; set; }

        public bool IsChangelogVisible { get; set; } = true;
        public Version CurrentVersion { get; set; } = Assembly.GetExecutingAssembly().GetName().Version;
        public UpdateChecker.UpdateInformation UpdatedVersionInformation { get; set; }
        public bool IsUpdateAvailable => UpdatedVersionInformation.Version > CurrentVersion;

        //

        [AddINotifyPropertyChangedInterface]
        public class PhaseState
        {
            public string TitleKey { get; set; }
            public PhaseControl.State State { get; set; } = PhaseControl.State.Queued;
            public TimeSpan Duration { get; set; } = TimeSpan.Zero;
            public UIElement Child { get; set; }
        }

        public ObservableCollection<PhaseState> Phases { get; set; }

        public bool UploadErrorButtonVisible { get; set; } = false;

        //

        public MainWindowViewModel(string pso2BinDirectory, UpdateChecker.UpdateInformation updateInformation)
        {
            InstallConfiguration = new InstallConfiguration(pso2BinDirectory);
            UpdatedVersionInformation = updateInformation;
        }

        public async Task InitializeAsync()
        {
            _ActivityCount += 1;

            try
            {
                App.Logger.Info(nameof(MainWindowViewModel), $"Game directory set to {Properties.Settings.Default.LastSelectedInstallLocation}");

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
            _LaunchCancellationTokenSource?.Cancel();

            _DestroyGameWatcher();

            return Task.CompletedTask;
        }

        public async Task<bool> CanOpenSettingsAsync()
        {
            _ActivityCount += 1;

            try
            {
                return await Task.Run(() => File.Exists(InstallConfiguration.PSO2DocumentsUserFile));
            }
            finally
            {
                _ActivityCount -= 1;
            }
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
                await task;
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

        public async Task LaunchAsync(bool shouldLaunchPSO2 = true)
        {
            IsLaunchingPSO2 = shouldLaunchPSO2;

            if (State == ApplicationState.Patching)
            {
                App.Logger.Info(nameof(MainWindowViewModel), "Cancelling launch");
                _LaunchCancellationTokenSource?.Cancel();
                return;
            }

            UploadErrorButtonVisible = false;
            IsChangelogVisible = false;

            _ActivityCount += 1;

            App.Logger.Info(nameof(MainWindowViewModel), "Starting launch procedure");
            try
            {
                _LaunchCancellationTokenSource = new CancellationTokenSource();

                App.Logger.Info(nameof(MainWindowViewModel), "Saving client settings");
                await Task.Run(() =>
                {
                    Properties.Settings.Default.EnglishPatchEnabled = ArksLayerEnglishPatchEnabled;
                    Properties.Settings.Default.TelepipeProxyEnabled = ArksLayerTelepipeProxyEnabled;
                    Properties.Settings.Default.ModFilesEnabled = ModFilesEnabled;
                    Properties.Settings.Default.Save();
                });

                while (true)
                {
                    try
                    {
                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        var newPhases = new ObservableCollection<PhaseState>();

                        var pso2DirectoriesPhase = new PSO2DirectoriesPhase(InstallConfiguration);
                        var pso2DirectoriesPhaseState = new PhaseState
                        {
                            TitleKey = "MainWindow_Phase_PSO2Directories"
                        };
                        newPhases.Add(pso2DirectoriesPhaseState);

                        ModFilesPhase modFilesPhase = null;
                        PhaseState modFilesPhaseState = null;
                        if (Properties.Settings.Default.ModFilesEnabled)
                        {
                            modFilesPhase = new ModFilesPhase(InstallConfiguration);
                            modFilesPhaseState = new PhaseState
                            {
                                TitleKey = "MainWindow_Phase_ModFiles"
                            };
                            newPhases.Add(modFilesPhaseState);
                        }

                        var deleteCensorFilePhase = new DeleteCensorFilePhase(InstallConfiguration);
                        var deleteCensorFilePhaseState = new PhaseState
                        {
                            TitleKey = "MainWindow_Phase_DeleteCensorFile"
                        };
                        newPhases.Add(deleteCensorFilePhaseState);

                        var downloadConfigurationState = new PhaseState
                        {
                            TitleKey = "MainWindow_Phase_DownloadConfiguration"
                        };
                        newPhases.Add(downloadConfigurationState);

                        var patchCacheState = new PhaseState
                        {
                            TitleKey = "MainWindow_Phase_PatchCache"
                        };
                        newPhases.Add(patchCacheState);

                        var comparePhase = new ComparePhase(InstallConfiguration);
                        var comparePhaseState = new PhaseState
                        {
                            TitleKey = "MainWindow_Phase_Compare"
                        };
                        newPhases.Add(comparePhaseState);
                        {
                            var compareProgressControl = new ProgressControl();
                            compareProgressControl.SetBinding(ProgressControl.ProgressProperty, new Binding
                            {
                                Source = comparePhase.Progress,
                                Path = new PropertyPath(nameof(comparePhase.Progress.Progress)),
                                Mode = BindingMode.OneWay
                            });
                            compareProgressControl.SetBinding(ProgressControl.IsIndeterminateProperty, new Binding
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
                            TitleKey = "MainWindow_Phase_VerifyFiles"
                        };
                        newPhases.Add(verifyFilesPhaseState);
                        {
                            var verifyFilesControl = new ProgressControl();
                            verifyFilesControl.SetBinding(ProgressControl.ProgressProperty, new Binding
                            {
                                Source = verifyFilesPhase.Progress,
                                Path = new PropertyPath(nameof(verifyFilesPhase.Progress.Progress)),
                                Mode = BindingMode.OneWay
                            });
                            verifyFilesControl.SetBinding(ProgressControl.IsIndeterminateProperty, new Binding
                            {
                                Source = verifyFilesPhase.Progress,
                                Path = new PropertyPath(nameof(verifyFilesPhase.Progress.IsIndeterminate)),
                                Mode = BindingMode.OneWay
                            });
                            var verifyFilesControlMessageBinding = new MultiBinding
                            {
                                Converter = StringFormatValueConverter.Instance
                            };
                            verifyFilesControlMessageBinding.Bindings.Add(new LocaleBindingExtension("MainWindow_Phase_VerifyFiles_Message"));
                            verifyFilesControlMessageBinding.Bindings.Add(new Binding
                            {
                                Source = verifyFilesPhase.Progress,
                                Path = new PropertyPath(nameof(verifyFilesPhase.Progress.CompletedCount)),
                                Mode = BindingMode.OneWay
                            });
                            verifyFilesControlMessageBinding.Bindings.Add(new Binding
                            {
                                Source = verifyFilesPhase.Progress,
                                Path = new PropertyPath(nameof(verifyFilesPhase.Progress.TotalCount)),
                                Mode = BindingMode.OneWay
                            });
                            verifyFilesControl.SetBinding(ProgressControl.MessageProperty, verifyFilesControlMessageBinding);
                            verifyFilesPhaseState.Child = verifyFilesControl;
                        }

                        var pluginInfoState = new PhaseState
                        {
                            TitleKey = "MainWindow_Phase_PluginInfo"
                        };
                        newPhases.Add(pluginInfoState);

                        var pso2hPhase = new PSO2hPhase(InstallConfiguration, ArksLayerEnglishPatchEnabled || ArksLayerTelepipeProxyEnabled);
                        var pso2hPhaseState = new PhaseState
                        {
                            TitleKey = ArksLayerEnglishPatchEnabled || ArksLayerTelepipeProxyEnabled
                                ? "MainWindow_Phase_PSO2h_TitleEnabled"
                                : "MainWindow_Phase_PSO2h_TitleDisabled"
                        };
                        newPhases.Add(pso2hPhaseState);

                        var telepipeProxyPhase = new TelepipeProxyPhase(InstallConfiguration, ArksLayerTelepipeProxyEnabled);
                        var telepipeProxyPhaseState = new PhaseState
                        {
                            TitleKey = ArksLayerTelepipeProxyEnabled
                                ? "MainWindow_Phase_TelepipeProxy_TitleEnabled"
                                : "MainWindow_Phase_TelepipeProxy_TitleDisabled"
                        };
                        newPhases.Add(telepipeProxyPhaseState);

                        var englishPatchPhase = new EnglishPatchPhase(InstallConfiguration, ArksLayerEnglishPatchEnabled);
                        var englishPatchPhaseState = new PhaseState
                        {
                            TitleKey = ArksLayerEnglishPatchEnabled
                                ? "MainWindow_Phase_EnglishPatch_TitleEnabled"
                                : "MainWindow_Phase_EnglishPatch_TitleDisabled"
                        };
                        newPhases.Add(englishPatchPhaseState);

                        LargeAddressAwarePhase largeAddressAwarePhase = null;
                        PhaseState largeAddressAwarePhaseState = null;
                        if (Properties.Settings.Default.LargeAddressAwareEnabled)
                        {
                            largeAddressAwarePhase = new LargeAddressAwarePhase(InstallConfiguration);
                            largeAddressAwarePhaseState = new PhaseState
                            {
                                TitleKey = "MainWindow_Phase_LargeAddressAware"
                            };
                            newPhases.Add(largeAddressAwarePhaseState);
                        }

                        var launchPSO2State = new PhaseState
                        {
                            TitleKey = "MainWindow_Phase_LaunchPSO2"
                        };
                        if (IsLaunchingPSO2)
                            newPhases.Add(launchPSO2State);

                        Phases = newPhases;

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Logger.Info("Launch", $"Running {nameof(PSO2DirectoriesPhase)}");
                        await _AttemptPhase(pso2DirectoriesPhaseState,
                            () => pso2DirectoriesPhase.RunAsync(_LaunchCancellationTokenSource.Token));

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        if (modFilesPhase != null && modFilesPhaseState != null)
                        {
                            App.Logger.Info("Launch", $"Running {nameof(ModFilesPhase)}");
                            await _AttemptPhase(modFilesPhaseState,
                                () => modFilesPhase.RunAsync(_LaunchCancellationTokenSource.Token));
                        }

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Logger.Info("Launch", $"Running {nameof(DeleteCensorFilePhase)}");
                        await _AttemptPhase(deleteCensorFilePhaseState,
                            () => deleteCensorFilePhase.RunAsync(_LaunchCancellationTokenSource.Token));

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Logger.Info("Launch", "Fetching download configuration");
                        var downloadConfiguration = await _AttemptPhase(downloadConfigurationState,
                            () => DownloadConfiguration.CreateDefaultAsync(_LaunchCancellationTokenSource.Token));

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Logger.Info("Launch", "Connecting to patch cache database");
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

                            App.Logger.Info("Launch", $"Running {nameof(ComparePhase)}");
                            var toUpdate = await _AttemptPhase(comparePhaseState,
                                () => comparePhase.RunAsync(downloadConfiguration, patchCache, _LaunchCancellationTokenSource.Token));
                            if (toUpdate.Length == 0)
                            {
                                verifyFilesPhaseState.State = PhaseControl.State.Success;
                                break;
                            }

                            _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                            App.Logger.Info("Launch", $"Running {nameof(VerifyFilesPhase)}");
                            await _AttemptPhase(verifyFilesPhaseState,
                                () => verifyFilesPhase.RunAsync(toUpdate, patchCache, _LaunchCancellationTokenSource.Token));
                        }

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Logger.Info("Launch", "Fetching plugin info");
                        var pluginInfo = await _AttemptPhase(pluginInfoState,
                            () => PluginInfo.FetchAsync(_LaunchCancellationTokenSource.Token));

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Logger.Info("Launch", $"Running {nameof(PSO2hPhase)}");
                        await _AttemptPhase(pso2hPhaseState,
                            () => pso2hPhase.RunAsync(pluginInfo, _LaunchCancellationTokenSource.Token));

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Logger.Info("Launch", $"Running {nameof(TelepipeProxyPhase)}");
                        await _AttemptPhase(telepipeProxyPhaseState,
                            () => telepipeProxyPhase.RunAsync(pluginInfo, _LaunchCancellationTokenSource.Token));

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        App.Logger.Info("Launch", $"Running {nameof(EnglishPatchPhase)}");
                        await _AttemptPhase(englishPatchPhaseState,
                            () => englishPatchPhase.RunAsync(patchCache, pluginInfo, _LaunchCancellationTokenSource.Token));

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        if (largeAddressAwarePhaseState != null && largeAddressAwarePhase != null)
                        {
                            App.Logger.Info("Launch", $"Running {nameof(LargeAddressAwarePhase)}");
                            await _AttemptPhase(largeAddressAwarePhaseState,
                                () => largeAddressAwarePhase.RunAsync(_LaunchCancellationTokenSource.Token));
                        }

                        _LaunchCancellationTokenSource.Token.ThrowIfCancellationRequested();

                        if (!IsLaunchingPSO2)
                            break;

                        // cancellation no longer works after this point

                        App.Logger.Info("Launch", "Starting PSO2");

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

                        App.Logger.Info("Launch", "PSO2 launch process ended");

                        if (Properties.Settings.Default.CloseOnLaunchEnabled)
                            App.Current.Shutdown();
                    }
                    catch (OperationCanceledException)
                    {
                        App.Logger.Info("Launch", "Launch cancelled");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Info("Launch", "Error during launch phases", ex);
                        UploadErrorButtonVisible = true;

                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(10), _LaunchCancellationTokenSource.Token);
                        }
                        catch (OperationCanceledException) { }

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
                App.Logger.Error("GameGuard", "Removing GameGuard files and directories");
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
                    App.Logger.Error("GameGuard", "Error deleting game guard files", ex);
                }

                App.Logger.Error("GameGuard", "Removing GameGuard registries");
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
                    App.Logger.Error("GameGuard", "Unable to delete GameGuard registry files", ex);
                }

            }
            finally
            {
                _ActivityCount -= 1;
            }
        }

        public async Task CancelAndUploadErrorAsync()
        {
            _LaunchCancellationTokenSource?.Cancel();
            UploadErrorButtonVisible = false;
            await Task.Run(() => App.UploadAndOpenLog());
        }
    }
}
