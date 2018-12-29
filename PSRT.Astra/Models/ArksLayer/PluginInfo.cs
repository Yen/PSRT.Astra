using Newtonsoft.Json;
using PSRT.Astra.Models.ArksLayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
            App.Current.Logger.Info(nameof(PluginInfo), "Downloading plugin info");

            using (var client = new ArksLayerHttpClient())
            {
                using (var request = await client.GetAsync(DownloadConfiguration.PluginsFile, ct))
                {
                    var downloadText = await request.Content.ReadAsStringAsync();
                    var downloadJson = JsonConvert.DeserializeObject<Dictionary<string, PluginEntry>>(downloadText);

                    return new PluginInfo
                    {
                        PSO2hDll = downloadJson["pso2h.dll"],
                        DDrawDll = downloadJson["ddraw.dll"],
                        TelepipeProxyDll = downloadJson["TelepipeProxy.dll"],
                        PSO2BlockRenameDll = downloadJson["PSO2BlockRename.dll"],
                        PSO2ItemTranslatorDll = downloadJson["PSO2ItemTranslator.dll"],
                        PSO2RAISERSystemDll = downloadJson["PSO2RAISERSystem.dll"],
                        PSO2TitleTranslatorDll = downloadJson["PSO2TitleTranslator.dll"]
                    };
                }
            }
        }
    }
}
