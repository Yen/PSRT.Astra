using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;

namespace PSRT.Astra.Models
{
    public class GitHubHttpClient : HttpClient
    {
        public GitHubHttpClient()
        {
            DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(
                Assembly.GetEntryAssembly().GetName().Name,
                Assembly.GetEntryAssembly().GetName().Version.ToString()));
        }
    }
}