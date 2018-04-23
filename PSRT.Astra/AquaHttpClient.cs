using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PSO2Patcher
{
    public class AquaHttpClient : HttpClient
    {
        public AquaHttpClient() => DefaultRequestHeaders.Add("User-Agent", "AQUA_HTTP");
    }
}
