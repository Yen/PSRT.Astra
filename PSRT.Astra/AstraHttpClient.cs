using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Astra
{
    public class AstraHttpClient : HttpClient
    {
        public AstraHttpClient() => DefaultRequestHeaders.Add("User-Agent", "PSRT.Astra");
    }
}
