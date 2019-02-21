using Newtonsoft.Json;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT.Astra.ViewModels
{
    public partial class MainWindowViewModel
    {
        public Version CurrentVersion { get; set; } = Assembly.GetExecutingAssembly().GetName().Version;
        public ClientUpdateInformation UpdatedVersionInformation { get; set; }
        public bool IsUpdateAvailable { get; set; } = false;
        public bool ErrorFetchingUpdateInformation { get; set; } = false;

        [AddINotifyPropertyChangedInterface]
        public class ClientUpdateInformation
        {
            public Uri GithubUri { get; set; }
            public Version Version { get; set; }
            public string UpdateBody { get; set; }
        }

        private class GithubUpdateInformation
        {
            [JsonProperty(PropertyName = "html_url", Required = Required.Always)]
            public string HtmlUrl = null;

            [JsonProperty(PropertyName = "tag_name", Required = Required.Always)]
            public string TagName = null;

            [JsonProperty(PropertyName = "body", Required = Required.Always)]
            public string Body = null;
        }

        private async void _CheckForUpdate()
        {
            try
            {
                UpdatedVersionInformation = await _GetUpdateInformationAsync();

                if (UpdatedVersionInformation.Version > CurrentVersion)
                {
                    App.Logger.Info("UpdateChecker", $"New update available (Version {UpdatedVersionInformation.Version})");
                    IsUpdateAvailable = true;
                }
                else
                {
                    App.Logger.Info("UpdateChecker", "Client is up-to-date");
                    IsUpdateAvailable = false;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error("UpdateChecker", "Error getting update information", ex);
                ErrorFetchingUpdateInformation = true;
            }
        }

        private async Task<ClientUpdateInformation> _GetUpdateInformationAsync()
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(
                    "PSRT.Astra",
                    Assembly.GetExecutingAssembly().GetName().Version.ToString()));

                const string githubLatestReleaseUrl = "https://api.github.com/repos/Yen/PSRT.Astra/releases/latest";

                var data = await client.GetStringAsync(githubLatestReleaseUrl);
                var json = JsonConvert.DeserializeObject<GithubUpdateInformation>(data);

                if (!Regex.IsMatch(json.TagName, @"^v(\d+(.\d+){0,3})$"))
                    throw new Exception("Tag is not a valid tag version format");

                var cleanedTag = json.TagName.TrimStart('v');

                return new ClientUpdateInformation
                {
                    GithubUri = new Uri(json.HtmlUrl),
                    Version = Version.Parse(cleanedTag),
                    UpdateBody = json.Body
                };
            }
        }
    }
}
