using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.ArksLayer
{
    public class ProxyInfo
    {
        [JsonProperty(PropertyName = "host", Required = Required.DisallowNull)]
        public string Host;

        [JsonProperty(PropertyName = "version", Required = Required.DisallowNull)]
        public int Version;

        [JsonProperty(PropertyName = "name", Required = Required.DisallowNull)]
        public string Name;

        [JsonProperty(PropertyName = "publickeyurl", Required = Required.DisallowNull)]
        public string PublicKeyUrl;
    }
}
