using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
    public class TranslationInfo
    {
        [JsonRequired]
        public string BlockMD5;
        [JsonRequired]
        public string BlockPatch;
        [JsonRequired]
        public string BranchID;
        [JsonRequired]
        public bool Enabled;
        [JsonRequired]
        public string ItemMD5;
        [JsonRequired]
        public string ItemPatch;
        [JsonRequired]
        public string RaiserMD5;
        [JsonRequired]
        public string RaiserPatch;
        [JsonRequired]
        public string TextMD5;
        [JsonRequired]
        public string TextPatch;
        [JsonRequired]
        public string TitleMD5;
        [JsonRequired]
        public string TitlePatch;

        public static async Task<Dictionary<string, TranslationInfo>> FetchAllAsync(CancellationToken ct = default)
        {
            App.Current.Logger.Info(nameof(TranslationInfo), "Downloading translation info");

            using (var client = new ArksLayerHttpClient())
            {
                using (var request = await client.GetAsync(DownloadConfiguration.TranslationsFile, ct))
                {
                    var downloadText = await request.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<Dictionary<string, TranslationInfo>>(downloadText);
                }
            }
        }

        public static async Task<TranslationInfo> FetchEnglishAsync(CancellationToken ct = default)
        {
            var all = await FetchAllAsync(ct);
            return all["EN"];
        }
    }
}
