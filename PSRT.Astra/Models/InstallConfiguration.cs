using PropertyChanged;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Astra.Models
{
    [AddINotifyPropertyChangedInterface]
    public class InstallConfiguration
    {
        public string PSO2BinDirectory { get; }

        public string PluginsDirectory { get; }
        public string PluginsDisabledDirectory { get; }

        public string PatchesDirectory { get; }

        public string DataDirectory { get; }
        public string DataLicenseDirectory { get; }
        public string DataWin32Directory { get; }
        public string DataWin32ScriptDirectory { get; }

        public string PSO2Executable { get; }
        public string PSO2LauncherExecutable { get; }
        public string PSO2UpdaterExecutable { get; }
        public string PSO2PreDownloadExecutable { get; }
        public string PSO2DownloadExecutable { get; }

        public string TweakerBin { get; }
        public string PatchCacheDatabase { get; }
        public string CensorFile { get; }

        public string GameGuardDirectory { get; }
        public string GameGuardFile { get; }

        public static string[] GameGuardSystemFiles { get; }

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

        static InstallConfiguration()
        {
            var gameGuardDir = Environment.GetFolderPath(
                Environment.Is64BitOperatingSystem
                    ? Environment.SpecialFolder.SystemX86
                    : Environment.SpecialFolder.System);

            GameGuardSystemFiles = new string[]
            {
                Path.Combine(gameGuardDir, "npptnt2.sys"),
                Path.Combine(gameGuardDir, "nppt9x.vxd"),
                Path.Combine(gameGuardDir, "GameMon.des")
            };
        }

        public InstallConfiguration(string pso2BinDirectory)
        {
            PSO2BinDirectory = pso2BinDirectory;

            PluginsDirectory = Path.Combine(PSO2BinDirectory, "plugins");
            PluginsDisabledDirectory = Path.Combine(PluginsDirectory, "disabled");

            PatchesDirectory = Path.Combine(PSO2BinDirectory, "patches");

            DataDirectory = Path.Combine(PSO2BinDirectory, "data");
            DataLicenseDirectory = Path.Combine(DataDirectory, "license");
            DataWin32Directory = Path.Combine(DataDirectory, "win32");
            DataWin32ScriptDirectory = Path.Combine(DataWin32Directory, "script");

            PSO2Executable = Path.Combine(PSO2BinDirectory, "pso2.exe");
            PSO2LauncherExecutable = Path.Combine(PSO2BinDirectory, "pso2launcher.exe");
            PSO2UpdaterExecutable = Path.Combine(PSO2BinDirectory, "pso2updater.exe");
            PSO2PreDownloadExecutable = Path.Combine(PSO2BinDirectory, "pso2predownload.exe");
            PSO2DownloadExecutable = Path.Combine(PSO2BinDirectory, "pso2download.exe");

            TweakerBin = Path.Combine(PSO2BinDirectory, "tweaker.bin");
            PatchCacheDatabase = Path.Combine(PSO2BinDirectory, "patchcache.db");
            CensorFile = Path.Combine(DataWin32Directory, "ffbff2ac5b7a7948961212cefd4d402c");

            GameGuardDirectory = Path.Combine(PSO2BinDirectory, "GameGuard");
            GameGuardFile = Path.Combine(PSO2BinDirectory, "GameGuard.des");

            DDrawDll = Path.Combine(PSO2BinDirectory, "ddraw.dll");
            PSO2hDll = Path.Combine(PSO2BinDirectory, "pso2h.dll");

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

            TelepipeProxyConfig = Path.Combine(PSO2BinDirectory, "proxy.txt");
            TelepipeProxyPublicKey = Path.Combine(PSO2BinDirectory, "publickey.blob");
        }
    }
}
