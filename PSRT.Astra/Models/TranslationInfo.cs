using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT.Astra.Models
{
    public class TranslationInfo
    {
        [JsonRequired]
        public string BlockMD5;
        [JsonRequired]
        public string BlockPatch;
        [JsonRequired]
        public long BranchID;
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

        public static async Task<TranslationInfo> FetchAsync(CancellationToken ct = default)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "ADragonIsFineToo");
                using (var request = await client.GetAsync(DownloadConfiguration.TranslationsFile, ct))
                {
                    var downloadText = await request.Content.ReadAsStringAsync();
                    var downloadJson = JObject.Parse(downloadText);
                    return downloadJson["EN"].ToObject<TranslationInfo>();
                }
            }
        }
    }
}
