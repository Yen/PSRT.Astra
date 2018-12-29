using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Astra.Models.ArksLayer
{
    public class ArksLayerInstallConfiguration
    {
        public string PluginsDirectory { get; }
        public string PluginsDisabledDirectory { get; }

        public string PatchesDirectory { get; }

        public string TweakerBin { get; }

        public string DDrawDll { get; }
        public string PSO2hDll { get; }

        public string PluginPSO2BlockRenameDll { get; }
        public string PluginPSO2ItemTranslatorDll { get; }
        public string PluginPSO2TitleTranslatorDll { get; }
        public string PluginPSO2RAISERSystemDll { get; }
        public string PluginTelepipeProxyDll { get; }

        public string EnglishBlockPatch { get; }
        public string EnglishItemPatch { get; }
        public string EnglishRaiserPatch { get; }
        public string EnglishTextPatch { get; }
        public string EnglishTitlePatch { get; }

        public string TelepipeProxyConfig { get; }
        public string TelepipeProxyPublicKey { get; }

        public ArksLayerInstallConfiguration(InstallConfiguration configuration)
        {
            App.Current.Logger.Info(nameof(ArksLayerInstallConfiguration), "Creating ArksLayer install configuration");

            PluginsDirectory = Path.Combine(configuration.PSO2BinDirectory, "plugins/");
            PluginsDisabledDirectory = Path.Combine(PluginsDirectory, "disabled/");

            PatchesDirectory = Path.Combine(configuration.PSO2BinDirectory, "patches/");

            TweakerBin = Path.Combine(configuration.PSO2BinDirectory, "tweaker.bin");

            DDrawDll = Path.Combine(configuration.PSO2BinDirectory, "ddraw.dll");
            PSO2hDll = Path.Combine(configuration.PSO2BinDirectory, "pso2h.dll");

            PluginPSO2BlockRenameDll = Path.Combine(PluginsDirectory, "PSO2BlockRename.dll");
            PluginPSO2ItemTranslatorDll = Path.Combine(PluginsDirectory, "PSO2ItemTranslator.dll");
            PluginPSO2TitleTranslatorDll = Path.Combine(PluginsDirectory, "PSO2TitleTranslator.dll");
            PluginPSO2RAISERSystemDll = Path.Combine(PluginsDirectory, "PSO2RAISERSystem.dll");
            PluginTelepipeProxyDll = Path.Combine(PluginsDirectory, "TelepipeProxy.dll");

            EnglishBlockPatch = Path.Combine(PatchesDirectory, "translation_blocks.bin");
            EnglishItemPatch = Path.Combine(PatchesDirectory, "translation_items.bin");
            EnglishRaiserPatch = Path.Combine(PatchesDirectory, "translation_raiser.bin");
            EnglishTextPatch = Path.Combine(PatchesDirectory, "patch.tar");
            EnglishTitlePatch = Path.Combine(PatchesDirectory, "translation_titles.bin");

            TelepipeProxyConfig = Path.Combine(configuration.PSO2BinDirectory, "proxy.txt");
            TelepipeProxyPublicKey = Path.Combine(configuration.PSO2BinDirectory, "publickey.blob");
        }
    }
}
