using Newtonsoft.Json;
using PSRT.Astra.Models.ArksLayer;
using SharpCompress.Archives.Rar;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.ArksLayer
{
    public class PluginInfo
    {
        public class PluginData
        {
            [JsonProperty(PropertyName = "Data", Required = Required.Always)]
            public string FilePath;

            [JsonProperty(PropertyName = "Hash", Required = Required.Always)]
            public string Hash;

            [JsonProperty(PropertyName = "URL", Required = Required.Always)]
            public string Url;
        }

        public class PluginEntry
        {
            [JsonProperty(PropertyName = "Plugin", Required = Required.Always)]
            public string FilePath;

            [JsonProperty(PropertyName = "Hash", Required = Required.Always)]
            public string Hash;

            [JsonProperty(PropertyName = "URL", Required = Required.Always)]
            public string Url;

            [JsonProperty(PropertyName = "EN")]
            public PluginData EnglishData;

            public async Task ValidateAsync(InstallConfiguration installConfiguration, CancellationToken ct = default)
            {
                async Task ValidateFileAsync(string relativePath, string hash, string url)
                {
                    var path = Path.Combine(installConfiguration.PSO2BinDirectory, relativePath);

                    if (File.Exists(path))
                    {
                        using (var md5 = MD5.Create())
                        using (var fs = File.OpenRead(path))
                        {
                            var hashBytes = md5.ComputeHash(fs);
                            var hashString = string.Concat(hashBytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
                            if (hashString == Hash)
                                return;
                        }
                    }

                    App.Logger.Info(nameof(PluginInfo), $"Downloading plugin file english data: \"{relativePath}\"");
                    using (var client = new ArksLayerHttpClient())
                    using (var response = await client.GetAsync(url, ct))
                    {
                        response.EnsureSuccessStatusCode();

                        using (var ns = await response.Content.ReadAsStreamAsync())
                        {
                            if (Path.GetExtension(new Uri(url).LocalPath).ToLowerInvariant() != ".rar")
                            {
                                using (var fs = File.Create(path, 4096, FileOptions.Asynchronous))
                                    await ns.CopyToAsync(fs);
                                return;
                            }

                            // when the file is an archive we hash against the file named the same
                            using (var archive = RarArchive.Open(ns))
                            {
                                var fileEntries = archive.Entries.Where(e => !e.IsDirectory).ToArray();

                                if (fileEntries.Length == 0)
                                {
                                    var entryNames = fileEntries.Select(e => e.Key).ToArray();
                                    throw new Exception($"Expected more than one file in archive");
                                }

                                if (fileEntries.Length == 1)
                                {
                                    using (var fs = File.Create(path, 4096, FileOptions.Asynchronous))
                                    using (var es = fileEntries.First().OpenEntryStream())
                                        await es.CopyToAsync(fs);
                                    return;
                                }

                                App.Logger.Info(nameof(PluginInfo), "Multiple entries present in archive");

                                var primaryEntry = fileEntries.First(e => e.Key.ToLowerInvariant() == Path.GetFileName(path).ToLowerInvariant());
                                var otherEntries = fileEntries.Where(e => e != primaryEntry).ToArray();

                                using (var fs = File.Create(path, 4096, FileOptions.Asynchronous))
                                using (var es = primaryEntry.OpenEntryStream())
                                    await es.CopyToAsync(fs);

                                foreach (var entry in otherEntries)
                                {
                                    var directory = Path.GetDirectoryName(path);
                                    var otherPath = Path.Combine(directory, entry.Key);

                                    using (var fs = File.Create(otherPath, 4096, FileOptions.Asynchronous))
                                    using (var es = entry.OpenEntryStream())
                                        await es.CopyToAsync(fs);
                                }
                            }
                        }
                    }
                }

                App.Logger.Info(nameof(PluginInfo), $"Validating plugin file: \"{FilePath}\"");
                await Task.Run(async () => await ValidateFileAsync(FilePath, Hash, Url));
                if (EnglishData != null)
                {
                    App.Logger.Info(nameof(PluginInfo), $"Validating plugin file english data: \"{EnglishData.FilePath}\"");
                    await Task.Run(async () => await ValidateFileAsync(EnglishData.FilePath, EnglishData.Hash, EnglishData.Url));
                }
            }
        }

        public PluginEntry PluginLoader;
        public PluginEntry ProxyLoader;
        public PluginEntry BlockTranslation;
        public PluginEntry ItemTranslation;
        public PluginEntry TextTranslation;
        public PluginEntry TitleTranslation;

        public static async Task<PluginInfo> FetchAsync(CancellationToken ct = default)
        {
            App.Logger.Info(nameof(PluginInfo), "Downloading plugin info");

            using (var client = new ArksLayerHttpClient())
            {
                using (var request = await client.GetAsync(DownloadConfiguration.TranslationsFile, ct))
                {
                    var downloadText = await request.Content.ReadAsStringAsync();
                    var downloadJson = JsonConvert.DeserializeObject<Dictionary<string, PluginEntry>>(downloadText);

                    return new PluginInfo
                    {
                        PluginLoader = downloadJson["Plugin Loader"],
                        ProxyLoader = downloadJson["Proxy Loader"],
                        BlockTranslation = downloadJson["Block Translation"],
                        ItemTranslation = downloadJson["Item Translation"],
                        TextTranslation = downloadJson["Text Translation"],
                        TitleTranslation = downloadJson["Title Translation"]
                    };
                }
            }
        }
    }
}
