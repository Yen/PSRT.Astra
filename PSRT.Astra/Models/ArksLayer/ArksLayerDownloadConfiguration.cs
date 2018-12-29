using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.ArksLayer
{
    public static class ArksLayerDownloadConfiguration
    {
        public static readonly Uri TranslationsFile = new Uri("https://pso2.acf.me.uk/Translations/Translations.json");

        public static readonly Uri PluginsRoot = new Uri("https://pso2.acf.me.uk/Plugins/");
        public static readonly Uri PluginsFile = new Uri(PluginsRoot, "plugins.json");
    }
}
