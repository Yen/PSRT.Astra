using Newtonsoft.Json;
using PSRT.Astra.Models.ArksLayer;
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
        public class PluginEntry
        {
            [JsonProperty(PropertyName = "Filename", Required = Required.Always)]
            public string FileName;

            [JsonProperty(PropertyName = "MD5Hash", Required = Required.Always)]
            public string Hash;

            [JsonProperty(PropertyName = "StorePath", Required = Required.Always)]
            public string Directory;

            public async Task ValidateFileAsync(string filePath, CancellationToken ct = default)
            {
                var valid = await Task.Run(() =>
                {
                    if (File.Exists(filePath))
                    {
                        using (var md5 = MD5.Create())
                        using (var fs = File.OpenRead(filePath))
                        {
                            var hashBytes = md5.ComputeHash(fs);
                            var hashString = string.Concat(hashBytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
                            if (hashString == Hash)
                                return true;
                        }
                    }

                    File.Delete(filePath);
                    return false;
                });

                if (valid)
                    return;

                using (var client = new ArksLayerHttpClient())
                using (var ns = await client.GetStreamAsync(new Uri(DownloadConfiguration.PluginsRoot, FileName)))
                using (var fs = File.Create(filePath, 4096, FileOptions.Asynchronous))
                {
                    await ns.CopyToAsync(fs);
                }
            }
        }

        public PluginEntry PSO2hDll;
        public PluginEntry DDrawDll;

        public PluginEntry TelepipeProxyDll;
        public PluginEntry PSO2BlockRenameDll;
        public PluginEntry PSO2ItemTranslatorDll;
        public PluginEntry PSO2RAISERSystemDll;
        public PluginEntry PSO2TitleTranslatorDll;

        public static async Task<PluginInfo> FetchAsync(CancellationToken ct = default)
        {
            App.Logger.Info(nameof(PluginInfo), "Downloading plugin info");

            using (var client = new ArksLayerHttpClient())
            {
                using (var request = await client.GetAsync(DownloadConfiguration.PluginsFile, ct))
                {
                    var downloadText = await request.Content.ReadAsStringAsync();
                    var downloadJson = JsonConvert.DeserializeObject<Dictionary<string, PluginEntry>>(downloadText);

                    return new PluginInfo
                    {
                        PSO2hDll = downloadJson["PluginLoader"],
                        DDrawDll = downloadJson["PluginHook"],
                        TelepipeProxyDll = downloadJson["TelepipeProxy"],
                        PSO2BlockRenameDll = downloadJson["BlockTranslation"],
                        PSO2ItemTranslatorDll = downloadJson["ItemTranslation"],
                        PSO2RAISERSystemDll = downloadJson["TextTranslation"],
                        PSO2TitleTranslatorDll = downloadJson["TitleTranslation"]
                    };
                }
            }
        }
    }
}
