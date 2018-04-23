using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PSRT.Astra.Models
{
    public class DownloadConfiguration
    {
        public static readonly Uri ManagementFile = new Uri("http://patch01.pso2gs.net/patch_prod/patches/management_beta.txt");

        public Uri RootMaster { get; }
        public Uri RootPatches { get; }

        public Uri PSO2Executable { get; }
        public Uri PSO2LauncherExecutable { get; }
        public Uri PSO2UpdaterExecutable { get; }
        public Uri PSO2DownloadExecutable { get; }
        public Uri PSO2PreDownloadExecutable { get; }

        public Uri PatchesLauncherList { get; }
        public Uri PatchesPatchList { get; }

        public DownloadConfiguration(Uri masterUrl, Uri patchUrl)
        {
            RootMaster = masterUrl;
            RootPatches = patchUrl;

            PSO2Executable = new Uri(RootPatches, "pso2.exe.pat");
            PSO2LauncherExecutable = new Uri(RootPatches, "pso2launcher.exe.pat");
            PSO2UpdaterExecutable = new Uri(RootPatches, "pso2updater.exe.pat");
            PSO2DownloadExecutable = new Uri(RootPatches, "pso2download.exe.pat");
            PSO2PreDownloadExecutable = new Uri(RootPatches, "pso2predownload.exe.pat");

            PatchesLauncherList = new Uri(RootPatches, "launcherlist.txt");
            PatchesPatchList = new Uri(RootPatches, "patchlist.txt");
        }

        public static async Task<DownloadConfiguration> CreateDefaultAsync(CancellationToken ct = default)
        {
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync(ManagementFile, ct))
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(stream))
            {
                var lines = new List<string>();
                for (var line = await reader.ReadLineAsync(); line != null; line = await reader.ReadLineAsync())
                    lines.Add(line);

                var fields = new Dictionary<string, string>();
                foreach (var line in lines)
                {
                    var equalsIndex = line.IndexOf('=');
                    if (equalsIndex == -1)
                        continue;

                    var key = line.Substring(0, equalsIndex);
                    var value = line.Substring(equalsIndex + 1);

                    fields[key] = value;
                }

                var masterUrl = new Uri(fields["MasterURL"]);
                var patchUrl = new Uri(fields["PatchURL"]);
                return new DownloadConfiguration(masterUrl, patchUrl);
            }
        }
    }
}
