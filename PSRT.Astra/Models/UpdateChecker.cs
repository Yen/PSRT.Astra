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
using System.Threading.Tasks;

namespace PSRT.Astra.Models
{
    public static class UpdateChecker
    {
        public class GitHubUpdateInformationAsset
        {
            [JsonProperty(PropertyName = "name", Required = Required.Always)]
            public string Name;

            [JsonProperty(PropertyName = "content_type", Required = Required.Always)]
            public string ContentType;

            [JsonProperty(PropertyName = "size", Required = Required.Always)]
            public int Size;

            [JsonProperty(PropertyName = "download_count", Required = Required.Always)]
            public int DownloadCount;

            [JsonProperty(PropertyName = "browser_download_url", Required = Required.Always)]
            public string BrowserDownloadUrl;
        }

        public class GitHubUpdateInformation
        {
            [JsonProperty(PropertyName = "html_url", Required = Required.Always)]
            public string HtmlUrl;

            [JsonProperty(PropertyName = "tag_name", Required = Required.Always)]
            public string TagName;

            [JsonProperty(PropertyName = "body", Required = Required.Always)]
            public string Body;

            [JsonProperty(PropertyName = "published_at", Required = Required.Always)]
            public DateTimeOffset PublishedAt;

            [JsonProperty(PropertyName = "assets", Required = Required.Always)]
            public GitHubUpdateInformationAsset[] Assets;
        }

        [AddINotifyPropertyChangedInterface]
        public class UpdateInformationAsset
        {
            public string Name { get; set; }
            public string ContentType { get; set; }
            public int Size { get; set; }
            public int DownloadCount { get; set; }
            public Uri DownloadUri { get; set; }
        }

        [AddINotifyPropertyChangedInterface]
        public class UpdateInformation
        {
            public Uri GitHubUri { get; set; }
            public Version Version { get; set; }
            public string UpdateBody { get; set; }
            public DateTimeOffset PublishDate { get; set; }

            public UpdateInformationAsset ExecutableAsset { get; set; }
        }

        public static async Task<GitHubUpdateInformation> GetGitHubUpdateInformationAsync()
        {
            using (var client = new GitHubHttpClient())
            {
                const string gitHubLatestReleaseUrl = "https://api.github.com/repos/Yen/PSRT.Astra/releases/latest";

                var data = await client.GetStringAsync(gitHubLatestReleaseUrl);
                return JsonConvert.DeserializeObject<GitHubUpdateInformation>(data);
            }
        }

        public static async Task<UpdateInformation> GetUpdateInformationAsync()
        {
            var gitHubInfo = await GetGitHubUpdateInformationAsync();

            if (!Regex.IsMatch(gitHubInfo.TagName, @"^v(\d+(.\d+){1,3})$"))
                throw new Exception("Tag is not a valid tag version format");
            var cleanedTag = gitHubInfo.TagName.TrimStart('v');
            var version = Version.Parse(cleanedTag);

            var assets = gitHubInfo.Assets
                .Select(asset => new UpdateInformationAsset
                {
                    Name = asset.Name,
                    ContentType = asset.ContentType,
                    Size = asset.Size,
                    DownloadCount = asset.DownloadCount,
                    DownloadUri = new Uri(asset.BrowserDownloadUrl)
                })
                .ToArray();

            return new UpdateInformation
            {
                GitHubUri = new Uri(gitHubInfo.HtmlUrl),
                Version = version,
                UpdateBody = gitHubInfo.Body,
                PublishDate = gitHubInfo.PublishedAt,
                ExecutableAsset = assets.FirstOrDefault(asset => asset.Name == "PSRT.Astra.exe"),
            };
        }
    }
}