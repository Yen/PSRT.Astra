using Newtonsoft.Json;
using PSRT.Astra.Models;
using PSRT.Astra.Properties;
using PSRT.Astra.ViewModels;
using PSRT.Astra.Views;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PSRT.Astra
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static new App Current => Application.Current as App;

        public static Logger Logger = new Logger();

        public App()
        {
            // older version of .net (or perhaps just windows) use a different
            // security protocol by default which does not work with specific
            // web server settings used by some of the servers astra needs
            // to contact
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            Logger.Info(nameof(App), $"Version {Assembly.GetExecutingAssembly().GetName().Version}");

            if (Settings.Default.UpgradeRequired)
            {
                Logger.Info(nameof(App), "Upgrading settings file");

                Settings.Default.Upgrade();
                Settings.Default.UpgradeRequired = false;
                Settings.Default.Save();
            }

            DispatcherUnhandledException += _UnhandledException;
        }

        private async void _Application_Startup(object sender, StartupEventArgs e)
        {
            var astraAlreadyRunning = Process.GetProcesses().Any(p => p.ProcessName == Process.GetCurrentProcess().ProcessName && p.Id != Process.GetCurrentProcess().Id);
            if (astraAlreadyRunning)
            {
                MessageBox.Show("An instance of Astra is already running", "PSRT Astra");
                Shutdown();
                return;
            }

            await Task.Run(async () => await _DeleteOldExecutableAsync());

            var updateInfo = await _CheckForUpdateAsync();
            if (updateInfo.ShouldUpdate)
            {
                var shouldRunAstra = await _UpdateClientAsync(updateInfo.UpdateInformation);
                if (!shouldRunAstra)
                {
                    Shutdown();
                    return;
                }
            }

            // check if the last path is remembered and valid
            var installSelectorWindowViewModel = new InstallSelectorWindowViewModel();
            installSelectorWindowViewModel.SelectedPath = Settings.Default.LastSelectedInstallLocation;
            if (installSelectorWindowViewModel.SelectedPathContainsPSO2Bin)
            {
                Logger.Info(nameof(App), "Last selected install location is valid, opening main window");
                var mainWindow = new MainWindow(Path.Combine(installSelectorWindowViewModel.SelectedPath, "pso2_bin"), updateInfo.UpdateInformation);
                mainWindow.Show();
            }
            else
            {
                Logger.Info(nameof(App), "No valid last selected install location, opening install selection window");
                var installSelectionWindow = new InstallSelectorWindow(installSelectorWindowViewModel, updateInfo.UpdateInformation);
                installSelectionWindow.Show();
            }
        }

        private async Task<bool> _UpdateClientAsync(UpdateChecker.UpdateInformation updateInformation)
        {
            // TODO: localisation

            if (updateInformation.ExecutableAsset == null)
            {
                var manualUpdateResult = MessageBox.Show(
                    "An update for Astra is avaliable but the auto updater is unable to install it.\nAstra is unable to ensure a valid PSO2 installation with an out of date client.\n\nWould you like to open the latest Astra release page containing a manual download?",
                    "PSRT Astra",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (manualUpdateResult == MessageBoxResult.Yes)
                {
                    Process.Start(updateInformation.GitHubUri.AbsoluteUri);
                    return false;
                }
            }
            else
            {
                Logger.Info(nameof(App), "Updating Astra");
                try
                {
                    var executableLocation = Assembly.GetExecutingAssembly().Location;
                    if (Path.GetExtension(executableLocation) != ".exe")
                        throw new Exception($"Astra executable under the name \"{executableLocation}\" must end with a .exe file extension");
                    var oldExecutableLocation = $"{executableLocation}.old";

                    using (var client = new GitHubHttpClient())
                    {
                        var data = await client.GetByteArrayAsync(updateInformation.ExecutableAsset.DownloadUri);
                        File.Move(executableLocation, oldExecutableLocation);
                        var writeUpdateAttempts = 0;
                        while (true)
                        {
                            try
                            {
                                using (var fs = File.Create(executableLocation))
                                    await fs.WriteAsync(data, 0, data.Length);

                                break;
                            }
                            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                            {
                                writeUpdateAttempts++;
                                if (writeUpdateAttempts >= 5)
                                    throw;

                                await Task.Delay(1000);
                            }
                        }
                    }

                    Process.Start(executableLocation);
                    return false;
                }
                catch (Exception ex)
                {
                    Logger.Error(nameof(App), "Error updating Astra", ex);

                    var errorMessageResult = MessageBox.Show(
                        "An error occurred while trying to update Astra.\n\nIf this is a repeat issue please click below to upload the error log information and share it with a developer.\nWould you like to upload the error information?",
                        "PSRT Astra",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Error);

                    if (errorMessageResult == MessageBoxResult.Yes)
                        UploadAndOpenLog(ex);
                }
            }

            var launchAnywayResult = MessageBox.Show(
                "Astra cannot ensure a valid PSO2 installation with an out of date client.\n\nWould you like to launch the current version of Astra anyway?",
                "PSRT Astra",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            return launchAnywayResult == MessageBoxResult.Yes;
        }

        private async Task _DeleteOldExecutableAsync()
        {
            var executableLocation = Assembly.GetExecutingAssembly().Location;
            if (Path.GetExtension(executableLocation) != ".exe")
                return;
            var oldExecutableLocation = $"{executableLocation}.old";
            if (File.Exists(oldExecutableLocation))
            {
                int oldDeletionAttempts = 0;
                while (true)
                {
                    try
                    {
                        File.Delete(oldExecutableLocation);
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        oldDeletionAttempts++;
                        if (oldDeletionAttempts >= 5)
                        {
                            Logger.Error(nameof(App), $"Unable to delete old Astra executable after {oldDeletionAttempts} attempts", ex);
                            break;
                        }

                        await Task.Delay(1000);
                    }
                    break;
                }
            }
        }

        private static async Task<(UpdateChecker.UpdateInformation UpdateInformation, bool ShouldUpdate)> _CheckForUpdateAsync()
        {
            Logger.Info(nameof(App), "Checking for update");

            try
            {
                var updateInformation = await UpdateChecker.GetUpdateInformationAsync();
                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

                if (updateInformation.Version > currentVersion)
                {
                    Logger.Info(nameof(App), $"New update available (Version {updateInformation.Version})");
                    return (updateInformation, true);
                }
                else
                {
                    Logger.Info(nameof(App), "Client is up-to-date");
                    return (updateInformation, false);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("UpdateChecker", "Error getting update information", ex);
                return (null, false);
            }
        }

        private void _UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Warning(nameof(Astra), "Entered dispatcher unhandled exception handler", e.Exception);

            var messageBoxResult = MessageBox.Show(
                LocaleManager.Instance["Astra_UnhandledException_WindowMessage"],
                LocaleManager.Instance["Astra_UnhandledException_WindowTitle"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);
            if (messageBoxResult == MessageBoxResult.No)
                return;

            UploadAndOpenLog(e.Exception);
        }

        public static void UploadAndOpenLog(Exception ex = null)
        {
            Logger.Info(nameof(App), "Uploading log");

            var uploadResult = Task.Run(async () =>
            {
                var sections = new List<dynamic>();
                sections.Add(new
                {
                    name = "Log",
                    syntax = "text",
                    contents = Logger.Content
                });
                if (ex != null)
                {
                    sections.Add(new
                    {
                        name = "Exception",
                        syntax = "text",
                        contents = ex.ToString()
                    });
                }

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("X-Auth-Token", "ahDhbbK8esumH1uZgcvIwFjk2yMC4DhKZykRHoYDW");
                    var json = JsonConvert.SerializeObject(new
                    {
                        description = $"PSRT.Astra {Assembly.GetExecutingAssembly().GetName().Version} | {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")}",
                        expiration = "never",
                        sections
                    });
                    return await client.PostAsync("https://api.paste.ee/v1/pastes", new StringContent(json, Encoding.UTF8, "application/json"));
                }
            }).Result;

            if (uploadResult.StatusCode != HttpStatusCode.Created)
            {
                MessageBox.Show(
                    LocaleManager.Instance["Astra_UploadLogError_WindowMessage"],
                    LocaleManager.Instance["Astra_UploadLogError_WindowTitle"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
                return;
            }

            var resultJson = Task.Run(async () => await uploadResult.Content.ReadAsStringAsync()).Result;
            var resultObject = JsonConvert.DeserializeAnonymousType(resultJson, new { link = string.Empty });
            Process.Start(resultObject.link);

            var discordResult = MessageBox.Show(
                LocaleManager.Instance["Astra_OpenDiscord_WindowMessage"],
                LocaleManager.Instance["Astra_OpenDiscord_WindowTitle"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (discordResult == MessageBoxResult.Yes)
                Process.Start("https://discord.gg/sH2ZxPV");
        }
    }
}
