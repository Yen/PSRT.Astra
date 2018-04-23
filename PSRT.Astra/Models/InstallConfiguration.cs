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

        public InstallConfiguration(string pso2BinDirectory)
        {
            PSO2BinDirectory = pso2BinDirectory;

            PluginsDirectory = Path.Combine(PSO2BinDirectory, "plugins");
            PluginsDisabledDirectory = Path.Combine(PluginsDirectory, "disabled");

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
        }
    }
}
