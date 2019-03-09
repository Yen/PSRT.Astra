using PSRT.Astra.Models.ArksLayer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSRT.Astra.Models
{
    public class InstallConfiguration
    {
        public static string PSO2DocumentsDirectory { get; }
        public static string PSO2DocumentsUserFile { get; }

        public string PSO2BinDirectory { get; }

        public string ModsDirectory { get; }

        public string DataDirectory { get; }
        public string DataWin32Directory { get; }

        public string PSO2Executable { get; }
        public string PSO2LauncherExecutable { get; }
        public string PSO2UpdaterExecutable { get; }
        public string PSO2PreDownloadExecutable { get; }
        public string PSO2DownloadExecutable { get; }

        public string PatchCacheDatabase { get; }
        public string CensorFile { get; }

        public string GameGuardDirectory { get; }
        public string GameGuardFile { get; }

        public static string[] GameGuardSystemFiles { get; }

        public string LargeAddressAwareConfig { get; }
        public object PluginsDirectory { get; internal set; }

        public ArksLayerInstallConfiguration ArksLayer { get; }

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

            PSO2DocumentsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"SEGA\PHANTASYSTARONLINE2");
            PSO2DocumentsUserFile = Path.Combine(PSO2DocumentsDirectory, "user.pso2");
        }

        public InstallConfiguration(string pso2BinDirectory)
        {
            App.Logger.Info(nameof(InstallConfiguration), "Creating Install configuration");

            PSO2BinDirectory = pso2BinDirectory;

            ModsDirectory = Path.Combine(PSO2BinDirectory, "mods");

            DataDirectory = Path.Combine(PSO2BinDirectory, "data");
            DataWin32Directory = Path.Combine(DataDirectory, "win32");

            PSO2Executable = Path.Combine(PSO2BinDirectory, "pso2.exe");
            PSO2LauncherExecutable = Path.Combine(PSO2BinDirectory, "pso2launcher.exe");
            PSO2UpdaterExecutable = Path.Combine(PSO2BinDirectory, "pso2updater.exe");
            PSO2PreDownloadExecutable = Path.Combine(PSO2BinDirectory, "pso2predownload.exe");
            PSO2DownloadExecutable = Path.Combine(PSO2BinDirectory, "pso2download.exe");
            
            PatchCacheDatabase = Path.Combine(PSO2BinDirectory, "patchcache.db");
            CensorFile = Path.Combine(DataWin32Directory, "ffbff2ac5b7a7948961212cefd4d402c");

            GameGuardDirectory = Path.Combine(PSO2BinDirectory, "GameGuard");
            GameGuardFile = Path.Combine(PSO2BinDirectory, "GameGuard.des");

            LargeAddressAwareConfig = Path.Combine(PSO2BinDirectory, "largeAddressAware.json");

            ArksLayer = new ArksLayerInstallConfiguration(this);
        }
    }
}
