using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace PSRT.Astra.Models.ArksLayer
{
    public class ArksLayerHttpClient : HttpClient
    {
        private static object _TweakerVersionLock = new object();
        private static Version _TweakerVersion;

        public ArksLayerHttpClient()
        {
            // this is very dumb but the arks-layer servers have been changed to only accept downloads from
            // clients with a user-agent representing the current version of the tweaker and nothing else.
            // the original agreed upon PSRT.Astra user-agent has been blocked
            if (_TweakerVersion == null)
            {
                lock (_TweakerVersionLock)
                {
                    // weird concurrency double check dont think too much about it
                    if (_TweakerVersion == null)
                    {
                        App.Logger.Info(nameof(ArksLayerHttpClient), "Downloading PSO2 Tweaker version info from arks-layer feed");
                        using (var client = new HttpClient())
                        {
                            // run the async task in the thread pool as as to not cause a deadlock because constructors must be synchronous
                            var tweakerVersionXml = Task.Run(async () => await client.GetStringAsync("https://arks-layer.com/justice/version2.xml")).GetAwaiter().GetResult();

                            App.Logger.Info(nameof(ArksLayerHttpClient), "Parsing PSO2 Tweaker version info from xml");
                            var document = new XmlDocument();
                            document.LoadXml(tweakerVersionXml);

                            var versionString = document["item"]["version"].InnerText;
                            _TweakerVersion = Version.Parse(versionString);
                            App.Logger.Info(nameof(ArksLayerHttpClient), $"PSO2 Tweaker version found to be {_TweakerVersion}");
                        }
                    }
                }
            }
            DefaultRequestHeaders.Add("User-Agent", $"PSO2.Tweaker.v{_TweakerVersion}");

            //DefaultRequestHeaders.Add("User-Agent", "PSRT.Astra");
            DefaultRequestHeaders.Add("Cache-Control", "no-cache, no-store, must-revalidate");
        }
    }
}
