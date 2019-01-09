using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.ArksLayer
{
    public class ArksLayerHttpClient : HttpClient
    {
        public ArksLayerHttpClient()
        {
            DefaultRequestHeaders.Add("User-Agent", "PSRT.Astra");
            DefaultRequestHeaders.Add("Cache-Control", "no-cache, no-store, must-revalidate");
        }
    }
}
