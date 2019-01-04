using Newtonsoft.Json;
using PSRT.Astra.Properties;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
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

        public Logger Logger = new Logger();

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

        private void _UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Warning(nameof(Astra), "Entered dispatcher unhandled exception handler", e.Exception);

            var messageBoxResult = MessageBox.Show(
                LocaleManager.Instance["Astra_UnhandledException_WindowTitle"],
                LocaleManager.Instance["Astra_UnhandledException_WindowMessage"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);
            if (messageBoxResult == MessageBoxResult.No)
                return;

            UploadAndOpenLog(e.Exception);
        }

        public static void UploadAndOpenLog(Exception ex = null)
        {
            App.Current.Logger.Info(nameof(App), "Uploading log");

            var uploadResult = Task.Run(async () =>
            {
                var sections = new List<dynamic>();
                sections.Add(new
                {
                    name = "Log",
                    syntax = "text",
                    contents = App.Current.Logger.Content
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
                    LocaleManager.Instance["Astra_UploadLogError_WindowTitle"],
                    LocaleManager.Instance["Astra_UploadLogError_WindowMessage"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
                return;
            }

            var resultJson = Task.Run(async () => await uploadResult.Content.ReadAsStringAsync()).Result;
            var resultObject = JsonConvert.DeserializeAnonymousType(resultJson, new { link = string.Empty });
            Process.Start(resultObject.link);
        }
    }
}
